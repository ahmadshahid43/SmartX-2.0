using OmniBusiness.Application.Abstractions.Persistence;
using OmniBusiness.Application.Contracts;
using OmniBusiness.Domain.Foundation;

namespace OmniBusiness.Application.Services;

public sealed class WarehouseWorkflowService(
    IWorkspaceRepository workspaceRepository,
    IWorkspaceQueryService workspaceQueryService) : IWarehouseWorkflowService
{
    public async Task<WarehouseHubDto> CreateStockTransferAsync(
        Guid tenantId,
        Guid userId,
        SaveStockTransferRequestDto request,
        CancellationToken cancellationToken)
    {
        ValidateStockTransferRequest(request);

        await workspaceRepository.UpdateWorkspaceSnapshotAsync(snapshot =>
        {
            EnsureTenant(snapshot, tenantId);
            var currentUser = ResolveInventoryUser(snapshot, userId);
            var transfer = new StockTransfer(
                Guid.NewGuid(),
                tenantId,
                BuildTransferNumber(snapshot.StockTransfers),
                request.FromBranchName.Trim(),
                request.ToBranchName.Trim(),
                NormalizeStatus(request.Status, "Pending Dispatch"),
                DateTimeOffset.Now,
                request.ExpectedAt,
                request.Units,
                currentUser.DisplayName,
                NormalizeNotes(request.Notes));

            return snapshot with
            {
                StockTransfers = (snapshot.StockTransfers ?? Array.Empty<StockTransfer>())
                    .Prepend(transfer)
                    .OrderByDescending(item => item.CreatedAt)
                    .ToArray()
            };
        }, cancellationToken);

        return await workspaceQueryService.GetWarehouseHubAsync(tenantId, cancellationToken);
    }

    public async Task<WarehouseHubDto> CreateGoodsReceiptAsync(
        Guid tenantId,
        Guid userId,
        SaveGoodsReceiptRequestDto request,
        CancellationToken cancellationToken)
    {
        ValidateGoodsReceiptRequest(request);

        await workspaceRepository.UpdateWorkspaceSnapshotAsync(snapshot =>
        {
            EnsureTenant(snapshot, tenantId);
            var currentUser = ResolveInventoryUser(snapshot, userId);
            var receipt = new GoodsReceipt(
                Guid.NewGuid(),
                tenantId,
                BuildReceiptNumber(snapshot.GoodsReceipts),
                request.PurchaseOrderNo.Trim(),
                request.VendorName.Trim(),
                request.WarehouseName.Trim(),
                NormalizeStatus(request.Status, request.VarianceUnits > 0 ? "Partial Received" : "Received"),
                DateTimeOffset.Now,
                currentUser.DisplayName,
                request.LineCount,
                request.ReceivedUnits,
                request.VarianceUnits,
                NormalizeNotes(request.Notes));

            var purchaseOrders = (snapshot.PurchaseOrders ?? Array.Empty<PurchaseOrder>())
                .Select(order =>
                {
                    if (!string.Equals(order.PurchaseOrderNo, request.PurchaseOrderNo.Trim(), StringComparison.OrdinalIgnoreCase))
                    {
                        return order;
                    }

                    var nextReceivedUnits = Math.Min(order.OrderedUnits, order.ReceivedUnits + request.ReceivedUnits);
                    return order with
                    {
                        ReceivedUnits = nextReceivedUnits,
                        Status = nextReceivedUnits >= order.OrderedUnits ? "Received" : "Partial Received"
                    };
                })
                .OrderByDescending(order => order.CreatedAt)
                .ToArray();

            return snapshot with
            {
                PurchaseOrders = purchaseOrders,
                GoodsReceipts = (snapshot.GoodsReceipts ?? Array.Empty<GoodsReceipt>())
                    .Prepend(receipt)
                    .OrderByDescending(item => item.ReceivedAt)
                    .ToArray()
            };
        }, cancellationToken);

        return await workspaceQueryService.GetWarehouseHubAsync(tenantId, cancellationToken);
    }

    public async Task<WarehouseHubDto> CreateGatePassAsync(
        Guid tenantId,
        Guid userId,
        SaveGatePassRequestDto request,
        CancellationToken cancellationToken)
    {
        ValidateGatePassRequest(request);

        await workspaceRepository.UpdateWorkspaceSnapshotAsync(snapshot =>
        {
            EnsureTenant(snapshot, tenantId);
            var currentUser = ResolveInventoryUser(snapshot, userId);
            var status = NormalizeStatus(request.Status, "Prepared");
            var gatePass = new GatePass(
                Guid.NewGuid(),
                tenantId,
                BuildGatePassNumber(snapshot.GatePasses),
                request.MovementType.Trim(),
                request.WarehouseName.Trim(),
                request.DestinationName.Trim(),
                request.ReferenceNo.Trim(),
                status,
                DateTimeOffset.Now,
                currentUser.DisplayName,
                request.Units,
                NormalizeNotes(request.Notes));

            var stockTransfers = (snapshot.StockTransfers ?? Array.Empty<StockTransfer>())
                .Select(transfer =>
                {
                    if (!string.Equals(transfer.TransferNo, request.ReferenceNo.Trim(), StringComparison.OrdinalIgnoreCase))
                    {
                        return transfer;
                    }

                    return transfer with
                    {
                        Status = status.Contains("Dispatch", StringComparison.OrdinalIgnoreCase)
                            ? status
                            : "Dispatched"
                    };
                })
                .OrderByDescending(item => item.CreatedAt)
                .ToArray();

            return snapshot with
            {
                StockTransfers = stockTransfers,
                GatePasses = (snapshot.GatePasses ?? Array.Empty<GatePass>())
                    .Prepend(gatePass)
                    .OrderByDescending(item => item.IssuedAt)
                    .ToArray()
            };
        }, cancellationToken);

        return await workspaceQueryService.GetWarehouseHubAsync(tenantId, cancellationToken);
    }

    private static AppUser ResolveInventoryUser(WorkspaceSnapshot snapshot, Guid userId)
    {
        var currentUser = (snapshot.Users ?? Array.Empty<AppUser>())
            .FirstOrDefault(user => user.Id == userId)
            ?? (snapshot.AdminUser.Id == userId ? snapshot.AdminUser : null)
            ?? throw new InvalidOperationException("The current user could not be resolved.");

        if (!WorkspaceRoleAccess.CanManageInventory(currentUser.Role))
        {
            throw new InvalidOperationException("The current user is not allowed to manage warehouse workflows.");
        }

        return currentUser;
    }

    private static string BuildTransferNumber(IReadOnlyList<StockTransfer>? transfers)
    {
        var next = transfers?
            .Select(transfer => ExtractNumericSuffix(transfer.TransferNo))
            .DefaultIfEmpty(120)
            .Max() + 1 ?? 121;

        return $"TR-{next:0000}";
    }

    private static string BuildReceiptNumber(IReadOnlyList<GoodsReceipt>? receipts)
    {
        var next = receipts?
            .Select(receipt => ExtractNumericSuffix(receipt.ReceiptNo))
            .DefaultIfEmpty(320)
            .Max() + 1 ?? 321;

        return $"GRN-{next:0000}";
    }

    private static string BuildGatePassNumber(IReadOnlyList<GatePass>? gatePasses)
    {
        var next = gatePasses?
            .Select(pass => ExtractNumericSuffix(pass.GatePassNo))
            .DefaultIfEmpty(510)
            .Max() + 1 ?? 511;

        return $"GP-{next:0000}";
    }

    private static int ExtractNumericSuffix(string value)
    {
        var digits = new string(value.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out var parsed) ? parsed : 0;
    }

    private static void ValidateStockTransferRequest(SaveStockTransferRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.FromBranchName) || string.IsNullOrWhiteSpace(request.ToBranchName))
        {
            throw new InvalidOperationException("Source and destination branches are required.");
        }

        if (string.Equals(request.FromBranchName.Trim(), request.ToBranchName.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Source and destination branches must be different.");
        }

        if (request.Units <= 0)
        {
            throw new InvalidOperationException("Transfer units must be greater than zero.");
        }
    }

    private static void ValidateGoodsReceiptRequest(SaveGoodsReceiptRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.PurchaseOrderNo) ||
            string.IsNullOrWhiteSpace(request.VendorName) ||
            string.IsNullOrWhiteSpace(request.WarehouseName))
        {
            throw new InvalidOperationException("Purchase order, vendor, and warehouse are required.");
        }

        if (request.LineCount <= 0 || request.ReceivedUnits <= 0)
        {
            throw new InvalidOperationException("Line count and received units must be greater than zero.");
        }

        if (request.VarianceUnits < 0)
        {
            throw new InvalidOperationException("Variance units cannot be negative.");
        }
    }

    private static void ValidateGatePassRequest(SaveGatePassRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.MovementType) ||
            string.IsNullOrWhiteSpace(request.WarehouseName) ||
            string.IsNullOrWhiteSpace(request.DestinationName) ||
            string.IsNullOrWhiteSpace(request.ReferenceNo))
        {
            throw new InvalidOperationException("Movement type, warehouse, destination, and reference are required.");
        }

        if (request.Units <= 0)
        {
            throw new InvalidOperationException("Gate pass units must be greater than zero.");
        }
    }

    private static void EnsureTenant(WorkspaceSnapshot snapshot, Guid tenantId)
    {
        if (snapshot.Tenant.Id != tenantId)
        {
            throw new InvalidOperationException("The current user does not belong to the requested tenant.");
        }
    }

    private static string NormalizeStatus(string? candidate, string fallback)
    {
        return string.IsNullOrWhiteSpace(candidate)
            ? fallback
            : candidate.Trim();
    }

    private static string NormalizeNotes(string? notes)
    {
        return string.IsNullOrWhiteSpace(notes)
            ? string.Empty
            : notes.Trim();
    }
}
