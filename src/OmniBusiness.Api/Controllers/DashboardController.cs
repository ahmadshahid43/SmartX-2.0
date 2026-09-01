using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OmniBusiness.Api.Infrastructure;
using OmniBusiness.Application.Contracts;
using OmniBusiness.Application.Services;

namespace OmniBusiness.Api.Controllers;

[ApiController]
[Authorize(Roles = "Owner,Manager,Back Office")]
[Route("api/v1/dashboard")]
public sealed class DashboardController(IWorkspaceQueryService workspaceQueryService) : ControllerBase
{
    [HttpGet("overview")]
    [ProducesResponseType<DashboardOverviewDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<DashboardOverviewDto>> GetOverviewAsync(CancellationToken cancellationToken)
    {
        var overview = await workspaceQueryService.GetDashboardAsync(User.GetTenantId(), cancellationToken);
        return Ok(overview);
    }

    [HttpGet("reports")]
    [ProducesResponseType<ReportsHubDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ReportsHubDto>> GetReportsAsync(CancellationToken cancellationToken)
    {
        var reports = await workspaceQueryService.GetReportsHubAsync(User.GetTenantId(), cancellationToken);
        return Ok(reports);
    }
}
