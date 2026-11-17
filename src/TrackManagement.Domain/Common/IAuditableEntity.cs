namespace TrackManagement.Domain.Common;

/// <summary>Entities with creation/update timestamps, filled in by the persistence layer.</summary>
public interface IAuditableEntity
{
    DateTime CreatedAtUtc { get; set; }

    DateTime? UpdatedAtUtc { get; set; }
}
