using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OmniBusiness.Application.Abstractions.Persistence;
using OmniBusiness.Application.Abstractions.Security;

namespace OmniBusiness.Infrastructure.Security;

public sealed class OmniBusinessBearerAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    ITokenService tokenService,
    IWorkspaceRepository workspaceRepository) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.Authorization.Any())
        {
            return AuthenticateResult.NoResult();
        }

        var header = Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.Fail("Unsupported authorization scheme.");
        }

        var token = header["Bearer ".Length..].Trim();
        var validated = tokenService.ValidateToken(token);
        if (validated is null)
        {
            return AuthenticateResult.Fail("The access token is invalid or expired.");
        }

        var currentUser = await workspaceRepository.GetUserByIdAsync(
            validated.TenantId,
            validated.UserId,
            Context.RequestAborted);

        if (currentUser is null)
        {
            return AuthenticateResult.Fail("The user linked to this access token no longer exists.");
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, currentUser.Id.ToString()),
            new Claim(ClaimTypes.Email, currentUser.Email),
            new Claim(ClaimTypes.Name, currentUser.DisplayName),
            new Claim(ClaimTypes.Role, currentUser.Role),
            new Claim("tenant_id", currentUser.TenantId.ToString()),
            new Claim("branch_id", currentUser.BranchId.ToString())
        };

        var identity = new ClaimsIdentity(claims, OmniBusinessAuthenticationDefaults.Scheme);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, OmniBusinessAuthenticationDefaults.Scheme);

        return AuthenticateResult.Success(ticket);
    }
}
