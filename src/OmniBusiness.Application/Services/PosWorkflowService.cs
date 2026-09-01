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
    private const int MaxHeldOrders = 20;
    private const int MaxBookings = 80;

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

    public async Task<PosTerminalDto> SelectCustomerAsync(
        Guid tenantId,
        SelectPosCustomerRequestDto request,
        CancellationToken cancellationToken)
    {
        await workspaceRepository.UpdateWorkspaceSnapshotAsync(snapshot =>
        {
            EnsureTenant(snapshot, tenantId);

            var customers = snapshot.Customers?.ToList() ?? [];
            CustomerProfile? selectedCustomer = null;

            if (request.CustomerId is { } customerId)
            {
                selectedCustomer = customers.FirstOrDefault(customer => customer.Id == customerId && customer.TenantId == tenantId)
                    ?? throw new InvalidOperationException("The selected customer was not found.");
            }
            else
            {
                var name = NormalizeText(request.CustomerName);
                if (string.IsNullOrWhiteSpace(name))
                {
                    selectedCustomer = customers.FirstOrDefault(customer => customer.IsWalkIn);
                }
                else
                {
                    var phoneNumber = NormalizeNullableText(request.PhoneNumber);
                    var email = NormalizeNullableText(request.Email);
                    selectedCustomer = customers.FirstOrDefault(customer =>
                        string.Equals(customer.Name, name, StringComparison.OrdinalIgnoreCase) ||
                        (!string.IsNullOrWhiteSpace(phoneNumber) && string.Equals(customer.PhoneNumber, phoneNumber, StringComparison.OrdinalIgnoreCase)) ||
                        (!string.IsNullOrWhiteSpace(email) && string.Equals(customer.Email, email, StringComparison.OrdinalIgnoreCase)));

                    if (selectedCustomer is null)
                    {
                        selectedCustomer = new CustomerProfile(
                            Guid.NewGuid(),
                            tenantId,
                            name,
                            "Retail Pricing",
                            name[..1].ToUpperInvariant(),
                            phoneNumber,
                            false,
                            email,
                            LastVisitAt: DateTimeOffset.Now);
                        customers.Add(selectedCustomer);
                    }
                }
            }

            var activeCustomer = selectedCustomer is null
                ? new PosCustomer("Walk-in Customer", "Retail Pricing", "W")
                : new PosCustomer(selectedCustomer.Name, selectedCustomer.PricingTier, selectedCustomer.AvatarLetter);

            return snapshot with
            {
                ActiveCustomer = activeCustomer,
                Customers = customers
            };
        }, cancellationToken);

        return await workspaceQueryService.GetPosTerminalAsync(tenantId, cancellationToken);
    }

    public async Task<PosWorkflowActionDto> HoldCurrentSaleAsync(
        Guid tenantId,
        Guid userId,
        CreateHeldOrderRequestDto request,
        CancellationToken cancellationToken)
    {
        var currentUser = await workspaceRepository.GetUserByIdAsync(tenantId, userId, cancellationToken)
            ?? throw new InvalidOperationException("The current user is not allowed to hold this sale.");
        Guid heldOrderId = Guid.Empty;
        string ticketNo = string.Empty;

        await workspaceRepository.UpdateWorkspaceSnapshotAsync(snapshot =>
        {
            EnsureTenant(snapshot, tenantId);

            if (snapshot.ActiveCart.Count == 0)
            {
                throw new InvalidOperationException("Add items to the cart before holding the current sale.");
            }

            var summary = PosPricingCalculator.BuildSummary(snapshot.ActiveCart);
            ticketNo = $"HOLD-{snapshot.NextHoldSequence:D4}";
            var heldOrder = new PosHeldOrder(
                Guid.NewGuid(),
                tenantId,
                ticketNo,
                snapshot.ActiveCustomer.Name,
                snapshot.ActiveCustomer.PricingTier,
                currentUser.DisplayName,
                DateTimeOffset.Now,
                summary.ItemCount,
                summary.Total,
                snapshot.ActiveCart
                    .Select(line => line with { })
                    .ToArray(),
                NormalizeText(request.Notes));
            heldOrderId = heldOrder.Id;

            return snapshot with
            {
                ActiveCart = Array.Empty<CartLine>(),
                HeldOrders = (snapshot.HeldOrders ?? Array.Empty<PosHeldOrder>())
                    .Prepend(heldOrder)
                    .Take(MaxHeldOrders)
                    .ToArray(),
                NextHoldSequence = snapshot.NextHoldSequence + 1
            };
        }, cancellationToken);

        var terminal = await workspaceQueryService.GetPosTerminalAsync(tenantId, cancellationToken);
        var heldOrder = terminal.HeldOrders.FirstOrDefault(order => order.Id == heldOrderId);

        return new PosWorkflowActionDto(
            heldOrder is null
                ? "Current sale hold par save ho gayi."
                : $"{heldOrder.TicketNo} hold par save ho gayi.",
            terminal,
            null,
            null);
    }

    public async Task<PosWorkflowActionDto> ResumeHeldOrderAsync(
        Guid tenantId,
        Guid heldOrderId,
        CancellationToken cancellationToken)
    {
        string resumedTicketNo = string.Empty;

        await workspaceRepository.UpdateWorkspaceSnapshotAsync(snapshot =>
        {
            EnsureTenant(snapshot, tenantId);

            if (snapshot.ActiveCart.Count > 0)
            {
                throw new InvalidOperationException("Resume karne se pehle current cart ko hold ya clear karein.");
            }

            var heldOrders = snapshot.HeldOrders?.ToList() ?? [];
            var heldOrder = heldOrders.FirstOrDefault(order => order.Id == heldOrderId)
                ?? throw new InvalidOperationException("The selected held order could not be found.");

            foreach (var line in heldOrder.Lines)
            {
                var product = snapshot.Products.FirstOrDefault(item => item.Id == line.ProductId && !item.IsArchived)
                    ?? throw new InvalidOperationException($"Product '{line.Name}' is no longer available.");

                var available = Math.Max(product.InHand - product.Reserved, 0);
                if (line.Quantity > available)
                {
                    throw new InvalidOperationException(
                        $"Only {available} unit(s) of {product.Name} are available. Held ticket needs update before resume.");
                }
            }

            resumedTicketNo = heldOrder.TicketNo;
            heldOrders.RemoveAll(order => order.Id == heldOrderId);

            return snapshot with
            {
                ActiveCart = heldOrder.Lines
                    .OrderBy(line => line.Name)
                    .ToArray(),
                HeldOrders = heldOrders
                    .OrderByDescending(order => order.HeldAt)
                    .ToArray()
            };
        }, cancellationToken);

        var terminal = await workspaceQueryService.GetPosTerminalAsync(tenantId, cancellationToken);
        return new PosWorkflowActionDto(
            $"{resumedTicketNo} resume ho gayi. Cashier sale continue kar sakta hai.",
            terminal);
    }

    public async Task<PosWorkflowActionDto> CreateBookingAsync(
        Guid tenantId,
        Guid userId,
        CreateBookingOrderRequestDto request,
        CancellationToken cancellationToken)
    {
        var currentUser = await workspaceRepository.GetUserByIdAsync(tenantId, userId, cancellationToken)
            ?? throw new InvalidOperationException("The current user is not allowed to create a booking.");
        PosBookingOrder? booking = null;

        await workspaceRepository.UpdateWorkspaceSnapshotAsync(snapshot =>
        {
            EnsureTenant(snapshot, tenantId);

            if (snapshot.ActiveCart.Count == 0)
            {
                throw new InvalidOperationException("Add items to the cart before creating a booking.");
            }

            var customerName = NormalizeText(request.CustomerName);
            if (string.IsNullOrWhiteSpace(customerName))
            {
                throw new InvalidOperationException("Customer name is required for booked orders.");
            }

            var lines = BuildSaleLines(snapshot.ActiveCart, snapshot.Products);
            var summary = PosPricingCalculator.BuildSummary(snapshot.ActiveCart);
            var payments = NormalizePaymentRequests(request.Payments, allowEmpty: true);
            var paidAmount = decimal.Round(payments.Sum(payment => payment.Amount), 2, MidpointRounding.AwayFromZero);

            if (paidAmount > summary.Total)
            {
                throw new InvalidOperationException("Advance amount cannot exceed the booking total.");
            }

            var reservedQuantityByProduct = lines
                .GroupBy(line => line.ProductId)
                .ToDictionary(group => group.Key, group => group.Sum(line => line.Quantity));

            var updatedProducts = snapshot.Products
                .Select(product =>
                {
                    if (!reservedQuantityByProduct.TryGetValue(product.Id, out var reservedQuantity))
                    {
                        return product;
                    }

                    var available = Math.Max(product.InHand - product.Reserved, 0);
                    if (reservedQuantity > available)
                    {
                        throw new InvalidOperationException(
                            $"Only {available} unit(s) of {product.Name} are available for booking.");
                    }

                    return ProductStateCalculator.ApplyInventory(
                        product,
                        product.InHand,
                        product.Reserved + reservedQuantity);
                })
                .ToArray();

            var balanceAmount = decimal.Round(summary.Total - paidAmount, 2, MidpointRounding.AwayFromZero);
            booking = new PosBookingOrder(
                Guid.NewGuid(),
                tenantId,
                $"BOOK-{snapshot.NextBookingSequence:D4}",
                customerName,
                NormalizeNullableText(request.PhoneNumber),
                NormalizeNullableText(request.Email),
                DetermineBookingStatus(balanceAmount, paidAmount),
                DateTimeOffset.Now,
                request.DueAt,
                currentUser.DisplayName,
                lines,
                summary.ItemCount,
                summary.Subtotal,
                summary.Discount,
                summary.Tax,
                summary.Total,
                paidAmount,
                balanceAmount,
                DeterminePaymentStatus(balanceAmount, paidAmount),
                payments,
                NormalizeText(request.Notes));

            return snapshot with
            {
                Products = updatedProducts,
                ActiveCart = Array.Empty<CartLine>(),
                Bookings = (snapshot.Bookings ?? Array.Empty<PosBookingOrder>())
                    .Prepend(booking)
                    .Take(MaxBookings)
                    .ToArray(),
                NextBookingSequence = snapshot.NextBookingSequence + 1
            };
        }, cancellationToken);

        var terminal = await workspaceQueryService.GetPosTerminalAsync(tenantId, cancellationToken);
        var bookingDto = terminal.Bookings.FirstOrDefault(item => item.Id == booking?.Id);

        return new PosWorkflowActionDto(
            bookingDto is null
                ? "Booking create ho gayi."
                : $"{bookingDto.BookingNo} booked order save ho gaya.",
            terminal,
            bookingDto);
    }

    public async Task<PosWorkflowActionDto> CollectBookingPaymentAsync(
        Guid tenantId,
        Guid bookingId,
        CollectBookingPaymentRequestDto request,
        CancellationToken cancellationToken)
    {
        if (request.Amount <= 0)
        {
            throw new InvalidOperationException("Installment amount must be greater than zero.");
        }

        await workspaceRepository.UpdateWorkspaceSnapshotAsync(snapshot =>
        {
            EnsureTenant(snapshot, tenantId);

            var bookings = snapshot.Bookings?.ToList() ?? [];
            var index = bookings.FindIndex(item => item.Id == bookingId);
            if (index < 0)
            {
                throw new InvalidOperationException("The selected booking could not be found.");
            }

            var booking = bookings[index];
            if (booking.BalanceAmount <= 0)
            {
                throw new InvalidOperationException("This booking is already fully settled.");
            }

            if (request.Amount > booking.BalanceAmount)
            {
                throw new InvalidOperationException("Installment amount cannot exceed the current balance.");
            }

            var payment = new PaymentAllocation(
                NormalizePaymentMethod(request.PaymentMethod),
                decimal.Round(request.Amount, 2, MidpointRounding.AwayFromZero),
                NormalizeNullableText(request.ReferenceNo));
            var payments = (booking.Payments ?? Array.Empty<PaymentAllocation>())
                .Append(payment)
                .ToArray();
            var paidAmount = decimal.Round(booking.PaidAmount + payment.Amount, 2, MidpointRounding.AwayFromZero);
            var balanceAmount = decimal.Round(Math.Max(booking.TotalAmount - paidAmount, 0), 2, MidpointRounding.AwayFromZero);

            bookings[index] = booking with
            {
                PaidAmount = paidAmount,
                BalanceAmount = balanceAmount,
                Status = DetermineBookingStatus(balanceAmount, paidAmount),
                PaymentStatus = DeterminePaymentStatus(balanceAmount, paidAmount),
                Payments = payments,
                Notes = MergeNotes(booking.Notes, request.Notes)
            };

            return snapshot with
            {
                Bookings = bookings
                    .OrderByDescending(item => item.CreatedAt)
                    .ToArray()
            };
        }, cancellationToken);

        var terminal = await workspaceQueryService.GetPosTerminalAsync(tenantId, cancellationToken);
        var updatedBooking = terminal.Bookings.FirstOrDefault(item => item.Id == bookingId)
            ?? throw new InvalidOperationException("Booking payment save ho gayi lekin refreshed booking load nahi hui.");

        return new PosWorkflowActionDto(
            $"{updatedBooking.BookingNo} par installment receive ho gayi. Remaining balance PKR {updatedBooking.BalanceAmount:N0}.",
            terminal,
            updatedBooking);
    }

    public async Task<PosWorkflowActionDto> CompleteBookingAsync(
        Guid tenantId,
        Guid userId,
        Guid bookingId,
        CompleteBookingRequestDto request,
        CancellationToken cancellationToken)
    {
        var currentUser = await workspaceRepository.GetUserByIdAsync(tenantId, userId, cancellationToken)
            ?? throw new InvalidOperationException("The current user is not allowed to complete this booking.");
        PosCheckoutReceiptDto? receipt = null;
        Guid createdSaleId = Guid.Empty;
        string bookingNo = string.Empty;

        await workspaceRepository.UpdateWorkspaceSnapshotAsync(snapshot =>
        {
            EnsureTenant(snapshot, tenantId);

            var bookings = snapshot.Bookings?.ToList() ?? [];
            var index = bookings.FindIndex(item => item.Id == bookingId);
            if (index < 0)
            {
                throw new InvalidOperationException("The selected booking could not be found.");
            }

            var booking = bookings[index];
            bookingNo = booking.BookingNo;

            if (booking.BalanceAmount > 0)
            {
                throw new InvalidOperationException("Complete karne se pehle booking ka full balance receive karein.");
            }

            var lines = booking.Lines ?? Array.Empty<SaleLine>();
            if (lines.Count == 0)
            {
                throw new InvalidOperationException("Booked order has no lines to complete.");
            }

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

                    if (product.Reserved < soldQuantity)
                    {
                        throw new InvalidOperationException(
                            $"Reserved quantity mismatch found for {product.Name}. Review inventory before completion.");
                    }

                    return ProductStateCalculator.ApplyInventory(
                        product,
                        product.InHand - soldQuantity,
                        product.Reserved - soldQuantity);
                })
                .ToArray();

            var paymentLines = (booking.Payments ?? Array.Empty<PaymentAllocation>())
                .Where(payment => payment.Amount > 0)
                .ToArray();
            var paymentMethod = BuildPaymentMethodLabel(paymentLines, "Booking Settlement");
            var occurredAt = DateTimeOffset.Now;
            var grossProfit = decimal.Round(booking.Subtotal * 0.25m, 2, MidpointRounding.AwayFromZero);

            var saleRecord = new SaleRecord(
                Guid.NewGuid(),
                tenantId,
                $"TRX-{snapshot.NextSaleSequence:D4}",
                booking.CustomerName,
                booking.TotalAmount,
                grossProfit,
                "Completed",
                occurredAt,
                booking.ItemCount,
                booking.Discount,
                booking.Tax,
                paymentMethod,
                currentUser.DisplayName,
                lines,
                booking.PaidAmount,
                0,
                request.SendToFbr ? "PendingSubmission" : "NotSubmitted",
                null,
                null,
                null,
                booking.PaidAmount,
                0,
                "Paid",
                paymentLines);
            createdSaleId = saleRecord.Id;

            var updatedTransactions = snapshot.RecentTransactions
                .Prepend(saleRecord)
                .Take(100)
                .ToArray();
            var updatedDailyFigures = UpdateDailyFigures(snapshot.DailyFigures, booking.TotalAmount, grossProfit, occurredAt);
            var updatedTrend = UpdateSalesTrend(snapshot.SalesTrend, booking.TotalAmount, occurredAt);
            var updatedTopSelling = UpdateTopSelling(snapshot.TopSelling, lines);
            bookings.RemoveAt(index);

            receipt = MapReceipt(saleRecord);

            return snapshot with
            {
                Products = updatedProducts,
                RecentTransactions = updatedTransactions,
                DailyFigures = updatedDailyFigures,
                SalesTrend = updatedTrend,
                TopSelling = updatedTopSelling,
                Bookings = bookings
                    .OrderByDescending(item => item.CreatedAt)
                    .ToArray(),
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

        var terminal = await workspaceQueryService.GetPosTerminalAsync(tenantId, cancellationToken);

        return new PosWorkflowActionDto(
            $"{bookingNo} complete ho gaya aur invoice issue ho gayi.",
            terminal,
            null,
            receipt);
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
            var lines = BuildSaleLines(snapshot.ActiveCart, snapshot.Products);
            var summary = PosPricingCalculator.BuildSummary(
                snapshot.ActiveCart,
                request.TaxRatePercent,
                request.TaxExempt);
            var payments = NormalizePaymentRequests(
                request.Payments,
                allowEmpty: false,
                request.PaymentMethod,
                request.ReceivedAmount ?? summary.Total);
            var receivedAmount = decimal.Round(payments.Sum(payment => payment.Amount), 2, MidpointRounding.AwayFromZero);

            if (receivedAmount < summary.Total)
            {
                throw new InvalidOperationException("Received amount cannot be less than the payable total.");
            }

            var referenceNo = $"TRX-{snapshot.NextSaleSequence:D4}";
            var grossProfit = decimal.Round(summary.Subtotal * 0.25m, 2, MidpointRounding.AwayFromZero);
            var fbrStatus = request.SendToFbr ? "PendingSubmission" : "NotSubmitted";
            var paymentMethod = BuildPaymentMethodLabel(payments, request.PaymentMethod);

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
                fbrStatus,
                null,
                null,
                null,
                summary.Total,
                0,
                "Paid",
                payments);
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

            receipt = MapReceipt(saleRecord);

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

        if (IsRefundedTransaction(sale))
        {
            throw new InvalidOperationException("Refunded sale ko dubara FBR submit nahi kiya ja sakta. Credit-note reconciliation required.");
        }

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

    public async Task<SalesHistoryItemDto> RefundSaleAsync(
        Guid tenantId,
        Guid userId,
        Guid saleId,
        RefundSaleRequestDto request,
        CancellationToken cancellationToken)
    {
        var currentUser = await workspaceRepository.GetUserByIdAsync(tenantId, userId, cancellationToken)
            ?? throw new InvalidOperationException("The current user is not allowed to refund this sale.");
        SalesHistoryItemDto? updatedSaleDto = null;

        await workspaceRepository.UpdateWorkspaceSnapshotAsync(snapshot =>
        {
            EnsureTenant(snapshot, tenantId);

            var transactions = snapshot.RecentTransactions.ToList();
            var index = transactions.FindIndex(item => item.Id == saleId);
            if (index < 0)
            {
                throw new InvalidOperationException("The selected sale could not be found.");
            }

            var sale = transactions[index];
            if (IsRefundedTransaction(sale))
            {
                throw new InvalidOperationException("This sale has already been refunded.");
            }

            var refundLines = sale.Lines ?? Array.Empty<SaleLine>();
            var refundedAt = DateTimeOffset.Now;
            var refundReason = string.IsNullOrWhiteSpace(request.Reason)
                ? $"Refund processed on {refundedAt:dd MMM yyyy, h:mm tt}."
                : request.Reason.Trim();

            var updatedProducts = snapshot.Products.ToArray();
            var updatedAdjustments = snapshot.StockAdjustments ?? Array.Empty<StockAdjustmentRecord>();

            if (request.ReturnToInventory && refundLines.Count > 0)
            {
                var quantityByProduct = refundLines
                    .GroupBy(line => line.ProductId)
                    .ToDictionary(group => group.Key, group => group.Sum(line => line.Quantity));

                updatedProducts = snapshot.Products
                    .Select(product =>
                    {
                        if (!quantityByProduct.TryGetValue(product.Id, out var returnQuantity))
                        {
                            return product;
                        }

                        return ProductStateCalculator.ApplyInventory(
                            product,
                            product.InHand + returnQuantity,
                            product.Reserved);
                    })
                    .ToArray();

                updatedAdjustments = refundLines
                    .Select(line => new StockAdjustmentRecord(
                        Guid.NewGuid(),
                        tenantId,
                        line.ProductId,
                        line.Name,
                        line.Quantity,
                        $"Refund restock for {sale.ReferenceNo}",
                        currentUser.DisplayName,
                        refundedAt))
                    .Concat(updatedAdjustments)
                    .Take(150)
                    .ToArray();
            }

            var updatedSale = sale with
            {
                Status = "Refunded",
                PaymentStatus = "Refunded",
                FbrStatus = ResolveRefundFbrStatus(sale),
                RefundedAmount = decimal.Round(sale.Amount, 2, MidpointRounding.AwayFromZero),
                RefundedAt = refundedAt,
                RefundedBy = currentUser.DisplayName,
                RefundReason = refundReason,
                InventoryReturned = request.ReturnToInventory
            };

            transactions[index] = updatedSale;
            updatedSaleDto = MapSale(updatedSale);

            return snapshot with
            {
                Products = updatedProducts,
                RecentTransactions = transactions
                    .OrderByDescending(item => item.OccurredAt)
                    .ToArray(),
                StockAdjustments = updatedAdjustments,
                DailyFigures = UpdateDailyFigures(snapshot.DailyFigures, -sale.Amount, -sale.GrossProfit, refundedAt),
                SalesTrend = UpdateSalesTrend(snapshot.SalesTrend, -sale.Amount, refundedAt),
                TopSelling = UpdateTopSelling(snapshot.TopSelling, refundLines, -1)
            };
        }, cancellationToken);

        return updatedSaleDto ?? throw new InvalidOperationException("Refund save ho gaya lekin updated sale load nahi hui.");
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
        IReadOnlyList<SaleLine> lines,
        int direction = 1)
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

            aggregate[line.Name] = (
                current.Item1 + (line.Quantity * direction),
                current.Item2 + (line.LineTotal * direction));
        }

        return aggregate
            .Where(item => item.Value.Units > 0 || item.Value.Revenue > 0)
            .Select(item => new TopSellingItem(item.Key, item.Value.Units, item.Value.Revenue))
            .OrderByDescending(item => item.Units)
            .ThenByDescending(item => item.Revenue)
            .Take(8)
            .ToArray();
    }

    private static SaleLine[] BuildSaleLines(
        IReadOnlyList<CartLine> cartLines,
        IReadOnlyList<Product> products)
    {
        return cartLines
            .Select(line =>
            {
                var product = products.FirstOrDefault(item => item.Id == line.ProductId && !item.IsArchived)
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
    }

    private static PaymentAllocation[] NormalizePaymentRequests(
        IReadOnlyList<PosPaymentLineRequestDto>? payments,
        bool allowEmpty,
        string? fallbackMethod = null,
        decimal fallbackAmount = 0)
    {
        var normalized = payments?
            .Where(payment => payment is not null && payment.Amount > 0)
            .Select(payment => new PaymentAllocation(
                NormalizePaymentMethod(payment.Method),
                decimal.Round(payment.Amount, 2, MidpointRounding.AwayFromZero),
                NormalizeNullableText(payment.ReferenceNo)))
            .ToArray()
            ?? Array.Empty<PaymentAllocation>();

        if (normalized.Length > 0)
        {
            return normalized;
        }

        if (allowEmpty)
        {
            return Array.Empty<PaymentAllocation>();
        }

        if (fallbackAmount <= 0)
        {
            throw new InvalidOperationException("Payment amount must be greater than zero.");
        }

        return
        [
            new PaymentAllocation(
                NormalizePaymentMethod(fallbackMethod),
                decimal.Round(fallbackAmount, 2, MidpointRounding.AwayFromZero))
        ];
    }

    private static string BuildPaymentMethodLabel(
        IReadOnlyList<PaymentAllocation>? payments,
        string? fallbackMethod)
    {
        var methods = (payments ?? Array.Empty<PaymentAllocation>())
            .Where(payment => payment.Amount > 0)
            .Select(payment => NormalizePaymentMethod(payment.Method))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (methods.Length == 0)
        {
            return NormalizePaymentMethod(fallbackMethod);
        }

        return methods.Length == 1 ? methods[0] : string.Join(" + ", methods);
    }

    private static string DetermineBookingStatus(decimal balanceAmount, decimal paidAmount)
    {
        if (balanceAmount <= 0)
        {
            return "Paid Pending Pickup";
        }

        return paidAmount > 0 ? "Advance Collected" : "Booked";
    }

    private static string DeterminePaymentStatus(decimal balanceAmount, decimal paidAmount)
    {
        if (balanceAmount <= 0)
        {
            return "Paid";
        }

        return paidAmount > 0 ? "Partially Paid" : "Unpaid";
    }

    private static string MergeNotes(string currentNotes, string? nextNote)
    {
        var normalizedCurrent = NormalizeText(currentNotes);
        var normalizedNext = NormalizeText(nextNote);

        if (string.IsNullOrWhiteSpace(normalizedNext))
        {
            return normalizedCurrent;
        }

        return string.IsNullOrWhiteSpace(normalizedCurrent)
            ? normalizedNext
            : $"{normalizedCurrent} | {normalizedNext}";
    }

    private static string NormalizePaymentMethod(string? method)
    {
        return string.IsNullOrWhiteSpace(method) ? "Cash" : method.Trim();
    }

    private static string ResolveRefundFbrStatus(SaleRecord sale)
    {
        return !string.IsNullOrWhiteSpace(sale.FbrInvoiceNumber)
            || sale.FbrStatus.Contains("Submitted", StringComparison.OrdinalIgnoreCase)
            || sale.FbrStatus.Contains("Reported", StringComparison.OrdinalIgnoreCase)
            ? "RefundPendingAdjustment"
            : "RefundedOffline";
    }

    private static bool IsRefundedTransaction(SaleRecord sale)
    {
        return sale.RefundedAmount > 0
            || sale.Status.Contains("Refund", StringComparison.OrdinalIgnoreCase)
            || string.Equals(sale.PaymentStatus, "Refunded", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static string? NormalizeNullableText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static void EnsureTenant(WorkspaceSnapshot snapshot, Guid tenantId)
    {
        if (snapshot.Tenant.Id != tenantId)
        {
            throw new InvalidOperationException("The current user does not belong to the requested tenant.");
        }
    }

    private static PosCheckoutReceiptDto MapReceipt(SaleRecord sale)
    {
        return new PosCheckoutReceiptDto(
            sale.Id,
            sale.ReferenceNo,
            sale.CustomerName,
            sale.PaymentMethod,
            sale.CashierName,
            sale.OccurredAt,
            (sale.Lines ?? Array.Empty<SaleLine>())
                .Select(line => new SaleLineDto(
                    line.ProductId,
                    line.Sku,
                    line.Name,
                    line.Quantity,
                    line.UnitPrice,
                    line.LineTotal))
                .ToArray(),
            new PosSummaryDto(
                sale.ItemCount,
                sale.Amount - sale.Tax + sale.Discount,
                sale.Discount,
                sale.Tax,
                sale.Amount),
            sale.ReceivedAmount,
            sale.ChangeAmount,
            sale.FbrStatus,
            sale.FbrInvoiceNumber,
            sale.PaidAmount,
            sale.BalanceAmount,
            sale.PaymentStatus,
            (sale.Payments ?? Array.Empty<PaymentAllocation>())
                .Select(payment => new PosPaymentLineDto(payment.Method, payment.Amount, payment.ReferenceNo))
                .ToArray());
    }

    private static SalesHistoryItemDto MapSale(SaleRecord sale)
    {
        var refundedAmount = decimal.Round(Math.Max(sale.RefundedAmount, 0), 2, MidpointRounding.AwayFromZero);
        var netAmount = decimal.Round(Math.Max(sale.Amount - refundedAmount, 0), 2, MidpointRounding.AwayFromZero);

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
            sale.FbrReportedAt,
            sale.PaidAmount,
            sale.BalanceAmount,
            sale.PaymentStatus,
            (sale.Payments ?? Array.Empty<PaymentAllocation>())
                .Select(payment => new PosPaymentLineDto(payment.Method, payment.Amount, payment.ReferenceNo))
                .ToArray(),
            netAmount,
            refundedAmount,
            sale.RefundedAt,
            sale.RefundedBy,
            sale.RefundReason,
            sale.InventoryReturned);
    }
}
