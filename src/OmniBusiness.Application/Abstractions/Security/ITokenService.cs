using OmniBusiness.Application.Contracts;

namespace OmniBusiness.Application.Abstractions.Security;

public interface ITokenService
{
    string CreateToken(WorkspaceUserDto user);

    TokenValidationResult? ValidateToken(string token);
}

public sealed record TokenValidationResult(
    Guid UserId,
    Guid TenantId,
    Guid BranchId,
    string Email,
    string DisplayName,
    string Role,
    DateTimeOffset ExpiresAt);
