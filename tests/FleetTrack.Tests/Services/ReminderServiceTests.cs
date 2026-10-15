using FleetTrack.Core.Entities;
using FleetTrack.Core.Rules;
using FleetTrack.Core.Security;
using FleetTrack.Web.Data;
using FleetTrack.Web.Reminders;
using Microsoft.EntityFrameworkCore;

namespace FleetTrack.Tests.Services;

public class ReminderServiceTests
{
    private static readonly DateOnly Today = TestServices.Today;

    private static async Task<int> AddVehicleAsync(TestServices app, string registration, int diskExpiresInDays, int? serviceDueInDays = null)
    {
        await using var db = await app.CreateDbAsync();
        var holder = await db.LicenceHolders.FirstOrDefaultAsync()
            ?? db.LicenceHolders.Add(new LicenceHolder { Name = "Karoo Line Logistics", Identifier = "2014/208815/07" }).Entity;
        var vehicle = new Vehicle
        {
            RegistrationNumber = registration, Make = "Toyota", Model = "Hilux", CategoryId = 2, LicenceHolder = holder,
            LicenceExpiryDate = Today.AddDays(diskExpiresInDays),
        };
        if (serviceDueInDays is { } days)
            vehicle.ServiceRecords.Add(new ServiceRecord
            {
                ServiceDate = Today.AddMonths(-6), OdometerKm = 80_000, Description = "Minor service", Workshop = "Bosveld",
                CreatedBy = "test", NextServiceDueDate = Today.AddDays(days),
            });
        db.Vehicles.Add(vehicle);
        await db.SaveChangesAsync();
        return vehicle.Id;
    }

    private static async Task AddUserAsync(TestServices app, string email, bool reminders, bool active = true)
    {
        await using var db = await app.CreateDbAsync();
        db.Users.Add(new ApplicationUser
        {
            UserName = email, Email = email, FullName = email, EmailConfirmed = true, IsActive = active, ReceivesReminderEmails = reminders,
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task OneDigest_GoesToEachOptedInActiveUser()
    {
        await using var app = await TestServices.CreateAsync(Roles.Admin);
        await AddVehicleAsync(app, "CA 100-001", diskExpiresInDays: 5);
        await AddUserAsync(app, "manager@test", reminders: true);
        await AddUserAsync(app, "clerk@test", reminders: true);
        await AddUserAsync(app, "quiet@test", reminders: false);
        await AddUserAsync(app, "left@test", reminders: true, active: false);

        var result = await app.Get<ReminderService>().RunAsync();

        Assert.Equal(ReminderRunOutcome.Sent, result.Outcome);
        Assert.Equal(["clerk@test", "manager@test"], app.Get<FakeMailSender>().Sent.Select(m => m.To).Order());
    }

    [Fact]
    public async Task TheEmail_ListsDisksAndServicesWithLinks()
    {
        await using var app = await TestServices.CreateAsync(Roles.Admin);
        var id = await AddVehicleAsync(app, "CA 100-001", diskExpiresInDays: -3, serviceDueInDays: 2);
        await AddUserAsync(app, "manager@test", reminders: true);

        await app.Get<ReminderService>().RunAsync();

        var email = app.Get<FakeMailSender>().Sent.Single();
        Assert.Contains("1 licence disk(s) and 1 service(s)", email.Subject);
        Assert.Contains("expired 3 days ago", email.TextBody);
        Assert.Contains("service due in 2 days", email.TextBody);
        Assert.Contains($"https://fleettrack.test/vehicles/{id}", email.HtmlBody);
    }

    [Fact]
    public async Task RunningAgain_DoesNotSendTheSameReminderTwice()
    {
        await using var app = await TestServices.CreateAsync(Roles.Admin);
        await AddVehicleAsync(app, "CA 100-001", diskExpiresInDays: 20);
        await AddUserAsync(app, "manager@test", reminders: true);
        var reminders = app.Get<ReminderService>();

        await reminders.RunAsync();
        var second = await reminders.RunAsync();

        Assert.Equal(ReminderRunOutcome.NothingDue, second.Outcome);
        Assert.Single(app.Get<FakeMailSender>().Sent);
    }

    [Fact]
    public async Task RenewingTheDisk_StartsTheRemindersAfresh()
    {
        await using var app = await TestServices.CreateAsync(Roles.Admin);
        var id = await AddVehicleAsync(app, "CA 100-001", diskExpiresInDays: 5);
        await AddUserAsync(app, "manager@test", reminders: true);
        var reminders = app.Get<ReminderService>();
        await reminders.RunAsync();

        // Renewed, but someone typed an expiry that is again only 25 days away: that is a new reminder.
        await using (var db = await app.CreateDbAsync())
        {
            (await db.Vehicles.FindAsync(id))!.LicenceExpiryDate = Today.AddDays(25);
            await db.SaveChangesAsync();
        }

        Assert.Single(await reminders.PendingAsync());
    }

    [Fact]
    public async Task VehiclesWithNothingDue_AreNotMentioned()
    {
        await using var app = await TestServices.CreateAsync(Roles.Admin);
        await AddVehicleAsync(app, "CA 100-001", diskExpiresInDays: 200, serviceDueInDays: 60);
        await AddUserAsync(app, "manager@test", reminders: true);

        Assert.Equal(ReminderRunOutcome.NothingDue, (await app.Get<ReminderService>().RunAsync()).Outcome);
        Assert.Empty(app.Get<FakeMailSender>().Sent);
    }

    [Fact]
    public async Task IfSendingFails_TheRemindersAreRetriedNextTime()
    {
        await using var app = await TestServices.CreateAsync(Roles.Admin);
        await AddVehicleAsync(app, "CA 100-001", diskExpiresInDays: 5);
        await AddUserAsync(app, "manager@test", reminders: true);
        var sender = app.Get<FakeMailSender>();
        var reminders = app.Get<ReminderService>();

        sender.Fail = true;
        Assert.Equal(ReminderRunOutcome.Failed, (await reminders.RunAsync()).Outcome);

        sender.Fail = false;
        Assert.Equal(ReminderRunOutcome.Sent, (await reminders.RunAsync()).Outcome);
    }

    [Fact]
    public async Task WithNobodyOptedIn_NothingIsMarkedAsSent()
    {
        await using var app = await TestServices.CreateAsync(Roles.Admin);
        await AddVehicleAsync(app, "CA 100-001", diskExpiresInDays: 5);

        Assert.Equal(ReminderRunOutcome.NoRecipients, (await app.Get<ReminderService>().RunAsync()).Outcome);
        Assert.Single(await app.Get<ReminderService>().PendingAsync()); // still waiting for someone to opt in
    }

    [Fact]
    public async Task TextTypedIntoFleetTrack_CannotInjectHtmlIntoTheEmail()
    {
        await using var app = await TestServices.CreateAsync(Roles.Admin);
        await using (var db = await app.CreateDbAsync())
        {
            db.Vehicles.Add(new Vehicle
            {
                RegistrationNumber = "CA 100-001", Make = "<script>alert(1)</script>", Model = "X", CategoryId = 2,
                LicenceHolder = new LicenceHolder { Name = "Test", Identifier = "2014/208815/07" }, LicenceExpiryDate = Today.AddDays(3),
            });
            await db.SaveChangesAsync();
        }

        var items = await app.Get<ReminderService>().PendingAsync();
        var html = app.Get<ReminderService>().Compose(items).HtmlBody;

        Assert.DoesNotContain("<script>", html);
        Assert.Contains("&lt;script&gt;", html);
    }

    [Fact]
    public async Task Staff_CannotTriggerOrViewReminders()
    {
        await using var app = await TestServices.CreateAsync(Roles.Staff);
        var reminders = app.Get<ReminderService>();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => reminders.RunNowAsync());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => reminders.SendTestAsync("me@test"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => reminders.HistoryAsync());
    }

    [Fact]
    public async Task SentReminders_AreRecordedPerMilestone()
    {
        await using var app = await TestServices.CreateAsync(Roles.Admin);
        await AddVehicleAsync(app, "CA 100-001", diskExpiresInDays: 5, serviceDueInDays: -10);
        await AddUserAsync(app, "manager@test", reminders: true);

        await app.Get<ReminderService>().RunAsync();

        await using var db = await app.CreateDbAsync();
        var sent = await db.RemindersSent.OrderBy(r => r.Kind).ToListAsync();
        Assert.Equal([(ReminderKind.LicenceDisk, "7d"), (ReminderKind.Service, "overdue-w1")], sent.Select(r => (r.Kind, r.Milestone)));
        Assert.Empty(await db.AuditLogs.Where(l => l.EntityType == nameof(ReminderSent)).ToListAsync());
    }
}
