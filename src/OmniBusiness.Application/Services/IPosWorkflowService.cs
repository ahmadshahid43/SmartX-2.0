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

    Task<PosTerminalDto> SelectCustomerAsync(
        Guid tenantId,
        SelectPosCustomerRequestDto request,
        CancellationToken cancellationToken);

    Task<PosWorkflowActionDto> HoldCurrentSaleAsync(
        Guid tenantId,
        Guid userId,
        CreateHeldOrderRequestDto request,
        CancellationToken cancellationToken);

    Task<PosWorkflowActionDto> ResumeHeldOrderAsync(
        Guid tenantId,
        Guid heldOrderId,
        CancellationToken cancellationToken);

    Task<PosWorkflowActionDto> CreateBookingAsync(
        Guid tenantId,
        Guid userId,
        CreateBookingOrderRequestDto request,
        CancellationToken cancellationToken);

    Task<PosWorkflowActionDto> CollectBookingPaymentAsync(
        Guid tenantId,
        Guid bookingId,
        CollectBookingPaymentRequestDto request,
        CancellationToken cancellationToken);

    Task<PosWorkflowActionDto> CompleteBookingAsync(
        Guid tenantId,
        Guid userId,
        Guid bookingId,
        CompleteBookingRequestDto request,
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

    Task<SalesHistoryItemDto> RefundSaleAsync(
        Guid tenantId,
        Guid userId,
        Guid saleId,
        RefundSaleRequestDto request,
        CancellationToken cancellationToken);
}
