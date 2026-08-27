using OmniBusiness.Application.Contracts;

namespace OmniBusiness.Application.Services;

public interface IPosWorkflowService
{
    Task<PosTerminalDto> SaveCartLineAsync(
        Guid tenantId,
        PosCartMutationRequestDto request,
        CancellationToken cancellationToken);

    Task<PosTerminalDto> RemoveCartLineAsync(
        Guid tenantId,
        Guid productId,
        CancellationToken cancellationToken);

    Task<PosCheckoutReceiptDto> CheckoutAsync(
        Guid tenantId,
        Guid userId,
        PosCheckoutRequestDto request,
        CancellationToken cancellationToken);

    Task<SalesHistoryItemDto> SubmitSaleToFbrAsync(
        Guid tenantId,
        Guid saleId,
        CancellationToken cancellationToken);
}
