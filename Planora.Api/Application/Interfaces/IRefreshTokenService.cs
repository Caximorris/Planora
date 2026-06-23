using Planora.Api.Domain.Entities;

namespace Planora.Api.Application.Interfaces;

public interface IRefreshTokenService
{
    Task<string> CreateAsync(string userId);
    Task<(bool IsValid, AppUser? User)> ValidateAsync(string token);
    Task RevokeAsync(string token);
    Task RevokeAllAsync(string userId);
}
