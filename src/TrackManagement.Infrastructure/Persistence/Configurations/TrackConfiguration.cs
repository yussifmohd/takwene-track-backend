using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrackManagement.Domain.Tracks;

namespace TrackManagement.Infrastructure.Persistence.Configurations;

public sealed class TrackConfiguration : IEntityTypeConfiguration<Track>
{
    public const string IsrcIndex = "ix_tracks_isrc";

    public void Configure(EntityTypeBuilder<Track> builder)
    {
        builder.ToTable("tracks", table =>
        {
            // Defence in depth: the validators enforce these first; the database refuses anything that slips past.
            table.HasCheckConstraint("ck_tracks_isrc_format", "isrc ~ '^[A-Z]{2}[A-Z0-9]{3}[0-9]{7}$'");
            table.HasCheckConstraint("ck_tracks_genre", $"genre IN ({string.Join(", ", Genres.All.Select(g => $"'{g}'"))})");
            table.HasCheckConstraint("ck_tracks_status", $"status IN ({string.Join(", ", TrackStatusNames.All.Select(s => $"'{s}'"))})");
        });

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.Property(t => t.Title).HasMaxLength(Track.TitleMaxLength).IsRequired();
        builder.Property(t => t.Isrc).HasMaxLength(Isrc.Length).IsFixedLength().IsRequired();
        builder.Property(t => t.Genre).HasMaxLength(Genres.MaxLength).IsRequired();
        builder.Property(t => t.ReleaseDate).IsRequired();
        builder.Property(t => t.Status)
            .HasConversion(s => s.ToName(), s => TrackStatusNames.Parse(s))
            .HasMaxLength(12)
            .IsRequired();
        builder.Property(t => t.CreatedAtUtc).IsRequired();

        // Npgsql maps a uint row version to the xmin system column: no extra column, changes on every update.
        builder.Property(t => t.Version).IsRowVersion();

        builder.HasOne(t => t.Artist)
            .WithMany()
            .HasForeignKey(t => t.ArtistId)
            .OnDelete(DeleteBehavior.Restrict);

        // Deliveries belong to the track (aggregate child): loaded through the backing field.
        builder.HasMany(t => t.Distributions)
            .WithOne()
            .HasForeignKey(d => d.TrackId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(t => t.Distributions).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Ignore(t => t.AllowedNextStatuses);
        builder.Ignore(t => t.CanDistribute);

        // The real guard against duplicate ISRCs (the validator check can race).
        builder.HasIndex(t => t.Isrc).IsUnique().HasDatabaseName(IsrcIndex);
        builder.HasIndex(t => t.Status).HasDatabaseName("ix_tracks_status");
        // Default list order: newest release first, id as the stable tie-breaker.
        builder.HasIndex(t => new { t.ReleaseDate, t.Id }).IsDescending(true, false).HasDatabaseName("ix_tracks_release_date_id");
    }
}
