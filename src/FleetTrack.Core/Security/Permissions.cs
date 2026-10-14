namespace FleetTrack.Core.Security;

public static class Roles
{
    public const string Admin = "Admin";
    public const string Staff = "Staff";

    public static readonly IReadOnlyList<string> All = [Admin, Staff];
}

/// <summary>
/// Things a user can do. Pages and services ask "does this user have permission X?"
/// rather than "is this user an Admin?", so changing who may do what only means
/// editing the table in <see cref="RolePermissions"/>.
/// </summary>
public enum Permission
{
    /// <summary>See vehicles, dashboard, services, renewals and history.</summary>
    ViewRecords,

    /// <summary>Log a new service or repair.</summary>
    AddServiceRecords,

    /// <summary>Open, download and print stored documents (including ID copies).</summary>
    ViewDocuments,

    /// <summary>Add and edit vehicles, renewals, drivers, licence holders, categories and checklist ticks.</summary>
    EditRecords,

    /// <summary>Upload or replace document copies.</summary>
    UploadDocuments,

    /// <summary>Delete any record or document (including POPIA deletion requests).</summary>
    DeleteRecords,

    ManageUsers,

    ViewAuditLog
}

public static class RolePermissions
{
    private static readonly Dictionary<string, HashSet<Permission>> Map = new()
    {
        [Roles.Admin] = [.. Enum.GetValues<Permission>()],
        [Roles.Staff] =
        [
            Permission.ViewRecords,
            Permission.AddServiceRecords,
            Permission.ViewDocuments,
        ],
    };

    public static bool RoleHas(string role, Permission permission) =>
        Map.TryGetValue(role, out var permissions) && permissions.Contains(permission);

    /// <summary>The roles that hold a permission; used to build the ASP.NET Core authorization policies.</summary>
    public static string[] RolesWith(Permission permission) =>
        Map.Where(entry => entry.Value.Contains(permission)).Select(entry => entry.Key).ToArray();
}
