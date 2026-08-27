namespace OmniBusiness.Application.Contracts;

public sealed record LoginRequest(string Identifier, string Password);

public sealed record LoginResponse(
    string AccessToken,
    DateTimeOffset ExpiresAt,
    WorkspaceUserDto User);

public sealed record WorkspaceUserDto(
    Guid UserId,
    Guid TenantId,
    Guid BranchId,
    string Email,
    string DisplayName,
    string Role);
