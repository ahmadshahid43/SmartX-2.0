namespace OmniBusiness.Domain.Foundation;

public sealed record DailyBusinessFigure(
    DateOnly Date,
    decimal Sales,
    decimal Purchases,
    decimal GrossProfit);

public sealed record TrendPoint(
    string Label,
    decimal Value);

public sealed record TopSellingItem(
    string Name,
    int Units,
    decimal Revenue);

public sealed record BranchPerformance(
    string BranchName,
    int Percentage);

public sealed record Product(
    Guid Id,
    Guid TenantId,
    string Sku,
    string Name,
    string Category,
    decimal UnitPrice,
    int InHand,
    int Reserved,
    string Warehouse,
    string Status,
    bool IsFavorite,
    bool IsQuickSale,
    bool IsLowStock,
    string VisualCode,
    int ReorderLevel = 5,
    bool IsArchived = false);

public sealed record CustomerProfile(
    Guid Id,
    Guid TenantId,
    string Name,
    string PricingTier,
    string AvatarLetter,
    string? PhoneNumber = null,
    bool IsWalkIn = false,
    string? Email = null,
    string LoyaltyTier = "Standard",
    int LoyaltyPoints = 0,
    decimal StoreCreditBalance = 0,
    decimal GiftCardBalance = 0,
    bool MarketingOptIn = false,
    DateTimeOffset? LastVisitAt = null);

public sealed record SaleLine(
    Guid ProductId,
    string Sku,
    string Name,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal);

public sealed record PaymentAllocation(
    string Method,
    decimal Amount,
    string? ReferenceNo = null);

public sealed record SaleRecord(
    Guid Id,
    Guid TenantId,
    string ReferenceNo,
    string CustomerName,
    decimal Amount,
    decimal GrossProfit,
    string Status,
    DateTimeOffset OccurredAt,
    int ItemCount = 0,
    decimal Discount = 0,
    decimal Tax = 0,
    string PaymentMethod = "Cash",
    string CashierName = "",
    IReadOnlyList<SaleLine>? Lines = null,
    decimal ReceivedAmount = 0,
    decimal ChangeAmount = 0,
    string FbrStatus = "QueuedOffline",
    string? FbrInvoiceNumber = null,
    string? FbrErrorMessage = null,
    DateTimeOffset? FbrReportedAt = null,
    decimal PaidAmount = 0,
    decimal BalanceAmount = 0,
    string PaymentStatus = "Paid",
    IReadOnlyList<PaymentAllocation>? Payments = null,
    decimal RefundedAmount = 0,
    DateTimeOffset? RefundedAt = null,
    string? RefundedBy = null,
    string? RefundReason = null,
    bool InventoryReturned = false);

public sealed record StockAdjustmentRecord(
    Guid Id,
    Guid TenantId,
    Guid ProductId,
    string ProductName,
    int QuantityDelta,
    string Reason,
    string PerformedBy,
    DateTimeOffset OccurredAt);

public sealed record PosCustomer(
    string Name,
    string PricingTier,
    string AvatarLetter);

public sealed record CartLine(
    Guid ProductId,
    string Name,
    int Quantity,
    decimal UnitPrice,
    bool AllowQuantityEdit);

public sealed record PosHeldOrder(
    Guid Id,
    Guid TenantId,
    string TicketNo,
    string CustomerName,
    string PricingTier,
    string HeldBy,
    DateTimeOffset HeldAt,
    int ItemCount,
    decimal Total,
    IReadOnlyList<CartLine> Lines,
    string Notes = "");

public sealed record PosBookingOrder(
    Guid Id,
    Guid TenantId,
    string BookingNo,
    string CustomerName,
    string? PhoneNumber,
    string? Email,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? DueAt,
    string BookedBy,
    IReadOnlyList<SaleLine> Lines,
    int ItemCount,
    decimal Subtotal,
    decimal Discount,
    decimal Tax,
    decimal TotalAmount,
    decimal PaidAmount,
    decimal BalanceAmount,
    string PaymentStatus,
    IReadOnlyList<PaymentAllocation>? Payments = null,
    string Notes = "");

public sealed record Vendor(
    Guid Id,
    Guid TenantId,
    string Name,
    string ContactPerson,
    string PhoneNumber,
    string City,
    string LeadTimeLabel,
    string PaymentTerms,
    string Status = "Active");

public sealed record PurchaseOrder(
    Guid Id,
    Guid TenantId,
    Guid VendorId,
    string PurchaseOrderNo,
    string VendorName,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpectedAt,
    decimal TotalAmount,
    int LineCount,
    int OrderedUnits,
    int ReceivedUnits);

public sealed record StockTransfer(
    Guid Id,
    Guid TenantId,
    string TransferNo,
    string FromBranchName,
    string ToBranchName,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpectedAt,
    int Units,
    string RequestedBy,
    string Notes = "");

public sealed record GoodsReceipt(
    Guid Id,
    Guid TenantId,
    string ReceiptNo,
    string PurchaseOrderNo,
    string VendorName,
    string WarehouseName,
    string Status,
    DateTimeOffset ReceivedAt,
    string ReceivedBy,
    int LineCount,
    int ReceivedUnits,
    int VarianceUnits,
    string Notes = "");

public sealed record GatePass(
    Guid Id,
    Guid TenantId,
    string GatePassNo,
    string MovementType,
    string WarehouseName,
    string DestinationName,
    string ReferenceNo,
    string Status,
    DateTimeOffset IssuedAt,
    string IssuedBy,
    int Units,
    string Notes = "");

public sealed record StockTakeSession(
    Guid Id,
    Guid TenantId,
    Guid ProductId,
    string Sku,
    string ProductName,
    string Warehouse,
    int SystemQuantity,
    int CountedQuantity,
    int VarianceQuantity,
    string Status,
    string CountedBy,
    string Notes,
    DateTimeOffset CountedAt);

public sealed record CashShift(
    Guid Id,
    Guid TenantId,
    Guid UserId,
    string CashierName,
    string RegisterName,
    DateTimeOffset OpenedAt,
    DateTimeOffset? ClosedAt,
    decimal OpeningFloat,
    decimal CashSales,
    decimal Refunds,
    decimal PaidOuts,
    decimal ExpectedCash,
    decimal CountedCash,
    string Status);
