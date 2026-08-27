using System.Security.Claims;

namespace OmniBusiness.Api.Infrastructure;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetTenantId(this ClaimsPrincipal user)
        => Guid.Parse(user.FindFirstValue("tenant_id")
            ?? throw new InvalidOperationException("The authenticated user does not contain a tenant identifier."));

    public static Guid GetUserId(this ClaimsPrincipal user)
        => Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("The authenticated user does not contain a user identifier."));
}
