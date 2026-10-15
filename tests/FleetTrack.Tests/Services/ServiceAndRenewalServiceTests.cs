using FleetTrack.Core.Entities;
using FleetTrack.Core.Security;
using FleetTrack.Web.Services;
using Microsoft.EntityFrameworkCore;

namespace FleetTrack.Tests.Services;

public class ServiceAndRenewalServiceTests
{
    private static readonly DateOnly Today = TestServices.Today;

    private static async Task<int> AddVehicleAsync(TestServices app, DateOnly expiry)
    {
        await using var db = await app.CreateDbAsync();
        var vehicle = new Vehicle
        {
            RegistrationNumber = "CA 123-456", Make = "Toyota", Model = "Hilux", CategoryId = 2,
            LicenceHolder = new LicenceHolder { Name = "Karoo Line Logistics", Identifier = "2014/208815/07" },
            LicenceExpiryDate = expiry,
        };
        db.Vehicles.Add(vehicle);
        await db.SaveChangesAsync();
        return vehicle.Id;
    }

    private static RenewalForm Renewal(DateOnly newExpiry) => new()
    {
        DatePurchased = Today.ToDateTime(TimeOnly.MinValue),
        NewExpiryDate = newExpiry.ToDateTime(TimeOnly.MinValue),
        AmountPaid = 900m,
        ReferenceNumber = "LR2026123456",
        ProcessedBy = "N. Govender",
    };

    private static ServiceForm Service(int vehicleId, int odometer, DateOnly date, DateOnly? next = null) => new()
    {
        VehicleId = vehicleId,
        ServiceDate = date.ToDateTime(TimeOnly.MinValue),
        OdometerKm = odometer,
        Description = "Minor service",
        Workshop = "Bosveld Auto Repairs",
        Cost = 2400m,
        NextServiceDueDate = next?.ToDateTime(TimeOnly.MinValue),
    };

    private static async Task<DateOnly> ExpiryOfAsync(TestServices app, int vehicleId)
    {
        await using var db = await app.CreateDbAsync();
        return (await db.Vehicles.FindAsync(vehicleId))!.LicenceExpiryDate;
    }

    // ----- Renewals -----

    [Fact]
    public async Task SavingARenewal_UpdatesTheVehiclesExpiryDate()
    {
        await using var app = await TestServices.CreateAsync(Roles.Admin);
        var vehicleId = await AddVehicleAsync(app, expiry: Today.AddDays(-12)); // expired

        var result = await app.Get<RenewalService>().CreateAsync(vehicleId, Renewal(new DateOnly(2027, 10, 3)));

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(new DateOnly(2027, 10, 3), await ExpiryOfAsync(app, vehicleId));

        await using var db = await app.CreateDbAsync();
        var renewal = await db.LicenceRenewals.SingleAsync();
        Assert.Equal(Today.AddDays(-12), renewal.PreviousExpiryDate);
        Assert.Equal("admin@fleettrack.local", renewal.CreatedBy);
    }

    [Fact]
    public async Task SavingARenewal_IsAuditedAsAChangeToTheVehiclesExpiryDate()
    {
        await using var app = await TestServices.CreateAsync(Roles.Admin);
        var vehicleId = await AddVehicleAsync(app, expiry: new DateOnly(2026, 10, 31));

        await app.Get<RenewalService>().CreateAsync(vehicleId, Renewal(new DateOnly(2027, 10, 31)));

        await using var db = await app.CreateDbAsync();
        var log = await db.AuditLogs.SingleAsync(l => l.EntityType == nameof(Vehicle) && l.Action == AuditAction.Update);
        Assert.Contains("2026-10-31", log.Details);
        Assert.Contains("2027-10-31", log.Details);
    }

    [Fact]
    public async Task DeletingAMistakenRenewal_RestoresThePreviousExpiryDate()
    {
        await using var app = await TestServices.CreateAsync(Roles.Admin);
        var vehicleId = await AddVehicleAsync(app, expiry: new DateOnly(2026, 10, 31));
        var service = app.Get<RenewalService>();
        var id = (await service.CreateAsync(vehicleId, Renewal(new DateOnly(2027, 10, 31)))).Id!.Value;
        await service.AddStepAsync(id, new RenewalStepForm { StepType = RenewalStepType.DiskHandedToDriver, StepDate = Today.ToDateTime(TimeOnly.MinValue) });

        await service.DeleteAsync(id);

        Assert.Equal(new DateOnly(2026, 10, 31), await ExpiryOfAsync(app, vehicleId));
        await using var db = await app.CreateDbAsync();
        Assert.Empty(await db.RenewalSteps.ToListAsync());
    }

    [Fact]
    public async Task RenewalSteps_AreListedWithTheRenewal()
    {
        await using var app = await TestServices.CreateAsync(Roles.Admin);
        var vehicleId = await AddVehicleAsync(app, expiry: new DateOnly(2026, 10, 31));
        var service = app.Get<RenewalService>();
        var id = (await service.CreateAsync(vehicleId, Renewal(new DateOnly(2027, 10, 31)))).Id!.Value;

        await service.AddStepAsync(id, new RenewalStepForm { StepType = RenewalStepType.DiskFittedToVehicle, StepDate = Today.ToDateTime(TimeOnly.MinValue), Notes = "Fitted by Sipho" });

        var row = (await service.ListAsync(vehicleId)).Single();
        Assert.Equal(RenewalStepType.DiskFittedToVehicle, row.Steps.Single().StepType);
        Assert.Equal([RenewalStepType.DiskHandedToDriver, RenewalStepType.CopyFiled], row.OutstandingSteps);
    }

    [Fact]
    public async Task AStepDatedBeforeTheDiskWasBought_IsRejected()
    {
        await using var app = await TestServices.CreateAsync(Roles.Admin);
        var vehicleId = await AddVehicleAsync(app, expiry: new DateOnly(2026, 10, 31));
        var service = app.Get<RenewalService>();
        var id = (await service.CreateAsync(vehicleId, Renewal(new DateOnly(2027, 10, 31)))).Id!.Value;

        var result = await service.AddStepAsync(id, new RenewalStepForm { StepDate = Today.AddDays(-5).ToDateTime(TimeOnly.MinValue) });

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Staff_CannotRecordOrDeleteRenewals()
    {
        await using var app = await TestServices.CreateAsync(Roles.Staff);
        var vehicleId = await AddVehicleAsync(app, expiry: new DateOnly(2026, 10, 31));
        var service = app.Get<RenewalService>();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.CreateAsync(vehicleId, Renewal(new DateOnly(2027, 10, 31))));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.DeleteAsync(1));
        Assert.Equal(new DateOnly(2026, 10, 31), await ExpiryOfAsync(app, vehicleId));
    }

    // ----- Services -----

    [Fact]
    public async Task Staff_CanLogAService()
    {
        await using var app = await TestServices.CreateAsync(Roles.Staff);
        var vehicleId = await AddVehicleAsync(app, expiry: Today.AddDays(100));

        var result = await app.Get<ServiceLogService>().CreateAsync(Service(vehicleId, 85_000, Today));

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal("staff@fleettrack.local", (await app.Get<ServiceLogService>().ListAsync(vehicleId)).Single().CreatedBy);
    }

    [Fact]
    public async Task Staff_CannotEditOrDeleteAService()
    {
        await using var app = await TestServices.CreateAsync(Roles.Staff);
        var vehicleId = await AddVehicleAsync(app, expiry: Today.AddDays(100));
        var service = app.Get<ServiceLogService>();
        var id = (await service.CreateAsync(Service(vehicleId, 85_000, Today))).Id!.Value;

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.UpdateAsync(id, Service(vehicleId, 86_000, Today)));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.DeleteAsync(id));
    }

    [Fact]
    public async Task AnOdometerReadingThatGoesBackwards_IsRejected()
    {
        await using var app = await TestServices.CreateAsync(Roles.Admin);
        var vehicleId = await AddVehicleAsync(app, expiry: Today.AddDays(100));
        var service = app.Get<ServiceLogService>();
        await service.CreateAsync(Service(vehicleId, 85_000, Today.AddMonths(-6)));

        var result = await service.CreateAsync(Service(vehicleId, 8_500, Today));

        Assert.False(result.Succeeded);
        Assert.Contains("85,000", result.Error);
    }

    [Fact]
    public async Task Reminders_ComeFromEachVehiclesLatestService()
    {
        await using var app = await TestServices.CreateAsync(Roles.Admin);
        var vehicleId = await AddVehicleAsync(app, expiry: Today.AddDays(100));
        var service = app.Get<ServiceLogService>();
        await service.CreateAsync(Service(vehicleId, 80_000, Today.AddMonths(-7), next: Today.AddMonths(-1)));

        var overdue = (await service.RemindersAsync()).Single();
        Assert.Equal(Core.Rules.ServiceReminder.Overdue, overdue.ReminderOn(Today));

        // Logging the overdue service clears the old reminder and sets the next one.
        await service.CreateAsync(Service(vehicleId, 88_000, Today, next: Today.AddMonths(6)));
        var next = (await service.RemindersAsync()).Single();
        Assert.Equal(Today.AddMonths(6), next.NextServiceDue);
        Assert.Equal(Core.Rules.ServiceReminder.Scheduled, next.ReminderOn(Today));
    }
}
