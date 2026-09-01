using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OmniBusiness.Api.Infrastructure;
using OmniBusiness.Application.Contracts;
using OmniBusiness.Application.Services;

namespace OmniBusiness.Api.Controllers;

[ApiController]
[Authorize(Roles = "Owner,Manager,Back Office")]
[Route("api/v1/inventory")]
public sealed class InventoryController(
    IWorkspaceQueryService workspaceQueryService,
    IInventoryManagementService inventoryManagementService) : ControllerBase
{
    [HttpGet("overview")]
    [ProducesResponseType<InventoryOverviewDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<InventoryOverviewDto>> GetOverviewAsync(CancellationToken cancellationToken)
    {
        var overview = await workspaceQueryService.GetInventoryOverviewAsync(User.GetTenantId(), cancellationToken);
        return Ok(overview);
    }

    [HttpPost("products")]
    [ProducesResponseType<InventoryOverviewDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<InventoryOverviewDto>> CreateProductAsync(
        [FromBody] SaveProductRequestDto request,
        CancellationToken cancellationToken)
    {
        var overview = await inventoryManagementService.CreateProductAsync(
            User.GetTenantId(),
            request,
            cancellationToken);

        return Ok(overview);
    }

    [HttpPut("products/{productId:guid}")]
    [ProducesResponseType<InventoryOverviewDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<InventoryOverviewDto>> UpdateProductAsync(
        Guid productId,
        [FromBody] SaveProductRequestDto request,
        CancellationToken cancellationToken)
    {
        var overview = await inventoryManagementService.UpdateProductAsync(
            User.GetTenantId(),
            productId,
            request,
            cancellationToken);

        return Ok(overview);
    }

    [HttpPost("stock-adjustments")]
    [ProducesResponseType<InventoryOverviewDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<InventoryOverviewDto>> AdjustStockAsync(
        [FromBody] StockAdjustmentRequestDto request,
        CancellationToken cancellationToken)
    {
        var overview = await inventoryManagementService.AdjustStockAsync(
            User.GetTenantId(),
            User.GetUserId(),
            request,
            cancellationToken);

        return Ok(overview);
    }

    [HttpPost("stock-takes")]
    [ProducesResponseType<InventoryOverviewDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<InventoryOverviewDto>> CreateStockTakeAsync(
        [FromBody] SaveStockTakeRequestDto request,
        CancellationToken cancellationToken)
    {
        var overview = await inventoryManagementService.CreateStockTakeAsync(
            User.GetTenantId(),
            User.GetUserId(),
            request,
            cancellationToken);

        return Ok(overview);
    }

    [HttpPost("imports")]
    [ProducesResponseType<InventoryImportResultDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<InventoryImportResultDto>> ImportInventoryAsync(
        [FromForm] ImportInventoryCommand command,
        CancellationToken cancellationToken)
    {
        if (command.File is null || command.File.Length == 0)
        {
            throw new InvalidOperationException("Select an Excel or CSV file to import.");
        }

        await using var stream = new MemoryStream();
        await command.File.CopyToAsync(stream, cancellationToken);

        var result = await inventoryManagementService.ImportInventoryAsync(
            User.GetTenantId(),
            new InventoryImportFileDto(command.File.FileName, stream.ToArray()),
            cancellationToken);

        return Ok(result);
    }

    public sealed class ImportInventoryCommand
    {
        public IFormFile? File { get; init; }
    }
}
