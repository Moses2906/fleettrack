namespace FleetTrack.Web.Security;

public static class SecurityHeaders
{
    /// <summary>
    /// Content Security Policy: the browser may only load scripts, styles, fonts and images from this site.
    /// Even if an attacker managed to inject a &lt;script&gt; tag, the browser would refuse to run it.
    /// 'unsafe-inline' for styles is needed because MudBlazor sets style attributes on components;
    /// scripts get no such exception.
    /// </summary>
    public const string ContentSecurityPolicy =
        "default-src 'self'; " +
        "script-src 'self'; " +
        "style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data: blob:; " +
        "font-src 'self'; " +
        "connect-src 'self'; " +
        "object-src 'none'; " +
        "base-uri 'self'; " +
        "form-action 'self'; " +
        "frame-ancestors 'none'";

    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            var headers = context.Response.Headers;
            headers.ContentSecurityPolicy = ContentSecurityPolicy;
            headers.XContentTypeOptions = "nosniff";      // don't guess file types (e.g. run an upload as script)
            headers.XFrameOptions = "DENY";               // can't be embedded in another site (clickjacking)
            headers["Referrer-Policy"] = "no-referrer";   // don't leak internal URLs to other sites
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
            await next();
        });
}
