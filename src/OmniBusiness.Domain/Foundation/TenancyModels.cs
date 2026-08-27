namespace OmniBusiness.Domain.Foundation;

public sealed record Tenant(
    Guid Id,
    string Slug,
    string Name,
    string IndustryTemplate,
    string SubscriptionPlan);

public sealed record Company(
    Guid Id,
    Guid TenantId,
    string Name,
    string BaseCurrency,
    string TimeZone,
    string Country);

public sealed record Branch(
    Guid Id,
    Guid TenantId,
    string Code,
    string Name,
    string WarehouseName,
    bool IsPrimary);

public sealed record AppUser(
    Guid Id,
    Guid TenantId,
    Guid BranchId,
    string Email,
    string DisplayName,
    string Role,
    string PasswordHash);

public sealed record ModuleEntitlement(
    string ModuleKey,
    bool Enabled,
    decimal AddOnMonthlyPrice);

public sealed record SubscriptionPlanSettings(
    string PlanCode,
    string PlanName,
    string Currency,
    decimal BaseMonthlyPrice,
    int IncludedUsers,
    int IncludedBranches,
    bool AllowCustomModuleOverrides,
    IReadOnlyList<ModuleEntitlement> ModuleEntitlements);
