using OmniBusiness.Domain.Foundation;

namespace OmniBusiness.Application.Services;

internal static class ProductStateCalculator
{
    public static Product ApplyInventory(Product product, int inHand, int reserved)
    {
        var normalizedReserved = Math.Max(0, reserved);
        var normalizedInHand = Math.Max(0, inHand);
        var available = Math.Max(normalizedInHand - normalizedReserved, 0);
        var reorderLevel = Math.Max(product.ReorderLevel, 0);
        var isLowStock = available <= reorderLevel;

        var status = normalizedInHand <= 0
            ? "Out of Stock"
            : isLowStock
                ? "Low Stock"
                : "In Stock";

        return product with
        {
            InHand = normalizedInHand,
            Reserved = normalizedReserved,
            IsLowStock = isLowStock,
            Status = status
        };
    }
}
