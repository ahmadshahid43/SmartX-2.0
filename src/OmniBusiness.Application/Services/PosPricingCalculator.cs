using OmniBusiness.Application.Contracts;
using OmniBusiness.Domain.Foundation;

namespace OmniBusiness.Application.Services;

internal static class PosPricingCalculator
{
    private const decimal DiscountThreshold = 3m;
    private const decimal FixedDiscount = 500m;
    private const decimal DefaultTaxRatePercent = 17m;

    public static PosSummaryDto BuildSummary(
        IEnumerable<CartLine> cartLines,
        decimal? taxRatePercent = null,
        bool taxExempt = false)
    {
        var lines = cartLines.ToArray();
        var itemCount = lines.Sum(line => line.Quantity);
        var subtotal = lines.Sum(line => line.Quantity * line.UnitPrice);
        var discount = itemCount >= DiscountThreshold ? FixedDiscount : 0m;
        var taxableAmount = Math.Max(subtotal - discount, 0m);
        var normalizedTaxRatePercent = decimal.Clamp(taxRatePercent ?? DefaultTaxRatePercent, 0m, 100m);
        var tax = taxExempt
            ? 0m
            : decimal.Round(taxableAmount * normalizedTaxRatePercent / 100m, 2, MidpointRounding.AwayFromZero);

        return new PosSummaryDto(
            itemCount,
            subtotal,
            discount,
            tax,
            taxableAmount + tax);
    }
}
