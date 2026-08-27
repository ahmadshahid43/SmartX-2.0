using OmniBusiness.Domain.Foundation;

namespace OmniBusiness.Domain.Tests;

public sealed class FoundationSnapshotTests
{
    [Fact]
    public void Product_AvailableQuantity_IsDerivedFromInHandAndReserved()
    {
        var product = new Product(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "BEV-001",
            "Coca Cola 1.5L",
            "Grocery",
            250m,
            1200,
            150,
            "Main Warehouse",
            "In Stock",
            false,
            true,
            false,
            "COLA");

        var available = product.InHand - product.Reserved;

        Assert.Equal(1050, available);
    }
}
