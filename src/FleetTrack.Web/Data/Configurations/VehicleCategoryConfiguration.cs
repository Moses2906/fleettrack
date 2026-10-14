using FleetTrack.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FleetTrack.Web.Data.Configurations;

public class VehicleCategoryConfiguration : IEntityTypeConfiguration<VehicleCategory>
{
    public void Configure(EntityTypeBuilder<VehicleCategory> builder)
    {
        builder.Property(c => c.Name).HasMaxLength(60).IsRequired();
        builder.HasIndex(c => c.Name).IsUnique();
        builder.Property(c => c.Description).HasMaxLength(250);

        // Categories are reference data every installation needs (production included),
        // so they're seeded through the migration rather than the development-only seeder.
        builder.HasData(
            new VehicleCategory { Id = 1, SortOrder = 1, Name = "Light passenger vehicle", Description = "Sedans, hatchbacks and SUVs" },
            new VehicleCategory { Id = 2, SortOrder = 2, Name = "Light delivery vehicle", Description = "Bakkies and panel vans up to 3 500 kg GVM" },
            new VehicleCategory { Id = 3, SortOrder = 3, Name = "Minibus", Description = "Up to 16 passengers" },
            new VehicleCategory { Id = 4, SortOrder = 4, Name = "Bus", Description = "More than 16 passengers" },
            new VehicleCategory { Id = 5, SortOrder = 5, Name = "Medium goods vehicle", Description = "3 500 kg to 16 000 kg GVM" },
            new VehicleCategory { Id = 6, SortOrder = 6, Name = "Heavy goods vehicle", Description = "Over 16 000 kg GVM" },
            new VehicleCategory { Id = 7, SortOrder = 7, Name = "Motorcycle", Description = "Motorcycles and scooters" },
            new VehicleCategory { Id = 8, SortOrder = 8, Name = "Trailer", Description = "Trailers and caravans" });
    }
}
