using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OmniBusiness.Api.Infrastructure;
using OmniBusiness.Application.Contracts;
using OmniBusiness.Application.Services;

namespace OmniBusiness.Api.Controllers;

[ApiController]
[Authorize(Roles = "Owner,Manager,Cashier,Back Office")]
[Route("api/v1/pos")]
public sealed class PosController(
    IWorkspaceQueryService workspaceQueryService,
    IPosWorkflowService posWorkflowService) : ControllerBase
{
    [HttpGet("terminal")]
    [ProducesResponseType<PosTerminalDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PosTerminalDto>> GetTerminalAsync(CancellationToken cancellationToken)
    {
        var terminal = await workspaceQueryService.GetPosTerminalAsync(User.GetTenantId(), cancellationToken);
        return Ok(terminal);
    }

    [HttpPut("cart/items/{productId:guid}")]
    [ProducesResponseType<PosTerminalDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PosTerminalDto>> SaveCartLineAsync(
        Guid productId,
        [FromBody] PosCartMutationRequestDto request,
        CancellationToken cancellationToken)
    {
        var terminal = await posWorkflowService.SaveCartLineAsync(
            User.GetTenantId(),
            request with { ProductId = productId },
            cancellationToken);

        return Ok(terminal);
    }

    [HttpDelete("cart/items/{productId:guid}")]
    [ProducesResponseType<PosTerminalDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PosTerminalDto>> RemoveCartLineAsync(
        Guid productId,
        CancellationToken cancellationToken)
    {
        var terminal = await posWorkflowService.RemoveCartLineAsync(
            User.GetTenantId(),
            productId,
            cancellationToken);

        return Ok(terminal);
    }

    [HttpPost("checkout")]
    [ProducesResponseType<PosCheckoutReceiptDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PosCheckoutReceiptDto>> CheckoutAsync(
        [FromBody] PosCheckoutRequestDto request,
        CancellationToken cancellationToken)
    {
        var receipt = await posWorkflowService.CheckoutAsync(
            User.GetTenantId(),
            User.GetUserId(),
            request,
            cancellationToken);

        return Ok(receipt);
    }

    [HttpGet("sales")]
    [ProducesResponseType<SalesHistoryDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SalesHistoryDto>> GetSalesAsync(CancellationToken cancellationToken)
    {
        var sales = await workspaceQueryService.GetSalesHistoryAsync(User.GetTenantId(), cancellationToken);
        return Ok(sales);
    }

    [HttpPost("sales/{saleId:guid}/submit-fbr")]
    [Authorize(Roles = "Owner,Manager,Back Office")]
    [ProducesResponseType<SalesHistoryItemDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SalesHistoryItemDto>> SubmitSaleToFbrAsync(
        Guid saleId,
        CancellationToken cancellationToken)
    {
        var sale = await posWorkflowService.SubmitSaleToFbrAsync(
            User.GetTenantId(),
            saleId,
            cancellationToken);

        return Ok(sale);
    }
}
