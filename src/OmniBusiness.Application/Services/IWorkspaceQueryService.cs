using OmniBusiness.Application.Contracts;

namespace OmniBusiness.Application.Services;

public interface IWorkspaceQueryService
{
    Task<WorkspaceContextDto> GetWorkspaceContextAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken);

    Task<WorkspaceUsersDto> GetUsersAsync(Guid tenantId, CancellationToken cancellationToken);

    Task<ModuleSettingsDto> GetModuleSettingsAsync(Guid tenantId, CancellationToken cancellationToken);

    Task<CustomerHubDto> GetCustomerHubAsync(Guid tenantId, CancellationToken cancellationToken);

    Task<DashboardOverviewDto> GetDashboardAsync(Guid tenantId, CancellationToken cancellationToken);

    Task<InventoryOverviewDto> GetInventoryOverviewAsync(Guid tenantId, CancellationToken cancellationToken);

    Task<PosTerminalDto> GetPosTerminalAsync(Guid tenantId, CancellationToken cancellationToken);

    Task<SalesHistoryDto> GetSalesHistoryAsync(Guid tenantId, CancellationToken cancellationToken);

    Task<ProcurementHubDto> GetProcurementHubAsync(Guid tenantId, CancellationToken cancellationToken);

    Task<OperationsHubDto> GetOperationsHubAsync(Guid tenantId, CancellationToken cancellationToken);

    Task<FormBuilderDto> GetProductCustomFieldsAsync(Guid tenantId, CancellationToken cancellationToken);
}
