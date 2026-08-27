using OmniBusiness.Domain.Foundation;
using OmniBusiness.Infrastructure.Persistence;

namespace OmniBusiness.Infrastructure.Tests;

/// <summary>
/// Pure unit tests for the diff that keeps a snapshot save from rewriting every unbounded table
/// on each checkout. No database involved.
/// </summary>
public class WorkspaceSnapshotDifferTests
{
    private static readonly Guid TenantId = new("11111111-1111-1111-1111-111111111111");
    private static readonly DateTimeOffset When = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);

    private static Product Product(Guid id, string name = "Widget", decimal price = 10m, int inHand = 5) =>
        new(id, TenantId, "SKU-" + id.ToString("N")[..4], name, "General", price, inHand, 0,
            "Main", "Active", false, false, false, "VC");

    private static SaleRecord Sale(Guid id, IReadOnlyList<SaleLine> lines, decimal amount = 20m) =>
        new(id, TenantId, "S-" + id.ToString("N")[..4], "Walk-in", amount, 5m, "Completed", When,
            ItemCount: lines.Sum(line => line.Quantity), Lines: lines);

    private static SaleLine Line(Guid productId, int quantity = 2, decimal unitPrice = 10m) =>
        new(productId, "SKU", "Widget", quantity, unitPrice, quantity * unitPrice);

    // -- DiffById: the four fundamental transitions --------------------------------------------

    [Fact]
    public void DiffById_NewId_IsUpsert()
    {
        var existing = Product(Guid.NewGuid());
        var added = Product(Guid.NewGuid());

        var delta = WorkspaceSnapshotDiffer.DiffById(
            new[] { existing }, new[] { existing, added }, p => p.Id);

        Assert.Equal(new[] { added.Id }, delta.Upserts.Select(p => p.Id));
        Assert.Empty(delta.DeletedIds);
    }

    [Fact]
    public void DiffById_ChangedValue_IsUpsert()
    {
        var id = Guid.NewGuid();
        var before = Product(id, inHand: 5);
        var after = before with { InHand = 4 }; // e.g. sold one unit

        var delta = WorkspaceSnapshotDiffer.DiffById(new[] { before }, new[] { after }, p => p.Id);

        var upsert = Assert.Single(delta.Upserts);
        Assert.Equal(4, upsert.InHand);
        Assert.Empty(delta.DeletedIds);
    }

    [Fact]
    public void DiffById_UnchangedValue_IsNotUpsert()
    {
        var id = Guid.NewGuid();
        // Distinct-but-equal instances: value equality must treat them as unchanged.
        var before = Product(id);
        var after = Product(id);

        var delta = WorkspaceSnapshotDiffer.DiffById(new[] { before }, new[] { after }, p => p.Id);

        Assert.False(delta.HasChanges);
    }

    [Fact]
    public void DiffById_RemovedId_IsDeleted()
    {
        var kept = Product(Guid.NewGuid());
        var removed = Product(Guid.NewGuid());

        var delta = WorkspaceSnapshotDiffer.DiffById(
            new[] { kept, removed }, new[] { kept }, p => p.Id);

        Assert.Empty(delta.Upserts);
        Assert.Equal(new[] { removed.Id }, delta.DeletedIds);
    }

    // -- DiffById: seed-from-empty and drop-all edges ------------------------------------------

    [Fact]
    public void DiffById_NullPrevious_TreatsAllCurrentAsUpserts()
    {
        var a = Product(Guid.NewGuid());
        var b = Product(Guid.NewGuid());

        var delta = WorkspaceSnapshotDiffer.DiffById(null, new[] { a, b }, p => p.Id);

        Assert.Equal(2, delta.Upserts.Count);
        Assert.Empty(delta.DeletedIds);
    }

    [Fact]
    public void DiffById_NullCurrent_TreatsAllPreviousAsDeleted()
    {
        var a = Product(Guid.NewGuid());
        var b = Product(Guid.NewGuid());

        var delta = WorkspaceSnapshotDiffer.DiffById(new[] { a, b }, null, p => p.Id);

        Assert.Empty(delta.Upserts);
        Assert.Equal(2, delta.DeletedIds.Count);
    }

    [Fact]
    public void DiffById_BothEmpty_HasNoChanges()
    {
        var delta = WorkspaceSnapshotDiffer.DiffById<Product>(
            Array.Empty<Product>(), Array.Empty<Product>(), p => p.Id);

        Assert.False(delta.HasChanges);
    }

    // -- SalesEqual: the reference-identity trap that motivates the custom comparer -------------

    [Fact]
    public void SalesEqual_SameValuesButFreshLinesArray_ReturnsTrue()
    {
        var id = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var previous = Sale(id, new[] { Line(productId) });
        // Mimic WorkspaceSnapshotNormalization rebuilding the record with a brand-new Lines array.
        var reconstructed = previous with { Lines = new[] { Line(productId) } };

        Assert.NotSame(previous.Lines, reconstructed.Lines);
        Assert.True(WorkspaceSnapshotDiffer.SalesEqual(previous, reconstructed));
        // Guard: this is exactly the case default record equality gets wrong (compares Lines by ref).
        Assert.NotEqual(previous, reconstructed);
    }

    [Fact]
    public void SalesEqual_ChangedLineQuantity_ReturnsFalse()
    {
        var id = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var previous = Sale(id, new[] { Line(productId, quantity: 2) });
        var current = previous with { Lines = new[] { Line(productId, quantity: 3) } };

        Assert.False(WorkspaceSnapshotDiffer.SalesEqual(previous, current));
    }

    [Fact]
    public void SalesEqual_ChangedHeaderField_ReturnsFalse()
    {
        var id = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var previous = Sale(id, new[] { Line(productId) });
        var current = previous with { FbrStatus = "Reported", Lines = new[] { Line(productId) } };

        Assert.False(WorkspaceSnapshotDiffer.SalesEqual(previous, current));
    }

    [Fact]
    public void SalesEqual_DifferentLineCount_ReturnsFalse()
    {
        var id = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var previous = Sale(id, new[] { Line(productId) });
        var current = previous with { Lines = new[] { Line(productId), Line(Guid.NewGuid()) } };

        Assert.False(WorkspaceSnapshotDiffer.SalesEqual(previous, current));
    }

    // -- SalesEqual composed with DiffById: a checkout writes exactly one row ------------------

    [Fact]
    public void DiffById_WithSalesEqual_ReconstructedHistoryProducesNoUpserts()
    {
        var history = new[]
        {
            Sale(Guid.NewGuid(), new[] { Line(Guid.NewGuid()) }),
            Sale(Guid.NewGuid(), new[] { Line(Guid.NewGuid()) })
        };
        // Normalize rebuilds every SaleRecord (fresh Lines arrays) even when nothing changed.
        var afterNormalize = history.Select(sale => sale with { Lines = sale.Lines!.ToArray() }).ToArray();

        var delta = WorkspaceSnapshotDiffer.DiffById(
            history, afterNormalize, s => s.Id, WorkspaceSnapshotDiffer.SalesEqual);

        Assert.False(delta.HasChanges);
    }

    [Fact]
    public void DiffById_WithSalesEqual_OnlyNewSaleIsUpserted()
    {
        var existing = Sale(Guid.NewGuid(), new[] { Line(Guid.NewGuid()) });
        var newSale = Sale(Guid.NewGuid(), new[] { Line(Guid.NewGuid()) });

        var previous = new[] { existing };
        var current = new[]
        {
            existing with { Lines = existing.Lines!.ToArray() }, // reconstructed, unchanged
            newSale
        };

        var delta = WorkspaceSnapshotDiffer.DiffById(
            previous, current, s => s.Id, WorkspaceSnapshotDiffer.SalesEqual);

        var upsert = Assert.Single(delta.Upserts);
        Assert.Equal(newSale.Id, upsert.Id);
        Assert.Empty(delta.DeletedIds);
    }
}
