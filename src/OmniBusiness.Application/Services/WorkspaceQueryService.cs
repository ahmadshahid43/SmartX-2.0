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
                transactions.Sum(GetNetTransactionAmount),
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
                        customerSales.Sum(GetNetTransactionAmount),
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

    public async Task<ReportsHubDto> GetReportsHubAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var snapshot = await workspaceRepository.GetWorkspaceSnapshotAsync(cancellationToken);
        EnsureTenant(snapshot, tenantId);

        var sales = (snapshot.RecentTransactions ?? Array.Empty<SaleRecord>())
            .Where(sale => sale.TenantId == tenantId)
            .ToArray();
        var completedSales = sales.Where(sale => string.Equals(sale.Status, "Completed", StringComparison.OrdinalIgnoreCase)).ToArray();
        var refundedSales = sales.Where(IsRefundedTransaction).ToArray();
        var products = snapshot.Products.Where(product => product.TenantId == tenantId && !product.IsArchived).ToArray();
        var customers = (snapshot.Customers ?? Array.Empty<CustomerProfile>()).Where(customer => customer.TenantId == tenantId).ToArray();
        var purchaseOrders = (snapshot.PurchaseOrders ?? Array.Empty<PurchaseOrder>()).Where(order => order.TenantId == tenantId).ToArray();
        var transfers = (snapshot.StockTransfers ?? Array.Empty<StockTransfer>()).Where(transfer => transfer.TenantId == tenantId).ToArray();
        var adjustments = (snapshot.StockAdjustments ?? Array.Empty<StockAdjustmentRecord>()).Where(item => item.TenantId == tenantId).ToArray();
        var cashShifts = (snapshot.CashShifts ?? Array.Empty<CashShift>()).Where(shift => shift.TenantId == tenantId).ToArray();

        var salesAmount = completedSales.Sum(GetNetTransactionAmount);
        var grossProfit = completedSales.Sum(sale => sale.GrossProfit);
        var taxCollected = completedSales.Sum(sale => sale.Tax);
        var discounts = completedSales.Sum(sale => sale.Discount);
        var refundedAmount = refundedSales.Sum(sale => sale.RefundedAmount > 0 ? sale.RefundedAmount : sale.Amount);
        var completedCount = completedSales.Length;
        var fbrReported = completedSales.Count(sale => sale.FbrStatus.Contains("Reported", StringComparison.OrdinalIgnoreCase));
        var fbrQueued = completedSales.Count(sale => sale.FbrStatus.Contains("Queued", StringComparison.OrdinalIgnoreCase));
        var fbrFailed = completedSales.Count(sale => sale.FbrStatus.Contains("Failed", StringComparison.OrdinalIgnoreCase));
        var stockValue = products.Sum(product => product.InHand * product.UnitPrice);
        var lowStock = products.Count(product => product.InHand - product.Reserved <= product.ReorderLevel);
        var openPurchaseOrders = purchaseOrders.Count(order => !string.Equals(order.Status, "Received", StringComparison.OrdinalIgnoreCase) && !string.Equals(order.Status, "Closed", StringComparison.OrdinalIgnoreCase));
        var openTransfers = transfers.Count(transfer => !string.Equals(transfer.Status, "Received", StringComparison.OrdinalIgnoreCase) && !string.Equals(transfer.Status, "Closed", StringComparison.OrdinalIgnoreCase));
        var repeatCustomers = completedSales.GroupBy(sale => sale.CustomerName, StringComparer.OrdinalIgnoreCase).Count(group => group.Count() > 1 && !string.Equals(group.Key, "Walk-in Customer", StringComparison.OrdinalIgnoreCase));

        var salesByItem = completedSales
            .SelectMany(sale => sale.Lines ?? Array.Empty<SaleLine>())
            .GroupBy(line => line.Name)
            .OrderByDescending(group => group.Sum(line => line.LineTotal))
            .Take(12)
            .Select(group => new ReportTableRowDto(group.Key, group.Sum(line => line.LineTotal), group.Sum(line => line.Quantity), "units sold"))
            .ToArray();
        var salesByCategory = completedSales
            .SelectMany(sale => sale.Lines ?? Array.Empty<SaleLine>())
            .GroupJoin(products, line => line.ProductId, product => product.Id, (line, matchingProducts) => new { line, Category = matchingProducts.FirstOrDefault()?.Category ?? "Uncategorised" })
            .GroupBy(item => item.Category)
            .OrderByDescending(group => group.Sum(item => item.line.LineTotal))
            .Take(12)
            .Select(group => new ReportTableRowDto(group.Key, group.Sum(item => item.line.LineTotal), group.Sum(item => item.line.Quantity), "units sold"))
            .ToArray();
        var paymentMethods = completedSales
            .SelectMany(sale => sale.Payments?.Any() == true
                ? sale.Payments
                : new[] { new PaymentAllocation(sale.PaymentMethod, sale.PaidAmount > 0 ? sale.PaidAmount : sale.Amount) })
            .GroupBy(payment => payment.Method)
            .OrderByDescending(group => group.Sum(payment => payment.Amount))
            .Select(group => new ReportTableRowDto(group.Key, group.Sum(payment => payment.Amount), group.Count(), "tenders"))
            .ToArray();

        var sections = new[]
        {
            new ReportSectionDto("sales", "Sales & Profit", "Daily, item, category, cashier and margin performance.", "insights", new[]
            {
                new ReportMetricDto("sales-total", "Completed sales", salesAmount, "currency"),
                new ReportMetricDto("sales-count", "Completed invoices", completedCount, "number"),
                new ReportMetricDto("sales-average", "Average ticket", completedCount == 0 ? 0 : salesAmount / completedCount, "currency"),
                new ReportMetricDto("gross-profit", "Gross profit", grossProfit, "currency")
            }),
            new ReportSectionDto("payments", "Payments, Tax & FBR", "Tender mix, discounts, refunds and fiscal submission status.", "account_balance_wallet", new[]
            {
                new ReportMetricDto("tax-collected", "Tax collected", taxCollected, "currency"),
                new ReportMetricDto("discounts", "Discounts given", discounts, "currency"),
                new ReportMetricDto("refunds", "Refund / return value", refundedAmount, "currency"),
                new ReportMetricDto("fbr-reported", "FBR reported", fbrReported, "number"),
                new ReportMetricDto("fbr-queued", "FBR queued", fbrQueued, "number", fbrQueued > 0 ? "attention" : "ready"),
                new ReportMetricDto("fbr-failed", "FBR failed", fbrFailed, "number", fbrFailed > 0 ? "risk" : "ready")
            }),
            new ReportSectionDto("inventory", "Inventory & Stock", "Live valuation, reorder pressure, adjustments, usage and turnover foundations.", "inventory_2", new[]
            {
                new ReportMetricDto("stock-value", "Stock valuation", stockValue, "currency"),
                new ReportMetricDto("low-stock", "Low-stock SKUs", lowStock, "number", lowStock > 0 ? "attention" : "ready"),
                new ReportMetricDto("active-products", "Active products", products.Length, "number"),
                new ReportMetricDto("stock-adjustments", "Stock adjustments", adjustments.Length, "number")
            }),
            new ReportSectionDto("supply", "Suppliers & Warehouse", "Purchase commitments, goods movement, transfer and receiving visibility.", "warehouse", new[]
            {
                new ReportMetricDto("supplier-count", "Active suppliers", (snapshot.Vendors ?? Array.Empty<Vendor>()).Count(vendor => vendor.TenantId == tenantId && string.Equals(vendor.Status, "Active", StringComparison.OrdinalIgnoreCase)), "number"),
                new ReportMetricDto("open-po", "Open purchase orders", openPurchaseOrders, "number", openPurchaseOrders > 0 ? "attention" : "ready"),
                new ReportMetricDto("po-value", "Open PO value", purchaseOrders.Where(order => !string.Equals(order.Status, "Received", StringComparison.OrdinalIgnoreCase)).Sum(order => order.TotalAmount), "currency"),
                new ReportMetricDto("stock-transfers", "Open stock transfers", openTransfers, "number", openTransfers > 0 ? "attention" : "ready")
            }),
            new ReportSectionDto("customers", "Customers & Cashier", "Repeat buying, customer base, shift cash and operational accountability.", "groups", new[]
            {
                new ReportMetricDto("customers", "Customer profiles", customers.Count(customer => !customer.IsWalkIn), "number"),
                new ReportMetricDto("repeat-customers", "Repeat customers", repeatCustomers, "number"),
                new ReportMetricDto("open-shifts", "Open cash shifts", cashShifts.Count(shift => string.Equals(shift.Status, "Open", StringComparison.OrdinalIgnoreCase)), "number", "attention"),
                new ReportMetricDto("cash-variance", "Cash variance", cashShifts.Sum(shift => shift.CountedCash - shift.ExpectedCash), "currency")
            }),
            new ReportSectionDto("finance", "Finance & Compliance", "Profitability view now; expenses, ledger and cash-flow drilldowns activate as entries are posted.", "account_balance", new[]
            {
                new ReportMetricDto("pnl-revenue", "P&L revenue", salesAmount, "currency"),
                new ReportMetricDto("pnl-profit", "P&L gross profit", grossProfit, "currency"),
                new ReportMetricDto("expense-data", "Expense entries", 0, "number", "data-required"),
                new ReportMetricDto("ledger-data", "Ledger entries", 0, "number", "data-required")
            })
        };

        return new ReportsHubDto(DateTimeOffset.Now, sections, salesByItem, salesByCategory, paymentMethods);
    }

    public async Task<InventoryOverviewDto> GetInventoryOverviewAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var snapshot = await workspaceRepository.GetWorkspaceSnapshotAsync(cancellationToken);
        EnsureTenant(snapshot, tenantId);

        var products = snapshot.Products
            .Where(product => !product.IsArchived)
            .OrderBy(product => product.Name)
            .ToArray();
        var inventoryItems = products
            .Select(product => new InventoryItemDto(
                product.Id,
                product.Sku,
                product.Name,
                product.Category,
                product.Warehouse,
                product.InHand,
                product.Reserved,
                Math.Max(product.InHand - product.Reserved, 0),
                product.UnitPrice,
                product.UnitPrice * product.InHand,
                product.Status,
                product.ReorderLevel,
                product.VisualCode,
                product.IsFavorite,
                product.IsQuickSale))
            .ToArray();
        var transactions = snapshot.RecentTransactions ?? Array.Empty<SaleRecord>();
        var customers = snapshot.Customers ?? Array.Empty<CustomerProfile>();
        var activityAnchor = ResolveActivityAnchor(transactions, customers);
        var analysisStart = activityAnchor.AddDays(-30);
        var soldUnitsByProduct = transactions
            .Where(transaction => transaction.OccurredAt >= analysisStart && !IsRefundedTransaction(transaction))
            .SelectMany(transaction => transaction.Lines ?? Array.Empty<SaleLine>())
            .GroupBy(line => line.ProductId)
            .ToDictionary(group => group.Key, group => group.Sum(line => line.Quantity));
        var adjustmentDeltaByProduct = (snapshot.StockAdjustments ?? Array.Empty<StockAdjustmentRecord>())
            .Where(adjustment => adjustment.OccurredAt >= analysisStart)
            .GroupBy(adjustment => adjustment.ProductId)
            .ToDictionary(group => group.Key, group => group.Sum(adjustment => adjustment.QuantityDelta));
        var lowStockItems = inventoryItems
            .Where(item => item.Available <= item.ReorderLevel)
            .Select(item => new InventoryLowStockItemDto(
                item.ProductId,
                item.Sku,
                item.ProductName,
                item.Warehouse,
                item.Available,
                item.ReorderLevel,
                Math.Max(item.ReorderLevel - item.Available, 0)))
            .OrderByDescending(item => item.ShortfallUnits)
            .ThenBy(item => item.Available)
            .ThenBy(item => item.ProductName)
            .ToArray();
        var totalValue = inventoryItems.Sum(item => item.Value);
        var warehouseSummaries = inventoryItems
            .GroupBy(item => item.Warehouse)
            .Select(group => new InventoryWarehouseSummaryDto(
                group.Key,
                group.Count(),
                group.Sum(item => item.InHand),
                group.Sum(item => item.Available),
                group.Sum(item => item.Value)))
            .OrderByDescending(item => item.StockValue)
            .ThenBy(item => item.Warehouse)
            .ToArray();
        var categorySummaries = inventoryItems
            .GroupBy(item => item.Category)
            .Select(group =>
            {
                var stockValue = group.Sum(item => item.Value);
                var share = totalValue <= 0
                    ? 0m
                    : decimal.Round((stockValue / totalValue) * 100m, 1, MidpointRounding.AwayFromZero);

                return new InventoryCategorySummaryDto(
                    group.Key,
                    group.Count(),
                    group.Sum(item => item.Available),
                    stockValue,
                    share);
            })
            .OrderByDescending(item => item.StockValue)
            .ThenBy(item => item.Category)
            .ToArray();
        var usageInsights = inventoryItems
            .Select(item =>
            {
                var soldUnits = soldUnitsByProduct.GetValueOrDefault(item.ProductId);
                var adjustmentDelta = adjustmentDeltaByProduct.GetValueOrDefault(item.ProductId);
                var turnover = CalculateTurnoverRatio(soldUnits, item.InHand, item.Available);

                return new InventoryUsageInsightDto(
                    item.ProductId,
                    item.Sku,
                    item.ProductName,
                    soldUnits,
                    adjustmentDelta,
                    turnover,
                    BuildCoverageLabel(item.Available, soldUnits));
            })
            .Where(item => item.SoldUnits30Days > 0 || item.NetAdjustment30Days != 0 || lowStockItems.Any(low => low.ProductId == item.ProductId))
            .OrderByDescending(item => item.SoldUnits30Days)
            .ThenByDescending(item => item.TurnoverRatio30Days)
            .ThenBy(item => item.ProductName)
            .Take(8)
            .ToArray();
        var barcodeQueue = products
            .OrderByDescending(product => product.IsFavorite)
            .ThenByDescending(product => product.IsQuickSale)
            .ThenBy(product => product.Name)
            .Take(8)
            .Select(product => new InventoryBarcodeQueueItemDto(
                product.Id,
                product.Sku,
                product.Name,
                product.Category,
                product.VisualCode,
                product.IsFavorite,
                product.IsQuickSale))
            .ToArray();
        var recentStockTakes = (snapshot.StockTakes ?? Array.Empty<StockTakeSession>())
            .OrderByDescending(stockTake => stockTake.CountedAt)
            .Take(8)
            .Select(stockTake => new StockTakeSummaryDto(
                stockTake.Id,
                stockTake.ProductId,
                stockTake.Sku,
                stockTake.ProductName,
                stockTake.Warehouse,
                stockTake.SystemQuantity,
                stockTake.CountedQuantity,
                stockTake.VarianceQuantity,
                stockTake.Status,
                stockTake.CountedBy,
                stockTake.Notes,
                stockTake.CountedAt))
            .ToArray();
        var totalSoldUnits30Days = soldUnitsByProduct.Values.Sum();
        var totalAvailableUnits = inventoryItems.Sum(item => item.Available);
        var metrics = new InventoryMetricsDto(
            inventoryItems.Length,
            lowStockItems.Length,
            totalValue,
            warehouseSummaries.Length,
            categorySummaries.Length,
            recentStockTakes.Count(stockTake => stockTake.CountedAt >= analysisStart),
            totalSoldUnits30Days <= 0
                ? 0m
                : decimal.Round(totalSoldUnits30Days / Math.Max(totalAvailableUnits, 1m), 2, MidpointRounding.AwayFromZero));

        return new InventoryOverviewDto(
            "Inventory Stock Overview",
            "Manage stock, post cycle counts, watch warehouse exposure, and prepare barcode-ready items.",
            inventoryItems.Select(item => item.Warehouse).Distinct().OrderBy(name => name).ToArray(),
            inventoryItems.Select(item => item.Category).Distinct().OrderBy(name => name).ToArray(),
            inventoryItems.Select(item => item.Status).Distinct().OrderBy(name => name).ToArray(),
            inventoryItems,
            metrics,
            lowStockItems.Take(8).ToArray(),
            warehouseSummaries,
            categorySummaries,
            usageInsights,
            barcodeQueue,
            recentStockTakes);
    }

    public async Task<PosTerminalDto> GetPosTerminalAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var snapshot = await workspaceRepository.GetWorkspaceSnapshotAsync(cancellationToken);
        EnsureTenant(snapshot, tenantId);
        var heldOrders = snapshot.HeldOrders ?? Array.Empty<PosHeldOrder>();
        var bookings = snapshot.Bookings ?? Array.Empty<PosBookingOrder>();
        var paymentMethods = new[]
        {
            "Cash",
            "Card",
            "Bank Transfer",
            "Digital Wallet",
            "Mixed"
        };

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
            PosPricingCalculator.BuildSummary(snapshot.ActiveCart),
            heldOrders
                .OrderByDescending(order => order.HeldAt)
                .Select(order => new PosHeldOrderDto(
                    order.Id,
                    order.TicketNo,
                    order.CustomerName,
                    order.PricingTier,
                    order.HeldBy,
                    order.HeldAt,
                    order.ItemCount,
                    order.Total,
                    order.Lines
                        .Select(line => new CartLineDto(
                            line.ProductId,
                            line.Name,
                            line.Quantity,
                            line.UnitPrice,
                            line.AllowQuantityEdit))
                        .ToArray(),
                    order.Notes))
                .ToArray(),
            bookings
                .OrderByDescending(order => order.CreatedAt)
                .Select(MapBooking)
                .ToArray(),
            new PosWorkflowMetricsDto(
                heldOrders.Count,
                bookings.Count,
                bookings.Sum(order => order.BalanceAmount),
                bookings.Count(order => order.BalanceAmount > 0)),
            paymentMethods);
    }

    public async Task<SalesHistoryDto> GetSalesHistoryAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var snapshot = await workspaceRepository.GetWorkspaceSnapshotAsync(cancellationToken);
        EnsureTenant(snapshot, tenantId);
        var transactions = snapshot.RecentTransactions ?? Array.Empty<SaleRecord>();
        var openBookings = (snapshot.Bookings ?? Array.Empty<PosBookingOrder>())
            .OrderBy(order => order.DueAt ?? DateTimeOffset.MaxValue)
            .ThenByDescending(order => order.CreatedAt)
            .Select(order => new SalesBookingInsightDto(
                order.Id,
                order.BookingNo,
                order.CustomerName,
                order.TotalAmount,
                order.PaidAmount,
                order.BalanceAmount,
                order.PaymentStatus,
                order.CreatedAt,
                order.DueAt))
            .ToArray();
        var activeTransactions = transactions
            .Where(transaction => !IsRefundedTransaction(transaction))
            .ToArray();
        var refundedTransactions = transactions
            .Where(IsRefundedTransaction)
            .ToArray();
        var netRevenue = transactions.Sum(GetNetTransactionAmount);
        var grossProfit = transactions.Sum(GetNetTransactionGrossProfit);
        var averageTicket = activeTransactions.Length == 0
            ? 0m
            : decimal.Round(netRevenue / activeTransactions.Length, 2, MidpointRounding.AwayFromZero);
        var paymentMethods = transactions
            .Where(transaction => !IsRefundedTransaction(transaction))
            .SelectMany(GetPaymentAllocationsForAnalytics)
            .GroupBy(payment => payment.Method, StringComparer.OrdinalIgnoreCase)
            .Select(group => new SalesPaymentMethodSummaryDto(
                group.First().Method,
                decimal.Round(group.Sum(payment => payment.Amount), 2, MidpointRounding.AwayFromZero),
                group.Count()))
            .OrderByDescending(method => method.Amount)
            .ThenBy(method => method.Method)
            .ToArray();

        return new SalesHistoryDto(
            new SalesHistoryMetricsDto(
                transactions.Count,
                decimal.Round(netRevenue, 2, MidpointRounding.AwayFromZero),
                decimal.Round(grossProfit, 2, MidpointRounding.AwayFromZero),
                averageTicket,
                refundedTransactions.Length,
                decimal.Round(refundedTransactions.Sum(GetRefundedAmount), 2, MidpointRounding.AwayFromZero),
                openBookings.Length,
                decimal.Round(openBookings.Sum(order => order.BalanceAmount), 2, MidpointRounding.AwayFromZero),
                openBookings.Count(order => order.DueAt is not null && order.DueAt.Value.Date <= DateTimeOffset.Now.Date)),
            paymentMethods,
            openBookings,
            transactions
                .OrderByDescending(transaction => transaction.OccurredAt)
                .Select(MapSale)
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
                    transfer.RequestedBy,
                    transfer.Notes))
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
                    .Sum(GetNetTransactionAmount),
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

    public async Task<WarehouseHubDto> GetWarehouseHubAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var snapshot = await workspaceRepository.GetWorkspaceSnapshotAsync(cancellationToken);
        EnsureTenant(snapshot, tenantId);

        var stockTransfers = snapshot.StockTransfers ?? Array.Empty<StockTransfer>();
        var goodsReceipts = snapshot.GoodsReceipts ?? Array.Empty<GoodsReceipt>();
        var gatePasses = snapshot.GatePasses ?? Array.Empty<GatePass>();
        var branches = snapshot.Branches
            .Select(branch => branch.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var warehouses = snapshot.Branches
            .Select(branch => branch.WarehouseName)
            .Concat(snapshot.Products.Select(product => product.Warehouse))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new WarehouseHubDto(
            new WarehouseMetricsDto(
                stockTransfers.Count(transfer => IsTransferInMotion(transfer.Status) || IsWorkflowOpen(transfer.Status)),
                goodsReceipts.Count(receipt => IsWorkflowOpen(receipt.Status)),
                gatePasses.Count(pass => IsWorkflowOpen(pass.Status)),
                stockTransfers
                    .Where(transfer => IsTransferInMotion(transfer.Status) || IsWorkflowOpen(transfer.Status))
                    .Sum(transfer => transfer.Units)),
            branches,
            warehouses,
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
                    transfer.RequestedBy,
                    transfer.Notes))
                .ToArray(),
            goodsReceipts
                .OrderByDescending(receipt => receipt.ReceivedAt)
                .Select(receipt => new GoodsReceiptSummaryDto(
                    receipt.Id,
                    receipt.ReceiptNo,
                    receipt.PurchaseOrderNo,
                    receipt.VendorName,
                    receipt.WarehouseName,
                    receipt.Status,
                    receipt.ReceivedAt,
                    receipt.ReceivedBy,
                    receipt.LineCount,
                    receipt.ReceivedUnits,
                    receipt.VarianceUnits,
                    receipt.Notes))
                .ToArray(),
            gatePasses
                .OrderByDescending(pass => pass.IssuedAt)
                .Select(pass => new GatePassSummaryDto(
                    pass.Id,
                    pass.GatePassNo,
                    pass.MovementType,
                    pass.WarehouseName,
                    pass.DestinationName,
                    pass.ReferenceNo,
                    pass.Status,
                    pass.IssuedAt,
                    pass.IssuedBy,
                    pass.Units,
                    pass.Notes))
                .ToArray());
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

    private static PosBookingOrderDto MapBooking(PosBookingOrder order)
    {
        return new PosBookingOrderDto(
            order.Id,
            order.BookingNo,
            order.CustomerName,
            order.PhoneNumber,
            order.Email,
            order.Status,
            order.CreatedAt,
            order.DueAt,
            order.BookedBy,
            order.ItemCount,
            order.Subtotal,
            order.Discount,
            order.Tax,
            order.TotalAmount,
            order.PaidAmount,
            order.BalanceAmount,
            order.PaymentStatus,
            (order.Lines ?? Array.Empty<SaleLine>())
                .Select(line => new SaleLineDto(
                    line.ProductId,
                    line.Sku,
                    line.Name,
                    line.Quantity,
                    line.UnitPrice,
                    line.LineTotal))
                .ToArray(),
            (order.Payments ?? Array.Empty<PaymentAllocation>())
                .Select(payment => new PosPaymentLineDto(payment.Method, payment.Amount, payment.ReferenceNo))
                .ToArray(),
            order.Notes);
    }

    private static SalesHistoryItemDto MapSale(SaleRecord transaction)
    {
        var refundedAmount = GetRefundedAmount(transaction);

        return new SalesHistoryItemDto(
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
            transaction.FbrReportedAt,
            transaction.PaidAmount,
            transaction.BalanceAmount,
            transaction.PaymentStatus,
            (transaction.Payments ?? Array.Empty<PaymentAllocation>())
                .Select(payment => new PosPaymentLineDto(payment.Method, payment.Amount, payment.ReferenceNo))
                .ToArray(),
            GetNetTransactionAmount(transaction),
            refundedAmount,
            transaction.RefundedAt,
            transaction.RefundedBy,
            transaction.RefundReason,
            transaction.InventoryReturned);
    }

    private static PaymentAllocation[] GetPaymentAllocationsForAnalytics(SaleRecord transaction)
    {
        var payments = (transaction.Payments ?? Array.Empty<PaymentAllocation>())
            .Where(payment => payment.Amount > 0)
            .ToArray();

        if (payments.Length > 0)
        {
            return payments;
        }

        var netAmount = GetNetTransactionAmount(transaction);
        if (netAmount <= 0)
        {
            return Array.Empty<PaymentAllocation>();
        }

        return
        [
            new PaymentAllocation(transaction.PaymentMethod, netAmount)
        ];
    }

    private static decimal GetNetTransactionAmount(SaleRecord transaction)
    {
        return decimal.Round(Math.Max(transaction.Amount - GetRefundedAmount(transaction), 0), 2, MidpointRounding.AwayFromZero);
    }

    private static decimal GetNetTransactionGrossProfit(SaleRecord transaction)
    {
        return IsRefundedTransaction(transaction)
            ? 0m
            : decimal.Round(Math.Max(transaction.GrossProfit, 0), 2, MidpointRounding.AwayFromZero);
    }

    private static decimal GetRefundedAmount(SaleRecord transaction)
    {
        if (transaction.RefundedAmount > 0)
        {
            return decimal.Round(Math.Min(transaction.RefundedAmount, Math.Max(transaction.Amount, 0)), 2, MidpointRounding.AwayFromZero);
        }

        return IsRefundedTransaction(transaction)
            ? decimal.Round(Math.Max(transaction.Amount, 0), 2, MidpointRounding.AwayFromZero)
            : 0m;
    }

    private static bool IsRefundedTransaction(SaleRecord transaction)
    {
        return transaction.RefundedAmount > 0
            || transaction.Status.Contains("Refund", StringComparison.OrdinalIgnoreCase)
            || string.Equals(transaction.PaymentStatus, "Refunded", StringComparison.OrdinalIgnoreCase);
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

    private static bool IsWorkflowOpen(string status)
    {
        return !string.IsNullOrWhiteSpace(status)
            && !status.Contains("Received", StringComparison.OrdinalIgnoreCase)
            && !status.Contains("Closed", StringComparison.OrdinalIgnoreCase)
            && !status.Contains("Cancelled", StringComparison.OrdinalIgnoreCase)
            && !status.Contains("Completed", StringComparison.OrdinalIgnoreCase);
    }

    private static decimal CalculateTurnoverRatio(int soldUnits30Days, int inHandUnits, int availableUnits)
    {
        if (soldUnits30Days <= 0)
        {
            return 0m;
        }

        var stockBaseline = Math.Max((inHandUnits + availableUnits) / 2m, 1m);
        return decimal.Round(soldUnits30Days / stockBaseline, 2, MidpointRounding.AwayFromZero);
    }

    private static string BuildCoverageLabel(int availableUnits, int soldUnits30Days)
    {
        if (availableUnits <= 0)
        {
            return "Out of stock";
        }

        if (soldUnits30Days <= 0)
        {
            return "No recent movement";
        }

        var averageDailyDemand = soldUnits30Days / 30m;
        var coverDays = decimal.Round(availableUnits / averageDailyDemand, 0, MidpointRounding.AwayFromZero);

        if (coverDays < 7)
        {
            return "Urgent cover";
        }

        if (coverDays < 21)
        {
            return "Tight cover";
        }

        if (coverDays <= 45)
        {
            return "Healthy cover";
        }

        return "Deep stock cover";
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
