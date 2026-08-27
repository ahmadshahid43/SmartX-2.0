using OmniBusiness.Application.Contracts;

namespace OmniBusiness.Application.Services;

public interface IInventoryManagementService
{
    Task<InventoryOverviewDto> CreateProductAsync(
        Guid tenantId,
        SaveProductRequestDto request,
        CancellationToken cancellationToken);

    Task<InventoryOverviewDto> UpdateProductAsync(
        Guid tenantId,
        Guid productId,
        SaveProductRequestDto request,
        CancellationToken cancellationToken);

    Task<InventoryOverviewDto> AdjustStockAsync(
        Guid tenantId,
        Guid userId,
        StockAdjustmentRequestDto request,
        CancellationToken cancellationToken);

    Task<InventoryImportResultDto> ImportInventoryAsync(
        Guid tenantId,
        InventoryImportFileDto request,
        CancellationToken cancellationToken);
}
