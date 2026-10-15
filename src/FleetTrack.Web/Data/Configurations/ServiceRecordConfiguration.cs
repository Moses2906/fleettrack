using FleetTrack.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FleetTrack.Web.Data.Configurations;

public class ServiceRecordConfiguration : IEntityTypeConfiguration<ServiceRecord>
{
    public void Configure(EntityTypeBuilder<ServiceRecord> builder)
    {
        builder.Property(s => s.Description).HasMaxLength(250).IsRequired();
        builder.Property(s => s.Workshop).HasMaxLength(100).IsRequired();
        builder.Property(s => s.Cost).HasPrecision(12, 2);
        builder.Property(s => s.Notes).HasMaxLength(2000);
        builder.Property(s => s.CreatedBy).HasMaxLength(256).IsRequired();

        builder.HasIndex(s => new { s.VehicleId, s.ServiceDate });

        builder.Property(s => s.InvoiceFileName).HasMaxLength(255);
        builder.Property(s => s.InvoiceStoredFileName).HasMaxLength(64);
        builder.Property(s => s.InvoiceContentType).HasMaxLength(50);
        builder.Property(s => s.InvoiceSha256).HasMaxLength(64);
        builder.Ignore(s => s.HasInvoice);
    }
}
