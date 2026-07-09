using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using FluentValidation;
using Planora.Api.Application.Emails;
using Planora.Api.Application.Interfaces;
using Planora.Api.Application.Options;
using Planora.Api.Application.Services;
using Planora.Api.Domain.Entities;
using Planora.Api.Infrastructure.Data;
using Planora.Api.Infrastructure.Email;
using Planora.Api.Infrastructure.HealthChecks;
using Planora.Api.Infrastructure.Jobs;
using Planora.Api.Infrastructure.Storage;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// ── Structured JSON logging (production only) ──────────────────────────────────
if (!builder.Environment.IsDevelopment())
{
    builder.Logging.ClearProviders();
    builder.Logging.AddJsonConsole(options =>
    {
        options.IncludeScopes = true;
        options.TimestampFormat = "o";
        options.JsonWriterOptions = new JsonWriterOptions { Indented = false };
    });
}

// Railway sets PORT env var dynamically; fall back to 8080 for local Docker
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://+:{port}");

// wwwroot for board cover images
var webRootPath = Path.Combine(builder.Environment.ContentRootPath, "wwwroot");
Directory.CreateDirectory(webRootPath);
builder.Environment.WebRootPath = webRootPath;
builder.Environment.WebRootFileProvider = new PhysicalFileProvider(webRootPath);

// ── Database ──────────────────────────────────────────────────────────────────
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("Default"),
        npgsql => npgsql.CommandTimeout(30)));

// ── Identity ──────────────────────────────────────────────────────────────────
builder.Services.AddIdentity<AppUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 8;
    options.User.RequireUniqueEmail = true;
    // Progressive lockout is handled manually in AuthController.
    // MaxFailedAccessAttempts is set high to prevent Identity from auto-resetting the counter.
    options.Lockout.MaxFailedAccessAttempts = 100;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.AllowedForNewUsers = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Shorten the lifespan of data-protection tokens (password reset, email confirmation)
// from Identity's 1-day default to 1 hour.
builder.Services.Configure<DataProtectionTokenProviderOptions>(o => o.TokenLifespan = TimeSpan.FromHours(1));

// ── HSTS ──────────────────────────────────────────────────────────────────────
builder.Services.AddHsts(options =>
{
    options.MaxAge = TimeSpan.FromDays(365);
    options.IncludeSubDomains = true;
});

// ── JWT Authentication ────────────────────────────────────────────────────────
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key is not configured.");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ClockSkew = TimeSpan.Zero
    };
    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = async context =>
        {
            var userManager = context.HttpContext.RequestServices
                .GetRequiredService<UserManager<AppUser>>();

            var userId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            var stamp = context.Principal?.FindFirstValue("securityStamp");

            if (userId is null || stamp is null)
            {
                context.Fail("Invalid token.");
                return;
            }

            var user = await userManager.FindByIdAsync(userId);
            if (user?.SecurityStamp != stamp)
                context.Fail("Token has been revoked.");
        }
    };
});

// ── CORS for Blazor WASM ──────────────────────────────────────────────────────
var allowedOrigins = builder.Configuration["Cors:AllowedOrigins"]?.Split(",")
    ?? ["http://localhost:5010"];

builder.Services.AddCors(options =>
    options.AddPolicy("BlazorClient", policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()));

// ── Rate Limiting ─────────────────────────────────────────────────────────────
builder.Services.AddRateLimiter(options =>
{
    // PermitLimit is configurable so integration tests (which fire many auth calls
    // against a shared host in one window) can raise it; production keeps the default of 10.
    options.AddFixedWindowLimiter("auth", limiterOptions =>
    {
        limiterOptions.PermitLimit = builder.Configuration.GetValue<int?>("RateLimiting:AuthPermitLimit") ?? 10;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiterOptions.QueueLimit = 0;
    });
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

// ── Application Services ──────────────────────────────────────────────────────
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IRefreshTokenService, RefreshTokenService>();
builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddScoped<IDemoWorkspaceSeeder, DemoWorkspaceSeeder>();
// File storage: provider selected by config. Local disk in dev; production sets Storage:Provider
// to "AzureBlob" (durable — the container filesystem is ephemeral). Existing on-disk cover URLs keep
// resolving after cutover because the frontend and Blob backend both accept absolute Blob URLs and
// legacy "/uploads/..." paths (dual-read; see docs/azure-blob-storage.md).
builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection(StorageOptions.SectionName));
var storageOptions = builder.Configuration.GetSection(StorageOptions.SectionName).Get<StorageOptions>()
    ?? new StorageOptions();
switch (storageOptions.Provider.Trim().ToLowerInvariant())
{
    case "local":
        builder.Services.AddSingleton<IFileStorage, LocalFileStorage>();
        break;
    case "azureblob":
        builder.Services.AddSingleton<IFileStorage, BlobFileStorage>();
        break;
    default:
        throw new NotSupportedException(
            $"Unknown Storage:Provider '{storageOptions.Provider}'. Use 'Local' or 'AzureBlob'.");
}
// Email: provider selected by config. Console dev sink by default; production sets Email:Provider to
// "Resend" (API key comes from env/secret, never source).
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection(EmailOptions.SectionName));
var emailOptions = builder.Configuration.GetSection(EmailOptions.SectionName).Get<EmailOptions>()
    ?? new EmailOptions();
switch (emailOptions.Provider.Trim().ToLowerInvariant())
{
    case "console":
        builder.Services.AddSingleton<IEmailSender, ConsoleEmailSender>();
        break;
    case "resend":
        ValidateResendEmailOptions(emailOptions);
        builder.Services.AddHttpClient<IEmailSender, ResendEmailSender>();
        break;
    default:
        throw new NotSupportedException(
            $"Unknown Email:Provider '{emailOptions.Provider}'. Use 'Console' or 'Resend'.");
}
// Contextual sender mapping (no-reply@/security@/invites@/…) + high-level transactional composer.
builder.Services.AddSingleton<IEmailSenderResolver, EmailSenderResolver>();
builder.Services.AddScoped<ITransactionalEmailService, TransactionalEmailService>();
builder.Services.AddScoped<IActivityEmailNotifier, ActivityEmailNotifier>();

// Background cleanup: purges expired refresh tokens/invitations and trash past retention.
builder.Services.AddScoped<DataCleanupRunner>();
builder.Services.AddHostedService<DataCleanupBackgroundService>();

// ── Validation ────────────────────────────────────────────────────────────────
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// ── Health checks ─────────────────────────────────────────────────────────────
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database", tags: ["ready"]);

// ── API ───────────────────────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

// Apply pending EF Core migrations on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();
}

Directory.CreateDirectory(Path.Combine(app.Environment.WebRootPath, "uploads", "boards"));

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

// ── Exception handler (no stack traces in production) ─────────────────────────
app.UseExceptionHandler(errApp =>
{
    errApp.Run(async context =>
    {
        var ex = context.Features.Get<IExceptionHandlerFeature>()?.Error;
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Unhandled exception {Method} {Path}", context.Request.Method, context.Request.Path);

        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/json";
        var message = app.Environment.IsDevelopment() && ex is not null
            ? ex.Message
            : "An internal server error occurred.";
        await context.Response.WriteAsJsonAsync(new { error = message });
    });
});

app.UseCors("BlazorClient");

if (!app.Environment.IsDevelopment())
    app.UseHsts();

app.UseHttpsRedirection();
app.UseRateLimiter();

// ── Correlation ID middleware ─────────────────────────────────────────────────
app.Use(async (context, next) =>
{
    const string correlationHeader = "X-Correlation-ID";
    var correlationId = context.Request.Headers[correlationHeader].FirstOrDefault()
        ?? Guid.NewGuid().ToString("N")[..16];
    context.Response.Headers.Append(correlationHeader, correlationId);
    context.Items["CorrelationId"] = correlationId;
    await next();
});

// ── Security headers ──────────────────────────────────────────────────────────
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.Append("Permissions-Policy", "camera=(), microphone=(), geolocation=()");
    context.Response.Headers.Append("X-Permitted-Cross-Domain-Policies", "none");
    context.Response.Headers.Append("Content-Security-Policy", "default-src 'none'; frame-ancestors 'none'");
    await next();
});

app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// ── Health probes (anonymous; used by Azure Container Apps) ────────────────────
// Liveness: process is up and serving. Runs no checks so a slow/broken DB does
// not cause the container to be killed and restart-looped.
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});
// Readiness: only route traffic when dependencies (the database) are reachable.
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

app.Run();

static void ValidateResendEmailOptions(EmailOptions options)
{
    var missing = new List<string>();

    if (string.IsNullOrWhiteSpace(options.From.Address))
        missing.Add("Email:From:Address");
    if (string.IsNullOrWhiteSpace(options.Resend.ApiKey))
        missing.Add("Email:Resend:ApiKey");

    if (missing.Count > 0)
    {
        throw new InvalidOperationException(
            $"Email:Provider is Resend but required setting(s) are missing: {string.Join(", ", missing)}.");
    }
}

// Exposed so the integration test project can reference the entry point via
// WebApplicationFactory<Program>. Top-level statements otherwise emit an
// internal Program class.
public partial class Program;
