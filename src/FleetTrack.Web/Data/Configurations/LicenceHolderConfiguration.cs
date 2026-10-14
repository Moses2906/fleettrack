using FleetTrack.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FleetTrack.Web.Data.Configurations;

public class LicenceHolderConfiguration : IEntityTypeConfiguration<LicenceHolder>
{
    public void Configure(EntityTypeBuilder<LicenceHolder> builder)
    {
        builder.Property(h => h.Name).HasMaxLength(150).IsRequired();
        builder.Property(h => h.Identifier).HasMaxLength(20).IsRequired();
        builder.HasIndex(h => new { h.HolderType, h.Identifier }).IsUnique();
    }
}
