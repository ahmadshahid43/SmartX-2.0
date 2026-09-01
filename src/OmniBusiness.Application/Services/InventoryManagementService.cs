using OmniBusiness.Application.Abstractions.Persistence;
using OmniBusiness.Application.Contracts;
using OmniBusiness.Domain.Foundation;

namespace OmniBusiness.Application.Services;

public sealed class InventoryManagementService(
    IWorkspaceRepository workspaceRepository,
    IWorkspaceQueryService workspaceQueryService) : IInventoryManagementService
{
    public async Task<InventoryOverviewDto> CreateProductAsync(
        Guid tenantId,
        SaveProductRequestDto request,
        CancellationToken cancellationToken)
    {
        ValidateRequest(request);

        await workspaceRepository.UpdateWorkspaceSnapshotAsync(snapshot =>
        {
            EnsureTenant(snapshot, tenantId);

            if (snapshot.Products.Any(product =>
                    !product.IsArchived &&
                    string.Equals(product.Sku, request.Sku, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException($"SKU '{request.Sku}' already exists.");
            }

            var product = ProductStateCalculator.ApplyInventory(
                new Product(
                    Guid.NewGuid(),
                    tenantId,
                    request.Sku.Trim(),
                    request.Name.Trim(),
                    request.Category.Trim(),
                    request.UnitPrice,
                    request.InHand,
                    request.Reserved,
                    request.Warehouse.Trim(),
                    "In Stock",
                    request.IsFavorite,
                    request.IsQuickSale,
                    false,
                    NormalizeVisualCode(request.VisualCode, request.Name),
                    Math.Max(request.ReorderLevel, 0),
                    false),
                request.InHand,
                request.Reserved);

            return snapshot with
            {
                Products = snapshot.Products
                    .Append(product)
                    .OrderBy(item => item.Name)
                    .ToArray()
            };
        }, cancellationToken);

        return await workspaceQueryService.GetInventoryOverviewAsync(tenantId, cancellationToken);
    }

    public async Task<InventoryOverviewDto> UpdateProductAsync(
        Guid tenantId,
        Guid productId,
        SaveProductRequestDto request,
        CancellationToken cancellationToken)
    {
        ValidateRequest(request);

        await workspaceRepository.UpdateWorkspaceSnapshotAsync(snapshot =>
        {
            EnsureTenant(snapshot, tenantId);

            if (snapshot.Products.Any(product =>
                    product.Id != productId &&
                    !product.IsArchived &&
                    string.Equals(product.Sku, request.Sku, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException($"SKU '{request.Sku}' already exists.");
            }

            var existing = snapshot.Products.FirstOrDefault(product => product.Id == productId && !product.IsArchived)
                ?? throw new InvalidOperationException("The selected product was not found.");

            var updated = ProductStateCalculator.ApplyInventory(
                existing with
                {
                    Sku = request.Sku.Trim(),
                    Name = request.Name.Trim(),
                    Category = request.Category.Trim(),
                    UnitPrice = request.UnitPrice,
                    Warehouse = request.Warehouse.Trim(),
                    IsFavorite = request.IsFavorite,
                    IsQuickSale = request.IsQuickSale,
                    VisualCode = NormalizeVisualCode(request.VisualCode, request.Name),
                    ReorderLevel = Math.Max(request.ReorderLevel, 0)
                },
                request.InHand,
                request.Reserved);

            return snapshot with
            {
                Products = snapshot.Products
                    .Select(product => product.Id == productId ? updated : product)
                    .OrderBy(item => item.Name)
                    .ToArray()
            };
        }, cancellationToken);

        return await workspaceQueryService.GetInventoryOverviewAsync(tenantId, cancellationToken);
    }

    public async Task<InventoryOverviewDto> AdjustStockAsync(
        Guid tenantId,
        Guid userId,
        StockAdjustmentRequestDto request,
        CancellationToken cancellationToken)
    {
        if (request.QuantityDelta == 0)
        {
            throw new InvalidOperationException("Quantity delta cannot be zero.");
        }

        await workspaceRepository.UpdateWorkspaceSnapshotAsync(snapshot =>
        {
            EnsureTenant(snapshot, tenantId);

            var currentUser = (snapshot.Users ?? Array.Empty<AppUser>()).FirstOrDefault(user => user.Id == userId)
                ?? (snapshot.AdminUser.Id == userId ? snapshot.AdminUser : null)
                ?? throw new InvalidOperationException("The current user could not be resolved.");

            if (!WorkspaceRoleAccess.CanManageInventory(currentUser.Role))
            {
                throw new InvalidOperationException("The current user is not allowed to adjust stock.");
            }

            var existing = snapshot.Products.FirstOrDefault(product => product.Id == request.ProductId && !product.IsArchived)
                ?? throw new InvalidOperationException("The selected product was not found.");

            var nextInHand = existing.InHand + request.QuantityDelta;
            if (nextInHand < 0)
            {
                throw new InvalidOperationException(
                    $"The adjustment would make {existing.Name} stock negative.");
            }

            var updated = ProductStateCalculator.ApplyInventory(existing, nextInHand, existing.Reserved);
            var adjustment = new StockAdjustmentRecord(
                Guid.NewGuid(),
                tenantId,
                existing.Id,
                existing.Name,
                request.QuantityDelta,
                string.IsNullOrWhiteSpace(request.Reason) ? "Manual adjustment" : request.Reason.Trim(),
                currentUser.DisplayName,
                DateTimeOffset.Now);

            return snapshot with
            {
                Products = snapshot.Products
                    .Select(product => product.Id == request.ProductId ? updated : product)
                    .OrderBy(item => item.Name)
                    .ToArray(),
                StockAdjustments = (snapshot.StockAdjustments ?? Array.Empty<StockAdjustmentRecord>())
                    .Prepend(adjustment)
                    .Take(100)
                    .ToArray()
            };
        }, cancellationToken);

        return await workspaceQueryService.GetInventoryOverviewAsync(tenantId, cancellationToken);
    }

    public async Task<InventoryOverviewDto> CreateStockTakeAsync(
        Guid tenantId,
        Guid userId,
        SaveStockTakeRequestDto request,
        CancellationToken cancellationToken)
    {
        if (request.CountedQuantity < 0)
        {
            throw new InvalidOperationException("Counted quantity cannot be negative.");
        }

        await workspaceRepository.UpdateWorkspaceSnapshotAsync(snapshot =>
        {
            EnsureTenant(snapshot, tenantId);

            var currentUser = ResolveInventoryUser(snapshot, userId);
            var existing = snapshot.Products.FirstOrDefault(product => product.Id == request.ProductId && !product.IsArchived)
                ?? throw new InvalidOperationException("The selected product was not found.");

            var normalizedReserved = Math.Min(existing.Reserved, request.CountedQuantity);
            var variance = request.CountedQuantity - existing.InHand;
            var updated = ProductStateCalculator.ApplyInventory(existing, request.CountedQuantity, normalizedReserved);
            var countedAt = DateTimeOffset.Now;
            var notes = string.IsNullOrWhiteSpace(request.Notes)
                ? "Cycle count posted from inventory command center."
                : request.Notes.Trim();
            var stockTake = new StockTakeSession(
                Guid.NewGuid(),
                tenantId,
                existing.Id,
                existing.Sku,
                existing.Name,
                existing.Warehouse,
                existing.InHand,
                request.CountedQuantity,
                variance,
                ResolveStockTakeStatus(variance),
                currentUser.DisplayName,
                notes,
                countedAt);

            var adjustments = snapshot.StockAdjustments ?? Array.Empty<StockAdjustmentRecord>();
            if (variance != 0)
            {
                adjustments = adjustments
                    .Prepend(new StockAdjustmentRecord(
                        Guid.NewGuid(),
                        tenantId,
                        existing.Id,
                        existing.Name,
                        variance,
                        $"Stock take count: {notes}",
                        currentUser.DisplayName,
                        countedAt))
                    .Take(150)
                    .ToArray();
            }

            return snapshot with
            {
                Products = snapshot.Products
                    .Select(product => product.Id == request.ProductId ? updated : product)
                    .OrderBy(item => item.Name)
                    .ToArray(),
                StockAdjustments = adjustments,
                StockTakes = (snapshot.StockTakes ?? Array.Empty<StockTakeSession>())
                    .Prepend(stockTake)
                    .Take(100)
                    .ToArray()
            };
        }, cancellationToken);

        return await workspaceQueryService.GetInventoryOverviewAsync(tenantId, cancellationToken);
    }

    public async Task<InventoryImportResultDto> ImportInventoryAsync(
        Guid tenantId,
        InventoryImportFileDto request,
        CancellationToken cancellationToken)
    {
        var parsedFile = InventoryImportSpreadsheetReader.Parse(request);
        var createdCount = 0;
        var updatedCount = 0;

        await workspaceRepository.UpdateWorkspaceSnapshotAsync(snapshot =>
        {
            EnsureTenant(snapshot, tenantId);

            var primaryWarehouse = snapshot.Branches.FirstOrDefault(branch => branch.IsPrimary)?.WarehouseName
                ?? snapshot.Branches.FirstOrDefault()?.WarehouseName
                ?? "Main Warehouse";
            var products = snapshot.Products.ToList();

            foreach (var row in parsedFile.Rows)
            {
                var existingIndex = products.FindIndex(product =>
                    !product.IsArchived &&
                    string.Equals(product.Sku, row.Sku, StringComparison.OrdinalIgnoreCase));
                var category = string.IsNullOrWhiteSpace(row.Category) ? "General" : row.Category.Trim();
                var warehouse = string.IsNullOrWhiteSpace(row.Warehouse) ? primaryWarehouse : row.Warehouse.Trim();
                var inHand = Math.Max(row.InHand ?? 0, 0);
                var reserved = Math.Max(row.Reserved ?? 0, 0);
                var reorderLevel = Math.Max(row.ReorderLevel ?? 5, 0);
                var unitPrice = Math.Max(row.UnitPrice ?? 0m, 0m);

                if (existingIndex >= 0)
                {
                    var existing = products[existingIndex];
                    var updated = ProductStateCalculator.ApplyInventory(
                        existing with
                        {
                            Sku = row.Sku.Trim(),
                            Name = row.Name.Trim(),
                            Category = category,
                            UnitPrice = unitPrice,
                            Warehouse = warehouse,
                            IsFavorite = row.IsFavorite ?? existing.IsFavorite,
                            IsQuickSale = row.IsQuickSale ?? existing.IsQuickSale,
                            VisualCode = NormalizeVisualCode(row.VisualCode ?? existing.VisualCode, row.Name),
                            ReorderLevel = reorderLevel
                        },
                        inHand,
                        reserved);

                    products[existingIndex] = updated;
                    updatedCount++;
                    continue;
                }

                var created = ProductStateCalculator.ApplyInventory(
                    new Product(
                        Guid.NewGuid(),
                        tenantId,
                        row.Sku.Trim(),
                        row.Name.Trim(),
                        category,
                        unitPrice,
                        inHand,
                        reserved,
                        warehouse,
                        "In Stock",
                        row.IsFavorite ?? false,
                        row.IsQuickSale ?? true,
                        false,
                        NormalizeVisualCode(row.VisualCode ?? string.Empty, row.Name),
                        reorderLevel,
                        false),
                    inHand,
                    reserved);

                products.Add(created);
                createdCount++;
            }

            return snapshot with
            {
                Products = products
                    .OrderBy(product => product.Name)
                    .ToArray()
            };
        }, cancellationToken);

        var inventory = await workspaceQueryService.GetInventoryOverviewAsync(tenantId, cancellationToken);
        return new InventoryImportResultDto(
            inventory,
            parsedFile.Rows.Count,
            createdCount,
            updatedCount,
            parsedFile.Warnings);
    }

    private static void EnsureTenant(WorkspaceSnapshot snapshot, Guid tenantId)
    {
        if (snapshot.Tenant.Id != tenantId)
        {
            throw new InvalidOperationException("The current user does not belong to the requested tenant.");
        }
    }

    private static AppUser ResolveInventoryUser(WorkspaceSnapshot snapshot, Guid userId)
    {
        var currentUser = (snapshot.Users ?? Array.Empty<AppUser>()).FirstOrDefault(user => user.Id == userId)
            ?? (snapshot.AdminUser.Id == userId ? snapshot.AdminUser : null)
            ?? throw new InvalidOperationException("The current user could not be resolved.");

        if (!WorkspaceRoleAccess.CanManageInventory(currentUser.Role))
        {
            throw new InvalidOperationException("The current user is not allowed to manage inventory.");
        }

        return currentUser;
    }

    private static void ValidateRequest(SaveProductRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Sku) ||
            string.IsNullOrWhiteSpace(request.Name) ||
            string.IsNullOrWhiteSpace(request.Category) ||
            string.IsNullOrWhiteSpace(request.Warehouse))
        {
            throw new InvalidOperationException("SKU, name, category, and warehouse are required.");
        }

        if (request.UnitPrice < 0)
        {
            throw new InvalidOperationException("Unit price cannot be negative.");
        }

        if (request.InHand < 0 || request.Reserved < 0)
        {
            throw new InvalidOperationException("Inventory values cannot be negative.");
        }
    }

    private static string NormalizeVisualCode(string visualCode, string productName)
    {
        var candidate = string.IsNullOrWhiteSpace(visualCode)
            ? productName
            : visualCode;

        return new string(candidate
                .Where(char.IsLetterOrDigit)
                .Take(6)
                .ToArray())
            .ToUpperInvariant();
    }

    private static string ResolveStockTakeStatus(int variance)
    {
        if (variance == 0)
        {
            return "Matched";
        }

        return variance > 0 ? "Gain Posted" : "Loss Posted";
    }
}
