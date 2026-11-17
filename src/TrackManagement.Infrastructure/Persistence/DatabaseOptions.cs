using System.ComponentModel.DataAnnotations;

namespace TrackManagement.Infrastructure.Persistence;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>Apply migrations and run seeders at startup (true in Development/compose; one instance on AWS).</summary>
    public bool MigrateOnStartup { get; init; }
}

public sealed class ConnectionStringOptions
{
    public const string SectionName = "ConnectionStrings";

    [Required(AllowEmptyStrings = false, ErrorMessage = "ConnectionStrings:Default is required (set ConnectionStrings__Default).")]
    public string Default { get; init; } = string.Empty;
}
