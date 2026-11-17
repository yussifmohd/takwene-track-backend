using Microsoft.EntityFrameworkCore;

namespace TrackManagement.Infrastructure.Persistence;

/// <summary>
/// Arabic and other scripts are only stored faithfully in a UTF-8 database. Refuse to run against
/// anything else (e.g. SQL_ASCII or LATIN1) instead of silently corrupting names.
/// </summary>
public static class DatabaseEncodingCheck
{
    public const string Required = "UTF8";

    public static async Task EnsureUtf8Async(DbContext dbContext, CancellationToken cancellationToken = default)
    {
        if (!dbContext.Database.IsNpgsql())
        {
            return;
        }

        var encoding = await dbContext.Database
            .SqlQueryRaw<string>("SELECT current_setting('server_encoding') AS \"Value\"")
            .SingleAsync(cancellationToken);

        if (!string.Equals(encoding, Required, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Database encoding is {encoding}; {Required} is required for Arabic and other scripts. Recreate the database with ENCODING 'UTF8'.");
        }
    }
}
