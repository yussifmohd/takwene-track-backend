namespace TrackManagement.Application.Common.Persistence;

/// <summary>Commits all pending changes of the current request in one database round trip.</summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
