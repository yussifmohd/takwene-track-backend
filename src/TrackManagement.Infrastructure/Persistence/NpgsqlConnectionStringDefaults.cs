using System.Data.Common;
using Npgsql;

namespace TrackManagement.Infrastructure.Persistence;

/// <summary>
/// Safe defaults applied to every PostgreSQL connection string. Npgsql tries GSS (Kerberos)
/// encryption by default on Linux; the Alpine runtime has no Kerberos library, which logs
/// "Cannot load library libgssapi_krb5.so.2" on every start. We never use GSS, so it is
/// disabled unless the connection string sets the mode explicitly.
/// </summary>
public static class NpgsqlConnectionStringDefaults
{
    public static string Apply(string connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        if (!HasKey(connectionString, "gssencryptionmode"))
        {
            builder.GssEncryptionMode = GssEncryptionMode.Disable;
        }

        return builder.ConnectionString;
    }

    private static bool HasKey(string connectionString, string normalizedKey)
    {
        var raw = new DbConnectionStringBuilder { ConnectionString = connectionString };
        return raw.Keys.Cast<string>()
            .Any(k => string.Equals(k.Replace(" ", string.Empty, StringComparison.Ordinal), normalizedKey, StringComparison.OrdinalIgnoreCase));
    }
}
