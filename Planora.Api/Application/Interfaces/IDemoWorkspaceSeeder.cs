namespace Planora.Api.Application.Interfaces;

public interface IDemoWorkspaceSeeder
{
    /// <summary>
    /// Creates the lightweight welcome content by default. Full showcase content
    /// is reserved for instant demo accounts so normal registrations stay small.
    /// </summary>
    Task SeedAsync(string userId, bool fullShowcase = false);
}
