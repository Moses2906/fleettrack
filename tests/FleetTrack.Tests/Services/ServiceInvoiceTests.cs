using System.Text;
using FleetTrack.Core.Entities;
using FleetTrack.Core.Rules;
using FleetTrack.Core.Security;
using FleetTrack.Web.Services;
using Microsoft.EntityFrameworkCore;

namespace FleetTrack.Tests.Services;

public class ServiceInvoiceTests
{
    private static readonly DateOnly Today = TestServices.Today;

    private static MemoryStream FakeInvoice() => new(Encoding.ASCII.GetBytes("%PDF-1.7\nInvoice INV-1001\n%%EOF"));

    private static async Task<int> AddVehicleAsync(TestServices app)
    {
        await using var db = await app.CreateDbAsync();
        var vehicle = new Vehicle
        {
            RegistrationNumber = "CA 123-456", Make = "Toyota", Model = "Hilux", CategoryId = 2,
            LicenceHolder = new LicenceHolder { Name = "Karoo Line Logistics", Identifier = "2014/208815/07" },
            LicenceExpiryDate = Today.AddDays(100),
        };
        db.Vehicles.Add(vehicle);
        await db.SaveChangesAsync();
        return vehicle.Id;
    }

    private static ServiceForm YearlyService(int vehicleId, NextServiceBooking booking = NextServiceBooking.SameDateNextYear) => new()
    {
        VehicleId = vehicleId,
        ServiceDate = Today.ToDateTime(TimeOnly.MinValue),
        OdometerKm = 90_000,
        Description = "Yearly service",
        Workshop = "Bosveld Auto Repairs",
        Cost = 2400m,
        NextServiceDueDate = ServiceRules.NextServiceDate(booking, Today)?.ToDateTime(TimeOnly.MinValue),
    };

    [Fact]
    public async Task Staff_CanMarkAServiceDone_WithAnInvoice_AndBookNextYear()
    {
        await using var app = await TestServices.CreateAsync(Roles.Staff);
        var vehicleId = await AddVehicleAsync(app);

        var result = await app.Get<ServiceLogService>().CreateAsync(YearlyService(vehicleId), new InvoiceUpload(FakeInvoice(), "INV-1001.pdf"));

        Assert.True(result.Succeeded, result.Error);
        var row = (await app.Get<ServiceLogService>().ListAsync(vehicleId)).Single();
        Assert.Equal("INV-1001.pdf", row.InvoiceFileName);
        Assert.Equal(Today.AddYears(1), row.NextServiceDueDate);
        Assert.Single(app.StoredFiles());

        // The booking becomes the vehicle's next reminder.
        Assert.Equal(Today.AddYears(1), (await app.Get<ServiceLogService>().RemindersAsync()).Single().NextServiceDue);
    }

    [Fact]
    public async Task AFakeInvoice_IsRejected_AndNothingIsSaved()
    {
        await using var app = await TestServices.CreateAsync(Roles.Staff);
        var vehicleId = await AddVehicleAsync(app);
        var notAPdf = new MemoryStream(Encoding.ASCII.GetBytes("<html>not an invoice</html>"));

        var result = await app.Get<ServiceLogService>().CreateAsync(YearlyService(vehicleId), new InvoiceUpload(notAPdf, "invoice.pdf"));

        Assert.False(result.Succeeded);
        Assert.StartsWith("Invoice:", result.Error);
        Assert.Empty(await app.Get<ServiceLogService>().ListAsync(vehicleId));
        Assert.Empty(app.StoredFiles());
    }

    [Fact]
    public async Task OpeningAnInvoice_ReturnsItAndIsAudited()
    {
        await using var app = await TestServices.CreateAsync(Roles.Admin);
        var vehicleId = await AddVehicleAsync(app);
        var id = (await app.Get<ServiceLogService>().CreateAsync(YearlyService(vehicleId), new InvoiceUpload(FakeInvoice(), "INV-1001.pdf"))).Id!.Value;

        var file = await app.Get<ServiceLogService>().OpenInvoiceAsync(id, download: true);

        Assert.NotNull(file);
        Assert.Equal("application/pdf", file.ContentType);
        await using var db = await app.CreateDbAsync();
        Assert.Single(await db.AuditLogs.Where(l => l.EntityType == "ServiceInvoice" && l.Action == AuditAction.Download).ToListAsync());
    }

    [Fact]
    public async Task DeletingTheService_DeletesItsInvoiceFile()
    {
        await using var app = await TestServices.CreateAsync(Roles.Admin);
        var vehicleId = await AddVehicleAsync(app);
        var id = (await app.Get<ServiceLogService>().CreateAsync(YearlyService(vehicleId), new InvoiceUpload(FakeInvoice(), "INV-1001.pdf"))).Id!.Value;

        await app.Get<ServiceLogService>().DeleteAsync(id);

        Assert.Empty(app.StoredFiles());
    }

    [Fact]
    public async Task DeletingTheVehicle_DeletesItsInvoiceFiles()
    {
        await using var app = await TestServices.CreateAsync(Roles.Admin);
        var vehicleId = await AddVehicleAsync(app);
        await app.Get<ServiceLogService>().CreateAsync(YearlyService(vehicleId), new InvoiceUpload(FakeInvoice(), "INV-1001.pdf"));

        await app.Get<VehicleService>().DeleteAsync(vehicleId);

        Assert.Empty(app.StoredFiles());
    }

    [Fact]
    public async Task AdminCanAttachAnInvoiceLater_ButStaffCannot()
    {
        await using var admin = await TestServices.CreateAsync(Roles.Admin);
        var vehicleId = await AddVehicleAsync(admin);
        var id = (await admin.Get<ServiceLogService>().CreateAsync(YearlyService(vehicleId, NextServiceBooking.DontBook))).Id!.Value;

        await using var staff = await TestServices.CreateAsync(Roles.Staff, shareWith: admin);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            staff.Get<ServiceLogService>().AttachInvoiceAsync(id, new InvoiceUpload(FakeInvoice(), "late.pdf")));

        Assert.True((await admin.Get<ServiceLogService>().AttachInvoiceAsync(id, new InvoiceUpload(FakeInvoice(), "late.pdf"))).Succeeded);
        Assert.Equal("late.pdf", (await admin.Get<ServiceLogService>().ListAsync(vehicleId)).Single().InvoiceFileName);
    }
}
