using OmniBusiness.Domain.Foundation;

namespace OmniBusiness.Application.Abstractions.Persistence;

public interface IWorkspaceRepository
{
    Task<WorkspaceSnapshot> GetWorkspaceSnapshotAsync(CancellationToken cancellationToken);

    Task<WorkspaceSnapshot> UpdateWorkspaceSnapshotAsync(
        Func<WorkspaceSnapshot, WorkspaceSnapshot> update,
        CancellationToken cancellationToken);

    Task<AppUser?> GetUserByLoginIdentifierAsync(string identifier, CancellationToken cancellationToken);

    Task<AppUser?> GetUserByIdAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken);
}
