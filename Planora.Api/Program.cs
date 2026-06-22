using System.Text;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Planora.Api.Application.Interfaces;
using Planora.Api.Application.Services;
using Planora.Api.Domain.Entities;
using Planora.Api.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

// Railway sets PORT env var dynamically; fall back to 8080 for local Docker
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://+:{port}");

// This is an API-only project, so it has no wwwroot by default — WebApplication.CreateBuilder
// already snapshots WebRootFileProvider as a NullFileProvider before this point if the folder
// doesn't exist on disk yet, so just creating the directory and updating WebRootPath isn't
// enough; the file provider itself has to be replaced too. Board cover images (see
// BoardsController) need this to work.
var webRootPath = Path.Combine(builder.Environment.ContentRootPath, "wwwroot");
Directory.CreateDirectory(webRootPath);
builder.Environment.WebRootPath = webRootPath;
builder.Environment.WebRootFileProvider = new PhysicalFileProvider(webRootPath);

// ── Database ──────────────────────────────────────────────────────────────────
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

// ── Identity ──────────────────────────────────────────────────────────────────
builder.Services.AddIdentity<AppUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

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
});

// ── CORS for Blazor WASM ──────────────────────────────────────────────────────
var allowedOrigins = builder.Configuration["Cors:AllowedOrigins"]?.Split(",")
    ?? ["http://localhost:5010"];

builder.Services.AddCors(options =>
    options.AddPolicy("BlazorClient", policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()));

// ── Application Services ──────────────────────────────────────────────────────
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IDemoWorkspaceSeeder, DemoWorkspaceSeeder>();

// ── Validation ────────────────────────────────────────────────────────────────
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// ── API ───────────────────────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

// Apply pending EF Core migrations on startup (idempotent — safe to run every boot)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();
}

// Board cover images are saved here at runtime (see BoardsController) — make sure it
// exists before UseStaticFiles tries to serve from it.
Directory.CreateDirectory(Path.Combine(app.Environment.WebRootPath, "uploads", "boards"));

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseCors("BlazorClient");
app.UseHttpsRedirection();

// Prevents browsers from MIME-sniffing served files (e.g. uploaded board cover images)
// into a different type than the one we declare, regardless of signature checks upstream.
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    await next();
});

app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
