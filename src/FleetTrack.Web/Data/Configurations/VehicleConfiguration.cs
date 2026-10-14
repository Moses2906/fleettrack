using FleetTrack.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FleetTrack.Web.Data.Configurations;

public class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> builder)
    {
        builder.Property(v => v.RegistrationNumber).HasMaxLength(15).IsRequired();
        builder.Property(v => v.RegistrationKey).HasMaxLength(15).IsRequired();
        builder.HasIndex(v => v.RegistrationKey).IsUnique();

        builder.Property(v => v.Make).HasMaxLength(50).IsRequired();
        builder.Property(v => v.Model).HasMaxLength(50).IsRequired();
        builder.Property(v => v.Vin).HasMaxLength(17);
        builder.Property(v => v.Notes).HasMaxLength(2000);
        builder.Property(v => v.ConcurrencyStamp).HasMaxLength(36).IsConcurrencyToken();

        // Speeds up the dashboard's "expired / due soon" queries.
        builder.HasIndex(v => v.LicenceExpiryDate);

        // Restrict: you can't delete a category or licence holder that vehicles still use.
        builder.HasOne(v => v.Category)
            .WithMany(c => c.Vehicles)
            .HasForeignKey(v => v.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(v => v.LicenceHolder)
            .WithMany(h => h.Vehicles)
            .HasForeignKey(v => v.LicenceHolderId)
            .OnDelete(DeleteBehavior.Restrict);

        // SetNull: deleting a driver just leaves the vehicle unassigned.
        builder.HasOne(v => v.CurrentDriver)
            .WithMany(d => d.AssignedVehicles)
            .HasForeignKey(v => v.CurrentDriverId)
            .OnDelete(DeleteBehavior.SetNull);

        // Cascade: deleting a vehicle removes all of its records (POPIA "delete on request").
        builder.HasMany(v => v.Documents).WithOne(d => d.Vehicle).HasForeignKey(d => d.VehicleId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(v => v.ServiceRecords).WithOne(s => s.Vehicle).HasForeignKey(s => s.VehicleId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(v => v.LicenceRenewals).WithOne(r => r.Vehicle).HasForeignKey(r => r.VehicleId).OnDelete(DeleteBehavior.Cascade);

        builder.Ignore(v => v.HasAllDocuments);
    }
}
