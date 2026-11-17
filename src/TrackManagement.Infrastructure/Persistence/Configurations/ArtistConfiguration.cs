using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrackManagement.Domain.Artists;

namespace TrackManagement.Infrastructure.Persistence.Configurations;

public sealed class ArtistConfiguration : IEntityTypeConfiguration<Artist>
{
    public const string NormalizedEmailIndex = "ix_artists_normalized_email";

    public void Configure(EntityTypeBuilder<Artist> builder)
    {
        builder.ToTable("artists");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        // varchar(n) counts characters (code points), matching the validators.
        builder.Property(a => a.Name).HasMaxLength(Artist.NameMaxLength).IsRequired();
        builder.Property(a => a.CountryCode).HasMaxLength(2).IsFixedLength().IsRequired();
        builder.Property(a => a.Email).HasMaxLength(Artist.EmailMaxLength).IsRequired();
        builder.Property(a => a.NormalizedEmail).HasMaxLength(Artist.EmailMaxLength).IsRequired();
        builder.Property(a => a.CreatedAtUtc).IsRequired();

        // The real guard against duplicate emails (the validator check can race).
        builder.HasIndex(a => a.NormalizedEmail).IsUnique().HasDatabaseName(NormalizedEmailIndex);
    }
}
