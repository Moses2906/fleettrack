using FleetTrack.Core.Security;
using Microsoft.AspNetCore.Identity;

namespace FleetTrack.Web.Data.Seeding;

public static class IdentitySeeder
{
    // Demo logins for local development only. These are published in the README, which is
    // exactly why they are never created outside the Development environment.
    public const string DemoAdminEmail = "admin@fleettrack.local";
    public const string DemoAdminPassword = "Admin#Demo2026";
    public const string DemoStaffEmail = "staff@fleettrack.local";
    public const string DemoStaffPassword = "Staff#Demo2026";

    /// <summary>The roles must exist in every environment.</summary>
    public static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager)
    {
        foreach (var role in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }
    }

    public static async Task SeedDemoUsersAsync(UserManager<ApplicationUser> userManager)
    {
        await EnsureUserAsync(userManager, DemoAdminEmail, "Demo Admin", DemoAdminPassword, Roles.Admin, receivesReminders: true);
        await EnsureUserAsync(userManager, DemoStaffEmail, "Demo Staff", DemoStaffPassword, Roles.Staff);
    }

    /// <summary>
    /// For a real installation: creates the first admin from the BootstrapAdmin settings
    /// (environment variables), but only while there is no admin at all.
    /// </summary>
    public static async Task SeedBootstrapAdminAsync(UserManager<ApplicationUser> userManager, IConfiguration config, ILogger logger)
    {
        if ((await userManager.GetUsersInRoleAsync(Roles.Admin)).Count > 0)
            return;

        var email = config["BootstrapAdmin:Email"];
        var password = config["BootstrapAdmin:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("No admin account exists. Set BootstrapAdmin__Email and BootstrapAdmin__Password to create one.");
            return;
        }

        await EnsureUserAsync(userManager, email, config["BootstrapAdmin:FullName"] ?? "Administrator", password, Roles.Admin, receivesReminders: true);
        logger.LogInformation("Created the first admin account. Remove the BootstrapAdmin settings now.");
    }

    private static async Task EnsureUserAsync(UserManager<ApplicationUser> userManager, string email, string fullName, string password, string role,
        bool receivesReminders = false)
    {
        if (await userManager.FindByEmailAsync(email) is not null)
            return;

        var user = new ApplicationUser
        {
            UserName = email, Email = email, FullName = fullName, EmailConfirmed = true, ReceivesReminderEmails = receivesReminders,
        };
        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
            throw new InvalidOperationException($"Could not create {email}: {string.Join(" ", result.Errors.Select(e => e.Description))}");

        await userManager.AddToRoleAsync(user, role);
    }
}
