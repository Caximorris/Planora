using Microsoft.EntityFrameworkCore;
using Planora.Api.Application.Interfaces;
using Planora.Api.Infrastructure.Data;

namespace Planora.Api.Infrastructure.Jobs;

/// <summary>
/// Purges rows that are safe to remove permanently: expired refresh tokens, expired
/// invitations, and trash (soft-deleted boards/cards) past the retention window.
///
/// Deleting expired refresh tokens does NOT weaken reuse detection: that mechanism only
/// matters for a revoked-but-still-valid token being replayed (a session-hijack signal), and
/// those are never expired, so this predicate leaves them untouched.
///
/// Kept as a scoped service (separate from the hosted timer) so the deletion logic can be
/// exercised directly in tests with a controlled "now" instead of waiting on a schedule.
/// </summary>
public sealed class DataCleanupRunner
{
    private readonly ApplicationDbContext _db;
    private readonly IFileStorage _storage;
    private readonly ILogger<DataCleanupRunner> _logger;

    public DataCleanupRunner(ApplicationDbContext db, IFileStorage storage, ILogger<DataCleanupRunner> logger)
    {
        _db = db;
        _storage = storage;
        _logger = logger;
    }

    public async Task<CleanupResult> RunAsync(DateTime nowUtc, TimeSpan trashRetention, CancellationToken ct = default)
    {
        var tokens = await _db.RefreshTokens
            .Where(t => t.ExpiresAt < nowUtc)
            .ExecuteDeleteAsync(ct);

        var invitations = await _db.WorkspaceInvitations
            .Where(i => i.ExpiresAt < nowUtc)
            .ExecuteDeleteAsync(ct);

        var trashCutoff = nowUtc - trashRetention;

        // Cards trashed long ago and not part of a board being purged below.
        var cards = await _db.Cards
            .IgnoreQueryFilters()
            .Where(c => c.DeletedAt != null && c.DeletedAt < trashCutoff)
            .ExecuteDeleteAsync(ct);

        // Boards are loaded (not ExecuteDelete) so their cover images can be removed from storage;
        // the DB cascade takes care of columns/cards/comments under each purged board.
        var boardsToPurge = await _db.Boards
            .IgnoreQueryFilters()
            .Where(b => b.DeletedAt != null && b.DeletedAt < trashCutoff)
            .ToListAsync(ct);

        foreach (var board in boardsToPurge)
            await _storage.DeleteAsync(board.CoverImageUrl);

        if (boardsToPurge.Count > 0)
        {
            _db.Boards.RemoveRange(boardsToPurge);
            await _db.SaveChangesAsync(ct);
        }

        var result = new CleanupResult(tokens, invitations, cards, boardsToPurge.Count);
        if (result.Total > 0)
        {
            _logger.LogInformation(
                "Data cleanup removed {Tokens} expired refresh tokens, {Invitations} expired invitations, " +
                "{Cards} trashed cards, {Boards} trashed boards.",
                result.RefreshTokens, result.Invitations, result.TrashedCards, result.TrashedBoards);
        }

        return result;
    }
}

/// <summary>Counts of rows removed in a single cleanup pass.</summary>
public readonly record struct CleanupResult(int RefreshTokens, int Invitations, int TrashedCards, int TrashedBoards)
{
    public int Total => RefreshTokens + Invitations + TrashedCards + TrashedBoards;
}
