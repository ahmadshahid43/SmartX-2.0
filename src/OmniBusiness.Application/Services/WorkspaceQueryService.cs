using OmniBusiness.Application.Abstractions.Persistence;
using OmniBusiness.Application.Contracts;
using OmniBusiness.Domain.Foundation;

namespace OmniBusiness.Application.Services;

public sealed class WorkspaceQueryService(IWorkspaceRepository workspaceRepository) : IWorkspaceQueryService
{
    public async Task<WorkspaceContextDto> GetWorkspaceContextAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var snapshot = await workspaceRepository.GetWorkspaceSnapshotAsync(cancellationToken);
        EnsureTenant(snapshot, tenantId);
        var currentUser = FindUser(snapshot, userId);
        var subscriptionSettings = ResolveSubscriptionSettings(snapshot);

        return new WorkspaceContextDto(
            new TenantSummaryDto(
                snapshot.Tenant.Id,
                snapshot.Tenant.Name,
                snapshot.Tenant.IndustryTemplate,
                snapshot.Tenant.SubscriptionPlan),
            new CompanySummaryDto(
                snapshot.Company.Id,
                snapshot.Company.Name,
                snapshot.Company.BaseCurrency,
                snapshot.Company.TimeZone,
                snapshot.Company.Country),
            new WorkspaceUserDto(
                currentUser.Id,
                currentUser.TenantId,
                currentUser.BranchId,
                currentUser.Email,
                currentUser.DisplayName,
                currentUser.Role),
            snapshot.Branches
                .Select(branch => new BranchSummaryDto(
                    branch.Id,
                    branch.Code,
                    branch.Name,
                    branch.WarehouseName,
                    branch.IsPrimary))
                .ToArray(),
            WorkspaceModuleCatalog.BuildAccess(subscriptionSettings, currentUser.Role));
    }

    public async Task<WorkspaceUsersDto> GetUsersAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var snapshot = await workspaceRepository.GetWorkspaceSnapshotAsync(cancellationToken);
        EnsureTenant(snapshot, tenantId);

        return new WorkspaceUsersDto(
            (snapshot.Users ?? Array.Empty<AppUser>())
                .Select(user => new WorkspaceStaffDto(
                    user.Id,
                    user.TenantId,
                    user.BranchId,
                    snapshot.Branches.FirstOrDefault(branch => branch.Id == user.BranchId)?.Name ?? "Unassigned Branch",
                    user.Email,
                    user.DisplayName,
                    user.Role))
                .OrderBy(user => string.Equals(user.Role, "Owner", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(user => user.DisplayName)
                .ToArray());
    }

    public async Task<ModuleSettingsDto> GetModuleSettingsAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var snapshot = await workspaceRepository.GetWorkspaceSnapshotAsync(cancellationToken);
        EnsureTenant(snapshot, tenantId);
        var subscriptionSettings = ResolveSubscriptionSettings(snapshot);

        return WorkspaceModuleCatalog.BuildSettings(subscriptionSettings);
    }

    public async Task<CustomerHubDto> GetCustomerHubAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var snapshot = await workspaceRepository.GetWorkspaceSnapshotAsync(cancellationToken);
        EnsureTenant(snapshot, tenantId);

        var customers = (snapshot.Customers ?? Array.Empty<CustomerProfile>())
            .OrderByDescending(customer => customer.LastVisitAt ?? DateTimeOffset.MinValue)
            .ThenBy(customer => customer.Name)
            .ToArray();
        var transactions = snapshot.RecentTransactions ?? Array.Empty<SaleRecord>();
        var activityAnchor = ResolveActivityAnchor(transactions, customers);

        return new CustomerHubDto(
            new CustomerMetricsDto(
                customers.Count(customer => !customer.IsWalkIn),
                customers.Count(customer =>
                    !customer.IsWalkIn &&
                    (customer.LoyaltyPoints > 0 ||
                     !string.Equals(customer.LoyaltyTier, "Standard", StringComparison.OrdinalIgnoreCase))),
                transactions.Sum(transaction => transaction.Amount),
                customers.Count(customer => customer.LastVisitAt is not null && customer.LastVisitAt >= activityAnchor.AddDays(-30))),
            customers
                .Select(customer =>
                {
                    var customerSales = FilterTransactionsForCustomer(transactions, customer);
                    return new CustomerProfileDto(
                        customer.Id,
                        customer.Name,
                        customer.PricingTier,
                        customer.AvatarLetter,
                        customer.LoyaltyTier,
                        customer.LoyaltyPoints,
                        customer.StoreCreditBalance,
                        customer.GiftCardBalance,
                        customer.PhoneNumber,
                        customer.Email,
                        customer.MarketingOptIn,
                        customer.IsWalkIn,
                        customer.LastVisitAt,
                        customerSales.Sum(transaction => transaction.Amount),
                        customerSales.Length);
                })
                .ToArray());
    }

    public async Task<DashboardOverviewDto> GetDashboardAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var snapshot = await workspaceRepository.GetWorkspaceSnapshotAsync(cancellationToken);
        EnsureTenant(snapshot, tenantId);

        var zeroFigure = new DailyBusinessFigure(DateOnly.FromDateTime(DateTime.Today), 0m, 0m, 0m);
        var today = snapshot.DailyFigures.MaxBy(figure => figure.Date)
            ?? zeroFigure;
        var previous = snapshot.DailyFigures
            .Where(figure => figure.Date < today.Date)
            .MaxBy(figure => figure.Date)
            ?? today;

        return new DashboardOverviewDto(
            BuildMetric("Today's Sales", today.Sales, previous.Sales),
            BuildMetric("Today's Purchases", today.Purchases, previous.Purchases),
            BuildMetric("Gross Profit", today.GrossProfit, previous.GrossProfit),
            new DashboardAlertDto("Low Stock Items", snapshot.Products.Count(product => product.IsLowStock), "Review"),
            snapshot.SalesTrend
                .Select(point => new TrendPointDto(point.Label, point.Value))
                .ToArray(),
            snapshot.TopSelling
                .Select(item => new TopSellingItemDto(item.Name, item.Units, item.Revenue))
                .ToArray(),
            snapshot.RecentTransactions
                .OrderByDescending(transaction => transaction.OccurredAt)
                .Take(6)
                .Select(transaction => new TransactionSummaryDto(
                    transaction.ReferenceNo,
                    transaction.CustomerName,
                    transaction.Amount,
                    transaction.Status,
                    transaction.OccurredAt))
                .ToArray(),
            snapshot.BranchPerformance
                .Select(item => new BranchPerformanceDto(item.BranchName, item.Percentage))
                .ToArray());
    }

    public async Task<InventoryOverviewDto> GetInventoryOverviewAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var snapshot = await workspaceRepository.GetWorkspaceSnapshotAsync(cancellationToken);
        EnsureTenant(snapshot, tenantId);

        return new InventoryOverviewDto(
            "Inventory Stock Overview",
            "Manage and track your warehouse inventory levels.",
            snapshot.Products.Where(product => !product.IsArchived).Select(product => product.Warehouse).Distinct().OrderBy(name => name).ToArray(),
            snapshot.Products.Where(product => !product.IsArchived).Select(product => product.Category).Distinct().OrderBy(name => name).ToArray(),
            snapshot.Products.Where(product => !product.IsArchived).Select(product => product.Status).Distinct().OrderBy(name => name).ToArray(),
            snapshot.Products
                .Where(product => !product.IsArchived)
                .Select(product => new InventoryItemDto(
                product.Id,
                product.Sku,
                product.Name,
                product.Category,
                product.Warehouse,
                product.InHand,
                product.Reserved,
                product.InHand - product.Reserved,
                product.UnitPrice,
                product.UnitPrice * product.InHand,
                product.Status,
                product.ReorderLevel,
                product.VisualCode))
                .ToArray());
    }

    public async Task<PosTerminalDto> GetPosTerminalAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var snapshot = await workspaceRepository.GetWorkspaceSnapshotAsync(cancellationToken);
        EnsureTenant(snapshot, tenantId);

        return new PosTerminalDto(
            new PosCustomerDto(
                snapshot.ActiveCustomer.Name,
                snapshot.ActiveCustomer.PricingTier,
                snapshot.ActiveCustomer.AvatarLetter),
            snapshot.Products
                .Where(product => !product.IsArchived)
                .Select(product => product.Category)
                .Distinct()
                .OrderBy(category => category)
                .ToArray(),
            snapshot.Products
                .Where(product => !product.IsArchived)
                .OrderByDescending(product => product.IsFavorite)
                .ThenBy(product => product.Name)
                .Select(product => new PosProductDto(
                    product.Id,
                    product.Name,
                    product.Sku,
                    product.Category,
                    product.UnitPrice,
                    product.InHand,
                    product.IsLowStock,
                    product.IsFavorite,
                    product.VisualCode))
                .ToArray(),
            snapshot.ActiveCart
                .Select(line => new CartLineDto(
                    line.ProductId,
                    line.Name,
                    line.Quantity,
                    line.UnitPrice,
                    line.AllowQuantityEdit))
                .ToArray(),
            PosPricingCalculator.BuildSummary(snapshot.ActiveCart));
    }

    public async Task<SalesHistoryDto> GetSalesHistoryAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var snapshot = await workspaceRepository.GetWorkspaceSnapshotAsync(cancellationToken);
        EnsureTenant(snapshot, tenantId);

        return new SalesHistoryDto(
            snapshot.RecentTransactions
                .OrderByDescending(transaction => transaction.OccurredAt)
                .Select(transaction => new SalesHistoryItemDto(
                    transaction.Id,
                    transaction.ReferenceNo,
                    transaction.CustomerName,
                    transaction.Amount,
                    transaction.GrossProfit,
                    transaction.Status,
                    transaction.OccurredAt,
                    transaction.ItemCount,
                    transaction.Discount,
                    transaction.Tax,
                    transaction.PaymentMethod,
                    transaction.CashierName,
                    (transaction.Lines ?? Array.Empty<SaleLine>())
                        .Select(line => new SaleLineDto(
                            line.ProductId,
                            line.Sku,
                            line.Name,
                            line.Quantity,
                            line.UnitPrice,
                            line.LineTotal))
                        .ToArray(),
                    transaction.ReceivedAmount,
                    transaction.ChangeAmount,
                    transaction.FbrStatus,
                    transaction.FbrInvoiceNumber,
                    transaction.FbrErrorMessage,
                transaction.FbrReportedAt))
                .ToArray());
    }

    public async Task<ProcurementHubDto> GetProcurementHubAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var snapshot = await workspaceRepository.GetWorkspaceSnapshotAsync(cancellationToken);
        EnsureTenant(snapshot, tenantId);

        var vendors = snapshot.Vendors ?? Array.Empty<Vendor>();
        var purchaseOrders = snapshot.PurchaseOrders ?? Array.Empty<PurchaseOrder>();
        var stockTransfers = snapshot.StockTransfers ?? Array.Empty<StockTransfer>();

        return new ProcurementHubDto(
            new ProcurementMetricsDto(
                vendors.Count(vendor => string.Equals(vendor.Status, "Active", StringComparison.OrdinalIgnoreCase)),
                purchaseOrders.Count(order => IsOpenPurchaseOrder(order.Status)),
                purchaseOrders
                    .Where(order => IsOpenPurchaseOrder(order.Status))
                    .Sum(order => order.TotalAmount),
                stockTransfers
                    .Where(transfer => IsTransferInMotion(transfer.Status))
                    .Sum(transfer => transfer.Units)),
            vendors
                .OrderBy(vendor => vendor.Name)
                .Select(vendor =>
                {
                    var vendorOrders = purchaseOrders.Where(order => order.VendorId == vendor.Id).ToArray();
                    return new VendorSummaryDto(
                        vendor.Id,
                        vendor.Name,
                        vendor.ContactPerson,
                        vendor.PhoneNumber,
                        vendor.City,
                        vendor.LeadTimeLabel,
                        vendor.PaymentTerms,
                        vendor.Status,
                        vendorOrders.Count(order => IsOpenPurchaseOrder(order.Status)),
                        vendorOrders.Sum(order => order.TotalAmount));
                })
                .ToArray(),
            purchaseOrders
                .OrderByDescending(order => order.CreatedAt)
                .Select(order => new PurchaseOrderSummaryDto(
                    order.Id,
                    order.PurchaseOrderNo,
                    order.VendorName,
                    order.Status,
                    order.CreatedAt,
                    order.ExpectedAt,
                    order.TotalAmount,
                    order.LineCount,
                    order.OrderedUnits,
                    order.ReceivedUnits))
                .ToArray(),
            stockTransfers
                .OrderByDescending(transfer => transfer.CreatedAt)
                .Select(transfer => new StockTransferSummaryDto(
                    transfer.Id,
                    transfer.TransferNo,
                    transfer.FromBranchName,
                    transfer.ToBranchName,
                    transfer.Status,
                    transfer.CreatedAt,
                    transfer.ExpectedAt,
                    transfer.Units,
                    transfer.RequestedBy))
                .ToArray());
    }

    public async Task<OperationsHubDto> GetOperationsHubAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var snapshot = await workspaceRepository.GetWorkspaceSnapshotAsync(cancellationToken);
        EnsureTenant(snapshot, tenantId);
        var subscriptionSettings = ResolveSubscriptionSettings(snapshot);

        var cashShifts = snapshot.CashShifts ?? Array.Empty<CashShift>();
        var transactions = snapshot.RecentTransactions ?? Array.Empty<SaleRecord>();
        var activityAnchor = ResolveActivityAnchor(transactions, snapshot.Customers ?? Array.Empty<CustomerProfile>());
        var anchorDate = activityAnchor.Date;
        var openAndReviewShifts = cashShifts.Where(shift => !string.Equals(shift.Status, "Closed", StringComparison.OrdinalIgnoreCase)).ToArray();
        var queuedInvoices = transactions.Count(transaction => transaction.FbrStatus.Contains("Queued", StringComparison.OrdinalIgnoreCase));
        var reportedInvoices = transactions.Count(transaction =>
            !string.IsNullOrWhiteSpace(transaction.FbrInvoiceNumber) ||
            transaction.FbrStatus.Contains("Reported", StringComparison.OrdinalIgnoreCase));
        var failedInvoices = transactions.Count(transaction =>
            (!string.IsNullOrWhiteSpace(transaction.FbrErrorMessage)) ||
            transaction.FbrStatus.Contains("Failed", StringComparison.OrdinalIgnoreCase) ||
            transaction.FbrStatus.Contains("Rejected", StringComparison.OrdinalIgnoreCase));
        var pendingApprovals = transactions.Count(transaction => string.Equals(transaction.Status, "Pending", StringComparison.OrdinalIgnoreCase))
            + cashShifts.Count(shift => string.Equals(shift.Status, "Needs Review", StringComparison.OrdinalIgnoreCase));

        return new OperationsHubDto(
            new CashShiftMetricsDto(
                cashShifts.Count(shift => string.Equals(shift.Status, "Open", StringComparison.OrdinalIgnoreCase)),
                transactions
                    .Where(transaction =>
                        transaction.OccurredAt.Date == anchorDate &&
                        transaction.PaymentMethod.Contains("Cash", StringComparison.OrdinalIgnoreCase))
                    .Sum(transaction => transaction.Amount),
                openAndReviewShifts.Sum(shift => shift.ExpectedCash),
                openAndReviewShifts.Sum(shift => shift.CountedCash - shift.ExpectedCash)),
            new ComplianceMetricsDto(
                queuedInvoices,
                reportedInvoices,
                failedInvoices,
                pendingApprovals,
                queuedInvoices),
            cashShifts
                .OrderByDescending(shift => shift.OpenedAt)
                .Select(shift => new CashShiftSummaryDto(
                    shift.Id,
                    shift.CashierName,
                    shift.RegisterName,
                    shift.OpenedAt,
                    shift.ClosedAt,
                    shift.OpeningFloat,
                    shift.CashSales,
                    shift.ExpectedCash,
                    shift.CountedCash,
                    shift.CountedCash - shift.ExpectedCash,
                    shift.Status))
                .ToArray(),
            WorkspaceModuleCatalog.BuildOperationsModuleGroups(subscriptionSettings));
    }

    public async Task<FormBuilderDto> GetProductCustomFieldsAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var snapshot = await workspaceRepository.GetWorkspaceSnapshotAsync(cancellationToken);
        EnsureTenant(snapshot, tenantId);

        return new FormBuilderDto(
            snapshot.ProductCustomFields.Id,
            snapshot.ProductCustomFields.Title,
            snapshot.ProductCustomFields.Description,
            snapshot.ProductCustomFields.SelectedFieldId,
            snapshot.ProductCustomFields.Library
                .Select(field => new FormLibraryFieldDto(field.Key, field.Label, field.Group, field.Icon))
                .ToArray(),
            snapshot.ProductCustomFields.Canvas
                .Select(field => new FormCanvasFieldDto(
                    field.FieldId,
                    field.Label,
                    field.Type.ToString(),
                    field.Required,
                    field.Placeholder,
                    field.HelpText,
                    field.DefaultValue,
                    field.IsReadOnly,
                    field.MinValue,
                    field.MaxValue))
                .ToArray());
    }

    private static void EnsureTenant(WorkspaceSnapshot snapshot, Guid tenantId)
    {
        if (snapshot.Tenant.Id != tenantId)
        {
            throw new InvalidOperationException("The current user does not belong to the requested tenant.");
        }
    }

    private static AppUser FindUser(WorkspaceSnapshot snapshot, Guid userId)
    {
        return (snapshot.Users ?? Array.Empty<AppUser>())
            .FirstOrDefault(user => user.Id == userId)
            ?? throw new InvalidOperationException("The current user could not be found in the workspace.");
    }

    private static DashboardMetricDto BuildMetric(string label, decimal current, decimal previous)
    {
        var baseline = previous == 0 ? 1 : previous;
        var delta = decimal.Round(((current - previous) / baseline) * 100m, 1, MidpointRounding.AwayFromZero);
        var direction = delta >= 0 ? "up" : "down";

        return new DashboardMetricDto(label, current, delta, direction);
    }

    private static SaleRecord[] FilterTransactionsForCustomer(IEnumerable<SaleRecord> transactions, CustomerProfile customer)
    {
        return transactions
            .Where(transaction =>
                string.Equals(transaction.CustomerName, customer.Name, StringComparison.OrdinalIgnoreCase) ||
                (customer.IsWalkIn && string.IsNullOrWhiteSpace(transaction.CustomerName)))
            .ToArray();
    }

    private static DateTimeOffset ResolveActivityAnchor(
        IEnumerable<SaleRecord> transactions,
        IEnumerable<CustomerProfile> customers)
    {
        var transactionAnchor = transactions
            .Select(transaction => transaction.OccurredAt)
            .DefaultIfEmpty(DateTimeOffset.MinValue)
            .Max();
        var customerAnchor = customers
            .Where(customer => customer.LastVisitAt is not null)
            .Select(customer => customer.LastVisitAt!.Value)
            .DefaultIfEmpty(DateTimeOffset.MinValue)
            .Max();
        var anchor = transactionAnchor > customerAnchor ? transactionAnchor : customerAnchor;

        return anchor == DateTimeOffset.MinValue ? DateTimeOffset.UtcNow : anchor;
    }

    private static bool IsOpenPurchaseOrder(string status)
    {
        return !string.Equals(status, "Received", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(status, "Cancelled", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTransferInMotion(string status)
    {
        return status.Contains("Transit", StringComparison.OrdinalIgnoreCase)
            || status.Contains("Dispatch", StringComparison.OrdinalIgnoreCase)
            || status.Contains("Review", StringComparison.OrdinalIgnoreCase);
    }

    private static SubscriptionPlanSettings ResolveSubscriptionSettings(WorkspaceSnapshot snapshot)
    {
        if (snapshot.SubscriptionSettings is not null)
        {
            return snapshot.SubscriptionSettings;
        }

        return new SubscriptionPlanSettings(
            string.IsNullOrWhiteSpace(snapshot.Tenant.SubscriptionPlan)
                ? "starter"
                : snapshot.Tenant.SubscriptionPlan.Trim().ToLowerInvariant().Replace(' ', '-'),
            string.IsNullOrWhiteSpace(snapshot.Tenant.SubscriptionPlan) ? "Starter" : snapshot.Tenant.SubscriptionPlan,
            string.IsNullOrWhiteSpace(snapshot.Company.BaseCurrency) ? "PKR" : snapshot.Company.BaseCurrency,
            0m,
            3,
            1,
            true,
            WorkspaceModuleCatalog.DefaultEnabledModuleKeys
                .Select(moduleKey => new ModuleEntitlement(moduleKey, true, 0m))
                .ToArray());
    }

}
