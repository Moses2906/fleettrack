using FleetTrack.Core.Security;
using Microsoft.AspNetCore.Authorization;

namespace FleetTrack.Web.Security;

/// <summary>
/// Server-side permission checks. Every service method that reads or changes data calls
/// <see cref="DemandAsync"/> first, so hiding a button in the UI is never the only protection.
/// </summary>
public class PermissionService(CurrentUserService currentUser, IAuthorizationService authorizationService)
{
    public async Task<bool> HasAsync(Permission permission)
    {
        var user = await currentUser.GetPrincipalAsync();
        var result = await authorizationService.AuthorizeAsync(user, permission.ToString());
        return result.Succeeded;
    }

    /// <exception cref="UnauthorizedAccessException">The current user lacks the permission.</exception>
    public async Task DemandAsync(Permission permission)
    {
        if (!await HasAsync(permission))
            throw new UnauthorizedAccessException($"You don't have permission to do this ({permission}).");
    }
}
