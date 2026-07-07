using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
