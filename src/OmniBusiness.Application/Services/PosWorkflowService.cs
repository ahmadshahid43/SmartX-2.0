using OmniBusiness.Application.Abstractions.Compliance;
using OmniBusiness.Application.Abstractions.Persistence;
using OmniBusiness.Application.Contracts;
using OmniBusiness.Domain.Foundation;

namespace OmniBusiness.Application.Services;

public sealed class PosWorkflowService(
    IWorkspaceRepository workspaceRepository,
    IWorkspaceQueryService workspaceQueryService,
    IFbrInvoiceService fbrInvoiceService) : IPosWorkflowService
{
    public async Task<PosTerminalDto> SaveCartLineAsync(
        Guid tenantId,
        PosCartMutationRequestDto request,
        CancellationToken cancellationToken)
    {
        if (request.Quantity <= 0)
        {
            throw new InvalidOperationException("Quantity must be greater than zero.");
        }

        await workspaceRepository.UpdateWorkspaceSnapshotAsync(snapshot =>
        {
            EnsureTenant(snapshot, tenantId);

            var product = snapshot.Products.FirstOrDefault(item => item.Id == request.ProductId && !item.IsArchived)
                ?? throw new InvalidOperationException("The selected product was not found.");

            var available = Math.Max(product.InHand - product.Reserved, 0);
            if (request.Quantity > available)
            {
                throw new InvalidOperationException(
                    $"Only {available} unit(s) of {product.Name} are available for sale.");
            }

            var lines = snapshot.ActiveCart.ToList();
            var index = lines.FindIndex(line => line.ProductId == request.ProductId);
            var updatedLine = new CartLine(
                product.Id,
                product.Name,
                request.Quantity,
                product.UnitPrice,
                true);

            if (index >= 0)
            {
                lines[index] = updatedLine;
            }
            else
            {
                lines.Add(updatedLine);
            }

            return snapshot with
            {
                ActiveCart = lines
                    .OrderBy(line => line.Name)
                    .ToArray()
            };
        }, cancellationToken);

        return await workspaceQueryService.GetPosTerminalAsync(tenantId, cancellationToken);
    }

    public async Task<PosTerminalDto> RemoveCartLineAsync(
        Guid tenantId,
        Guid productId,
        CancellationToken cancellationToken)
    {
        await workspaceRepository.UpdateWorkspaceSnapshotAsync(snapshot =>
        {
            EnsureTenant(snapshot, tenantId);

            return snapshot with
            {
                ActiveCart = snapshot.ActiveCart
                    .Where(line => line.ProductId != productId)
                    .ToArray()
            };
        }, cancellationToken);

        return await workspaceQueryService.GetPosTerminalAsync(tenantId, cancellationToken);
    }

    public async Task<PosCheckoutReceiptDto> CheckoutAsync(
        Guid tenantId,
        Guid userId,
        PosCheckoutRequestDto request,
        CancellationToken cancellationToken)
    {
        var currentUser = await workspaceRepository.GetUserByIdAsync(tenantId, userId, cancellationToken)
            ?? throw new InvalidOperationException("The current user is not allowed to complete this checkout.");
        PosCheckoutReceiptDto? receipt = null;
        Guid createdSaleId = Guid.Empty;

        await workspaceRepository.UpdateWorkspaceSnapshotAsync(snapshot =>
        {
            EnsureTenant(snapshot, tenantId);

            if (snapshot.ActiveCart.Count == 0)
            {
                throw new InvalidOperationException("Add at least one item before completing payment.");
            }

            var occurredAt = DateTimeOffset.Now;
            var lines = snapshot.ActiveCart
                .Select(line =>
                {
                    var product = snapshot.Products.FirstOrDefault(item => item.Id == line.ProductId && !item.IsArchived)
                        ?? throw new InvalidOperationException($"Product '{line.Name}' is no longer available.");

                    var available = Math.Max(product.InHand - product.Reserved, 0);
                    if (line.Quantity > available)
                    {
                        throw new InvalidOperationException(
                            $"Only {available} unit(s) of {product.Name} are available for sale.");
                    }

                    return new SaleLine(
                        product.Id,
                        product.Sku,
                        product.Name,
                        line.Quantity,
                        line.UnitPrice,
                        line.Quantity * line.UnitPrice);
                })
                .ToArray();

            var summary = PosPricingCalculator.BuildSummary(snapshot.ActiveCart);
            var paymentMethod = string.IsNullOrWhiteSpace(request.PaymentMethod) ? "Cash" : request.PaymentMethod.Trim();
            var receivedAmount = request.ReceivedAmount ?? summary.Total;

            if (receivedAmount < summary.Total)
            {
                throw new InvalidOperationException("Received amount cannot be less than the payable total.");
            }

            var referenceNo = $"TRX-{snapshot.NextSaleSequence:D4}";
            var grossProfit = decimal.Round(summary.Subtotal * 0.25m, 2, MidpointRounding.AwayFromZero);
            var fbrStatus = request.SendToFbr ? "PendingSubmission" : "NotSubmitted";

            var saleRecord = new SaleRecord(
                Guid.NewGuid(),
                tenantId,
                referenceNo,
                snapshot.ActiveCustomer.Name,
                summary.Total,
                grossProfit,
                "Completed",
                occurredAt,
                summary.ItemCount,
                summary.Discount,
                summary.Tax,
                paymentMethod,
                currentUser.DisplayName,
                lines,
                receivedAmount,
                receivedAmount - summary.Total,
                fbrStatus);
            createdSaleId = saleRecord.Id;

            var soldQuantityByProduct = lines
                .GroupBy(line => line.ProductId)
                .ToDictionary(group => group.Key, group => group.Sum(line => line.Quantity));

            var updatedProducts = snapshot.Products
                .Select(product =>
                {
                    if (!soldQuantityByProduct.TryGetValue(product.Id, out var soldQuantity))
                    {
                        return product;
                    }

                    return ProductStateCalculator.ApplyInventory(
                        product,
                        product.InHand - soldQuantity,
                        product.Reserved);
                })
                .ToArray();

            var updatedTransactions = snapshot.RecentTransactions
                .Prepend(saleRecord)
                .Take(100)
                .ToArray();

            var updatedDailyFigures = UpdateDailyFigures(snapshot.DailyFigures, summary.Total, grossProfit, occurredAt);
            var updatedTrend = UpdateSalesTrend(snapshot.SalesTrend, summary.Total, occurredAt);
            var updatedTopSelling = UpdateTopSelling(snapshot.TopSelling, lines);

            receipt = new PosCheckoutReceiptDto(
                saleRecord.Id,
                saleRecord.ReferenceNo,
                saleRecord.CustomerName,
                saleRecord.PaymentMethod,
                saleRecord.CashierName,
                saleRecord.OccurredAt,
                lines.Select(line => new SaleLineDto(
                    line.ProductId,
                    line.Sku,
                    line.Name,
                    line.Quantity,
                    line.UnitPrice,
                    line.LineTotal))
                    .ToArray(),
                summary,
                saleRecord.ReceivedAmount,
                saleRecord.ChangeAmount,
                saleRecord.FbrStatus,
                saleRecord.FbrInvoiceNumber);

            return snapshot with
            {
                Products = updatedProducts,
                RecentTransactions = updatedTransactions,
                DailyFigures = updatedDailyFigures,
                SalesTrend = updatedTrend,
                TopSelling = updatedTopSelling,
                ActiveCart = Array.Empty<CartLine>(),
                NextSaleSequence = snapshot.NextSaleSequence + 1
            };
        }, cancellationToken);

        if (request.SendToFbr && createdSaleId != Guid.Empty)
        {
            var submittedSale = await SubmitSaleToFbrAsync(tenantId, createdSaleId, cancellationToken);
            receipt = receipt! with
            {
                FbrStatus = submittedSale.FbrStatus,
                FbrInvoiceNumber = submittedSale.FbrInvoiceNumber
            };
        }

        return receipt ?? throw new InvalidOperationException("Unable to build the checkout receipt.");
    }

    public async Task<SalesHistoryItemDto> SubmitSaleToFbrAsync(
        Guid tenantId,
        Guid saleId,
        CancellationToken cancellationToken)
    {
        var snapshot = await workspaceRepository.GetWorkspaceSnapshotAsync(cancellationToken);
        EnsureTenant(snapshot, tenantId);

        var sale = snapshot.RecentTransactions.FirstOrDefault(item => item.Id == saleId)
            ?? throw new InvalidOperationException("The selected sale could not be found.");

        if (string.Equals(sale.FbrStatus, "Submitted", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(sale.FbrInvoiceNumber))
        {
            return MapSale(sale);
        }

        var submission = await fbrInvoiceService.SubmitSaleAsync(snapshot.Company, sale, cancellationToken);
        SaleRecord? updatedSale = null;

        await workspaceRepository.UpdateWorkspaceSnapshotAsync(current =>
        {
            EnsureTenant(current, tenantId);

            var transactions = current.RecentTransactions
                .Select(item =>
                {
                    if (item.Id != saleId)
                    {
                        return item;
                    }

                    updatedSale = item with
                    {
                        FbrStatus = submission.Status,
                        FbrInvoiceNumber = submission.InvoiceNumber,
                        FbrErrorMessage = submission.ErrorMessage,
                        FbrReportedAt = submission.ReportedAt
                    };

                    return updatedSale;
                })
                .ToArray();

            return current with { RecentTransactions = transactions };
        }, cancellationToken);

        return MapSale(updatedSale ?? sale);
    }

    private static IReadOnlyList<DailyBusinessFigure> UpdateDailyFigures(
        IReadOnlyList<DailyBusinessFigure> figures,
        decimal amount,
        decimal grossProfit,
        DateTimeOffset occurredAt)
    {
        var businessDate = DateOnly.FromDateTime(occurredAt.LocalDateTime);
        var items = figures.ToList();
        var index = items.FindIndex(item => item.Date == businessDate);

        if (index >= 0)
        {
            var current = items[index];
            items[index] = current with
            {
                Sales = current.Sales + amount,
                GrossProfit = current.GrossProfit + grossProfit
            };
        }
        else
        {
            items.Add(new DailyBusinessFigure(businessDate, amount, 0m, grossProfit));
        }

        return items.OrderBy(item => item.Date).ToArray();
    }

    private static IReadOnlyList<TrendPoint> UpdateSalesTrend(
        IReadOnlyList<TrendPoint> trend,
        decimal amount,
        DateTimeOffset occurredAt)
    {
        var label = occurredAt.ToString("HH:00");
        var items = trend.ToList();
        var index = items.FindIndex(item => string.Equals(item.Label, label, StringComparison.OrdinalIgnoreCase));

        if (index >= 0)
        {
            var current = items[index];
            items[index] = current with { Value = current.Value + amount };
        }
        else
        {
            items.Add(new TrendPoint(label, amount));
        }

        return items
            .OrderBy(item => item.Label)
            .ToArray();
    }

    private static IReadOnlyList<TopSellingItem> UpdateTopSelling(
        IReadOnlyList<TopSellingItem> topSelling,
        IReadOnlyList<SaleLine> lines)
    {
        var aggregate = topSelling.ToDictionary(
            item => item.Name,
            item => (Units: item.Units, Revenue: item.Revenue),
            StringComparer.OrdinalIgnoreCase);

        foreach (var line in lines)
        {
            var current = aggregate.TryGetValue(line.Name, out var existing)
                ? existing
                : (0, 0m);

            aggregate[line.Name] = (current.Item1 + line.Quantity, current.Item2 + line.LineTotal);
        }

        return aggregate
            .Select(item => new TopSellingItem(item.Key, item.Value.Units, item.Value.Revenue))
            .OrderByDescending(item => item.Units)
            .ThenByDescending(item => item.Revenue)
            .Take(8)
            .ToArray();
    }

    private static void EnsureTenant(WorkspaceSnapshot snapshot, Guid tenantId)
    {
        if (snapshot.Tenant.Id != tenantId)
        {
            throw new InvalidOperationException("The current user does not belong to the requested tenant.");
        }
    }

    private static SalesHistoryItemDto MapSale(SaleRecord sale)
    {
        return new SalesHistoryItemDto(
            sale.Id,
            sale.ReferenceNo,
            sale.CustomerName,
            sale.Amount,
            sale.GrossProfit,
            sale.Status,
            sale.OccurredAt,
            sale.ItemCount,
            sale.Discount,
            sale.Tax,
            sale.PaymentMethod,
            sale.CashierName,
            (sale.Lines ?? Array.Empty<SaleLine>())
                .Select(line => new SaleLineDto(
                    line.ProductId,
                    line.Sku,
                    line.Name,
                    line.Quantity,
                    line.UnitPrice,
                    line.LineTotal))
                .ToArray(),
            sale.ReceivedAmount,
            sale.ChangeAmount,
            sale.FbrStatus,
            sale.FbrInvoiceNumber,
            sale.FbrErrorMessage,
            sale.FbrReportedAt);
    }
}
