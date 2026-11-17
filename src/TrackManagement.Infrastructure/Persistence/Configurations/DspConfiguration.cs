using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrackManagement.Domain.Dsps;

namespace TrackManagement.Infrastructure.Persistence.Configurations;

public sealed class DspConfiguration : IEntityTypeConfiguration<Dsp>
{
    public void Configure(EntityTypeBuilder<Dsp> builder)
    {
        builder.ToTable("dsps");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();
        builder.Property(d => d.Name).HasMaxLength(Dsp.NameMaxLength).IsRequired();
        builder.HasIndex(d => d.Name).IsUnique().HasDatabaseName("ix_dsps_name");

        // Part of the migration: present after every migrate, same ids everywhere, never duplicated.
        builder.HasData(Dsp.Seed);
    }
}
