using Microsoft.EntityFrameworkCore;
using Planora.Api.Application.Interfaces;
using Planora.Api.Domain.Entities;
using Planora.Api.Infrastructure.Data;
using Planora.Shared.Enums;

namespace Planora.Api.Application.Services;

public sealed class WorkspaceAccessService : IWorkspaceAccessService
{
    private readonly ApplicationDbContext _db;

    public WorkspaceAccessService(ApplicationDbContext db) => _db = db;

    public Task<bool> IsMemberAsync(Guid workspaceId, string userId, CancellationToken ct = default) =>
        _db.WorkspaceMembers.AnyAsync(m => m.WorkspaceId == workspaceId && m.UserId == userId, ct);

    public Task<WorkspaceMember?> GetMembershipAsync(Guid workspaceId, string userId, CancellationToken ct = default) =>
        _db.WorkspaceMembers.FirstOrDefaultAsync(m => m.WorkspaceId == workspaceId && m.UserId == userId, ct);

    public async Task<bool> HasAnyRoleAsync(
        Guid workspaceId,
        string userId,
        IReadOnlyCollection<WorkspaceRole> roles,
        CancellationToken ct = default)
    {
        if (roles.Count == 0)
            return false;

        var membership = await GetMembershipAsync(workspaceId, userId, ct);
        return membership is not null && roles.Contains(membership.Role);
    }
}
