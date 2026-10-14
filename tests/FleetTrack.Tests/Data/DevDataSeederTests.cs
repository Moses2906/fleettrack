using FleetTrack.Core.Rules;
using FleetTrack.Web.Data;
using FleetTrack.Web.Data.Seeding;
using Microsoft.EntityFrameworkCore;

namespace FleetTrack.Tests.Data;

/// <summary>
/// Runs the seeder against EF Core's in-memory database, so the demo data can be checked
/// without Oracle running.
/// </summary>
public class DevDataSeederTests
{
    private static readonly DateOnly Today = new(2026, 10, 14);

    private static async Task<ApplicationDbContext> SeededContextAsync()
    {
        var db = await TestDb.CreateAsync();
        await DevDataSeeder.SeedAsync(db, Today);
        return db;
    }

    [Fact]
    public async Task Seeds_TwentyVehiclesWithValidRegistrations()
    {
        await using var db = await SeededContextAsync();

        var vehicles = await db.Vehicles.ToListAsync();

        Assert.Equal(20, vehicles.Count);
        Assert.All(vehicles, v => Assert.True(RegistrationNumbers.IsValid(v.RegistrationNumber), v.RegistrationNumber));
        Assert.Equal(20, vehicles.Select(v => v.RegistrationKey).Distinct().Count());
    }

    [Fact]
    public async Task Seeds_AMixOfLicenceStatuses()
    {
        await using var db = await SeededContextAsync();

        var statuses = (await db.Vehicles.ToListAsync()).Select(v => v.GetLicenceStatus(Today)).ToList();

        Assert.Contains(LicenceStatus.Expired, statuses);
        Assert.Contains(LicenceStatus.DueSoon, statuses);
        Assert.Contains(LicenceStatus.Valid, statuses);
    }

    [Fact]
    public async Task LatestRenewal_MatchesEachVehiclesExpiryDate()
    {
        await using var db = await SeededContextAsync();

        var vehicles = await db.Vehicles.Include(v => v.LicenceRenewals).ToListAsync();

        Assert.All(vehicles, v =>
        {
            Assert.NotEmpty(v.LicenceRenewals);
            var latest = v.LicenceRenewals.MaxBy(r => r.DatePurchased)!;
            Assert.Equal(v.LicenceExpiryDate, latest.NewExpiryDate);
        });
    }

    [Fact]
    public async Task History_StaysWithinFiveYearsAndNotInTheFuture()
    {
        await using var db = await SeededContextAsync();

        var services = await db.ServiceRecords.ToListAsync();
        var renewals = await db.LicenceRenewals.ToListAsync();

        Assert.NotEmpty(services);
        Assert.All(services, s => Assert.InRange(s.ServiceDate, Today.AddYears(-5), Today));
        Assert.All(renewals, r => Assert.InRange(r.DatePurchased, Today.AddYears(-5), Today));
    }

    [Fact]
    public async Task DemoData_IsNotWrittenToTheAuditLog()
    {
        await using var db = await SeededContextAsync();

        Assert.Empty(await db.AuditLogs.ToListAsync());
    }

    [Fact]
    public async Task RunningTwice_DoesNotDuplicateData()
    {
        await using var db = await SeededContextAsync();

        await DevDataSeeder.SeedAsync(db, Today);

        Assert.Equal(20, await db.Vehicles.CountAsync());
    }
}
