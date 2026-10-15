using System.Text.RegularExpressions;
using FleetTrack.Core.Entities;
using FleetTrack.Web.Data.Conversions;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FleetTrack.Web.Data;

/// <param name="actorProvider">
/// Supplies the signed-in user for the audit log. Optional so tests and the EF command-line
/// tools can create the context without a web request; changes are then logged as "system".
/// </param>
public partial class ApplicationDbContext(
    DbContextOptions<ApplicationDbContext> options,
    IAuditActorProvider? actorProvider = null)
    : IdentityDbContext<ApplicationUser>(options)
{
    /// <summary>Switched off only by the development seeder, which would otherwise log hundreds of "created" rows.</summary>
    public bool AuditingEnabled { get; set; } = true;

    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<VehicleCategory> VehicleCategories => Set<VehicleCategory>();
    public DbSet<LicenceHolder> LicenceHolders => Set<LicenceHolder>();
    public DbSet<Driver> Drivers => Set<Driver>();
    public DbSet<VehicleDocument> VehicleDocuments => Set<VehicleDocument>();
    public DbSet<ServiceRecord> ServiceRecords => Set<ServiceRecord>();
    public DbSet<LicenceRenewal> LicenceRenewals => Set<LicenceRenewal>();
    public DbSet<RenewalStep> RenewalSteps => Set<RenewalStep>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<ReminderSent> RemindersSent => Set<ReminderSent>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        // Identity's own tables (users, roles, ...) are configured by the base class.
        base.OnModelCreating(builder);

        // Identity names its tables explicitly ("AspNetUsers"), which the naming convention
        // leaves alone. Rename them (ASP_NET_USERS) so every table follows the same Oracle style.
        foreach (var entity in builder.Model.GetEntityTypes())
        {
            var table = entity.GetTableName();
            if (table is not null && table.StartsWith("AspNet", StringComparison.Ordinal))
                entity.SetTableName(PascalToUpperSnake().Replace(table, "_").ToUpperInvariant());
        }

        builder.Entity<ApplicationUser>().Property(u => u.FullName).HasMaxLength(100);
        builder.Entity<ApplicationUser>().Property(u => u.ReceivesReminderEmails).HasDefaultValue(false);

        // Picks up every IEntityTypeConfiguration<T> class in the Data/Configurations folder.
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Store every enum as its name (e.g. 'ProxyId') instead of a number.
        configurationBuilder.Properties<Enum>().HaveConversion<string>().HaveMaxLength(30);

        // Store DateOnly as a real Oracle DATE rather than text.
        configurationBuilder.Properties<DateOnly>().HaveConversion<DateOnlyConverter>().HaveColumnType("DATE");
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        StampVehicles();

        var auditEntries = AuditingEnabled ? AuditEntryBuilder.Collect(ChangeTracker) : [];
        if (auditEntries.Count == 0)
            return await base.SaveChangesAsync(cancellationToken);

        var actor = actorProvider is null ? AuditActor.System : await actorProvider.GetActorAsync();

        // The change and its audit rows are saved in one transaction: either both are stored or neither is.
        // (Skipped when the caller already started a transaction, or for the in-memory test database.)
        var ownTransaction = Database.IsRelational() && Database.CurrentTransaction is null
            ? await Database.BeginTransactionAsync(cancellationToken)
            : null;
        await using (ownTransaction)
        {
            // First save the change itself, so new rows get their database-generated IDs...
            var result = await base.SaveChangesAsync(cancellationToken);

            // ...then record what happened.
            var now = DateTime.UtcNow;
            AuditLogs.AddRange(auditEntries.Select(entry => entry.ToAuditLog(actor, now)));
            await base.SaveChangesAsync(cancellationToken);

            if (ownTransaction is not null)
                await ownTransaction.CommitAsync(cancellationToken);
            return result;
        }
    }

    /// <summary>
    /// Synchronous saves are blocked because looking up the current user for the audit log is asynchronous.
    /// Always use SaveChangesAsync.
    /// </summary>
    public override int SaveChanges(bool acceptAllChangesOnSuccess) =>
        throw new NotSupportedException("Use SaveChangesAsync so the change is audited.");

    // Sets timestamps and a fresh concurrency stamp in one place, so no page can forget to.
    private void StampVehicles()
    {
        var now = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries<Vehicle>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAtUtc = now;
                entry.Entity.UpdatedAtUtc = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAtUtc = now;
                entry.Entity.ConcurrencyStamp = Guid.NewGuid().ToString();
            }
        }
    }

    // Matches the gap between a lowercase and an uppercase letter: "AspNetUsers" -> "Asp_Net_Users".
    [GeneratedRegex("(?<=[a-z])(?=[A-Z])")]
    private static partial Regex PascalToUpperSnake();
}
