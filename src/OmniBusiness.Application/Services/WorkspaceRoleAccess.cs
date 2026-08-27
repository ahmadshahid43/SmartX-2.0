namespace OmniBusiness.Application.Services;

internal static class WorkspaceRoleAccess
{
    private const string OwnerRole = "owner";
    private const string ManagerRole = "manager";
    private const string CashierRole = "cashier";
    private const string BackOfficeRole = "back office";

    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> ModuleRoleMap =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["dashboard-analytics"] = CreateRoleSet(OwnerRole, ManagerRole, BackOfficeRole),
            ["pos-checkout"] = CreateRoleSet(OwnerRole, ManagerRole, CashierRole),
            ["counter-orders"] = CreateRoleSet(OwnerRole, ManagerRole, CashierRole),
            ["book-orders"] = CreateRoleSet(OwnerRole, ManagerRole, CashierRole),
            ["hold-and-resume"] = CreateRoleSet(OwnerRole, ManagerRole, CashierRole),
            ["customer-profiles"] = CreateRoleSet(OwnerRole, ManagerRole, CashierRole, BackOfficeRole),
            ["split-payments"] = CreateRoleSet(OwnerRole, ManagerRole, CashierRole),
            ["late-payments"] = CreateRoleSet(OwnerRole, ManagerRole),
            ["service-cards"] = CreateRoleSet(OwnerRole, ManagerRole),
            ["returns-refunds"] = CreateRoleSet(OwnerRole, ManagerRole),
            ["inventory-core"] = CreateRoleSet(OwnerRole, ManagerRole, BackOfficeRole),
            ["trade-in"] = CreateRoleSet(OwnerRole, ManagerRole),
            ["stock-take"] = CreateRoleSet(OwnerRole, ManagerRole, BackOfficeRole),
            ["grn-receiving"] = CreateRoleSet(OwnerRole, ManagerRole, BackOfficeRole),
            ["warehouse-reports"] = CreateRoleSet(OwnerRole, ManagerRole, BackOfficeRole),
            ["barcode-suite"] = CreateRoleSet(OwnerRole, ManagerRole, BackOfficeRole),
            ["supplier-management"] = CreateRoleSet(OwnerRole, ManagerRole, BackOfficeRole),
            ["purchase-orders"] = CreateRoleSet(OwnerRole, ManagerRole, BackOfficeRole),
            ["expense-management"] = CreateRoleSet(OwnerRole, ManagerRole, BackOfficeRole),
            ["ledger-accounting"] = CreateRoleSet(OwnerRole, ManagerRole, BackOfficeRole),
            ["profit-loss"] = CreateRoleSet(OwnerRole, ManagerRole),
            ["fbr-compliance"] = CreateRoleSet(OwnerRole, ManagerRole, BackOfficeRole),
            ["pos-configuration"] = CreateRoleSet(OwnerRole, ManagerRole),
            ["order-listing"] = CreateRoleSet(OwnerRole, ManagerRole, CashierRole, BackOfficeRole),
            ["booking-analytics"] = CreateRoleSet(OwnerRole, ManagerRole),
            ["reporting-suite"] = CreateRoleSet(OwnerRole, ManagerRole, BackOfficeRole),
            ["tax-and-refund-reporting"] = CreateRoleSet(OwnerRole, ManagerRole, BackOfficeRole),
            ["expiry-and-usage-reporting"] = CreateRoleSet(OwnerRole, ManagerRole, BackOfficeRole),
            ["social-publishing"] = CreateRoleSet(OwnerRole, ManagerRole),
            ["customer-notifications"] = CreateRoleSet(OwnerRole, ManagerRole, BackOfficeRole),
            ["employee-management"] = CreateRoleSet(OwnerRole),
            ["role-permissions"] = CreateRoleSet(OwnerRole),
            ["no-code-builder"] = CreateRoleSet(OwnerRole),
            ["plan-and-module-control"] = CreateRoleSet(OwnerRole)
        };

    internal static string NormalizeRole(string? role)
    {
        return string.IsNullOrWhiteSpace(role)
            ? CashierRole
            : role.Trim().ToLowerInvariant();
    }

    internal static bool CanAccessModule(string? role, string moduleKey)
    {
        var normalizedRole = NormalizeRole(role);
        if (string.Equals(normalizedRole, OwnerRole, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var normalizedModuleKey = WorkspaceModuleCatalog.NormalizeModuleKey(moduleKey);
        return ModuleRoleMap.TryGetValue(normalizedModuleKey, out var allowedRoles)
               && allowedRoles.Contains(normalizedRole);
    }

    internal static bool CanManageInventory(string? role)
    {
        var normalizedRole = NormalizeRole(role);
        return string.Equals(normalizedRole, OwnerRole, StringComparison.OrdinalIgnoreCase)
               || string.Equals(normalizedRole, ManagerRole, StringComparison.OrdinalIgnoreCase)
               || string.Equals(normalizedRole, BackOfficeRole, StringComparison.OrdinalIgnoreCase);
    }

    internal static string[] FilterEnabledModules(string? role, IEnumerable<string> moduleKeys)
    {
        return moduleKeys
            .Where(moduleKey => CanAccessModule(role, moduleKey))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(moduleKey => moduleKey, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlySet<string> CreateRoleSet(params string[] roles)
    {
        return new HashSet<string>(roles, StringComparer.OrdinalIgnoreCase);
    }
}
