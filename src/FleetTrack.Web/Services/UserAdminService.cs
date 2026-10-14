using System.Security.Cryptography;
using System.Text.Json;
using FleetTrack.Core.Entities;
using FleetTrack.Core.Security;
using FleetTrack.Web.Data;
using FleetTrack.Web.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FleetTrack.Web.Services;

public record UserSummary(
    string Id, string FullName, string Email, string Role, bool IsActive, bool IsLockedOut, bool TwoFactorEnabled);

public record UserForm(string FullName, string Email, string Role, string? Password);

/// <summary>
/// Creating, editing, disabling and deleting user accounts. Admin only.
/// Every method checks the permission itself, and every change is written to the audit log.
/// </summary>
public class UserAdminService(
    IServiceScopeFactory scopeFactory,
    PermissionService permissions,
    CurrentUserService currentUser,
    AuditService audit)
{
    private const string AuditEntity = "User";

    public async Task<List<UserSummary>> ListAsync()
    {
        await permissions.DemandAsync(Permission.ManageUsers);

        // A fresh scope gives a fresh database context, so the list is never stale. (In Blazor Server a
        // normal scoped service lives as long as the browser tab, which could be hours.)
        await using var scope = scopeFactory.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var admins = (await userManager.GetUsersInRoleAsync(Roles.Admin)).Select(u => u.Id).ToHashSet();
        var users = await userManager.Users.AsNoTracking().OrderBy(u => u.FullName).ToListAsync();
        var now = DateTimeOffset.UtcNow;

        return users.Select(u => new UserSummary(
            u.Id, u.FullName, u.Email ?? "",
            admins.Contains(u.Id) ? Roles.Admin : Roles.Staff,
            u.IsActive,
            u.LockoutEnd > now,
            u.TwoFactorEnabled)).ToList();
    }

    public async Task<OperationResult> CreateAsync(UserForm form)
    {
        await permissions.DemandAsync(Permission.ManageUsers);
        if (!Roles.All.Contains(form.Role))
            return OperationResult.Fail("Unknown role.");
        if (string.IsNullOrWhiteSpace(form.Password))
            return OperationResult.Fail("A password is required.");

        await using var scope = scopeFactory.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = new ApplicationUser
        {
            UserName = form.Email.Trim(),
            Email = form.Email.Trim(),
            FullName = form.FullName.Trim(),
            // Accounts are created by an admin, so there is no confirmation email step.
            EmailConfirmed = true,
        };
        var result = await userManager.CreateAsync(user, form.Password);
        if (!result.Succeeded)
            return OperationResult.From(result);

        result = await userManager.AddToRoleAsync(user, form.Role);
        if (!result.Succeeded)
            return OperationResult.From(result);

        await audit.LogAsync(AuditAction.Create, AuditEntity, user.Id, Json(new { form.Role }));
        return OperationResult.Ok;
    }

    public async Task<OperationResult> UpdateAsync(string userId, UserForm form)
    {
        await permissions.DemandAsync(Permission.ManageUsers);
        if (!Roles.All.Contains(form.Role))
            return OperationResult.Fail("Unknown role.");

        await using var scope = scopeFactory.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
            return OperationResult.Fail("That user no longer exists.");

        var currentRole = await userManager.IsInRoleAsync(user, Roles.Admin) ? Roles.Admin : Roles.Staff;
        var roleChanging = currentRole != form.Role;
        if (roleChanging && await WouldRemoveLastAdminAsync(userManager, user, remainsAdmin: form.Role == Roles.Admin))
            return OperationResult.Fail("This is the last active admin. Make someone else an admin first.");

        var changed = new List<string>();
        if (user.FullName != form.FullName.Trim()) { user.FullName = form.FullName.Trim(); changed.Add("FullName"); }
        if (!string.Equals(user.Email, form.Email.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            user.Email = user.UserName = form.Email.Trim();
            changed.Add("Email");
        }

        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
            return OperationResult.From(result);

        if (roleChanging)
        {
            await userManager.RemoveFromRolesAsync(user, Roles.All);
            await userManager.AddToRoleAsync(user, form.Role);
            // Changing the stamp signs the user out of open sessions, so the new role applies straight away.
            await userManager.UpdateSecurityStampAsync(user);
        }

        await audit.LogAsync(AuditAction.Update, AuditEntity, user.Id,
            Json(new { changedFields = changed, role = roleChanging ? new { from = currentRole, to = form.Role } : null }));
        return OperationResult.Ok;
    }

    public async Task<OperationResult> SetActiveAsync(string userId, bool active)
    {
        await permissions.DemandAsync(Permission.ManageUsers);
        if (!active && await IsCurrentUserAsync(userId))
            return OperationResult.Fail("You can't disable your own account.");

        await using var scope = scopeFactory.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
            return OperationResult.Fail("That user no longer exists.");
        if (!active && await WouldRemoveLastAdminAsync(userManager, user, remainsAdmin: false))
            return OperationResult.Fail("This is the last active admin. Make someone else an admin first.");

        user.IsActive = active;
        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
            return OperationResult.From(result);
        if (!active)
            await userManager.UpdateSecurityStampAsync(user); // ends their open sessions

        await audit.LogAsync(AuditAction.Update, AuditEntity, user.Id, Json(new { IsActive = new { from = !active, to = active } }));
        return OperationResult.Ok;
    }

    /// <summary>Sets a new password and clears any lockout.</summary>
    public async Task<OperationResult> ResetPasswordAsync(string userId, string newPassword)
    {
        await permissions.DemandAsync(Permission.ManageUsers);

        await using var scope = scopeFactory.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
            return OperationResult.Fail("That user no longer exists.");

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var result = await userManager.ResetPasswordAsync(user, token, newPassword);
        if (!result.Succeeded)
            return OperationResult.From(result);

        await userManager.SetLockoutEndDateAsync(user, null);
        await userManager.ResetAccessFailedCountAsync(user);

        // The password itself is never logged.
        await audit.LogAsync(AuditAction.Update, AuditEntity, user.Id, Json(new { changedFields = new[] { "Password" } }));
        return OperationResult.Ok;
    }

    public async Task<OperationResult> DeleteAsync(string userId)
    {
        await permissions.DemandAsync(Permission.DeleteRecords);
        await permissions.DemandAsync(Permission.ManageUsers);
        if (await IsCurrentUserAsync(userId))
            return OperationResult.Fail("You can't delete your own account.");

        await using var scope = scopeFactory.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
            return OperationResult.Ok;
        if (await WouldRemoveLastAdminAsync(userManager, user, remainsAdmin: false))
            return OperationResult.Fail("This is the last active admin. Make someone else an admin first.");

        var result = await userManager.DeleteAsync(user);
        if (result.Succeeded)
            await audit.LogAsync(AuditAction.Delete, AuditEntity, userId);
        return OperationResult.From(result);
    }

    /// <summary>A random password that meets the password rules, for the admin to pass on to the new user.</summary>
    public static string GeneratePassword()
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ", lower = "abcdefghijkmnpqrstuvwxyz", digits = "23456789", symbols = "!@#$%*?";
        const string all = upper + lower + digits + symbols;

        // One of each required kind, then random characters, then shuffled.
        var chars = new List<char>
        {
            upper[RandomNumberGenerator.GetInt32(upper.Length)],
            lower[RandomNumberGenerator.GetInt32(lower.Length)],
            digits[RandomNumberGenerator.GetInt32(digits.Length)],
            symbols[RandomNumberGenerator.GetInt32(symbols.Length)],
        };
        while (chars.Count < 16)
            chars.Add(all[RandomNumberGenerator.GetInt32(all.Length)]);

        var shuffled = chars.ToArray();
        RandomNumberGenerator.Shuffle(shuffled.AsSpan());
        return new string(shuffled);
    }

    private async Task<bool> IsCurrentUserAsync(string userId) =>
        (await currentUser.GetActorAsync()).UserId == userId;

    private static async Task<bool> WouldRemoveLastAdminAsync(UserManager<ApplicationUser> userManager, ApplicationUser target, bool remainsAdmin)
    {
        var activeAdmins = (await userManager.GetUsersInRoleAsync(Roles.Admin)).Where(u => u.IsActive).ToList();
        var targetIsActiveAdmin = activeAdmins.Any(u => u.Id == target.Id);
        return UserAccountRules.WouldRemoveLastAdmin(activeAdmins.Count, targetIsActiveAdmin, remainsAdmin && target.IsActive);
    }

    private static string Json(object value) => JsonSerializer.Serialize(value);
}
