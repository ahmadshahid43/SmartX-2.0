using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using OmniBusiness.Application.Abstractions.Security;
using OmniBusiness.Application.Contracts;

namespace OmniBusiness.Infrastructure.Security;

public sealed class ProtectedTokenService(
    IDataProtectionProvider dataProtectionProvider,
    IConfiguration configuration) : ITokenService
{
    private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector("OmniBusiness.Foundation.AccessToken");
    private readonly int _tokenLifetimeMinutes = configuration.GetValue("Auth:TokenLifetimeMinutes", 480);

    public string CreateToken(WorkspaceUserDto user)
    {
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(_tokenLifetimeMinutes);
        var payload = new TokenEnvelope(
            user.UserId,
            user.TenantId,
            user.BranchId,
            user.Email,
            user.DisplayName,
            user.Role,
            expiresAt);

        var protectedPayload = _protector.Protect(JsonSerializer.Serialize(payload));
        return WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(protectedPayload));
    }

    public TokenValidationResult? ValidateToken(string token)
    {
        try
        {
            var protectedPayload = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token));
            var json = _protector.Unprotect(protectedPayload);
            var payload = JsonSerializer.Deserialize<TokenEnvelope>(json);

            if (payload is null || payload.ExpiresAt <= DateTimeOffset.UtcNow)
            {
                return null;
            }

            return new TokenValidationResult(
                payload.UserId,
                payload.TenantId,
                payload.BranchId,
                payload.Email,
                payload.DisplayName,
                payload.Role,
                payload.ExpiresAt);
        }
        catch
        {
            return null;
        }
    }

    private sealed record TokenEnvelope(
        Guid UserId,
        Guid TenantId,
        Guid BranchId,
        string Email,
        string DisplayName,
        string Role,
        DateTimeOffset ExpiresAt);
}
