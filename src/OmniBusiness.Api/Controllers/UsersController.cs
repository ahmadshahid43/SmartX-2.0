using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OmniBusiness.Api.Infrastructure;
using OmniBusiness.Application.Contracts;
using OmniBusiness.Application.Services;

namespace OmniBusiness.Api.Controllers;

[ApiController]
[Route("api/v1/users")]
[Authorize(Roles = "Owner")]
public sealed class UsersController(
    IWorkspaceQueryService workspaceQueryService,
    IUserManagementService userManagementService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<WorkspaceUsersDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<WorkspaceUsersDto>> GetUsersAsync(CancellationToken cancellationToken)
    {
        var tenantId = User.GetTenantId();
        return Ok(await workspaceQueryService.GetUsersAsync(tenantId, cancellationToken));
    }

    [HttpPost]
    [ProducesResponseType<WorkspaceUsersDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<WorkspaceUsersDto>> CreateUserAsync(
        [FromBody] SaveUserCommand command,
        CancellationToken cancellationToken)
    {
        var tenantId = User.GetTenantId();
        var result = await userManagementService.CreateUserAsync(
            tenantId,
            new SaveWorkspaceUserRequestDto(
                command.Email,
                command.DisplayName,
                command.Role,
                command.BranchId,
                command.Password),
            cancellationToken);

        return Ok(result);
    }

    [HttpPut("{userId:guid}")]
    [ProducesResponseType<WorkspaceUsersDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<WorkspaceUsersDto>> UpdateUserAsync(
        Guid userId,
        [FromBody] SaveUserCommand command,
        CancellationToken cancellationToken)
    {
        var tenantId = User.GetTenantId();
        var result = await userManagementService.UpdateUserAsync(
            tenantId,
            userId,
            new SaveWorkspaceUserRequestDto(
                command.Email,
                command.DisplayName,
                command.Role,
                command.BranchId,
                command.Password),
            cancellationToken);

        return Ok(result);
    }

    public sealed class SaveUserCommand
    {
        [Required]
        [EmailAddress]
        public string Email { get; init; } = string.Empty;

        [Required]
        public string DisplayName { get; init; } = string.Empty;

        [Required]
        public string Role { get; init; } = string.Empty;

        [Required]
        public Guid BranchId { get; init; }

        public string? Password { get; init; }
    }
}
