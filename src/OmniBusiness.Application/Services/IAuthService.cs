using OmniBusiness.Application.Contracts;

namespace OmniBusiness.Application.Services;

public interface IAuthService
{
    Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken);

    Task<WorkspaceUserDto?> GetUserAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken);
}
