using FleetTrack.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FleetTrack.Web.Data.Configurations;

public class RenewalStepConfiguration : IEntityTypeConfiguration<RenewalStep>
{
    public void Configure(EntityTypeBuilder<RenewalStep> builder)
    {
        builder.Property(s => s.Notes).HasMaxLength(1000);
        builder.Property(s => s.RecordedBy).HasMaxLength(256).IsRequired();
    }
}
