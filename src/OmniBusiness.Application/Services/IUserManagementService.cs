using OmniBusiness.Application.Contracts;

namespace OmniBusiness.Application.Services;

public interface IUserManagementService
{
    Task<WorkspaceUsersDto> CreateUserAsync(
        Guid tenantId,
        SaveWorkspaceUserRequestDto request,
        CancellationToken cancellationToken);

    Task<WorkspaceUsersDto> UpdateUserAsync(
        Guid tenantId,
        Guid userId,
        SaveWorkspaceUserRequestDto request,
        CancellationToken cancellationToken);
}
