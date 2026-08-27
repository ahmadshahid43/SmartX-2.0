using OmniBusiness.Domain.Foundation;

namespace OmniBusiness.Infrastructure.Persistence;

internal static class WorkspaceSnapshotNormalization
{
    private static readonly Guid WalkInCustomerId = Guid.Parse("77777777-7777-7777-7777-777777777771");
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
            ["grn-receiving"] = 350m,
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
                FbrStatus = string.IsNullOrWhiteSpace(transaction.FbrStatus) ? "QueuedOffline" : transaction.FbrStatus
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
            NextSaleSequence = nextSequence
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
}
