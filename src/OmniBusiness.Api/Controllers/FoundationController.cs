using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OmniBusiness.Api.Infrastructure;
using OmniBusiness.Application.Contracts;
using OmniBusiness.Application.Services;

namespace OmniBusiness.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/foundation")]
public sealed class FoundationController(
    IWorkspaceQueryService workspaceQueryService,
    IModuleManagementService moduleManagementService) : ControllerBase
{
    [HttpGet("context")]
    [ProducesResponseType<WorkspaceContextDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<WorkspaceContextDto>> GetContextAsync(CancellationToken cancellationToken)
    {
        var tenantId = User.GetTenantId();
        var userId = User.GetUserId();
        var context = await workspaceQueryService.GetWorkspaceContextAsync(tenantId, userId, cancellationToken);
        return Ok(context);
    }

    [HttpGet("module-settings")]
    [Authorize(Roles = "Owner")]
    [ProducesResponseType<ModuleSettingsDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ModuleSettingsDto>> GetModuleSettingsAsync(CancellationToken cancellationToken)
    {
        var tenantId = User.GetTenantId();
        var settings = await workspaceQueryService.GetModuleSettingsAsync(tenantId, cancellationToken);
        return Ok(settings);
    }

    [HttpPut("module-settings")]
    [Authorize(Roles = "Owner")]
    [ProducesResponseType<ModuleSettingsDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ModuleSettingsDto>> UpdateModuleSettingsAsync(
        [FromBody] SaveModuleSettingsRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = User.GetTenantId();
        var settings = await moduleManagementService.UpdateModuleSettingsAsync(
            tenantId,
            new SaveModuleSettingsRequestDto(
                request.PlanCode,
                request.PlanName,
                request.Currency,
                request.BaseMonthlyPrice,
                request.IncludedUsers,
                request.IncludedBranches,
                request.AllowCustomModuleOverrides,
                request.Modules
                    .Select(module => new SaveModuleEntitlementRequestDto(
                        module.ModuleKey,
                        module.Enabled,
                        module.AddOnMonthlyPrice))
                    .ToArray()),
            cancellationToken);

        return Ok(settings);
    }

    public sealed class SaveModuleSettingsRequest
    {
        public string PlanCode { get; init; } = "starter";

        public string PlanName { get; init; } = "Starter";

        public string Currency { get; init; } = "PKR";

        public decimal BaseMonthlyPrice { get; init; }

        public int IncludedUsers { get; init; } = 3;

        public int IncludedBranches { get; init; } = 1;

        public bool AllowCustomModuleOverrides { get; init; } = true;

        public IReadOnlyList<SaveModuleEntitlementRequest> Modules { get; init; } = Array.Empty<SaveModuleEntitlementRequest>();
    }

    public sealed class SaveModuleEntitlementRequest
    {
        public string ModuleKey { get; init; } = string.Empty;

        public bool Enabled { get; init; }

        public decimal AddOnMonthlyPrice { get; init; }
    }
}
