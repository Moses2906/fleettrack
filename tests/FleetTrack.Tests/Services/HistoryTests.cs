using System.Text;
using FleetTrack.Core.Entities;
using FleetTrack.Core.Rules;
using FleetTrack.Core.Security;
using FleetTrack.Web.Data.Seeding;
using FleetTrack.Web.History;
using Microsoft.EntityFrameworkCore;

namespace FleetTrack.Tests.Services;

public class HistoryTests
{
    private static readonly DateOnly Today = TestServices.Today;

    private static async Task<int> AddVehicleWithHistoryAsync(TestServices app)
    {
        await using var db = await app.CreateDbAsync();
        var vehicle = new Vehicle
        {
            RegistrationNumber = "CA 123-456", Make = "Toyota", Model = "Hilux", CategoryId = 2,
            LicenceHolder = new LicenceHolder { HolderType = HolderType.Individual, Name = "Thabo Mokoena", Identifier = "8504125123086" },
            LicenceExpiryDate = Today.AddDays(200),
        };
        vehicle.LicenceRenewals.Add(new LicenceRenewal { DatePurchased = Today.AddDays(-165), NewExpiryDate = Today.AddDays(200), AmountPaid = 900m, ProcessedBy = "N. Govender", CreatedBy = "seed" });
        vehicle.LicenceRenewals.Add(new LicenceRenewal { DatePurchased = Today.AddYears(-6), NewExpiryDate = Today.AddYears(-5), AmountPaid = 600m, ProcessedBy = "N. Govender", CreatedBy = "seed" });
        vehicle.ServiceRecords.Add(new ServiceRecord { ServiceDate = Today.AddDays(-30), OdometerKm = 90_000, Description = "Minor service", Workshop = "Bosveld", Cost = 2400m, CreatedBy = "seed" });
        vehicle.ServiceRecords.Add(new ServiceRecord { ServiceDate = Today.AddYears(-2), OdometerKm = 50_000, WorkType = WorkType.Tyres, Description = "Tyres", Workshop = "Tyre Corner", Cost = 4200m, CreatedBy = "seed" });
        db.Vehicles.Add(vehicle);
        await db.SaveChangesAsync();
        return vehicle.Id;
    }

    [Fact]
    public async Task History_CoversFiveYearsNewestFirst_WithRenewalsAndServices()
    {
        await using var app = await TestServices.CreateAsync(Roles.Staff);
        var id = await AddVehicleWithHistoryAsync(app);

        var history = (await app.Get<HistoryService>().GetAsync(id))!;

        Assert.Equal(Today.AddYears(-5), history.From);
        Assert.Equal(3, history.Entries.Count); // the six-year-old renewal is outside the window
        Assert.Equal(history.Entries.OrderByDescending(e => e.Date), history.Entries);
        Assert.Equal([HistoryEntryKind.Service, HistoryEntryKind.Renewal, HistoryEntryKind.Service], history.Entries.Select(e => e.Kind));
        Assert.Equal(900m, history.RenewalTotal);
        Assert.Equal(6600m, history.ServiceTotal);
    }

    [Fact]
    public async Task History_CanBeLimitedToOneYear()
    {
        await using var app = await TestServices.CreateAsync(Roles.Staff);
        var id = await AddVehicleWithHistoryAsync(app);

        var history = (await app.Get<HistoryService>().GetAsync(id, years: 1))!;

        Assert.Equal(2, history.Entries.Count);
    }

    [Fact]
    public async Task UnknownVehicle_ReturnsNull()
    {
        await using var app = await TestServices.CreateAsync(Roles.Staff);

        Assert.Null(await app.Get<HistoryService>().GetAsync(999));
    }

    [Fact]
    public async Task Pdf_IsAValidPdfDocument()
    {
        await using var app = await TestServices.CreateAsync(Roles.Staff);
        var id = await AddVehicleWithHistoryAsync(app);
        var history = (await app.Get<HistoryService>().GetAsync(id))!;

        var pdf = HistoryPdf.Generate(history, "Demo Staff", new DateTime(2026, 10, 15, 9, 30, 0));

        Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(pdf, 0, 5));
        Assert.True(pdf.Length > 5_000);
    }

    [Fact]
    public async Task Pdf_WorksForAFullSeededFleet()
    {
        // Every seeded vehicle has five years of renewals and services: a realistic multi-page report.
        await using var app = await TestServices.CreateAsync(Roles.Admin);
        await using (var db = await app.CreateDbAsync())
            await DevDataSeeder.SeedAsync(db, Today);

        await using var check = await app.CreateDbAsync();
        foreach (var vehicleId in await check.Vehicles.Select(v => v.Id).ToListAsync())
        {
            var history = (await app.Get<HistoryService>().GetAsync(vehicleId))!;
            Assert.NotEmpty(history.Entries);
            Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(HistoryPdf.Generate(history, "Demo Admin", DateTime.Now), 0, 5));
        }
    }

    // ----- History by month (whole fleet) -----

    private static async Task AddVehicleWithAsync(TestServices app, string registration, params (DateOnly Date, bool IsRenewal, decimal Amount)[] records)
    {
        await using var db = await app.CreateDbAsync();
        var holder = await db.LicenceHolders.FirstOrDefaultAsync()
            ?? db.LicenceHolders.Add(new LicenceHolder { Name = "Karoo Line Logistics", Identifier = "2014/208815/07" }).Entity;
        var vehicle = new Vehicle
        {
            RegistrationNumber = registration, Make = "Toyota", Model = "Hilux", CategoryId = 2, LicenceHolder = holder,
            LicenceExpiryDate = Today.AddDays(200),
        };
        var odometer = 10_000;
        foreach (var (date, isRenewal, amount) in records)
        {
            if (isRenewal)
                vehicle.LicenceRenewals.Add(new LicenceRenewal { DatePurchased = date, NewExpiryDate = date.AddYears(1), AmountPaid = amount, ProcessedBy = "N. Govender", CreatedBy = "test" });
            else
                vehicle.ServiceRecords.Add(new ServiceRecord { ServiceDate = date, OdometerKm = odometer += 5_000, Description = "Service", Workshop = "Bosveld", Cost = amount, CreatedBy = "test" });
        }
        db.Vehicles.Add(vehicle);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Month_ShowsEveryVehiclesRenewalsAndServicesInThatMonthOnly()
    {
        await using var app = await TestServices.CreateAsync(Roles.Staff);
        await AddVehicleWithAsync(app, "CA 100-001",
            (new DateOnly(2026, 8, 1), true, 900m),     // first day of August
            (new DateOnly(2026, 8, 31), false, 2400m),  // last day of August
            (new DateOnly(2026, 7, 31), false, 1000m)); // July: not included
        await AddVehicleWithAsync(app, "CA 100-002",
            (new DateOnly(2026, 8, 15), false, 4200m),
            (new DateOnly(2026, 9, 1), true, 650m));    // September: not included

        var month = await app.Get<HistoryService>().GetMonthAsync(new YearMonth(2026, 8));

        Assert.Equal(3, month.Entries.Count);
        Assert.Equal(2, month.VehicleCount);
        Assert.Equal(["CA 100-001", "CA 100-002", "CA 100-001"], month.Entries.Select(e => e.RegistrationNumber)); // newest first
        Assert.Equal(900m, month.RenewalTotal);
        Assert.Equal(6600m, month.ServiceTotal);
        Assert.Equal(new DateOnly(2026, 8, 1), month.From);
        Assert.Equal(new DateOnly(2026, 8, 31), month.To);
    }

    [Fact]
    public async Task AMonthWithNothing_IsEmpty()
    {
        await using var app = await TestServices.CreateAsync(Roles.Staff);
        await AddVehicleWithAsync(app, "CA 100-001", (new DateOnly(2026, 8, 10), true, 900m));

        Assert.Empty((await app.Get<HistoryService>().GetMonthAsync(new YearMonth(2025, 2))).Entries);
    }

    [Fact]
    public async Task MonthlyPdf_IsAValidPdfDocument()
    {
        await using var app = await TestServices.CreateAsync(Roles.Admin);
        await using (var db = await app.CreateDbAsync())
            await DevDataSeeder.SeedAsync(db, Today);

        var month = await app.Get<HistoryService>().GetMonthAsync(YearMonth.Of(Today).AddMonths(-1));
        var pdf = HistoryPdf.GenerateMonthly(month, "Demo Admin", new DateTime(2026, 10, 15, 9, 0, 0));

        Assert.NotEmpty(month.Entries);
        Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(pdf, 0, 5));
    }
}
