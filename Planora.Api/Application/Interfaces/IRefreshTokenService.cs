using Planora.Api.Domain.Entities;

namespace Planora.Api.Application.Interfaces;

public interface IRefreshTokenService
{
    Task<string> CreateAsync(string userId);
    Task<(bool IsValid, AppUser? User)> ValidateAsync(string token);
    Task<string?> GetUserIdAsync(string token);
    Task<IReadOnlyList<RefreshToken>> GetActiveSessionsAsync(string userId);
    Task RevokeAsync(string token);
    Task<bool> RevokeSessionAsync(string userId, Guid sessionId);
    Task<bool> RevokeOtherSessionsAsync(string userId, string currentRefreshToken);
    Task RevokeAllAsync(string userId);
}
