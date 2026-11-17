using Ardalis.Specification;

namespace TrackManagement.Application.Common.Persistence;

/// <summary>Read-only repository used by query handlers (no-tracking specifications).</summary>
public interface IReadRepository<T> : IReadRepositoryBase<T>
    where T : class;
