using Ardalis.Specification.EntityFrameworkCore;
using TrackManagement.Application.Common.Persistence;

namespace TrackManagement.Infrastructure.Persistence;

/// <summary>
/// Ardalis repository whose Add/Update/Delete only stage changes. Ardalis' base class calls
/// <see cref="SaveChangesAsync"/> inside every write; overriding it as a no-op lets the
/// Unit of Work behavior commit once per command, so multi-step commands are atomic.
/// </summary>
public sealed class EfRepository<T>(AppDbContext dbContext)
    : RepositoryBase<T>(dbContext), IRepository<T>, IReadRepository<T>
    where T : class
{
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(0);
}
