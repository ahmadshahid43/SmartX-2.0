using OmniBusiness.Application.Abstractions.Persistence;
using OmniBusiness.Application.Contracts;
using OmniBusiness.Domain.Foundation;

namespace OmniBusiness.Application.Services;

public sealed class ModuleManagementService(
    IWorkspaceRepository workspaceRepository,
    IWorkspaceQueryService workspaceQueryService) : IModuleManagementService
{
    public async Task<ModuleSettingsDto> UpdateModuleSettingsAsync(
        Guid tenantId,
        SaveModuleSettingsRequestDto request,
        CancellationToken cancellationToken)
    {
        await workspaceRepository.UpdateWorkspaceSnapshotAsync(snapshot =>
        {
            EnsureTenant(snapshot, tenantId);

            var planName = NormalizePlanName(request.PlanName, snapshot.Tenant.SubscriptionPlan);
            var planCode = NormalizePlanCode(request.PlanCode, planName);
            var currency = NormalizeCurrency(request.Currency, snapshot.Company.BaseCurrency);
            var subscriptionSettings = new SubscriptionPlanSettings(
                planCode,
                planName,
                currency,
                NormalizePrice(request.BaseMonthlyPrice),
                NormalizeCount(request.IncludedUsers),
                NormalizeCount(request.IncludedBranches),
                request.AllowCustomModuleOverrides,
                WorkspaceModuleCatalog.MergeEntitlements(snapshot.SubscriptionSettings, request.Modules ?? Array.Empty<SaveModuleEntitlementRequestDto>()));

            return snapshot with
            {
                Tenant = snapshot.Tenant with { SubscriptionPlan = planName },
                SubscriptionSettings = subscriptionSettings
            };
        }, cancellationToken);

        return await workspaceQueryService.GetModuleSettingsAsync(tenantId, cancellationToken);
    }

    private static void EnsureTenant(WorkspaceSnapshot snapshot, Guid tenantId)
    {
        if (snapshot.Tenant.Id != tenantId)
        {
            throw new InvalidOperationException("The current user does not belong to the requested tenant.");
        }
    }

    private static string NormalizePlanName(string? requestedPlanName, string fallbackPlanName)
    {
        var value = string.IsNullOrWhiteSpace(requestedPlanName)
            ? fallbackPlanName
            : requestedPlanName.Trim();

        return string.IsNullOrWhiteSpace(value) ? "Starter" : value;
    }

    private static string NormalizePlanCode(string? requestedPlanCode, string planName)
    {
        var source = string.IsNullOrWhiteSpace(requestedPlanCode)
            ? planName
            : requestedPlanCode.Trim();
        var sanitized = new string(source
            .Trim()
            .ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : '-')
            .ToArray())
            .Trim('-');

        return string.IsNullOrWhiteSpace(sanitized) ? "starter" : sanitized;
    }

    private static string NormalizeCurrency(string? requestedCurrency, string fallbackCurrency)
    {
        var value = string.IsNullOrWhiteSpace(requestedCurrency)
            ? fallbackCurrency
            : requestedCurrency.Trim().ToUpperInvariant();

        return string.IsNullOrWhiteSpace(value) ? "PKR" : value;
    }

    private static int NormalizeCount(int value)
    {
        return value <= 0 ? 1 : value;
    }

    private static decimal NormalizePrice(decimal value)
    {
        return value < 0 ? 0 : decimal.Round(value, 2, MidpointRounding.AwayFromZero);
    }
}
