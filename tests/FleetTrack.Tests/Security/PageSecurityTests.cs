using System.Reflection;
using FleetTrack.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;

namespace FleetTrack.Tests.Security;

/// <summary>
/// Guards the "secure by default" rule: if someone adds a page and forgets about security,
/// or marks the wrong page [AllowAnonymous], these tests fail.
/// </summary>
public class PageSecurityTests
{
    // The only pages anyone may open without signing in.
    private static readonly HashSet<string> AllowedAnonymousPages =
    [
        "/Account/Login", "/Account/LoginWith2fa", "/Account/LoginWithRecoveryCode",
        "/Account/Lockout", "/Account/AccessDenied", "/Account/InvalidUser", "/Error",
    ];

    private static IEnumerable<(Type Type, string Route)> Pages() =>
        typeof(PermissionService).Assembly.GetTypes()
            .Where(t => typeof(IComponent).IsAssignableFrom(t))
            .SelectMany(t => t.GetCustomAttributes<RouteAttribute>().Select(r => (t, r.Template)));

    [Fact]
    public void OnlyTheSignInAndErrorPages_AllowAnonymousAccess()
    {
        var anonymous = Pages()
            .Where(p => p.Type.GetCustomAttribute<AllowAnonymousAttribute>() is not null)
            .Select(p => p.Route)
            .ToHashSet();

        Assert.Subset(AllowedAnonymousPages, anonymous);
    }

    [Fact]
    public void EveryOtherPage_RequiresSignIn()
    {
        var unprotected = Pages()
            .Where(p => !AllowedAnonymousPages.Contains(p.Route))
            .Where(p => !p.Type.GetCustomAttributes<AuthorizeAttribute>(inherit: true).Any())
            .Select(p => p.Route)
            .ToList();

        Assert.Empty(unprotected);
    }

    [Theory]
    [InlineData("/admin/users", "ManageUsers")]
    [InlineData("/admin/audit-log", "ViewAuditLog")]
    [InlineData("/documents", "ViewDocuments")]
    [InlineData("/admin/licence-holders", "EditRecords")]
    [InlineData("/admin/reminders", "ManageUsers")]
    public void AdminAndDocumentPages_RequireTheRightPermission(string route, string policy)
    {
        var page = Pages().Single(p => p.Route == route).Type;

        // A page can carry several [Authorize] attributes (the general one from _Imports.razor plus its own);
        // ASP.NET Core requires all of them to pass.
        Assert.Contains(policy, page.GetCustomAttributes<AuthorizeAttribute>().Select(a => a.Policy));
    }

    [Fact]
    public void ContentSecurityPolicy_NeverAllowsInlineOrExternalScripts()
    {
        var scriptPolicy = SecurityHeaders.ContentSecurityPolicy.Split(';').Select(d => d.Trim())
            .Single(d => d.StartsWith("script-src", StringComparison.Ordinal));

        Assert.Equal("script-src 'self'", scriptPolicy);
        Assert.Contains("frame-ancestors 'none'", SecurityHeaders.ContentSecurityPolicy);
    }
}
