using System.Text;
using FleetTrack.Core.Entities;
using FleetTrack.Core.Security;
using FleetTrack.Web.Documents;
using FleetTrack.Web.Services;
using Microsoft.EntityFrameworkCore;

namespace FleetTrack.Tests.Services;

public class DocumentServiceTests
{
    private static MemoryStream FakePdf(string text = "fake RC1") => new(Encoding.ASCII.GetBytes($"%PDF-1.7\n{text}\n%%EOF"));

    private static async Task<int> AddVehicleAsync(TestServices app, bool hasRc1 = false)
    {
        await using var db = await app.CreateDbAsync();
        var vehicle = new Vehicle
        {
            RegistrationNumber = "CA 123-456", Make = "Toyota", Model = "Hilux", CategoryId = 2,
            LicenceHolder = new LicenceHolder { Name = "Karoo Line Logistics", Identifier = "2014/208815/07" },
            LicenceExpiryDate = TestServices.Today.AddDays(100), HasRc1 = hasRc1,
        };
        db.Vehicles.Add(vehicle);
        await db.SaveChangesAsync();
        return vehicle.Id;
    }

    [Fact]
    public async Task Uploading_StoresTheFileAndTicksTheChecklist()
    {
        await using var app = await TestServices.CreateAsync(Roles.Admin);
        var vehicleId = await AddVehicleAsync(app);

        var result = await app.Get<DocumentService>().UploadAsync(vehicleId, DocumentType.Rc1, FakePdf(), "rc1 scan.pdf", "Certified copy");

        Assert.True(result.Succeeded, result.Error);
        await using var db = await app.CreateDbAsync();
        Assert.True((await db.Vehicles.FindAsync(vehicleId))!.HasRc1);
        var doc = await db.VehicleDocuments.SingleAsync();
        Assert.Equal("application/pdf", doc.ContentType);
        Assert.Equal("rc1 scan.pdf", doc.OriginalFileName);
        Assert.Equal("Certified copy", doc.Notes);
        Assert.Matches("^[0-9a-f]{32}$", doc.StoredFileName); // random name, not the uploaded one
        Assert.Single(app.StoredFiles());
    }

    [Fact]
    public async Task AWebPageRenamedToPdf_IsRejected()
    {
        await using var app = await TestServices.CreateAsync(Roles.Admin);
        var vehicleId = await AddVehicleAsync(app);
        var disguised = new MemoryStream(Encoding.ASCII.GetBytes("<html><script>alert('hi')</script></html>"));

        var result = await app.Get<DocumentService>().UploadAsync(vehicleId, DocumentType.Rc1, disguised, "rc1.pdf", null);

        Assert.False(result.Succeeded);
        Assert.Empty(app.StoredFiles());
    }

    [Fact]
    public async Task FilesOver10Mb_AreRejected()
    {
        await using var app = await TestServices.CreateAsync(Roles.Admin);
        var vehicleId = await AddVehicleAsync(app);
        var big = new byte[11 * 1024 * 1024];
        "%PDF-"u8.CopyTo(big);

        var result = await app.Get<DocumentService>().UploadAsync(vehicleId, DocumentType.Rc1, new MemoryStream(big), "big.pdf", null);

        Assert.False(result.Succeeded);
        Assert.Contains("10 MB", result.Error);
        Assert.Empty(app.StoredFiles());
    }

    [Fact]
    public async Task Replacing_KeepsOneCopyAndDeletesTheOldFile()
    {
        await using var app = await TestServices.CreateAsync(Roles.Admin);
        var vehicleId = await AddVehicleAsync(app);
        var service = app.Get<DocumentService>();

        await service.UploadAsync(vehicleId, DocumentType.Rc1, FakePdf("old"), "old.pdf", null);
        await service.UploadAsync(vehicleId, DocumentType.Rc1, FakePdf("new"), "new.pdf", null);

        await using var db = await app.CreateDbAsync();
        Assert.Equal("new.pdf", (await db.VehicleDocuments.SingleAsync()).OriginalFileName);
        Assert.Single(app.StoredFiles());
    }

    [Fact]
    public async Task Opening_ReturnsTheFileAndRecordsAViewInTheAuditLog()
    {
        await using var app = await TestServices.CreateAsync(Roles.Admin);
        var vehicleId = await AddVehicleAsync(app);
        var service = app.Get<DocumentService>();
        var id = (await service.UploadAsync(vehicleId, DocumentType.ProxyId, FakePdf(), "id.pdf", null)).Id!.Value;

        var file = await service.OpenAsync(id, download: false);

        Assert.NotNull(file);
        Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(file.Content));
        await using var db = await app.CreateDbAsync();
        var log = await db.AuditLogs.SingleAsync(l => l.Action == AuditAction.View);
        Assert.Equal(nameof(VehicleDocument), log.EntityType);
        Assert.Equal("admin@fleettrack.local", log.UserName);
    }

    [Fact]
    public async Task AFileChangedOnDisk_FailsItsIntegrityCheck()
    {
        await using var app = await TestServices.CreateAsync(Roles.Admin);
        var vehicleId = await AddVehicleAsync(app);
        var service = app.Get<DocumentService>();
        var id = (await service.UploadAsync(vehicleId, DocumentType.Rc1, FakePdf(), "rc1.pdf", null)).Id!.Value;

        await File.AppendAllTextAsync(app.StoredFiles().Single(), "tampered");

        Assert.Null(await service.OpenAsync(id, download: false));
    }

    [Fact]
    public async Task DeletingACopy_CanLeaveTheChecklistTickedForThePaperCopy()
    {
        await using var app = await TestServices.CreateAsync(Roles.Admin);
        var vehicleId = await AddVehicleAsync(app);
        var service = app.Get<DocumentService>();
        var id = (await service.UploadAsync(vehicleId, DocumentType.Rc1, FakePdf(), "rc1.pdf", null)).Id!.Value;

        await service.DeleteAsync(id, untick: false);

        await using var db = await app.CreateDbAsync();
        Assert.Empty(await db.VehicleDocuments.ToListAsync());
        Assert.Empty(app.StoredFiles());
        Assert.True((await db.Vehicles.FindAsync(vehicleId))!.HasRc1);
    }

    [Fact]
    public async Task DeletingAVehicle_AlsoDeletesItsStoredFiles()
    {
        await using var app = await TestServices.CreateAsync(Roles.Admin);
        var vehicleId = await AddVehicleAsync(app);
        await app.Get<DocumentService>().UploadAsync(vehicleId, DocumentType.ProxyId, FakePdf(), "id.pdf", null);

        await app.Get<VehicleService>().DeleteAsync(vehicleId);

        Assert.Empty(app.StoredFiles());
    }

    // ----- Staff permissions -----

    [Fact]
    public async Task Staff_CanViewAndDownloadDocuments()
    {
        await using var admin = await TestServices.CreateAsync(Roles.Admin);
        var vehicleId = await AddVehicleAsync(admin);
        var id = (await admin.Get<DocumentService>().UploadAsync(vehicleId, DocumentType.ProxyId, FakePdf(), "id.pdf", null)).Id!.Value;

        await using var staff = await TestServices.CreateAsync(Roles.Staff, shareWith: admin);
        var file = await staff.Get<DocumentService>().OpenAsync(id, download: true);

        Assert.NotNull(file);
        await using var db = await staff.CreateDbAsync();
        Assert.Equal("staff@fleettrack.local", (await db.AuditLogs.SingleAsync(l => l.Action == AuditAction.Download)).UserName);
    }

    [Fact]
    public async Task Staff_CannotUploadOrDeleteDocuments()
    {
        await using var app = await TestServices.CreateAsync(Roles.Staff);
        var vehicleId = await AddVehicleAsync(app);
        var service = app.Get<DocumentService>();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.UploadAsync(vehicleId, DocumentType.Rc1, FakePdf(), "rc1.pdf", null));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.DeleteAsync(1, untick: true));
        Assert.Empty(app.StoredFiles());
    }
}
