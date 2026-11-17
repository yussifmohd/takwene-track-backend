namespace TrackManagement.Domain.Common;

/// <summary>Base type for entities whose identifier is generated in code (not by the database).</summary>
public abstract class BaseEntity<TKey>
    where TKey : notnull
{
    public TKey Id { get; init; } = default!;
}
