using FleetTrack.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FleetTrack.Web.Data.Configurations;

public class VehicleDocumentConfiguration : IEntityTypeConfiguration<VehicleDocument>
{
    public void Configure(EntityTypeBuilder<VehicleDocument> builder)
    {
        builder.Property(d => d.OriginalFileName).HasMaxLength(255).IsRequired();
        builder.Property(d => d.StoredFileName).HasMaxLength(64).IsRequired();
        builder.Property(d => d.ContentType).HasMaxLength(50).IsRequired();
        builder.Property(d => d.Sha256).HasMaxLength(64).IsRequired();
        builder.Property(d => d.Notes).HasMaxLength(2000);
        builder.Property(d => d.UploadedBy).HasMaxLength(256).IsRequired();

        // One current copy of each document type per vehicle.
        builder.HasIndex(d => new { d.VehicleId, d.DocumentType }).IsUnique();
        builder.HasIndex(d => d.StoredFileName).IsUnique();
    }
}
