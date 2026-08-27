using OmniBusiness.Application.Contracts;
using OmniBusiness.Domain.Foundation;

namespace OmniBusiness.Application.Services;

internal static class WorkspaceModuleCatalog
{
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

    private static readonly ModuleGroupBlueprint[] GroupBlueprints =
    [
        new(
            "pos-commerce",
            "POS Commerce",
            "Fast checkout, booking, tender flexibility, and customer-facing order workflows.",
            [
                new(
                    "dashboard-analytics",
                    "Retail Dashboard",
                    "Executive and branch dashboard with sales pulse, low stock alerts, and operating KPIs.",
                    "monitoring",
                    "/dashboard",
                    "Live Screen",
                    "Starter",
                    true,
                    true,
                    ["Live sales overview", "Low stock visibility", "Branch performance tracking"]),
                new(
                    "pos-checkout",
                    "Smart Checkout",
                    "Counter billing with barcode-optional selling, discounts, tax, receipt generation, and fast tender capture.",
                    "point_of_sale",
                    "/pos",
                    "Live Screen",
                    "Starter",
                    true,
                    true,
                    ["Cash and card billing flow", "Receipt and invoice print", "Discount and tax handling"]),
                new(
                    "counter-orders",
                    "Counter Orders",
                    "Multiple counters, register context, and quick walk-in order handling for busy retail floors.",
                    "storefront",
                    "/pos",
                    "Workflow Expansion",
                    "Growth",
                    true,
                    true,
                    ["Multi-counter service", "Register allocation", "Order queue at the counter"]),
                new(
                    "book-orders",
                    "Book Orders",
                    "Create advance bookings, reserve items, and fulfill later with pickup or payment follow-up.",
                    "event_note",
                    "/pos",
                    "Workflow Expansion",
                    "Growth",
                    false,
                    true,
                    ["Advance reservations", "Pickup and due-date flow", "Deposit-aware order capture"]),
                new(
                    "hold-and-resume",
                    "On-Hold Booking",
                    "Put carts on hold and resume them later by customer, counter, or receipt reference.",
                    "pause_circle",
                    "/pos",
                    "Workflow Expansion",
                    "Growth",
                    false,
                    true,
                    ["Cart hold and resume", "Counter handover support", "Reference-based recovery"]),
                new(
                    "customer-profiles",
                    "Customer Profiles",
                    "Capture customer name, phone, email, pricing tier, loyalty state, and future order history relationships.",
                    "group",
                    "/customers",
                    "Live Screen",
                    "Starter",
                    true,
                    true,
                    ["Customer name, phone, and email", "Walk-in and saved profiles", "Pricing and loyalty context"]),
                new(
                    "split-payments",
                    "Split Payment",
                    "Take multiple tenders in the same checkout including cash, card, wallet, or credit note.",
                    "payments",
                    "/pos",
                    "Workflow Expansion",
                    "Growth",
                    false,
                    true,
                    ["Multiple tender types", "Balanced checkout settlement", "Payment mix reporting"]),
                new(
                    "late-payments",
                    "Late Orders & Installments",
                    "Track due balances, part-payments, and installment collection against booked or delivered orders.",
                    "schedule_send",
                    "/sales",
                    "Roadmap",
                    "Enterprise",
                    false,
                    true,
                    ["Due amount tracking", "Installment collection", "Outstanding customer follow-up"]),
                new(
                    "service-cards",
                    "Service Cards",
                    "Service job card workflow for repair, salon, workshop, and after-sales businesses.",
                    "assignment",
                    null,
                    "Roadmap",
                    "Enterprise",
                    false,
                    false,
                    ["Service intake sheet", "Technician assignment", "Status-based completion flow"]),
                new(
                    "returns-refunds",
                    "Refunds & Returns",
                    "Return, exchange, void, and refund flow with receipt history and approval trail.",
                    "published_with_changes",
                    "/sales",
                    "Workflow Expansion",
                    "Growth",
                    false,
                    true,
                    ["Refund approvals", "Exchange processing", "Return audit log"])
            ]),
        new(
            "inventory-warehouse",
            "Inventory & Warehouse",
            "Product control, receiving, replenishment, barcode tooling, and warehouse reporting.",
            [
                new(
                    "inventory-core",
                    "Inventory Admin",
                    "Add and update products, categories, warehouse stock, reorder levels, and inventory import.",
                    "inventory_2",
                    "/inventory",
                    "Live Screen",
                    "Starter",
                    true,
                    true,
                    ["Product and category control", "Excel and CSV import", "Warehouse stock balances"]),
                new(
                    "trade-in",
                    "Trade-In Workflow",
                    "Accept used items against a sale with inspection, valuation, and resale-ready stock intake.",
                    "swap_horiz",
                    null,
                    "Roadmap",
                    "Enterprise",
                    false,
                    false,
                    ["Trade-in valuation", "Reverse inventory intake", "Trade-in credit on sale"]),
                new(
                    "stock-take",
                    "Stock Take",
                    "Cycle counts and full stock audits with variance posting and supervisor review.",
                    "inventory",
                    "/inventory",
                    "Workflow Expansion",
                    "Growth",
                    false,
                    true,
                    ["Cycle count sessions", "Variance posting", "Audit trail by operator"]),
                new(
                    "grn-receiving",
                    "GRN & Receiving",
                    "Goods receipt note workflow for purchase deliveries, partial receiving, and stock updates.",
                    "local_shipping",
                    "/procurement",
                    "Live Foundation",
                    "Growth",
                    true,
                    true,
                    ["Purchase receiving flow", "Partial GRN", "Supplier delivery reconciliation"]),
                new(
                    "warehouse-reports",
                    "Warehouse Reports",
                    "Low stock, stock level, stock valuation, usage, and turnover reporting across branches.",
                    "warehouse",
                    "/inventory",
                    "Workflow Expansion",
                    "Growth",
                    false,
                    true,
                    ["Low stock report", "Valuation and turnover", "Usage and replenishment metrics"]),
                new(
                    "barcode-suite",
                    "Barcode Generator",
                    "Generate printable product labels and barcode references for store and warehouse use.",
                    "qr_code_2",
                    "/inventory",
                    "Workflow Expansion",
                    "Growth",
                    false,
                    true,
                    ["Barcode generation", "Shelf label printing", "SKU and scanner compatibility"])
            ]),
        new(
            "erp-finance",
            "ERP & Finance",
            "Suppliers, purchasing, expenses, ledgers, compliance, and commercial operations control.",
            [
                new(
                    "supplier-management",
                    "Supplier Management",
                    "Supplier profiles, contacts, payment terms, lifecycle status, and purchase visibility.",
                    "group_work",
                    "/procurement",
                    "Live Screen",
                    "Starter",
                    true,
                    true,
                    ["Supplier master records", "Contact and payment terms", "Supplier performance visibility"]),
                new(
                    "purchase-orders",
                    "Purchase Orders",
                    "Purchase order creation, status tracking, expected delivery, and receiving pipeline.",
                    "shopping_bag",
                    "/procurement",
                    "Live Screen",
                    "Growth",
                    true,
                    true,
                    ["PO lifecycle tracking", "Expected delivery dates", "Ordered vs received units"]),
                new(
                    "expense-management",
                    "Expense Management",
                    "Track petty cash, overheads, branch expenses, and reporting against operational periods.",
                    "receipt_long",
                    null,
                    "Roadmap",
                    "Growth",
                    false,
                    false,
                    ["Branch expense posting", "Expense categorization", "Period expense reporting"]),
                new(
                    "ledger-accounting",
                    "Ledger Management",
                    "Customer, supplier, and finance ledgers with balance drill-down and aging support.",
                    "account_balance",
                    null,
                    "Roadmap",
                    "Enterprise",
                    false,
                    false,
                    ["General and party ledgers", "Ledger dashboard", "Opening, debit, credit tracking"]),
                new(
                    "profit-loss",
                    "Profit & Loss",
                    "Commercial profitability reporting with revenue, cost, expenses, and margin summaries.",
                    "trending_up",
                    null,
                    "Roadmap",
                    "Enterprise",
                    false,
                    false,
                    ["P&L snapshot", "Gross vs net margin", "Period comparison"]),
                new(
                    "fbr-compliance",
                    "FBR & Fiscal Compliance",
                    "Queue, report, retry, and monitor fiscal invoice submission for Pakistan retail operations.",
                    "policy",
                    "/operations",
                    "Live Foundation",
                    "Growth",
                    true,
                    true,
                    ["Queued invoice monitoring", "Reported vs failed visibility", "Offline-safe submission queue"]),
                new(
                    "pos-configuration",
                    "POS Configuration",
                    "Branch-level POS settings for tax, counters, pricing, receipt format, and selling rules.",
                    "tune",
                    "/operations",
                    "Workflow Expansion",
                    "Growth",
                    false,
                    true,
                    ["POS policy setup", "Counter defaults", "Receipt and pricing behavior"])
            ]),
        new(
            "reports-analytics",
            "Reports & Analytics",
            "Decision-ready reporting for sales, payments, category performance, tax, and operational exceptions.",
            [
                new(
                    "order-listing",
                    "Orders & Receipts",
                    "Order history with invoice reprint, receipt lookup, and sales detail drill-down.",
                    "receipt",
                    "/sales",
                    "Live Screen",
                    "Starter",
                    true,
                    true,
                    ["Sales history listing", "Invoice and slip reprint", "FBR submission retry"]),
                new(
                    "booking-analytics",
                    "Booking Analytics",
                    "Measure pending bookings, fulfillment age, late orders, and booking conversion.",
                    "query_stats",
                    "/sales",
                    "Roadmap",
                    "Growth",
                    false,
                    true,
                    ["Booking aging", "Pending order funnel", "Conversion monitoring"]),
                new(
                    "reporting-suite",
                    "Reporting Suite",
                    "Daily, weekly, monthly, item-wise, category-wise, payment-type, and exception reporting.",
                    "bar_chart",
                    "/dashboard",
                    "Workflow Expansion",
                    "Growth",
                    true,
                    true,
                    ["Sales by item and category", "Payment type analysis", "Best and worst seller reporting"]),
                new(
                    "tax-and-refund-reporting",
                    "Tax, Void & Refund Reports",
                    "Tax summaries, void activity, return ratios, and audit-ready refund reporting.",
                    "fact_check",
                    "/sales",
                    "Roadmap",
                    "Enterprise",
                    false,
                    true,
                    ["Refund and tax report", "Void sales monitoring", "Exception audit pack"]),
                new(
                    "expiry-and-usage-reporting",
                    "Expiry & Usage Reports",
                    "Expiry exposure, stock usage analysis, and inventory turnover intelligence for replenishment.",
                    "assessment",
                    "/inventory",
                    "Roadmap",
                    "Enterprise",
                    false,
                    true,
                    ["Product expiry report", "Usage analysis", "Turnover insights"])
            ]),
        new(
            "growth-automation",
            "Growth & Automation",
            "Marketing automation, social publishing, and media distribution from the same operating hub.",
            [
                new(
                    "social-publishing",
                    "Social Posting Hub",
                    "Create or upload assets once and schedule publishing to Facebook, Instagram, and TikTok.",
                    "campaign",
                    null,
                    "Roadmap",
                    "Enterprise",
                    false,
                    false,
                    ["Cross-post scheduling", "Media asset library", "Channel publishing queue"]),
                new(
                    "customer-notifications",
                    "Invoice Delivery",
                    "Send invoice, receipt, and order updates by email or WhatsApp-ready outbound workflow.",
                    "send",
                    "/sales",
                    "Roadmap",
                    "Growth",
                    false,
                    true,
                    ["Email invoice delivery", "WhatsApp-ready sending flow", "Order notification history"])
            ]),
        new(
            "administration",
            "Administration",
            "People, permissions, configuration, and no-code extensibility for client-specific deployment.",
            [
                new(
                    "employee-management",
                    "Employee Management",
                    "Create, update, and organize branch staff accounts for cashier, manager, and owner operations.",
                    "badge",
                    "/users",
                    "Live Screen",
                    "Starter",
                    true,
                    true,
                    ["Add or update employees", "Branch assignment", "Role tagging"]),
                new(
                    "role-permissions",
                    "Role Permissions",
                    "Grant or restrict modules by role and align access with the customer's subscribed plan.",
                    "admin_panel_settings",
                    "/users",
                    "Workflow Expansion",
                    "Growth",
                    true,
                    true,
                    ["Role-based access model", "Module-based entitlement gating", "Controlled admin access"]),
                new(
                    "no-code-builder",
                    "No-Code Builder",
                    "Client-specific form and data field builder so deployments can be tailored without code edits.",
                    "extension",
                    "/form-builder",
                    "Live Foundation",
                    "Growth",
                    true,
                    true,
                    ["Custom product fields", "Tenant-specific data capture", "Foundation for future screen builders"]),
                new(
                    "plan-and-module-control",
                    "Plans & Module Control",
                    "Define custom packages, set pricing, included users and branches, and enable only the modules each client buys.",
                    "tune",
                    "/plans",
                    "Live Foundation",
                    "Starter",
                    true,
                    true,
                    ["Base plan pricing", "Per-module enable or disable", "Included user and branch limits"])
            ])
    ];

    internal static IReadOnlyList<string> DefaultEnabledModuleKeys { get; } = GroupBlueprints
        .SelectMany(group => group.Modules)
        .Where(module => module.EnabledByDefault)
        .Select(module => module.ModuleKey)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    internal static IReadOnlyList<string> KnownModuleKeys { get; } = GroupBlueprints
        .SelectMany(group => group.Modules)
        .Select(module => module.ModuleKey)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    internal static WorkspaceModuleAccessDto BuildAccess(SubscriptionPlanSettings settings, string? role = null)
    {
        var enabledModules = settings.ModuleEntitlements
            .Where(entitlement => entitlement.Enabled)
            .Select(entitlement => NormalizeModuleKey(entitlement.ModuleKey));

        var visibleModules = string.IsNullOrWhiteSpace(role)
            ? enabledModules
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(moduleKey => moduleKey)
                .ToArray()
            : WorkspaceRoleAccess.FilterEnabledModules(role, enabledModules);

        return new WorkspaceModuleAccessDto(
            settings.PlanCode,
            settings.PlanName,
            settings.Currency,
            settings.BaseMonthlyPrice,
            settings.IncludedUsers,
            settings.IncludedBranches,
            settings.AllowCustomModuleOverrides,
            visibleModules);
    }

    internal static ModuleSettingsDto BuildSettings(SubscriptionPlanSettings settings)
    {
        var entitlements = ToEntitlementMap(settings);
        var access = BuildAccess(settings);
        var groups = GroupBlueprints
            .Select(group => new ModuleSettingsGroupDto(
                group.Key,
                group.Title,
                group.Description,
                group.Modules
                    .Select(module => BuildModuleDto(group, module, entitlements))
                    .ToArray()))
            .ToArray();

        var estimatedMonthlyTotal = settings.BaseMonthlyPrice
            + entitlements.Values.Where(entitlement => entitlement.Enabled).Sum(entitlement => entitlement.AddOnMonthlyPrice);

        return new ModuleSettingsDto(access, estimatedMonthlyTotal, groups);
    }

    internal static PosModuleGroupDto[] BuildOperationsModuleGroups(SubscriptionPlanSettings settings)
    {
        var entitlements = ToEntitlementMap(settings);

        return GroupBlueprints
            .Take(4)
            .Select(group => new PosModuleGroupDto(
                group.Title,
                group.Description,
                group.Modules
                    .Select(module =>
                    {
                        var entitlement = ResolveEntitlement(module.ModuleKey, entitlements);
                        var status = entitlement.Enabled
                            ? module.DeliveryStatus
                            : "Disabled for this client";
                        return new PosModuleCardDto(
                            module.Title,
                            module.Description,
                            module.Route ?? "/plans",
                            module.Icon,
                            status);
                    })
                    .ToArray()))
            .ToArray();
    }

    internal static ModuleEntitlement[] MergeEntitlements(
        SubscriptionPlanSettings? existingSettings,
        IReadOnlyList<SaveModuleEntitlementRequestDto> requestedModules)
    {
        var requestedMap = requestedModules
            .Where(module => !string.IsNullOrWhiteSpace(module.ModuleKey))
            .GroupBy(module => NormalizeModuleKey(module.ModuleKey))
            .ToDictionary(
                group => group.Key,
                group => group.Last(),
                StringComparer.OrdinalIgnoreCase);
        var existingMap = existingSettings?.ModuleEntitlements?
            .Where(module => !string.IsNullOrWhiteSpace(module.ModuleKey))
            .GroupBy(module => NormalizeModuleKey(module.ModuleKey))
            .ToDictionary(
                group => group.Key,
                group => group.Last(),
                StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, ModuleEntitlement>(StringComparer.OrdinalIgnoreCase);

        return GroupBlueprints
            .SelectMany(group => group.Modules)
            .Select(module =>
            {
                if (requestedMap.TryGetValue(module.ModuleKey, out var requested))
                {
                    return new ModuleEntitlement(
                        module.ModuleKey,
                        requested.Enabled,
                        NormalizePrice(requested.AddOnMonthlyPrice));
                }

                if (existingMap.TryGetValue(module.ModuleKey, out var existing))
                {
                    return new ModuleEntitlement(
                        module.ModuleKey,
                        existing.Enabled,
                        NormalizePrice(existing.AddOnMonthlyPrice));
                }

                return new ModuleEntitlement(
                    module.ModuleKey,
                    module.EnabledByDefault,
                    GetDefaultMonthlyPrice(module.ModuleKey));
            })
            .ToArray();
    }

    internal static bool IsKnownModule(string moduleKey)
    {
        return KnownModuleKeys.Contains(NormalizeModuleKey(moduleKey), StringComparer.OrdinalIgnoreCase);
    }

    internal static string NormalizeModuleKey(string moduleKey)
    {
        return string.IsNullOrWhiteSpace(moduleKey)
            ? string.Empty
            : moduleKey.Trim().ToLowerInvariant();
    }

    private static WorkspaceModuleDto BuildModuleDto(
        ModuleGroupBlueprint group,
        ModuleBlueprint module,
        IReadOnlyDictionary<string, ModuleEntitlement> entitlements)
    {
        var entitlement = ResolveEntitlement(module.ModuleKey, entitlements);

        return new WorkspaceModuleDto(
            module.ModuleKey,
            module.Title,
            module.Description,
            group.Title,
            module.Icon,
            module.Route,
            module.DeliveryStatus,
            module.RecommendedPlan,
            entitlement.Enabled,
            module.HasScreen,
            entitlement.AddOnMonthlyPrice,
            module.Capabilities);
    }

    private static ModuleEntitlement ResolveEntitlement(
        string moduleKey,
        IReadOnlyDictionary<string, ModuleEntitlement> entitlements)
    {
        return entitlements.TryGetValue(moduleKey, out var entitlement)
            ? entitlement
            : new ModuleEntitlement(
                moduleKey,
                DefaultEnabledModuleKeys.Contains(moduleKey, StringComparer.OrdinalIgnoreCase),
                GetDefaultMonthlyPrice(moduleKey));
    }

    private static Dictionary<string, ModuleEntitlement> ToEntitlementMap(SubscriptionPlanSettings settings)
    {
        return settings.ModuleEntitlements
            .Where(entitlement => !string.IsNullOrWhiteSpace(entitlement.ModuleKey))
            .GroupBy(entitlement => NormalizeModuleKey(entitlement.ModuleKey))
            .ToDictionary(
                group => group.Key,
                group => new ModuleEntitlement(
                    group.Key,
                    group.Last().Enabled,
                    NormalizePrice(group.Last().AddOnMonthlyPrice)),
                StringComparer.OrdinalIgnoreCase);
    }

    private static decimal NormalizePrice(decimal price)
    {
        return price < 0 ? 0 : decimal.Round(price, 2, MidpointRounding.AwayFromZero);
    }

    private static decimal GetDefaultMonthlyPrice(string moduleKey)
    {
        return MarketMonthlyPriceMap.TryGetValue(moduleKey, out var value)
            ? value
            : 0m;
    }

    private sealed record ModuleGroupBlueprint(
        string Key,
        string Title,
        string Description,
        IReadOnlyList<ModuleBlueprint> Modules);

    private sealed record ModuleBlueprint(
        string ModuleKey,
        string Title,
        string Description,
        string Icon,
        string? Route,
        string DeliveryStatus,
        string RecommendedPlan,
        bool EnabledByDefault,
        bool HasScreen,
        IReadOnlyList<string> Capabilities);
}
