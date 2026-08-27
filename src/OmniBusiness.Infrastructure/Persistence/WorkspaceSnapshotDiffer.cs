using OmniBusiness.Domain.Foundation;

namespace OmniBusiness.Infrastructure.Persistence;

/// <summary>
/// The result of diffing one keyed collection between two workspace snapshots:
/// the rows that must be inserted-or-updated and the ids that must be deleted.
/// </summary>
public sealed record CollectionDelta<T>(IReadOnlyList<T> Upserts, IReadOnlyList<Guid> DeletedIds)
{
    public bool HasChanges => Upserts.Count > 0 || DeletedIds.Count > 0;

    public static CollectionDelta<T> Empty { get; } =
        new(Array.Empty<T>(), Array.Empty<Guid>());
}

/// <summary>
/// Pure (no I/O) diffing used by <see cref="PostgresWorkspaceRepository"/> so that a
/// snapshot save writes only the rows that actually changed instead of rewriting every
/// unbounded table (sales history in particular) on every checkout. Kept free of any
/// database dependency so it can be unit-tested directly.
/// </summary>
public static class WorkspaceSnapshotDiffer
{
    /// <summary>
    /// Diffs two keyed collections. An item is an upsert when its id is new or when its
    /// value differs from the previous version; an id present only in <paramref name="previous"/>
    /// is a deletion. Value comparison defaults to record equality; pass
    /// <paramref name="valuesEqual"/> to override it (see <see cref="SalesEqual"/>).
    /// </summary>
    public static CollectionDelta<T> DiffById<T>(
        IReadOnlyList<T>? previous,
        IReadOnlyList<T>? current,
        Func<T, Guid> keySelector,
        Func<T, T, bool>? valuesEqual = null)
    {
        ArgumentNullException.ThrowIfNull(keySelector);
        valuesEqual ??= static (left, right) => EqualityComparer<T>.Default.Equals(left, right);

        var previousList = previous ?? Array.Empty<T>();
        var currentList = current ?? Array.Empty<T>();

        // Last-wins mirrors WorkspaceSnapshotNormalization, which de-duplicates by id with GroupBy(...).Last().
        var previousById = new Dictionary<Guid, T>(previousList.Count);
        foreach (var item in previousList)
        {
            previousById[keySelector(item)] = item;
        }

        var upserts = new List<T>();
        var currentIds = new HashSet<Guid>(currentList.Count);
        foreach (var item in currentList)
        {
            var id = keySelector(item);
            currentIds.Add(id);

            if (!previousById.TryGetValue(id, out var existing) || !valuesEqual(existing, item))
            {
                upserts.Add(item);
            }
        }

        var deletedIds = new List<Guid>();
        foreach (var id in previousById.Keys)
        {
            if (!currentIds.Contains(id))
            {
                deletedIds.Add(id);
            }
        }

        return new CollectionDelta<T>(upserts, deletedIds);
    }

    /// <summary>
    /// Value equality for sales that ignores the *reference identity* of
    /// <see cref="SaleRecord.Lines"/>. <see cref="WorkspaceSnapshotNormalization.Normalize"/>
    /// reconstructs every <see cref="SaleRecord"/> (with a brand-new <c>Lines</c> array) on
    /// every save, so the compiler-synthesized record equality — which compares <c>Lines</c>
    /// by reference — would flag every historical sale as changed and rewrite the entire
    /// sales table on each checkout. Comparing the scalar header plus the line values by
    /// sequence keeps a checkout to a single new-row insert.
    /// </summary>
    public static bool SalesEqual(SaleRecord previous, SaleRecord current)
    {
        if (ReferenceEquals(previous, current))
        {
            return true;
        }

        if (previous is null || current is null)
        {
            return false;
        }

        // Compare every header field by value (Lines nulled on both sides), then the lines by value.
        if ((previous with { Lines = null }) != (current with { Lines = null }))
        {
            return false;
        }

        var previousLines = previous.Lines ?? Array.Empty<SaleLine>();
        var currentLines = current.Lines ?? Array.Empty<SaleLine>();
        return previousLines.SequenceEqual(currentLines);
    }
}
