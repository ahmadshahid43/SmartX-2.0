using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OmniBusiness.Api.Infrastructure;
using OmniBusiness.Application.Contracts;
using OmniBusiness.Application.Services;

namespace OmniBusiness.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(IAuthService authService) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> LoginAsync(
        [FromBody] LoginCommand command,
        CancellationToken cancellationToken)
    {
        var response = await authService.LoginAsync(new LoginRequest(command.Email, command.Password), cancellationToken);
        if (response is null)
        {
            return Unauthorized(new ApiErrorResponse(
                false,
                "INVALID_CREDENTIALS",
                "The provided username, email, or password was not recognized.",
                Array.Empty<string>()));
        }

        return Ok(response);
    }

    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType<WorkspaceUserDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<WorkspaceUserDto>> GetCurrentUserAsync(CancellationToken cancellationToken)
    {
        var tenantId = User.GetTenantId();
        var userId = User.GetUserId();
        var user = await authService.GetUserAsync(tenantId, userId, cancellationToken);

        return user is null ? NotFound() : Ok(user);
    }

    public sealed class LoginCommand
    {
        [Required]
        public string Email { get; init; } = string.Empty;

        [Required]
        [MinLength(8)]
        public string Password { get; init; } = string.Empty;
    }
}
