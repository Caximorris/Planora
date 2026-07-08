using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Planora.Api.Application.Interfaces;
using Planora.Api.Domain.Entities;
using Planora.Api.Infrastructure.Data;
using Planora.Shared.DTOs.Account;

namespace Planora.Api.Application.Services;

/// <summary>
/// Implements account export and permanent deletion. Deletion relies on the schema's cascade rules
/// (memberships, comments, activity, notifications cascade with the user; card assignments are set
/// null) and enforces the one relationship that does not cascade: workspace ownership is
/// <c>Restrict</c>, so an owned workspace must be removed or transferred first.
/// </summary>
public sealed class AccountService : IAccountService
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<AppUser> _userManager;

    public AccountService(ApplicationDbContext db, UserManager<AppUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<AccountExportDto?> BuildExportAsync(string userId, CancellationToken ct = default)
    {
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return null;

        var workspaceIds = await _db.WorkspaceMembers
            .Where(m => m.UserId == userId)
            .Select(m => m.WorkspaceId)
            .ToListAsync(ct);

        // Load the full graph for the user's workspaces. IgnoreQueryFilters lifts the archived/trashed
        // filters on boards/cards so archived items are exported; trashed rows are dropped in-memory
        // below. Mapping happens in memory so enum-to-string conversions are trivially correct.
        var workspaces = await _db.Workspaces
            .IgnoreQueryFilters()
            .Where(w => workspaceIds.Contains(w.Id))
            .Include(w => w.Members).ThenInclude(m => m.User)
            .Include(w => w.Labels)
            .Include(w => w.Boards).ThenInclude(b => b.Columns).ThenInclude(c => c.Cards).ThenInclude(cd => cd.Comments)
            .Include(w => w.Boards).ThenInclude(b => b.Columns).ThenInclude(c => c.Cards).ThenInclude(cd => cd.Labels)
            .Include(w => w.Boards).ThenInclude(b => b.Columns).ThenInclude(c => c.Cards).ThenInclude(cd => cd.Checklists).ThenInclude(ch => ch.Items)
            .Include(w => w.Boards).ThenInclude(b => b.Columns).ThenInclude(c => c.Cards).ThenInclude(cd => cd.Attachments)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(ct);

        return new AccountExportDto
        {
            ExportedAt = DateTime.UtcNow,
            User = new ExportUserDto
            {
                DisplayName = user.DisplayName,
                Email = user.Email ?? string.Empty,
                EmailConfirmed = user.EmailConfirmed,
                CreatedAt = user.CreatedAt,
                EmailOnAssigned = user.EmailOnAssigned,
                EmailOnComment = user.EmailOnComment,
                EmailOnWorkspaceInvite = user.EmailOnWorkspaceInvite,
            },
            Workspaces = workspaces
                .OrderBy(w => w.Name)
                .Select(w => MapWorkspace(w, userId))
                .ToList(),
        };
    }

    private static ExportWorkspaceDto MapWorkspace(Workspace w, string userId)
    {
        var self = w.Members.FirstOrDefault(m => m.UserId == userId);
        return new ExportWorkspaceDto
        {
            Id = w.Id,
            Name = w.Name,
            Description = w.Description,
            Role = self?.Role.ToString() ?? string.Empty,
            IsOwner = w.OwnerId == userId,
            JoinedAt = self?.JoinedAt ?? default,
            Members = w.Members
                .OrderByDescending(m => m.Role)
                .Select(m => new ExportMemberDto
                {
                    UserId = m.UserId,
                    DisplayName = m.User?.DisplayName ?? string.Empty,
                    Email = m.User?.Email ?? string.Empty,
                    Role = m.Role.ToString(),
                })
                .ToList(),
            Labels = w.Labels
                .OrderBy(l => l.Name)
                .Select(l => new ExportLabelDto { Id = l.Id, Name = l.Name, Color = l.Color })
                .ToList(),
            Boards = w.Boards
                .Where(b => b.DeletedAt == null)
                .OrderBy(b => b.Position)
                .Select(MapBoard)
                .ToList(),
        };
    }

    private static ExportBoardDto MapBoard(Board b) => new()
    {
        Id = b.Id,
        Name = b.Name,
        Description = b.Description,
        IsArchived = b.IsArchived,
        CreatedAt = b.CreatedAt,
        Columns = b.Columns
            .OrderBy(c => c.Position)
            .Select(c => new ExportColumnDto
            {
                Id = c.Id,
                Title = c.Title,
                Position = c.Position,
                Cards = c.Cards
                    .Where(cd => cd.DeletedAt == null)
                    .OrderBy(cd => cd.Position)
                    .Select(MapCard)
                    .ToList(),
            })
            .ToList(),
    };

    private static ExportCardDto MapCard(Card cd) => new()
    {
        Id = cd.Id,
        Title = cd.Title,
        Description = cd.Description,
        Priority = cd.Priority.ToString(),
        Position = cd.Position,
        DueDate = cd.DueDate,
        IsArchived = cd.IsArchived,
        AssigneeId = cd.AssigneeId,
        CreatedAt = cd.CreatedAt,
        LabelIds = cd.Labels.Select(cl => cl.LabelId).ToList(),
        Comments = cd.Comments
            .OrderBy(cm => cm.CreatedAt)
            .Select(cm => new ExportCommentDto { AuthorId = cm.AuthorId, Text = cm.Text, CreatedAt = cm.CreatedAt })
            .ToList(),
        Checklists = cd.Checklists
            .OrderBy(ch => ch.Position)
            .Select(ch => new ExportChecklistDto
            {
                Title = ch.Title,
                Items = ch.Items
                    .OrderBy(i => i.Position)
                    .Select(i => new ExportChecklistItemDto { Text = i.Text, IsCompleted = i.IsCompleted })
                    .ToList(),
            })
            .ToList(),
        Attachments = cd.Attachments
            .OrderBy(a => a.CreatedAt)
            .Select(a => new ExportAttachmentDto
            {
                FileName = a.FileName,
                ContentType = a.ContentType,
                SizeBytes = a.SizeBytes,
                Url = a.Url,
                CreatedAt = a.CreatedAt,
            })
            .ToList(),
    };

    public async Task<AccountDeletionResult> DeleteAccountAsync(string userId, string password, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return AccountDeletionResult.NotFound;

        if (!await _userManager.CheckPasswordAsync(user, password))
            return AccountDeletionResult.WrongPassword;

        // Workspaces this user owns, with their member counts. Ownership is a Restrict FK, so any
        // still-owned workspace would otherwise fail the user delete at the database.
        var owned = await _db.Workspaces
            .Where(w => w.OwnerId == userId)
            .Select(w => new { w.Id, w.Name, MemberCount = w.Members.Count })
            .ToListAsync(ct);

        var blocking = owned
            .Where(w => w.MemberCount > 1)
            .Select(w => new BlockedWorkspaceDto { Id = w.Id, Name = w.Name, MemberCount = w.MemberCount })
            .ToList();
        if (blocking.Count > 0)
            return AccountDeletionResult.Blocked(blocking);

        // Remaining owned workspaces have only this user as a member — safe to delete (cascade removes
        // boards/columns/cards/labels/members and their children).
        var soloIds = owned.Select(w => w.Id).ToList();
        if (soloIds.Count > 0)
        {
            var solo = await _db.Workspaces.Where(w => soloIds.Contains(w.Id)).ToListAsync(ct);
            _db.Workspaces.RemoveRange(solo);
            await _db.SaveChangesAsync(ct);
        }

        // Drop refresh tokens explicitly (no FK to the user) before removing the account.
        await _db.RefreshTokens.Where(t => t.UserId == userId).ExecuteDeleteAsync(ct);

        var result = await _userManager.DeleteAsync(user);
        if (!result.Succeeded)
            return AccountDeletionResult.Failed(string.Join("; ", result.Errors.Select(e => e.Description)));

        return AccountDeletionResult.Success;
    }
}
