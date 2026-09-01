namespace OmniBusiness.Application.Contracts;

public sealed record WorkspaceContextDto(
    TenantSummaryDto Tenant,
    CompanySummaryDto Company,
    WorkspaceUserDto User,
    IReadOnlyList<BranchSummaryDto> Branches,
    WorkspaceModuleAccessDto Access);

public sealed record WorkspaceModuleAccessDto(
    string PlanCode,
    string PlanName,
    string Currency,
    decimal BaseMonthlyPrice,
    int IncludedUsers,
    int IncludedBranches,
    bool AllowCustomModuleOverrides,
    IReadOnlyList<string> EnabledModules);

public sealed record ModuleSettingsDto(
    WorkspaceModuleAccessDto Access,
    decimal EstimatedMonthlyTotal,
    IReadOnlyList<ModuleSettingsGroupDto> Groups);

public sealed record ModuleSettingsGroupDto(
    string Key,
    string Title,
    string Description,
    IReadOnlyList<WorkspaceModuleDto> Modules);

public sealed record WorkspaceModuleDto(
    string ModuleKey,
    string Title,
    string Description,
    string Category,
    string Icon,
    string? Route,
    string DeliveryStatus,
    string RecommendedPlan,
    bool IsEnabled,
    bool HasScreen,
    decimal AddOnMonthlyPrice,
    IReadOnlyList<string> Capabilities);

public sealed record SaveModuleSettingsRequestDto(
    string PlanCode,
    string PlanName,
    string Currency,
    decimal BaseMonthlyPrice,
    int IncludedUsers,
    int IncludedBranches,
    bool AllowCustomModuleOverrides,
    IReadOnlyList<SaveModuleEntitlementRequestDto> Modules);

public sealed record SaveModuleEntitlementRequestDto(
    string ModuleKey,
    bool Enabled,
    decimal AddOnMonthlyPrice);

public sealed record WorkspaceUsersDto(
    IReadOnlyList<WorkspaceStaffDto> Items);

public sealed record CustomerHubDto(
    CustomerMetricsDto Metrics,
    IReadOnlyList<CustomerProfileDto> Customers);

public sealed record CustomerMetricsDto(
    int TotalCustomers,
    int LoyaltyMembers,
    decimal LifetimeRevenue,
    int ActiveInLast30Days);

public sealed record CustomerProfileDto(
    Guid CustomerId,
    string Name,
    string PricingTier,
    string AvatarLetter,
    string LoyaltyTier,
    int LoyaltyPoints,
    decimal StoreCreditBalance,
    decimal GiftCardBalance,
    string? PhoneNumber,
    string? Email,
    bool MarketingOptIn,
    bool IsWalkIn,
    DateTimeOffset? LastVisitAt,
    decimal LifetimeValue,
    int VisitCount);

public sealed record ProcurementHubDto(
    ProcurementMetricsDto Metrics,
    IReadOnlyList<VendorSummaryDto> Vendors,
    IReadOnlyList<PurchaseOrderSummaryDto> PurchaseOrders,
    IReadOnlyList<StockTransferSummaryDto> StockTransfers);

public sealed record ProcurementMetricsDto(
    int ActiveVendors,
    int OpenPurchaseOrders,
    decimal PurchasePipelineAmount,
    int InTransitUnits);

public sealed record VendorSummaryDto(
    Guid VendorId,
    string Name,
    string ContactPerson,
    string PhoneNumber,
    string City,
    string LeadTimeLabel,
    string PaymentTerms,
    string Status,
    int OpenPurchaseOrders,
    decimal LifetimeSpend);

public sealed record PurchaseOrderSummaryDto(
    Guid PurchaseOrderId,
    string PurchaseOrderNo,
    string VendorName,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpectedAt,
    decimal TotalAmount,
    int LineCount,
    int OrderedUnits,
    int ReceivedUnits);

public sealed record StockTransferSummaryDto(
    Guid StockTransferId,
    string TransferNo,
    string FromBranchName,
    string ToBranchName,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpectedAt,
    int Units,
    string RequestedBy,
    string Notes);

public sealed record GoodsReceiptSummaryDto(
    Guid GoodsReceiptId,
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
    string Notes);

public sealed record OperationsHubDto(
    CashShiftMetricsDto Cash,
    ComplianceMetricsDto Compliance,
    IReadOnlyList<CashShiftSummaryDto> CashShifts,
    IReadOnlyList<PosModuleGroupDto> ModuleGroups);

public sealed record WarehouseHubDto(
    WarehouseMetricsDto Metrics,
    IReadOnlyList<string> Branches,
    IReadOnlyList<string> Warehouses,
    IReadOnlyList<StockTransferSummaryDto> StockTransfers,
    IReadOnlyList<GoodsReceiptSummaryDto> GoodsReceipts,
    IReadOnlyList<GatePassSummaryDto> GatePasses);

public sealed record WarehouseMetricsDto(
    int ActiveTransfers,
    int PendingReceipts,
    int OpenGatePasses,
    int UnitsInMotion);

public sealed record CashShiftMetricsDto(
    int OpenRegisters,
    decimal TodayCashSales,
    decimal ExpectedDrawerCash,
    decimal NetVariance);

public sealed record ComplianceMetricsDto(
    int QueuedFbrInvoices,
    int ReportedInvoices,
    int FailedInvoices,
    int PendingApprovals,
    int OfflineProtectedSales);

public sealed record CashShiftSummaryDto(
    Guid CashShiftId,
    string CashierName,
    string RegisterName,
    DateTimeOffset OpenedAt,
    DateTimeOffset? ClosedAt,
    decimal OpeningFloat,
    decimal CashSales,
    decimal ExpectedCash,
    decimal CountedCash,
    decimal Variance,
    string Status);

public sealed record GatePassSummaryDto(
    Guid GatePassId,
    string GatePassNo,
    string MovementType,
    string WarehouseName,
    string DestinationName,
    string ReferenceNo,
    string Status,
    DateTimeOffset IssuedAt,
    string IssuedBy,
    int Units,
    string Notes);

public sealed record PosModuleGroupDto(
    string Title,
    string Description,
    IReadOnlyList<PosModuleCardDto> Modules);

public sealed record PosModuleCardDto(
    string Title,
    string Description,
    string Route,
    string Icon,
    string Status);

public sealed record TenantSummaryDto(
    Guid Id,
    string Name,
    string IndustryTemplate,
    string SubscriptionPlan);

public sealed record CompanySummaryDto(
    Guid Id,
    string Name,
    string BaseCurrency,
    string TimeZone,
    string Country);

public sealed record BranchSummaryDto(
    Guid Id,
    string Code,
    string Name,
    string WarehouseName,
    bool IsPrimary);

public sealed record WorkspaceStaffDto(
    Guid UserId,
    Guid TenantId,
    Guid BranchId,
    string BranchName,
    string Email,
    string DisplayName,
    string Role);

public sealed record DashboardOverviewDto(
    DashboardMetricDto Sales,
    DashboardMetricDto Purchases,
    DashboardMetricDto GrossProfit,
    DashboardAlertDto LowStock,
    IReadOnlyList<TrendPointDto> Trend,
    IReadOnlyList<TopSellingItemDto> TopSelling,
    IReadOnlyList<TransactionSummaryDto> RecentTransactions,
    IReadOnlyList<BranchPerformanceDto> BranchPerformance);

public sealed record DashboardMetricDto(
    string Label,
    decimal Value,
    decimal DeltaPercentage,
    string DeltaDirection);

public sealed record DashboardAlertDto(
    string Label,
    int Count,
    string ActionLabel);

public sealed record ReportsHubDto(
    DateTimeOffset GeneratedAt,
    IReadOnlyList<ReportSectionDto> Sections,
    IReadOnlyList<ReportTableRowDto> SalesByItem,
    IReadOnlyList<ReportTableRowDto> SalesByCategory,
    IReadOnlyList<ReportTableRowDto> PaymentMethods,
    IReadOnlyList<ReportLedgerEntryDto> LedgerEntries,
    IReadOnlyList<ReportTransactionDto> Transactions);

public sealed record ReportSectionDto(
    string Key,
    string Title,
    string Description,
    string Icon,
    IReadOnlyList<ReportMetricDto> Metrics);

public sealed record ReportMetricDto(
    string Key,
    string Label,
    decimal Value,
    string Format,
    string Status = "ready");

public sealed record ReportTableRowDto(
    string Label,
    decimal Amount,
    int Count,
    string SecondaryLabel = "");

public sealed record ReportLedgerEntryDto(
    DateTimeOffset OccurredAt,
    string ReferenceNo,
    string Party,
    string EntryType,
    decimal Debit,
    decimal Credit,
    decimal Balance,
    string Status,
    string Notes);

public sealed record ReportTransactionDto(
    DateTimeOffset OccurredAt,
    string ReferenceNo,
    string CustomerName,
    string PaymentMethod,
    int ItemCount,
    decimal Subtotal,
    decimal Discount,
    decimal Tax,
    decimal Total,
    decimal GrossProfit,
    string FbrStatus,
    string Status);

public sealed record TrendPointDto(
    string Label,
    decimal Value);

public sealed record TopSellingItemDto(
    string Name,
    int Units,
    decimal Revenue);

public sealed record TransactionSummaryDto(
    string ReferenceNo,
    string CustomerName,
    decimal Amount,
    string Status,
    DateTimeOffset OccurredAt);

public sealed record BranchPerformanceDto(
    string BranchName,
    int Percentage);

public sealed record InventoryOverviewDto(
    string Title,
    string Subtitle,
    IReadOnlyList<string> Warehouses,
    IReadOnlyList<string> Categories,
    IReadOnlyList<string> Statuses,
    IReadOnlyList<InventoryItemDto> Items,
    InventoryMetricsDto Metrics,
    IReadOnlyList<InventoryLowStockItemDto> LowStockItems,
    IReadOnlyList<InventoryWarehouseSummaryDto> WarehouseSummaries,
    IReadOnlyList<InventoryCategorySummaryDto> CategorySummaries,
    IReadOnlyList<InventoryUsageInsightDto> UsageInsights,
    IReadOnlyList<InventoryBarcodeQueueItemDto> BarcodeQueue,
    IReadOnlyList<StockTakeSummaryDto> RecentStockTakes);

public sealed record InventoryImportFileDto(
    string FileName,
    byte[] Content);

public sealed record InventoryImportResultDto(
    InventoryOverviewDto Inventory,
    int ImportedCount,
    int CreatedCount,
    int UpdatedCount,
    IReadOnlyList<string> Warnings);

public sealed record InventoryItemDto(
    Guid ProductId,
    string Sku,
    string ProductName,
    string Category,
    string Warehouse,
    int InHand,
    int Reserved,
    int Available,
    decimal UnitPrice,
    decimal Value,
    string Status,
    int ReorderLevel,
    string VisualCode,
    bool IsFavorite,
    bool IsQuickSale);

public sealed record InventoryMetricsDto(
    int TotalProducts,
    int LowStockCount,
    decimal TotalValue,
    int WarehouseCount,
    int CategoryCount,
    int StockTakeCount30Days,
    decimal TurnoverRatio30Days);

public sealed record InventoryLowStockItemDto(
    Guid ProductId,
    string Sku,
    string ProductName,
    string Warehouse,
    int Available,
    int ReorderLevel,
    int ShortfallUnits);

public sealed record InventoryWarehouseSummaryDto(
    string Warehouse,
    int ProductCount,
    int InHandUnits,
    int AvailableUnits,
    decimal StockValue);

public sealed record InventoryCategorySummaryDto(
    string Category,
    int ProductCount,
    int AvailableUnits,
    decimal StockValue,
    decimal SharePercentage);

public sealed record InventoryUsageInsightDto(
    Guid ProductId,
    string Sku,
    string ProductName,
    int SoldUnits30Days,
    int NetAdjustment30Days,
    decimal TurnoverRatio30Days,
    string CoverageLabel);

public sealed record InventoryBarcodeQueueItemDto(
    Guid ProductId,
    string Sku,
    string ProductName,
    string Category,
    string VisualCode,
    bool IsFavorite,
    bool IsQuickSale);

public sealed record StockTakeSummaryDto(
    Guid Id,
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

public sealed record PosTerminalDto(
    PosCustomerDto Customer,
    IReadOnlyList<string> Categories,
    IReadOnlyList<PosProductDto> Products,
    IReadOnlyList<CartLineDto> Cart,
    PosSummaryDto Summary,
    IReadOnlyList<PosHeldOrderDto> HeldOrders,
    IReadOnlyList<PosBookingOrderDto> Bookings,
    PosWorkflowMetricsDto Metrics,
    IReadOnlyList<string> PaymentMethods);

public sealed record PosCustomerDto(
    string Name,
    string PricingTier,
    string AvatarLetter);

public sealed record PosProductDto(
    Guid ProductId,
    string Name,
    string Sku,
    string Category,
    decimal UnitPrice,
    int InHand,
    bool IsLowStock,
    bool IsFavorite,
    string VisualCode);

public sealed record CartLineDto(
    Guid ProductId,
    string Name,
    int Quantity,
    decimal UnitPrice,
    bool AllowQuantityEdit);

public sealed record PosSummaryDto(
    int ItemCount,
    decimal Subtotal,
    decimal Discount,
    decimal Tax,
    decimal Total);

public sealed record PosWorkflowMetricsDto(
    int HeldOrderCount,
    int BookingCount,
    decimal BookingDueAmount,
    int PendingCollectionCount);

public sealed record PosHeldOrderDto(
    Guid Id,
    string TicketNo,
    string CustomerName,
    string PricingTier,
    string HeldBy,
    DateTimeOffset HeldAt,
    int ItemCount,
    decimal Total,
    IReadOnlyList<CartLineDto> Lines,
    string Notes);

public sealed record PosBookingOrderDto(
    Guid Id,
    string BookingNo,
    string CustomerName,
    string? PhoneNumber,
    string? Email,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? DueAt,
    string BookedBy,
    int ItemCount,
    decimal Subtotal,
    decimal Discount,
    decimal Tax,
    decimal TotalAmount,
    decimal PaidAmount,
    decimal BalanceAmount,
    string PaymentStatus,
    IReadOnlyList<SaleLineDto> Lines,
    IReadOnlyList<PosPaymentLineDto> Payments,
    string Notes);

public sealed record PosPaymentLineDto(
    string Method,
    decimal Amount,
    string? ReferenceNo);

public sealed record PosCartMutationRequestDto(
    Guid ProductId,
    int Quantity);

public sealed record SelectPosCustomerRequestDto(
    Guid? CustomerId,
    string? CustomerName = null,
    string? PhoneNumber = null,
    string? Email = null);

public sealed record PosCheckoutRequestDto(
    string PaymentMethod,
    decimal? ReceivedAmount,
    bool SendToFbr,
    IReadOnlyList<PosPaymentLineRequestDto>? Payments = null,
    decimal? TaxRatePercent = null,
    bool TaxExempt = false);

public sealed record PosCheckoutReceiptDto(
    Guid SaleId,
    string ReferenceNo,
    string CustomerName,
    string PaymentMethod,
    string CashierName,
    DateTimeOffset OccurredAt,
    IReadOnlyList<SaleLineDto> Lines,
    PosSummaryDto Summary,
    decimal ReceivedAmount,
    decimal ChangeAmount,
    string FbrStatus,
    string? FbrInvoiceNumber,
    decimal PaidAmount,
    decimal BalanceAmount,
    string PaymentStatus,
    IReadOnlyList<PosPaymentLineDto> Payments);

public sealed record PosWorkflowActionDto(
    string Message,
    PosTerminalDto Terminal,
    PosBookingOrderDto? Booking = null,
    PosCheckoutReceiptDto? Receipt = null);

public sealed record SalesHistoryDto(
    SalesHistoryMetricsDto Metrics,
    IReadOnlyList<SalesPaymentMethodSummaryDto> PaymentMethods,
    IReadOnlyList<SalesBookingInsightDto> OpenBookings,
    IReadOnlyList<SalesHistoryItemDto> Items);

public sealed record SalesHistoryMetricsDto(
    int TransactionCount,
    decimal NetRevenue,
    decimal GrossProfit,
    decimal AverageTicket,
    int RefundedCount,
    decimal RefundedAmount,
    int OpenBookingCount,
    decimal BookingDueAmount,
    int DueTodayBookingCount);

public sealed record SalesPaymentMethodSummaryDto(
    string Method,
    decimal Amount,
    int TransactionCount);

public sealed record SalesBookingInsightDto(
    Guid BookingId,
    string BookingNo,
    string CustomerName,
    decimal TotalAmount,
    decimal PaidAmount,
    decimal BalanceAmount,
    string PaymentStatus,
    DateTimeOffset CreatedAt,
    DateTimeOffset? DueAt);

public sealed record SalesHistoryItemDto(
    Guid SaleId,
    string ReferenceNo,
    string CustomerName,
    decimal Amount,
    decimal GrossProfit,
    string Status,
    DateTimeOffset OccurredAt,
    int ItemCount,
    decimal Discount,
    decimal Tax,
    string PaymentMethod,
    string CashierName,
    IReadOnlyList<SaleLineDto> Lines,
    decimal ReceivedAmount,
    decimal ChangeAmount,
    string FbrStatus,
    string? FbrInvoiceNumber,
    string? FbrErrorMessage,
    DateTimeOffset? FbrReportedAt,
    decimal PaidAmount,
    decimal BalanceAmount,
    string PaymentStatus,
    IReadOnlyList<PosPaymentLineDto> Payments,
    decimal NetAmount,
    decimal RefundedAmount,
    DateTimeOffset? RefundedAt,
    string? RefundedBy,
    string? RefundReason,
    bool InventoryReturned);

public sealed record PosPaymentLineRequestDto(
    string Method,
    decimal Amount,
    string? ReferenceNo);

public sealed record CreateHeldOrderRequestDto(
    string? Notes);

public sealed record CreateBookingOrderRequestDto(
    string CustomerName,
    string? PhoneNumber,
    string? Email,
    DateTimeOffset? DueAt,
    string? Notes,
    IReadOnlyList<PosPaymentLineRequestDto>? Payments = null);

public sealed record CollectBookingPaymentRequestDto(
    decimal Amount,
    string PaymentMethod,
    string? ReferenceNo,
    string? Notes);

public sealed record CompleteBookingRequestDto(
    bool SendToFbr);

public sealed record RefundSaleRequestDto(
    string? Reason,
    bool ReturnToInventory = true);

public sealed record SaleLineDto(
    Guid ProductId,
    string Sku,
    string Name,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal);

public sealed record SaveProductRequestDto(
    string Sku,
    string Name,
    string Category,
    decimal UnitPrice,
    string Warehouse,
    int InHand,
    int Reserved,
    int ReorderLevel,
    bool IsFavorite,
    bool IsQuickSale,
    string VisualCode);

public sealed record StockAdjustmentRequestDto(
    Guid ProductId,
    int QuantityDelta,
    string Reason);

public sealed record SaveStockTakeRequestDto(
    Guid ProductId,
    int CountedQuantity,
    string? Notes);

public sealed record SaveStockTransferRequestDto(
    string FromBranchName,
    string ToBranchName,
    int Units,
    DateTimeOffset? ExpectedAt,
    string Status,
    string? Notes);

public sealed record SaveGoodsReceiptRequestDto(
    string PurchaseOrderNo,
    string VendorName,
    string WarehouseName,
    int LineCount,
    int ReceivedUnits,
    int VarianceUnits,
    string Status,
    string? Notes);

public sealed record SaveGatePassRequestDto(
    string MovementType,
    string WarehouseName,
    string DestinationName,
    string ReferenceNo,
    int Units,
    string Status,
    string? Notes);

public sealed record SaveWorkspaceUserRequestDto(
    string Email,
    string DisplayName,
    string Role,
    Guid BranchId,
    string? Password);

public sealed record FormBuilderDto(
    string FormId,
    string Title,
    string Description,
    string SelectedFieldId,
    IReadOnlyList<FormLibraryFieldDto> Library,
    IReadOnlyList<FormCanvasFieldDto> Canvas);

public sealed record FormLibraryFieldDto(
    string Key,
    string Label,
    string Group,
    string Icon);

public sealed record FormCanvasFieldDto(
    string FieldId,
    string Label,
    string Type,
    bool Required,
    string Placeholder,
    string? HelpText,
    string? DefaultValue,
    bool IsReadOnly,
    int? MinValue,
    int? MaxValue);

public sealed record SaveFormFieldRequestDto(
    string Label,
    string Type,
    bool Required,
    string Placeholder,
    string? HelpText,
    string? DefaultValue,
    bool IsReadOnly,
    int? MinValue,
    int? MaxValue);
