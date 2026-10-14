using FleetTrack.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FleetTrack.Web.Data.Configurations;

public class LicenceRenewalConfiguration : IEntityTypeConfiguration<LicenceRenewal>
{
    public void Configure(EntityTypeBuilder<LicenceRenewal> builder)
    {
        builder.Property(r => r.AmountPaid).HasPrecision(10, 2);
        builder.Property(r => r.ReferenceNumber).HasMaxLength(50);
        builder.Property(r => r.Notes).HasMaxLength(2000);
        builder.Property(r => r.ProcessedBy).HasMaxLength(100).IsRequired();
        builder.Property(r => r.CreatedBy).HasMaxLength(256).IsRequired();

        builder.HasIndex(r => new { r.VehicleId, r.DatePurchased });

        builder.HasMany(r => r.Steps)
            .WithOne(s => s.LicenceRenewal)
            .HasForeignKey(s => s.LicenceRenewalId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
