using FleetTrack.Core.Security;
using Microsoft.AspNetCore.Authorization;

namespace FleetTrack.Web.Security;

public static class AuthorizationSetup
{
    /// <summary>
    /// Registers one policy per <see cref="Permission"/>, e.g. [Authorize(Policy = nameof(Permission.ManageUsers))].
    /// Which roles hold which permission is defined once, in <see cref="RolePermissions"/>.
    /// Shared by Program.cs and the tests, so the tests check the real policies.
    /// </summary>
    public static AuthorizationBuilder AddFleetTrackAuthorization(this IServiceCollection services)
    {
        var authorization = services.AddAuthorizationBuilder()
            // Every page and endpoint requires a signed-in user unless it is explicitly marked [AllowAnonymous].
            // Secure by default: a new page someone forgets to protect is still protected.
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        foreach (var permission in Enum.GetValues<Permission>())
        {
            authorization.AddPolicy(permission.ToString(), policy => policy.RequireRole(RolePermissions.RolesWith(permission)));
        }
        return authorization;
    }
}
