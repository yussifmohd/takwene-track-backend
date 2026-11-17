using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrackManagement.Domain.Dsps;
using TrackManagement.Domain.Tracks;

namespace TrackManagement.Infrastructure.Persistence.Configurations;

public sealed class TrackDistributionConfiguration : IEntityTypeConfiguration<TrackDistribution>
{
    /// <summary>One delivery per track and platform; two racing first deliveries collide here.</summary>
    public const string PrimaryKey = "pk_track_distributions";

    public void Configure(EntityTypeBuilder<TrackDistribution> builder)
    {
        builder.ToTable("track_distributions", table =>
        {
            table.HasCheckConstraint(
                "ck_track_distributions_status",
                $"status IN ({string.Join(", ", DistributionStatusNames.All.Select(s => $"'{s}'"))})");
            // A pending delivery has no answer; an answered one always has its time.
            table.HasCheckConstraint("ck_track_distributions_answered", "(status = 'pending') = (answered_at_utc IS NULL)");
        });

        builder.HasKey(d => new { d.TrackId, d.DspId }).HasName(PrimaryKey);
        builder.Property(d => d.Status)
            .HasConversion(s => s.ToName(), s => DistributionStatusNames.Parse(s))
            .HasMaxLength(12)
            .IsRequired();
        builder.Property(d => d.SubmittedAtUtc).IsRequired();

        builder.HasOne(d => d.Dsp)
            .WithMany()
            .HasForeignKey(d => d.DspId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(d => d.DspId).HasDatabaseName("ix_track_distributions_dsp_id");
    }
}
