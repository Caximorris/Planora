using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Planora.Api.Application.Interfaces;
using Planora.Api.Domain.Entities;
using Planora.Api.Infrastructure.Data;

namespace Planora.Api.Application.Services;

public class RefreshTokenService : IRefreshTokenService
{
    private readonly ApplicationDbContext _db;
    private readonly IConfiguration _config;

    public RefreshTokenService(ApplicationDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    public async Task<string> CreateAsync(string userId)
    {
        // Housekeeping: remove expired/revoked tokens for this user on each create
        var stale = await _db.RefreshTokens
            .Where(t => t.UserId == userId && (t.RevokedAt != null || t.ExpiresAt <= DateTime.UtcNow))
            .ToListAsync();
        _db.RefreshTokens.RemoveRange(stale);

        var tokenBytes = RandomNumberGenerator.GetBytes(32);
        var tokenString = Convert.ToBase64String(tokenBytes)
            .Replace("+", "-").Replace("/", "_").TrimEnd('=');

        var days = int.TryParse(_config["Jwt:RefreshTokenDays"], out var d) ? d : 7;
        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = userId,
            Token = tokenString,
            ExpiresAt = DateTime.UtcNow.AddDays(days)
        });

        await _db.SaveChangesAsync();
        return tokenString;
    }

    public async Task<(bool IsValid, AppUser? User)> ValidateAsync(string token)
    {
        var rt = await _db.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Token == token);

        if (rt is null) return (false, null);

        // Reuse detection: revoked token used again — revoke all to contain potential theft
        if (rt.RevokedAt is not null)
        {
            await RevokeAllAsync(rt.UserId);
            return (false, null);
        }

        if (rt.ExpiresAt <= DateTime.UtcNow) return (false, null);

        return (true, rt.User);
    }

    public async Task RevokeAsync(string token)
    {
        var rt = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.Token == token);
        if (rt is null) return;
        rt.RevokedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task RevokeAllAsync(string userId)
    {
        var active = await _db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ToListAsync();
        foreach (var t in active)
            t.RevokedAt = DateTime.UtcNow;
        if (active.Count > 0)
            await _db.SaveChangesAsync();
    }
}
