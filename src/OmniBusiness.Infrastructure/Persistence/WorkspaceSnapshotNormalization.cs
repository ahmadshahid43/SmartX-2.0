using OmniBusiness.Domain.Foundation;

namespace OmniBusiness.Infrastructure.Persistence;

internal static class WorkspaceSnapshotNormalization
{
    private static readonly Guid WalkInCustomerId = Guid.Parse("77777777-7777-7777-7777-777777777771");
    private const decimal PosDiscountThreshold = 3m;
    private const decimal PosFixedDiscount = 500m;
    private const decimal PosTaxRate = 0.17m;
    private static readonly IReadOnlyDictionary<string, decimal> MarketMonthlyPriceMap =
        new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["dashboard-analytics"] = 350m,
            ["pos-checkout"] = 999m,
            ["counter-orders"] = 400m,
            ["book-orders"] = 450m,
            ["hold-and-resume"] = 250m,
            ["customer-profiles"] = 250m,
            ["split-payments"] = 300m,
            ["late-payments"] = 500m,
            ["service-cards"] = 700m,
            ["returns-refunds"] = 300m,
            ["inventory-core"] = 650m,
            ["trade-in"] = 450m,
            ["stock-take"] = 300m,
            ["stock-transfer-desk"] = 325m,
            ["grn-receiving"] = 350m,
            ["inward-register"] = 275m,
            ["gate-pass-control"] = 225m,
            ["warehouse-reports"] = 450m,
            ["barcode-suite"] = 250m,
            ["supplier-management"] = 300m,
            ["purchase-orders"] = 350m,
            ["expense-management"] = 300m,
            ["ledger-accounting"] = 700m,
            ["profit-loss"] = 450m,
            ["fbr-compliance"] = 550m,
            ["pos-configuration"] = 250m,
            ["order-listing"] = 250m,
            ["booking-analytics"] = 350m,
            ["reporting-suite"] = 500m,
            ["tax-and-refund-reporting"] = 350m,
            ["expiry-and-usage-reporting"] = 350m,
            ["social-publishing"] = 650m,
            ["customer-notifications"] = 250m,
            ["employee-management"] = 250m,
            ["role-permissions"] = 250m,
            ["no-code-builder"] = 650m,
            ["plan-and-module-control"] = 350m
        };
    private static readonly string[] DefaultEnabledModuleKeys =
    [
        "dashboard-analytics",
        "pos-checkout",
        "counter-orders",
        "customer-profiles",
        "inventory-core",
        "supplier-management",
        "purchase-orders",
        "grn-receiving",
        "order-listing",
        "reporting-suite",
        "fbr-compliance",
        "employee-management",
        "role-permissions",
        "no-code-builder",
        "plan-and-module-control"
    ];

    public static WorkspaceSnapshot Normalize(WorkspaceSnapshot snapshot)
    {
        var branches = snapshot.Branches ?? Array.Empty<Branch>();
        var primaryBranchId = branches.FirstOrDefault(branch => branch.IsPrimary)?.Id ?? snapshot.AdminUser.BranchId;
        var activeCustomer = snapshot.ActiveCustomer ?? new PosCustomer("Walk-in Customer", "Retail Pricing", "W");
        var users = NormalizeUsers(snapshot, primaryBranchId);
        var adminUser = users.FirstOrDefault(user => user.Id == snapshot.AdminUser.Id)
            ?? users.FirstOrDefault(user => string.Equals(user.Role, "Owner", StringComparison.OrdinalIgnoreCase))
            ?? users[0];

        var recentTransactions = (snapshot.RecentTransactions ?? Array.Empty<SaleRecord>())
            .Select(transaction => transaction with
            {
                Lines = transaction.Lines ?? Array.Empty<SaleLine>(),
                CashierName = string.IsNullOrWhiteSpace(transaction.CashierName)
                    ? snapshot.AdminUser.DisplayName
                    : transaction.CashierName,
                ReceivedAmount = transaction.ReceivedAmount <= 0 ? transaction.Amount : transaction.ReceivedAmount,
                ChangeAmount = transaction.ChangeAmount < 0 ? 0 : transaction.ChangeAmount,
                FbrStatus = string.IsNullOrWhiteSpace(transaction.FbrStatus) ? "QueuedOffline" : transaction.FbrStatus,
                PaidAmount = transaction.PaidAmount <= 0
                    ? Math.Max(transaction.Amount - Math.Max(transaction.BalanceAmount, 0), 0)
                    : transaction.PaidAmount,
                BalanceAmount = transaction.BalanceAmount < 0 ? 0 : transaction.BalanceAmount,
                PaymentStatus = string.IsNullOrWhiteSpace(transaction.PaymentStatus)
                    ? (IsRefundedStatus(transaction.Status)
                        ? "Refunded"
                        : transaction.BalanceAmount > 0
                            ? "Partially Paid"
                            : "Paid")
                    : transaction.PaymentStatus,
                Payments = NormalizePayments(
                    transaction.Payments,
                    transaction.PaymentMethod,
                    transaction.PaidAmount <= 0
                        ? Math.Max(transaction.Amount - Math.Max(transaction.BalanceAmount, 0), 0)
                        : transaction.PaidAmount),
                RefundedAmount = NormalizeRefundedAmount(transaction),
                RefundedBy = NormalizeNullableText(transaction.RefundedBy),
                RefundReason = NormalizeNullableText(transaction.RefundReason)
            })
            .ToArray();

        var nextSequence = snapshot.NextSaleSequence > 0
            ? snapshot.NextSaleSequence
            : InferNextSequence(recentTransactions);
        var customers = NormalizeCustomers(snapshot, activeCustomer, recentTransactions);
        var vendors = NormalizeVendors(snapshot);
        var purchaseOrders = NormalizePurchaseOrders(snapshot, vendors, recentTransactions);
        var stockTransfers = NormalizeStockTransfers(snapshot, branches, adminUser);
        var cashShifts = NormalizeCashShifts(snapshot, users, recentTransactions);
        var subscriptionSettings = NormalizeSubscriptionSettings(snapshot);
        var heldOrders = NormalizeHeldOrders(snapshot, activeCustomer, adminUser);
        var bookings = NormalizeBookings(snapshot, adminUser);
        var nextBookingSequence = snapshot.NextBookingSequence > 0
            ? snapshot.NextBookingSequence
            : InferNextBookingSequence(bookings);
        var nextHoldSequence = snapshot.NextHoldSequence > 0
            ? snapshot.NextHoldSequence
            : InferNextHoldSequence(heldOrders);

        return snapshot with
        {
            AdminUser = adminUser,
            ActiveCustomer = activeCustomer,
            Branches = branches,
            DailyFigures = snapshot.DailyFigures ?? Array.Empty<DailyBusinessFigure>(),
            SalesTrend = snapshot.SalesTrend ?? Array.Empty<TrendPoint>(),
            TopSelling = snapshot.TopSelling ?? Array.Empty<TopSellingItem>(),
            BranchPerformance = snapshot.BranchPerformance ?? Array.Empty<BranchPerformance>(),
            Products = snapshot.Products ?? Array.Empty<Product>(),
            RecentTransactions = recentTransactions,
            ActiveCart = snapshot.ActiveCart ?? Array.Empty<CartLine>(),
            Users = users,
            Customers = customers,
            StockAdjustments = snapshot.StockAdjustments ?? Array.Empty<StockAdjustmentRecord>(),
            Vendors = vendors,
            PurchaseOrders = purchaseOrders,
            StockTransfers = stockTransfers,
            CashShifts = cashShifts,
            SubscriptionSettings = subscriptionSettings,
            NextSaleSequence = nextSequence,
            GoodsReceipts = NormalizeGoodsReceipts(snapshot),
            GatePasses = NormalizeGatePasses(snapshot),
            StockTakes = NormalizeStockTakes(snapshot),
            HeldOrders = heldOrders,
            Bookings = bookings,
            NextBookingSequence = nextBookingSequence,
            NextHoldSequence = nextHoldSequence
        };
    }

    private static AppUser[] NormalizeUsers(WorkspaceSnapshot snapshot, Guid primaryBranchId)
    {
        var sourceUsers = snapshot.Users?.Where(user => user is not null).ToList()
            ?? new List<AppUser>();

        if (sourceUsers.Count == 0)
        {
            sourceUsers.Add(snapshot.AdminUser);
        }

        if (!sourceUsers.Any(user => user.Id == snapshot.AdminUser.Id))
        {
            sourceUsers.Insert(0, snapshot.AdminUser);
        }

        return sourceUsers
            .GroupBy(user => user.Id)
            .Select(group =>
            {
                var user = group.Last();
                return user with
                {
                    BranchId = user.BranchId == Guid.Empty ? primaryBranchId : user.BranchId,
                    Email = user.Email.Trim(),
                    DisplayName = string.IsNullOrWhiteSpace(user.DisplayName) ? user.Email.Trim() : user.DisplayName.Trim(),
                    Role = string.IsNullOrWhiteSpace(user.Role) ? "Cashier" : user.Role.Trim()
                };
            })
            .OrderBy(user => string.Equals(user.Role, "Owner", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(user => user.DisplayName)
            .ToArray();
    }

    private static CustomerProfile[] NormalizeCustomers(
        WorkspaceSnapshot snapshot,
        PosCustomer activeCustomer,
        IReadOnlyList<SaleRecord> recentTransactions)
    {
        var customers = snapshot.Customers?.ToList() ?? [];
        var activityAnchor = recentTransactions
            .Select(transaction => transaction.OccurredAt)
            .DefaultIfEmpty(DateTimeOffset.UtcNow)
            .Max();

        if (!customers.Any(customer => customer.Id == WalkInCustomerId || customer.IsWalkIn))
        {
            customers.Insert(
                0,
                new CustomerProfile(
                    WalkInCustomerId,
                    snapshot.Tenant.Id,
                    activeCustomer.Name,
                    activeCustomer.PricingTier,
                    activeCustomer.AvatarLetter,
                    null,
                    true));
        }

        if (customers.Count <= 1)
        {
            return customers
                .GroupBy(customer => customer.Id)
                .Select(group => group.Last())
                .OrderBy(customer => customer.IsWalkIn ? 0 : 1)
                .ThenByDescending(customer => customer.LastVisitAt ?? DateTimeOffset.MinValue)
                .ToArray();
        }

        return customers
            .GroupBy(customer => customer.Id)
            .Select(group => group.Last())
            .OrderBy(customer => customer.IsWalkIn ? 0 : 1)
            .ThenByDescending(customer => customer.LastVisitAt ?? DateTimeOffset.MinValue)
            .ToArray();
    }

    private static Vendor[] NormalizeVendors(WorkspaceSnapshot snapshot)
    {
        if (snapshot.Vendors is { Count: > 0 })
        {
            return snapshot.Vendors.ToArray();
        }

        return Array.Empty<Vendor>();
    }

    private static PurchaseOrder[] NormalizePurchaseOrders(
        WorkspaceSnapshot snapshot,
        IReadOnlyList<Vendor> vendors,
        IReadOnlyList<SaleRecord> recentTransactions)
    {
        if (snapshot.PurchaseOrders is { Count: > 0 })
        {
            return snapshot.PurchaseOrders.ToArray();
        }

        return Array.Empty<PurchaseOrder>();
    }

    private static StockTransfer[] NormalizeStockTransfers(
        WorkspaceSnapshot snapshot,
        IReadOnlyList<Branch> branches,
        AppUser adminUser)
    {
        if (snapshot.StockTransfers is { Count: > 0 })
        {
            return snapshot.StockTransfers.ToArray();
        }

        return Array.Empty<StockTransfer>();
    }

    private static CashShift[] NormalizeCashShifts(
        WorkspaceSnapshot snapshot,
        IReadOnlyList<AppUser> users,
        IReadOnlyList<SaleRecord> recentTransactions)
    {
        if (snapshot.CashShifts is { Count: > 0 })
        {
            return snapshot.CashShifts.ToArray();
        }

        return Array.Empty<CashShift>();
    }

    private static GoodsReceipt[] NormalizeGoodsReceipts(WorkspaceSnapshot snapshot)
    {
        return snapshot.GoodsReceipts is { Count: > 0 }
            ? snapshot.GoodsReceipts
                .OrderByDescending(receipt => receipt.ReceivedAt)
                .ToArray()
            : Array.Empty<GoodsReceipt>();
    }

    private static GatePass[] NormalizeGatePasses(WorkspaceSnapshot snapshot)
    {
        return snapshot.GatePasses is { Count: > 0 }
            ? snapshot.GatePasses
                .OrderByDescending(pass => pass.IssuedAt)
                .ToArray()
            : Array.Empty<GatePass>();
    }

    private static StockTakeSession[] NormalizeStockTakes(WorkspaceSnapshot snapshot)
    {
        return snapshot.StockTakes is { Count: > 0 }
            ? snapshot.StockTakes
                .OrderByDescending(stockTake => stockTake.CountedAt)
                .ToArray()
            : Array.Empty<StockTakeSession>();
    }

    private static PosHeldOrder[] NormalizeHeldOrders(
        WorkspaceSnapshot snapshot,
        PosCustomer activeCustomer,
        AppUser adminUser)
    {
        return snapshot.HeldOrders is { Count: > 0 }
            ? snapshot.HeldOrders
                .Select(order =>
                {
                    var lines = order.Lines ?? Array.Empty<CartLine>();
                    var derivedItemCount = lines.Sum(line => Math.Max(line.Quantity, 0));
                    var derivedTotal = lines.Sum(line => Math.Max(line.Quantity, 0) * line.UnitPrice);

                    return order with
                    {
                        CustomerName = string.IsNullOrWhiteSpace(order.CustomerName) ? activeCustomer.Name : order.CustomerName,
                        PricingTier = string.IsNullOrWhiteSpace(order.PricingTier) ? activeCustomer.PricingTier : order.PricingTier,
                        HeldBy = string.IsNullOrWhiteSpace(order.HeldBy) ? adminUser.DisplayName : order.HeldBy,
                        ItemCount = order.ItemCount <= 0 ? derivedItemCount : order.ItemCount,
                        Total = order.Total <= 0 ? derivedTotal : order.Total,
                        Lines = lines
                    };
                })
                .OrderByDescending(order => order.HeldAt)
                .ToArray()
            : Array.Empty<PosHeldOrder>();
    }

    private static PosBookingOrder[] NormalizeBookings(WorkspaceSnapshot snapshot, AppUser adminUser)
    {
        return snapshot.Bookings is { Count: > 0 }
            ? snapshot.Bookings
                .Select(order =>
                {
                    var lines = order.Lines ?? Array.Empty<SaleLine>();
                    var totals = BuildTotals(lines);
                    var totalAmount = order.TotalAmount <= 0 ? totals.Total : order.TotalAmount;
                    var paidAmount = order.PaidAmount < 0 ? 0 : order.PaidAmount;
                    var balanceAmount = order.BalanceAmount < 0
                        ? Math.Max(totalAmount - paidAmount, 0)
                        : order.BalanceAmount;

                    return order with
                    {
                        BookedBy = string.IsNullOrWhiteSpace(order.BookedBy) ? adminUser.DisplayName : order.BookedBy,
                        Lines = lines,
                        ItemCount = order.ItemCount <= 0 ? totals.ItemCount : order.ItemCount,
                        Subtotal = order.Subtotal <= 0 ? totals.Subtotal : order.Subtotal,
                        Discount = order.Discount < 0 ? totals.Discount : order.Discount,
                        Tax = order.Tax < 0 ? totals.Tax : order.Tax,
                        TotalAmount = totalAmount,
                        PaidAmount = paidAmount,
                        BalanceAmount = balanceAmount,
                        PaymentStatus = string.IsNullOrWhiteSpace(order.PaymentStatus)
                            ? (balanceAmount > 0
                                ? (paidAmount > 0 ? "Partially Paid" : "Unpaid")
                                : "Paid")
                            : order.PaymentStatus,
                        Payments = NormalizePayments(order.Payments, null, paidAmount)
                    };
                })
                .OrderByDescending(order => order.CreatedAt)
                .ToArray()
            : Array.Empty<PosBookingOrder>();
    }

    private static int InferNextSequence(IEnumerable<SaleRecord> transactions)
    {
        var maxSequence = transactions
            .Select(transaction => transaction.ReferenceNo)
            .Select(reference =>
            {
                var parts = reference.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                return parts.Length == 2 && int.TryParse(parts[1], out var sequence) ? sequence : 8901;
            })
            .DefaultIfEmpty(8901)
            .Max();

        return maxSequence + 1;
    }

    private static int InferNextBookingSequence(IEnumerable<PosBookingOrder> bookings)
    {
        var maxSequence = bookings
            .Select(booking => booking.BookingNo)
            .Select(reference => ParseSequence(reference, 4099))
            .DefaultIfEmpty(4099)
            .Max();

        return maxSequence + 1;
    }

    private static int InferNextHoldSequence(IEnumerable<PosHeldOrder> heldOrders)
    {
        var maxSequence = heldOrders
            .Select(order => order.TicketNo)
            .Select(reference => ParseSequence(reference, 119))
            .DefaultIfEmpty(119)
            .Max();

        return maxSequence + 1;
    }

    private static SubscriptionPlanSettings NormalizeSubscriptionSettings(WorkspaceSnapshot snapshot)
    {
        if (snapshot.SubscriptionSettings is null)
        {
            return CreateDefaultSubscriptionSettings(snapshot);
        }

        var entitlements = (snapshot.SubscriptionSettings.ModuleEntitlements ?? Array.Empty<ModuleEntitlement>())
            .Where(entitlement => !string.IsNullOrWhiteSpace(entitlement.ModuleKey))
            .GroupBy(entitlement => entitlement.ModuleKey.Trim().ToLowerInvariant())
            .Select(group =>
            {
                var entitlement = group.Last();
                return new ModuleEntitlement(
                    group.Key,
                    entitlement.Enabled,
                    entitlement.AddOnMonthlyPrice < 0 ? 0 : decimal.Round(entitlement.AddOnMonthlyPrice, 2, MidpointRounding.AwayFromZero));
            })
            .ToArray();

        if (entitlements.Length == 0)
        {
            entitlements = CreateDefaultModuleEntitlements();
        }

        var normalizedPlanCode = NormalizePlanCode(snapshot.SubscriptionSettings.PlanCode, snapshot.Tenant.SubscriptionPlan);
        entitlements = AppendPlanUpgradeModules(normalizedPlanCode, entitlements);
        var baseMonthlyPrice = snapshot.SubscriptionSettings.BaseMonthlyPrice < 0
            ? 0
            : decimal.Round(snapshot.SubscriptionSettings.BaseMonthlyPrice, 2, MidpointRounding.AwayFromZero);

        if (RequiresMarketPricingRefresh(baseMonthlyPrice, entitlements))
        {
            entitlements = RefreshEntitlementsWithMarketPricing(entitlements);
            baseMonthlyPrice = GetDefaultBaseMonthlyPrice(normalizedPlanCode);
        }

        return snapshot.SubscriptionSettings with
        {
            PlanCode = normalizedPlanCode,
            PlanName = string.IsNullOrWhiteSpace(snapshot.SubscriptionSettings.PlanName)
                ? snapshot.Tenant.SubscriptionPlan
                : snapshot.SubscriptionSettings.PlanName.Trim(),
            Currency = string.IsNullOrWhiteSpace(snapshot.SubscriptionSettings.Currency)
                ? snapshot.Company.BaseCurrency
                : snapshot.SubscriptionSettings.Currency.Trim().ToUpperInvariant(),
            BaseMonthlyPrice = baseMonthlyPrice,
            IncludedUsers = snapshot.SubscriptionSettings.IncludedUsers <= 0 ? 3 : snapshot.SubscriptionSettings.IncludedUsers,
            IncludedBranches = snapshot.SubscriptionSettings.IncludedBranches <= 0 ? 1 : snapshot.SubscriptionSettings.IncludedBranches,
            ModuleEntitlements = entitlements
        };
    }

    private static SubscriptionPlanSettings CreateDefaultSubscriptionSettings(WorkspaceSnapshot snapshot)
    {
        return new SubscriptionPlanSettings(
            NormalizePlanCode(snapshot.Tenant.SubscriptionPlan, snapshot.Tenant.SubscriptionPlan),
            string.IsNullOrWhiteSpace(snapshot.Tenant.SubscriptionPlan) ? "Starter" : snapshot.Tenant.SubscriptionPlan,
            string.IsNullOrWhiteSpace(snapshot.Company.BaseCurrency) ? "PKR" : snapshot.Company.BaseCurrency,
            GetDefaultBaseMonthlyPrice(snapshot.Tenant.SubscriptionPlan),
            3,
            1,
            true,
            CreateDefaultModuleEntitlements());
    }

    private static ModuleEntitlement[] CreateDefaultModuleEntitlements()
    {
        return DefaultEnabledModuleKeys
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(moduleKey => new ModuleEntitlement(moduleKey, true, GetDefaultModuleMonthlyPrice(moduleKey)))
            .ToArray();
    }

    private static bool RequiresMarketPricingRefresh(
        decimal baseMonthlyPrice,
        IReadOnlyList<ModuleEntitlement> entitlements)
    {
        return baseMonthlyPrice <= 0
            && entitlements.Count > 0
            && entitlements.All(entitlement => entitlement.AddOnMonthlyPrice <= 0);
    }

    private static ModuleEntitlement[] RefreshEntitlementsWithMarketPricing(
        IReadOnlyList<ModuleEntitlement> entitlements)
    {
        return entitlements
            .Select(entitlement => new ModuleEntitlement(
                entitlement.ModuleKey,
                entitlement.Enabled,
                GetDefaultModuleMonthlyPrice(entitlement.ModuleKey)))
            .ToArray();
    }

    private static ModuleEntitlement[] AppendPlanUpgradeModules(
        string planCode,
        IReadOnlyList<ModuleEntitlement> entitlements)
    {
        var upgraded = entitlements
            .GroupBy(entitlement => entitlement.ModuleKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Last())
            .ToDictionary(entitlement => entitlement.ModuleKey, StringComparer.OrdinalIgnoreCase);

        foreach (var moduleKey in GetPlanUpgradeModuleKeys(planCode))
        {
            if (upgraded.ContainsKey(moduleKey))
            {
                continue;
            }

            upgraded[moduleKey] = new ModuleEntitlement(moduleKey, true, GetDefaultModuleMonthlyPrice(moduleKey));
        }

        return upgraded.Values
            .OrderBy(entitlement => entitlement.ModuleKey, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<string> GetPlanUpgradeModuleKeys(string planCode)
    {
        return planCode switch
        {
            "growth" => ["book-orders", "hold-and-resume", "split-payments", "stock-transfer-desk", "inward-register"],
            "business" => ["book-orders", "hold-and-resume", "split-payments", "late-payments", "stock-transfer-desk", "inward-register", "gate-pass-control"],
            "premium" => ["book-orders", "hold-and-resume", "split-payments", "late-payments", "stock-transfer-desk", "inward-register", "gate-pass-control"],
            _ => Array.Empty<string>()
        };
    }

    private static decimal GetDefaultBaseMonthlyPrice(string? planCode)
    {
        return NormalizePlanCode(planCode, planCode) switch
        {
            "starter" => 550m,
            "growth" => 1200m,
            "business" => 2400m,
            "premium" => 4200m,
            "enterprise" => 4200m,
            _ => 550m
        };
    }

    private static decimal GetDefaultModuleMonthlyPrice(string moduleKey)
    {
        return MarketMonthlyPriceMap.TryGetValue(moduleKey, out var price)
            ? price
            : 0m;
    }

    private static string NormalizePlanCode(string? planCode, string? planName)
    {
        var source = string.IsNullOrWhiteSpace(planCode) ? planName : planCode;
        if (string.IsNullOrWhiteSpace(source))
        {
            return "starter";
        }

        var sanitized = new string(source
            .Trim()
            .ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : '-')
            .ToArray())
            .Trim('-');

        return string.IsNullOrWhiteSpace(sanitized) ? "starter" : sanitized;
    }

    private static PaymentAllocation[] NormalizePayments(
        IReadOnlyList<PaymentAllocation>? payments,
        string? fallbackMethod,
        decimal fallbackAmount)
    {
        var normalized = payments?
            .Where(payment => payment is not null && payment.Amount > 0)
            .Select(payment => new PaymentAllocation(
                string.IsNullOrWhiteSpace(payment.Method) ? "Cash" : payment.Method.Trim(),
                decimal.Round(payment.Amount, 2, MidpointRounding.AwayFromZero),
                string.IsNullOrWhiteSpace(payment.ReferenceNo) ? null : payment.ReferenceNo.Trim()))
            .ToArray()
            ?? Array.Empty<PaymentAllocation>();

        if (normalized.Length > 0)
        {
            return normalized;
        }

        if (fallbackAmount <= 0)
        {
            return Array.Empty<PaymentAllocation>();
        }

        return
        [
            new PaymentAllocation(
                string.IsNullOrWhiteSpace(fallbackMethod) ? "Cash" : fallbackMethod.Trim(),
                decimal.Round(fallbackAmount, 2, MidpointRounding.AwayFromZero))
        ];
    }

    private static (int ItemCount, decimal Subtotal, decimal Discount, decimal Tax, decimal Total) BuildTotals(
        IReadOnlyList<SaleLine> lines)
    {
        var itemCount = lines.Sum(line => Math.Max(line.Quantity, 0));
        var subtotal = lines.Sum(line => line.LineTotal);
        var discount = itemCount >= PosDiscountThreshold ? PosFixedDiscount : 0m;
        var taxable = Math.Max(subtotal - discount, 0m);
        var tax = decimal.Round(taxable * PosTaxRate, 2, MidpointRounding.AwayFromZero);

        return (itemCount, subtotal, discount, tax, taxable + tax);
    }

    private static decimal NormalizeRefundedAmount(SaleRecord transaction)
    {
        if (transaction.RefundedAmount > 0)
        {
            return decimal.Round(Math.Min(transaction.RefundedAmount, Math.Max(transaction.Amount, 0)), 2, MidpointRounding.AwayFromZero);
        }

        return IsRefundedStatus(transaction.Status)
            ? decimal.Round(Math.Max(transaction.Amount, 0), 2, MidpointRounding.AwayFromZero)
            : 0m;
    }

    private static bool IsRefundedStatus(string? status)
    {
        return !string.IsNullOrWhiteSpace(status)
            && status.Contains("Refund", StringComparison.OrdinalIgnoreCase);
    }

    private static string? NormalizeNullableText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static int ParseSequence(string reference, int fallback)
    {
        var parts = reference.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 2 && int.TryParse(parts[1], out var sequence) ? sequence : fallback;
    }
}
