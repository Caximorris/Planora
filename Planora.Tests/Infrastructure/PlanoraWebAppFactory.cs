using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Planora.Api.Application.Interfaces;
using Planora.Api.Infrastructure.Data;

namespace Planora.Tests.Infrastructure;

/// <summary>
/// Boots the real API in-memory (TestServer) against a dedicated Postgres test
/// database. Docker is not available locally, so this reuses the local Postgres
/// instance with a separate database that is dropped and re-migrated per run.
///
/// Required configuration is supplied via environment variables set in the
/// constructor — Program.cs reads Jwt:Key inline before the host is built, so
/// ConfigureAppConfiguration would apply too late. Env vars are read during
/// WebApplication.CreateBuilder and are therefore visible to those inline reads.
/// This also keeps tests self-contained (no dependency on the gitignored
/// appsettings.Development.json), so they run identically in CI.
/// </summary>
public sealed class PlanoraWebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    // A distinct database name keeps test runs isolated from the dev database.
    private const string TestConnectionString =
        "Host=localhost;Port=5433;Database=planora_test;Username=postgres;Password=admin1234";

    public PlanoraWebAppFactory()
    {
        // Set before the base factory builds the host on first client/service access.
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        Environment.SetEnvironmentVariable("ConnectionStrings__Default", TestConnectionString);
        Environment.SetEnvironmentVariable("Jwt__Key", "test-super-secret-key-minimum-32-characters-long!!");
        Environment.SetEnvironmentVariable("Jwt__Issuer", "PlanoraTest");
        Environment.SetEnvironmentVariable("Jwt__Audience", "PlanoraTestClient");
        Environment.SetEnvironmentVariable("Jwt__ExpirationMinutes", "15");
        Environment.SetEnvironmentVariable("Jwt__RefreshTokenDays", "7");
        Environment.SetEnvironmentVariable("Cors__AllowedOrigins", "http://localhost");
        // The auth rate limiter is a single global window shared by the whole host; the test
        // suite fires many auth calls in one minute, so raise the limit to avoid spurious 429s.
        // Lockout still returns 429 through its own path, which these tests assert independently.
        Environment.SetEnvironmentVariable("RateLimiting__AuthPermitLimit", "10000");
        // The uploads limiter is partitioned per user, so other tests (one-ish upload per
        // fresh user) never hit it; a low limit keeps UploadRateLimitTests cheap.
        Environment.SetEnvironmentVariable("RateLimiting__UploadPermitLimit", "10");
    }

    /// <summary>Records emails the API "sends" so tests can assert on them.</summary>
    public CapturingEmailSender EmailSender { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Replace the console email sink with a capturing double for assertions.
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(EmailSender);
        });
    }

    public async Task InitializeAsync()
    {
        // Accessing Services builds the host (which runs the startup migration on
        // the test DB). Then reset to a clean, freshly-migrated schema.
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();
    }

    public new async Task DisposeAsync()
    {
        using (var scope = Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Database.EnsureDeletedAsync();
        }
        await base.DisposeAsync();
    }
}

/// <summary>
/// Shares a single booted API + migrated database across every test in the
/// "Integration" collection, so the migrate cost is paid once. Tests stay
/// independent by using unique data (e.g. unique emails) rather than resetting.
/// </summary>
[CollectionDefinition("Integration")]
public sealed class IntegrationCollection : ICollectionFixture<PlanoraWebAppFactory>;
