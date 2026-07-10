using Planora.Api.Domain.Entities;
using Planora.Shared.Enums;

namespace Planora.Api.Application.Interfaces;

public interface IWorkspaceAccessService
{
    Task<bool> IsMemberAsync(Guid workspaceId, string userId, CancellationToken ct = default);

    Task<WorkspaceMember?> GetMembershipAsync(Guid workspaceId, string userId, CancellationToken ct = default);

    Task<bool> HasAnyRoleAsync(
        Guid workspaceId,
        string userId,
        IReadOnlyCollection<WorkspaceRole> roles,
        CancellationToken ct = default);
}
