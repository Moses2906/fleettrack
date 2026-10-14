using Microsoft.AspNetCore.Identity;

namespace FleetTrack.Web.Data;

public class ApplicationUser : IdentityUser
{
    [PersonalData]
    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// Disabled users can't sign in, and are signed out of open sessions within a few minutes.
    /// Disabling keeps the account (and the audit trail that names it) instead of deleting it.
    /// </summary>
    public bool IsActive { get; set; } = true;
}
