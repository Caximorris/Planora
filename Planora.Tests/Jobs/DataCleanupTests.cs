using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Planora.Api.Domain.Entities;
using Planora.Api.Infrastructure.Data;
using Planora.Api.Infrastructure.Jobs;
using Planora.Shared.DTOs.Card;
using Planora.Shared.DTOs.Column;
using Planora.Shared.Enums;
using Planora.Tests.Infrastructure;

namespace Planora.Tests.Jobs;

/// <summary>
/// The scheduled cleanup pass must remove only rows that are genuinely stale — expired refresh
/// tokens, expired invitations, and trash past its retention window — and leave active rows and
/// recent trash alone. Tests exercise <see cref="DataCleanupRunner"/> directly with a controlled
/// "now" so nothing depends on the background timer.
/// </summary>
[Collection("Integration")]
public class DataCleanupTests(PlanoraWebAppFactory factory)
{
    private static readonly TimeSpan Retention = TimeSpan.FromDays(30);

    [Fact]
    public async Task Removes_expired_refresh_tokens_and_keeps_active_ones()
    {
        var (_, auth) = await factory.RegisterAndAuthenticateAsync();
        var now = DateTime.UtcNow;

        var expired = new RefreshToken { UserId = auth.UserId, Token = Guid.NewGuid().ToString("N"), ExpiresAt = now.AddDays(-1) };
        var active = new RefreshToken { UserId = auth.UserId, Token = Guid.NewGuid().ToString("N"), ExpiresAt = now.AddDays(7) };

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.RefreshTokens.AddRange(expired, active);
        await db.SaveChangesAsync();

        await scope.ServiceProvider.GetRequiredService<DataCleanupRunner>().RunAsync(now, Retention);

        Assert.False(await db.RefreshTokens.AnyAsync(t => t.Id == expired.Id));
        Assert.True(await db.RefreshTokens.AnyAsync(t => t.Id == active.Id));
    }

    [Fact]
    public async Task Removes_expired_invitations_and_keeps_pending_ones()
    {
        var (client, auth) = await factory.RegisterAndAuthenticateAsync();
        var workspace = await client.CreateWorkspaceAsync("Cleanup WS");
        var now = DateTime.UtcNow;

        var expired = new WorkspaceInvitation
        {
            WorkspaceId = workspace.Id, InviterUserId = auth.UserId, InviteeEmail = $"a.{Guid.NewGuid():N}@planora.test",
            Token = Guid.NewGuid().ToString("N"), ExpiresAt = now.AddDays(-1), Status = InvitationStatus.Pending
        };
        var pending = new WorkspaceInvitation
        {
            WorkspaceId = workspace.Id, InviterUserId = auth.UserId, InviteeEmail = $"b.{Guid.NewGuid():N}@planora.test",
            Token = Guid.NewGuid().ToString("N"), ExpiresAt = now.AddDays(7), Status = InvitationStatus.Pending
        };

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.WorkspaceInvitations.AddRange(expired, pending);
        await db.SaveChangesAsync();

        await scope.ServiceProvider.GetRequiredService<DataCleanupRunner>().RunAsync(now, Retention);

        Assert.False(await db.WorkspaceInvitations.AnyAsync(i => i.Id == expired.Id));
        Assert.True(await db.WorkspaceInvitations.AnyAsync(i => i.Id == pending.Id));
    }

    [Fact]
    public async Task Purges_trash_past_retention_and_keeps_recent_trash()
    {
        var (client, _) = await factory.RegisterAndAuthenticateAsync();
        var workspace = await client.CreateWorkspaceAsync("Retention WS");
        var oldBoard = await client.CreateBoardAsync(workspace.Id, "Old Trash Board");
        var recentBoard = await client.CreateBoardAsync(workspace.Id, "Recent Trash Board");

        // A card living under a board that stays; it is itself trashed long ago and must be purged.
        var column = await CreateColumnAsync(client, recentBoard.Id);
        var oldCard = await CreateCardAsync(client, column.Id);

        var now = DateTime.UtcNow;
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Trash the rows directly with controlled timestamps.
        await db.Boards.IgnoreQueryFilters().Where(b => b.Id == oldBoard.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(b => b.DeletedAt, now.AddDays(-40)));
        await db.Boards.IgnoreQueryFilters().Where(b => b.Id == recentBoard.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(b => b.DeletedAt, now.AddDays(-1)));
        await db.Cards.IgnoreQueryFilters().Where(c => c.Id == oldCard.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.DeletedAt, now.AddDays(-40)));

        var result = await scope.ServiceProvider.GetRequiredService<DataCleanupRunner>().RunAsync(now, Retention);

        // Old board gone (with its column), recent-trash board still present.
        Assert.False(await db.Boards.IgnoreQueryFilters().AnyAsync(b => b.Id == oldBoard.Id));
        Assert.True(await db.Boards.IgnoreQueryFilters().AnyAsync(b => b.Id == recentBoard.Id));
        // Old trashed card gone even though its board is kept.
        Assert.False(await db.Cards.IgnoreQueryFilters().AnyAsync(c => c.Id == oldCard.Id));
        Assert.True(result.Total >= 2);
    }

    private static async Task<ColumnDto> CreateColumnAsync(HttpClient client, Guid boardId)
    {
        var res = await client.PostAsJsonAsync("/api/columns", new CreateColumnRequest { BoardId = boardId, Title = "Todo" });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<ColumnDto>())!;
    }

    private static async Task<CardDto> CreateCardAsync(HttpClient client, Guid columnId)
    {
        var res = await client.PostAsJsonAsync("/api/cards", new CreateCardRequest { ColumnId = columnId, Title = "Old card" });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<CardDto>())!;
    }
}
