using Ardalis.Specification;

namespace TrackManagement.Application.Common.Persistence;

/// <summary>
/// Write repository used by command handlers. Implementations MUST NOT save on their own:
/// changes are committed once per command by the Unit of Work pipeline behavior.
/// </summary>
public interface IRepository<T> : IRepositoryBase<T>
    where T : class;
