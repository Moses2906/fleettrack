using System.Security.Claims;
using System.Text.Json;
using FleetTrack.Core.Entities;
using FleetTrack.Web.Data;
using FleetTrack.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace FleetTrack.Web.Security;

/// <summary>
/// Identity's normal sign-in rules, plus:
/// - disabled accounts can't sign in (sessions that are already open are ended separately:
///   disabling a user also changes their security stamp, see UserAdminService);
/// - every sign-in, failed password and lockout is written to the audit log.
/// </summary>
public class FleetTrackSignInManager(
    UserManager<ApplicationUser> userManager,
    IHttpContextAccessor contextAccessor,
    IUserClaimsPrincipalFactory<ApplicationUser> claimsFactory,
    IOptions<IdentityOptions> optionsAccessor,
    ILogger<SignInManager<ApplicationUser>> logger,
    IAuthenticationSchemeProvider schemes,
    IUserConfirmation<ApplicationUser> confirmation,
    AuditService audit)
    : SignInManager<ApplicationUser>(userManager, contextAccessor, claimsFactory, optionsAccessor, logger, schemes, confirmation)
{
    private const string AuditEntity = "User";

    public override async Task<bool> CanSignInAsync(ApplicationUser user) =>
        user.IsActive && await base.CanSignInAsync(user);

    /// <summary>
    /// Every successful sign-in (password, passkey or two-factor) ends up here. So does Identity's session
    /// refresh after a user changes their own password, which is logged as a sign-in too.
    /// </summary>
    public override async Task SignInWithClaimsAsync(ApplicationUser user, AuthenticationProperties? authenticationProperties, IEnumerable<Claim> additionalClaims)
    {
        await base.SignInWithClaimsAsync(user, authenticationProperties, additionalClaims);
        await audit.LogAsAsync(ActorFor(user), AuditAction.SignIn, AuditEntity, user.Id);
    }

    /// <summary>
    /// Password checks for existing accounts. Unknown email addresses never reach this method,
    /// so nothing someone mistyped into the email box (sometimes their password) is ever logged.
    /// </summary>
    public override async Task<SignInResult> CheckPasswordSignInAsync(ApplicationUser user, string password, bool lockoutOnFailure)
    {
        var result = await base.CheckPasswordSignInAsync(user, password, lockoutOnFailure);
        if (!result.Succeeded && !result.RequiresTwoFactor)
        {
            var reason = result.IsLockedOut ? "Locked out" : result.IsNotAllowed ? "Account disabled" : "Wrong password";
            await audit.LogAsAsync(ActorFor(user), AuditAction.SignInFailed, AuditEntity, user.Id,
                JsonSerializer.Serialize(new { reason }));
        }
        return result;
    }

    private AuditActor ActorFor(ApplicationUser user) =>
        new(user.Id, user.Email, Context.Connection.RemoteIpAddress?.ToString());
}
