using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OmniBusiness.Api.Infrastructure;
using OmniBusiness.Application.Contracts;
using OmniBusiness.Application.Services;

namespace OmniBusiness.Api.Controllers;

[ApiController]
[Authorize(Roles = "Owner,Manager,Back Office")]
[Route("api/v1/procurement")]
public sealed class ProcurementController(IWorkspaceQueryService workspaceQueryService) : ControllerBase
{
    [HttpGet("hub")]
    [ProducesResponseType<ProcurementHubDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ProcurementHubDto>> GetHubAsync(CancellationToken cancellationToken)
    {
        var hub = await workspaceQueryService.GetProcurementHubAsync(User.GetTenantId(), cancellationToken);
        return Ok(hub);
    }
}
