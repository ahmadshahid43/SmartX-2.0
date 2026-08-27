using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OmniBusiness.Api.Infrastructure;
using OmniBusiness.Application.Contracts;
using OmniBusiness.Application.Services;

namespace OmniBusiness.Api.Controllers;

[ApiController]
[Authorize(Roles = "Owner,Manager,Cashier,Back Office")]
[Route("api/v1/customers")]
public sealed class CustomersController(IWorkspaceQueryService workspaceQueryService) : ControllerBase
{
    [HttpGet("hub")]
    [ProducesResponseType<CustomerHubDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CustomerHubDto>> GetHubAsync(CancellationToken cancellationToken)
    {
        var hub = await workspaceQueryService.GetCustomerHubAsync(User.GetTenantId(), cancellationToken);
        return Ok(hub);
    }
}
