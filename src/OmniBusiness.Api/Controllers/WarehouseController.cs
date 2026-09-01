using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OmniBusiness.Api.Infrastructure;
using OmniBusiness.Application.Contracts;
using OmniBusiness.Application.Services;

namespace OmniBusiness.Api.Controllers;

[ApiController]
[Authorize(Roles = "Owner,Manager,Back Office")]
[Route("api/v1/warehouse")]
public sealed class WarehouseController(
    IWorkspaceQueryService workspaceQueryService,
    IWarehouseWorkflowService warehouseWorkflowService) : ControllerBase
{
    [HttpGet("hub")]
    [ProducesResponseType<WarehouseHubDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<WarehouseHubDto>> GetHubAsync(CancellationToken cancellationToken)
    {
        var hub = await workspaceQueryService.GetWarehouseHubAsync(User.GetTenantId(), cancellationToken);
        return Ok(hub);
    }

    [HttpPost("stock-transfers")]
    [ProducesResponseType<WarehouseHubDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<WarehouseHubDto>> CreateStockTransferAsync(
        [FromBody] SaveStockTransferRequestDto request,
        CancellationToken cancellationToken)
    {
        var hub = await warehouseWorkflowService.CreateStockTransferAsync(
            User.GetTenantId(),
            User.GetUserId(),
            request,
            cancellationToken);

        return Ok(hub);
    }

    [HttpPost("goods-receipts")]
    [ProducesResponseType<WarehouseHubDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<WarehouseHubDto>> CreateGoodsReceiptAsync(
        [FromBody] SaveGoodsReceiptRequestDto request,
        CancellationToken cancellationToken)
    {
        var hub = await warehouseWorkflowService.CreateGoodsReceiptAsync(
            User.GetTenantId(),
            User.GetUserId(),
            request,
            cancellationToken);

        return Ok(hub);
    }

    [HttpPost("gate-passes")]
    [ProducesResponseType<WarehouseHubDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<WarehouseHubDto>> CreateGatePassAsync(
        [FromBody] SaveGatePassRequestDto request,
        CancellationToken cancellationToken)
    {
        var hub = await warehouseWorkflowService.CreateGatePassAsync(
            User.GetTenantId(),
            User.GetUserId(),
            request,
            cancellationToken);

        return Ok(hub);
    }
}
