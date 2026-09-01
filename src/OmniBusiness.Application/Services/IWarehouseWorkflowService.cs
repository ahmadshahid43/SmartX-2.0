using OmniBusiness.Application.Contracts;

namespace OmniBusiness.Application.Services;

public interface IWarehouseWorkflowService
{
    Task<WarehouseHubDto> CreateStockTransferAsync(
        Guid tenantId,
        Guid userId,
        SaveStockTransferRequestDto request,
        CancellationToken cancellationToken);

    Task<WarehouseHubDto> CreateGoodsReceiptAsync(
        Guid tenantId,
        Guid userId,
        SaveGoodsReceiptRequestDto request,
        CancellationToken cancellationToken);

    Task<WarehouseHubDto> CreateGatePassAsync(
        Guid tenantId,
        Guid userId,
        SaveGatePassRequestDto request,
        CancellationToken cancellationToken);
}
