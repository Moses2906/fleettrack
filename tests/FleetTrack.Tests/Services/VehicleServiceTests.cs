using FleetTrack.Core.Entities;
using FleetTrack.Core.Security;
using FleetTrack.Web.Services;
using Microsoft.EntityFrameworkCore;

namespace FleetTrack.Tests.Services;

public class VehicleServiceTests
{
    private static readonly DateOnly Today = TestServices.Today;

    /// <summary>Adds a holder and a vehicle directly to the database and returns the vehicle's ID.</summary>
    private static async Task<int> AddVehicleAsync(TestServices app, string registration = "CA 123-456", int expiresInDays = 200,
        bool allDocuments = true)
    {
        await using var db = await app.CreateDbAsync();
        var holder = await db.LicenceHolders.FirstOrDefaultAsync()
            ?? db.LicenceHolders.Add(new LicenceHolder { Name = "Karoo Line Logistics", Identifier = "2014/208815/07" }).Entity;
        var vehicle = new Vehicle
        {
            RegistrationNumber = registration, Make = "Toyota", Model = "Hilux", CategoryId = 2, LicenceHolder = holder,
            LicenceExpiryDate = Today.AddDays(expiresInDays),
            HasRc1 = true, HasBrnc = true, HasProxyLetter = true, HasProxyId = allDocuments,
        };
        db.Vehicles.Add(vehicle);
        await db.SaveChangesAsync();
        return vehicle.Id;
    }

    private static VehicleForm ValidForm(int holderId, string registration = "ND 304-118") => new()
    {
        RegistrationNumber = registration, Make = "Ford", Model = "Ranger", Year = 2023,
        CategoryId = 2, LicenceHolderId = holderId, LicenceExpiryDate = new DateTime(2027, 8, 11),
    };

    // ----- Permissions are enforced by the service, not just the UI -----

    [Fact]
    public async Task Staff_CanListVehicles()
    {
        await using var app = await TestServices.CreateAsync(Roles.Staff);
        await AddVehicleAsync(app);

        Assert.Single(await app.Get<VehicleService>().ListAsync());
    }

    [Fact]
    public async Task Staff_CannotDeleteAVehicle_EvenByCallingTheServiceDirectly()
    {
        await using var app = await TestServices.CreateAsync(Roles.Staff);
        var id = await AddVehicleAsync(app);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => app.Get<VehicleService>().DeleteAsync(id));

        await using var db = await app.CreateDbAsync();
        Assert.True(await db.Vehicles.AnyAsync(v => v.Id == id));
    }

    [Fact]
    public async Task Staff_CannotAddOrEditVehicles()
    {
        await using var app = await TestServices.CreateAsync(Roles.Staff);
        var id = await AddVehicleAsync(app);
        var service = app.Get<VehicleService>();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.CreateAsync(ValidForm(1)));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.UpdateAsync(id, ValidForm(1)));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.SetDocumentHeldAsync(id, DocumentType.Rc1, false));
    }

    [Fact]
    public async Task Staff_CannotDeleteDriversOrLicenceHolders()
    {
        await using var app = await TestServices.CreateAsync(Roles.Staff);
        var service = app.Get<ReferenceDataService>();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.DeleteDriverAsync(1));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.DeleteLicenceHolderAsync(1));
    }

    [Fact]
    public async Task Admin_CanDeleteAVehicle()
    {
        await using var app = await TestServices.CreateAsync(Roles.Admin);
        var id = await AddVehicleAsync(app);

        Assert.True((await app.Get<VehicleService>().DeleteAsync(id)).Succeeded);

        await using var db = await app.CreateDbAsync();
        Assert.False(await db.Vehicles.AnyAsync());
    }

    // ----- Validation happens on the server too -----

    [Fact]
    public async Task Create_RejectsTheSamePlateWithDifferentSpacing()
    {
        await using var app = await TestServices.CreateAsync(Roles.Admin);
        await AddVehicleAsync(app, "CA 123-456");

        var result = await app.Get<VehicleService>().CreateAsync(ValidForm(1, "ca123456"));

        Assert.False(result.Succeeded);
        Assert.Contains("already exists", result.Error);
    }

    [Fact]
    public async Task Create_RejectsAnInvalidRegistrationNumber()
    {
        await using var app = await TestServices.CreateAsync(Roles.Admin);
        await AddVehicleAsync(app);

        var result = await app.Get<VehicleService>().CreateAsync(ValidForm(1, "<script>"));

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Create_SavesANormalisedRegistrationNumber()
    {
        await using var app = await TestServices.CreateAsync(Roles.Admin);
        await AddVehicleAsync(app);

        var result = await app.Get<VehicleService>().CreateAsync(ValidForm(1, "  nd 304 - 118 "));

        Assert.True(result.Succeeded, result.Error);
        await using var db = await app.CreateDbAsync();
        Assert.Equal("ND 304-118", (await db.Vehicles.FindAsync(result.Id))!.RegistrationNumber);
    }

    [Fact]
    public async Task Update_WithAStaleForm_DoesNotOverwriteSomeoneElsesChanges()
    {
        await using var app = await TestServices.CreateAsync(Roles.Admin);
        var id = await AddVehicleAsync(app);
        var service = app.Get<VehicleService>();

        // Two admins open the same vehicle...
        var first = (await service.GetFormAsync(id))!;
        var second = (await service.GetFormAsync(id))!;

        // ...the first saves...
        first.Make = "Isuzu";
        Assert.True((await service.UpdateAsync(id, first)).Succeeded);

        // ...so the second, still holding the old version, is told to reload.
        second.Make = "Nissan";
        var result = await service.UpdateAsync(id, second);

        Assert.False(result.Succeeded);
        Assert.Contains("Someone else changed", result.Error);
    }

    [Fact]
    public async Task TickingAChecklistItem_UpdatesTheVehicle()
    {
        await using var app = await TestServices.CreateAsync(Roles.Admin);
        var id = await AddVehicleAsync(app, allDocuments: false);

        await app.Get<VehicleService>().SetDocumentHeldAsync(id, DocumentType.ProxyId, true);

        await using var db = await app.CreateDbAsync();
        Assert.True((await db.Vehicles.FindAsync(id))!.HasAllDocuments);
    }

    [Fact]
    public async Task LicenceHolder_WithInvalidIdNumber_IsRejected()
    {
        await using var app = await TestServices.CreateAsync(Roles.Admin);

        var result = await app.Get<ReferenceDataService>().SaveLicenceHolderAsync(null,
            new LicenceHolderForm { HolderType = HolderType.Individual, Name = "Test Person", Identifier = "8504125123087" });

        Assert.False(result.Succeeded);
    }

    // ----- Dashboard -----

    [Fact]
    public async Task Dashboard_CountsExpiredDueSoonAndMissingDocuments()
    {
        await using var app = await TestServices.CreateAsync(Roles.Staff);
        await AddVehicleAsync(app, "CA 100-001", expiresInDays: -1);                      // expired yesterday
        await AddVehicleAsync(app, "CA 100-002", expiresInDays: 0);                       // expires today: due, not expired
        await AddVehicleAsync(app, "CA 100-003", expiresInDays: 30);                      // last day of "due soon"
        await AddVehicleAsync(app, "CA 100-004", expiresInDays: 31, allDocuments: false); // valid, but missing a document

        var summary = await app.Get<VehicleService>().GetDashboardAsync();

        Assert.Equal(4, summary.TotalVehicles);
        Assert.Equal(1, summary.Expired);
        Assert.Equal(2, summary.DueSoon);
        Assert.Equal(1, summary.MissingDocuments);
        Assert.Equal(["CA 100-001", "CA 100-002", "CA 100-003"], summary.LicenceAttention.Select(v => v.RegistrationNumber));
    }
}
