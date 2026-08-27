using OmniBusiness.Application.Abstractions.Persistence;
using OmniBusiness.Application.Abstractions.Security;
using OmniBusiness.Application.Contracts;

namespace OmniBusiness.Application.Services;

public sealed class AuthService(
    IWorkspaceRepository workspaceRepository,
    IPasswordHasher passwordHasher,
    ITokenService tokenService) : IAuthService
{
    public async Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await workspaceRepository.GetUserByLoginIdentifierAsync(request.Identifier, cancellationToken);
        if (user is null || !passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            return null;
        }

        var workspaceUser = new WorkspaceUserDto(
            user.Id,
            user.TenantId,
            user.BranchId,
            user.Email,
            user.DisplayName,
            user.Role);

        var token = tokenService.CreateToken(workspaceUser);
        var validated = tokenService.ValidateToken(token)!;

        return new LoginResponse(token, validated.ExpiresAt, workspaceUser);
    }

    public async Task<WorkspaceUserDto?> GetUserAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken)
    {
        var user = await workspaceRepository.GetUserByIdAsync(tenantId, userId, cancellationToken);
        return user is null
            ? null
            : new WorkspaceUserDto(
                user.Id,
                user.TenantId,
                user.BranchId,
                user.Email,
                user.DisplayName,
                user.Role);
    }
}
