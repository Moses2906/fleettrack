using System.Security.Claims;
using FleetTrack.Web.Data;
using Microsoft.AspNetCore.Components.Authorization;

namespace FleetTrack.Web.Security;

/// <summary>
/// Answers "who is making this request?" from wherever we are running:
/// inside an interactive Blazor page (no HttpContext), a statically rendered page,
/// or a plain HTTP endpoint such as a document download.
/// </summary>
public class CurrentUserService(
    AuthenticationStateProvider authenticationStateProvider,
    IHttpContextAccessor httpContextAccessor) : IAuditActorProvider
{
    public async Task<ClaimsPrincipal> GetPrincipalAsync()
    {
        try
        {
            // Inside Blazor (interactive or static rendering) this is the reliable source.
            var state = await authenticationStateProvider.GetAuthenticationStateAsync();
            return state.User;
        }
        catch (InvalidOperationException)
        {
            // Plain HTTP endpoints don't set up Blazor's authentication state; use the request's user instead.
            return httpContextAccessor.HttpContext?.User ?? new ClaimsPrincipal(new ClaimsIdentity());
        }
    }

    public async Task<AuditActor> GetActorAsync()
    {
        var user = await GetPrincipalAsync();
        if (user.Identity?.IsAuthenticated != true)
            return AuditActor.System;

        // The IP address is only known while handling an HTTP request; interactive
        // Blazor events arrive over the already-open WebSocket connection instead.
        var ip = httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
        return new AuditActor(user.FindFirstValue(ClaimTypes.NameIdentifier), user.Identity.Name, ip);
    }
}
