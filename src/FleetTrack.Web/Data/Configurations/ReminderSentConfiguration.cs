using FleetTrack.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FleetTrack.Web.Data.Configurations;

public class ReminderSentConfiguration : IEntityTypeConfiguration<ReminderSent>
{
    public void Configure(EntityTypeBuilder<ReminderSent> builder)
    {
        builder.Property(r => r.Milestone).HasMaxLength(20).IsRequired();

        // The database itself refuses a second row for the same reminder, as a last line of defence.
        builder.HasIndex(r => new { r.Kind, r.VehicleId, r.DueDate, r.Milestone }).IsUnique();

        builder.HasOne(r => r.Vehicle).WithMany().HasForeignKey(r => r.VehicleId).OnDelete(DeleteBehavior.Cascade);
    }
}
