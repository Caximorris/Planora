namespace Planora.Api.Application.Interfaces;

public interface IDemoWorkspaceSeeder
{
    /// <summary>
    /// Creates a showcase workspace with a sample board, columns and cards
    /// for a newly registered user, demonstrating the app's capabilities.
    /// </summary>
    Task SeedAsync(string userId);
}
