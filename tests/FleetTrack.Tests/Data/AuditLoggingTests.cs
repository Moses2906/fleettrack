using System.Text.Json;
using FleetTrack.Core.Entities;
using FleetTrack.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace FleetTrack.Tests.Data;

public class AuditLoggingTests
{
    private static readonly AuditActor Admin = new("user-1", "admin@fleettrack.local", "10.0.0.5");

    private static async Task<(ApplicationDbContext Db, Vehicle Vehicle, LicenceHolder Holder)> WithVehicleAsync()
    {
        var db = await TestDb.CreateAsync(Admin);
        var holder = new LicenceHolder { HolderType = HolderType.Individual, Name = "Test Person", Identifier = "8504125123086" };
        var vehicle = new Vehicle
        {
            RegistrationNumber = "CA 123-456", Make = "Toyota", Model = "Hilux", CategoryId = 2,
            LicenceHolder = holder, LicenceExpiryDate = new DateOnly(2026, 12, 31),
        };
        db.Vehicles.Add(vehicle);
        await db.SaveChangesAsync();
        db.AuditLogs.RemoveRange(db.AuditLogs); // start each test with an empty log
        await db.SaveChangesAsync();
        return (db, vehicle, holder);
    }

    [Fact]
    public async Task CreatingARecord_LogsCreateWithItsNewId()
    {
        var db = await TestDb.CreateAsync(Admin);
        db.LicenceHolders.Add(new LicenceHolder { Name = "Acme (Pty) Ltd", Identifier = "2020/123456/07" });
        await db.SaveChangesAsync();

        var log = await db.AuditLogs.SingleAsync();
        var holder = await db.LicenceHolders.SingleAsync();
        Assert.Equal(AuditAction.Create, log.Action);
        Assert.Equal(nameof(LicenceHolder), log.EntityType);
        Assert.Equal(holder.Id.ToString(), log.EntityId);
        Assert.Equal("admin@fleettrack.local", log.UserName);
        Assert.Equal("10.0.0.5", log.IpAddress);
    }

    [Fact]
    public async Task ChangingAVehicle_LogsOldAndNewValues()
    {
        var (db, vehicle, _) = await WithVehicleAsync();

        vehicle.LicenceExpiryDate = new DateOnly(2027, 12, 31);
        await db.SaveChangesAsync();

        var log = await db.AuditLogs.SingleAsync();
        Assert.Equal(AuditAction.Update, log.Action);
        using var details = JsonDocument.Parse(log.Details!);
        var change = details.RootElement.GetProperty("LicenceExpiryDate");
        Assert.Equal("2026-12-31", change.GetProperty("from").GetString());
        Assert.Equal("2027-12-31", change.GetProperty("to").GetString());
    }

    [Fact]
    public async Task ChangingAnIdNumber_NeverWritesTheIdNumberToTheLog()
    {
        var (db, _, holder) = await WithVehicleAsync();

        holder.Identifier = "9207300154087";
        await db.SaveChangesAsync();

        var log = await db.AuditLogs.SingleAsync();
        Assert.Contains("Identifier", log.Details);
        Assert.DoesNotContain("8504125123086", log.Details); // old value
        Assert.DoesNotContain("9207300154087", log.Details); // new value
    }

    [Fact]
    public async Task ChangingNotes_RecordsThatTheyChangedButNotTheText()
    {
        var (db, vehicle, _) = await WithVehicleAsync();

        vehicle.Notes = "Driver Thabo's cell is 082 555 0100";
        await db.SaveChangesAsync();

        var log = await db.AuditLogs.SingleAsync();
        Assert.Contains("Notes", log.Details);
        Assert.DoesNotContain("Thabo", log.Details);
    }

    [Fact]
    public async Task DeletingAPerson_LogsTheDeletionWithoutTheirDetails()
    {
        var (db, vehicle, holder) = await WithVehicleAsync();

        db.Vehicles.Remove(vehicle);
        db.LicenceHolders.Remove(holder);
        await db.SaveChangesAsync();

        var holderLog = await db.AuditLogs.SingleAsync(l => l.EntityType == nameof(LicenceHolder));
        Assert.Equal(AuditAction.Delete, holderLog.Action);
        Assert.Equal(holder.Id.ToString(), holderLog.EntityId);
        Assert.Null(holderLog.Details);

        // Vehicles hold no personal information, so the deleted values are kept as evidence.
        var vehicleLog = await db.AuditLogs.SingleAsync(l => l.EntityType == nameof(Vehicle));
        Assert.Contains("CA 123-456", vehicleLog.Details);
    }

    [Fact]
    public async Task SavingWithoutRealChanges_LogsNothing()
    {
        var (db, vehicle, _) = await WithVehicleAsync();

        vehicle.Make = "Toyota"; // same value as before
        await db.SaveChangesAsync();

        Assert.Empty(await db.AuditLogs.ToListAsync());
    }

    [Fact]
    public async Task WithoutASignedInUser_ChangesAreLoggedAsSystem()
    {
        var db = await TestDb.CreateAsync(actor: null);
        db.Drivers.Add(new Driver { FullName = "Sipho Ndlovu" });
        await db.SaveChangesAsync();

        Assert.Equal("system", (await db.AuditLogs.SingleAsync()).UserName);
    }
}
