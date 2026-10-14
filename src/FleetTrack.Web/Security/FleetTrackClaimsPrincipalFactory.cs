using System.Security.Claims;
using FleetTrack.Web.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace FleetTrack.Web.Security;

/// <summary>Adds the user's full name to their sign-in identity, so pages can greet them by name.</summary>
public class FleetTrackClaimsPrincipalFactory(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager,
    IOptions<IdentityOptions> options)
    : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>(userManager, roleManager, options)
{
    public const string FullNameClaim = "full_name";

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        if (!string.IsNullOrWhiteSpace(user.FullName))
            identity.AddClaim(new Claim(FullNameClaim, user.FullName));
        return identity;
    }
}

public static class ClaimsPrincipalExtensions
{
    /// <summary>The user's full name, falling back to their email address.</summary>
    public static string DisplayName(this ClaimsPrincipal user) =>
        user.FindFirstValue(FleetTrackClaimsPrincipalFactory.FullNameClaim) ?? user.Identity?.Name ?? "";
}
