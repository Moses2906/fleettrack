using System.Globalization;
using FleetTrack.Core.Rules;
using FleetTrack.Web.Components;
using FleetTrack.Web.Components.Account;
using FleetTrack.Web.Data;
using FleetTrack.Web.Data.Seeding;
using FleetTrack.Web.Documents;
using FleetTrack.Web.History;
using FleetTrack.Web.Security;
using FleetTrack.Web.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection;
using MudBlazor.Services;
using System.Threading.RateLimiting;
using Oracle.EntityFrameworkCore.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Dates and numbers look the same on every server, whatever its regional settings:
// "15 Oct 2026", and a decimal point (not a comma) when typing amounts like 2400.50.
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("en-GB");

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddMudServices(options =>
    options.SnackbarConfiguration.PositionClass = MudBlazor.Defaults.Classes.Position.BottomRight);

// ---------------------------------------------------------------------------
// Database
// ---------------------------------------------------------------------------

// The connection string is NOT in appsettings.json. Locally it comes from user secrets;
// on a server it comes from the ConnectionStrings__DefaultConnection environment variable.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Connection string 'DefaultConnection' not found. See README: set it with 'dotnet user-secrets'.");

// Blazor Server keeps one DI scope per browser tab for hours, so services create a short-lived
// DbContext per operation from this factory instead of sharing one long-lived context.
// Scoped lifetime lets each context know the signed-in user (for the audit log).
builder.Services.AddDbContextFactory<ApplicationDbContext>(options => options
    .UseOracle(connectionString, oracle => oracle.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion23))
    // VEHICLES / LICENCE_EXPIRY_DATE instead of "Vehicles" / "LicenceExpiryDate", so plain SQL works without quotes.
    .UseUpperSnakeCaseNamingConvention(),
    ServiceLifetime.Scoped);
// ASP.NET Core Identity asks for the context directly.
builder.Services.AddScoped(sp => sp.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContext());
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// ---------------------------------------------------------------------------
// Authentication: who are you?
// ---------------------------------------------------------------------------

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();

builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = true;
        options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
        options.User.RequireUniqueEmail = true;

        // Strong passwords: at least 12 characters with upper, lower, digit and symbol.
        options.Password.RequiredLength = 12;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireDigit = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Password.RequiredUniqueChars = 4;

        // 5 wrong passwords lock the account for 15 minutes, which stops password guessing.
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.AllowedForNewUsers = true;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager<FleetTrackSignInManager>()
    .AddClaimsPrincipalFactory<FleetTrackClaimsPrincipalFactory>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "FleetTrack.Auth";
    options.Cookie.HttpOnly = true;                          // JavaScript can't read it (limits XSS damage)
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always; // only ever sent over HTTPS
    options.Cookie.SameSite = SameSiteMode.Lax;              // not sent on cross-site requests (CSRF protection)
    options.ExpireTimeSpan = TimeSpan.FromHours(8);          // one working day...
    options.SlidingExpiration = true;                        // ...counted from the last activity
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

// How often an open session re-checks the account (disabled, password changed, role changed).
builder.Services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.FromMinutes(5));

// ---------------------------------------------------------------------------
// Authorization: what may you do?
// ---------------------------------------------------------------------------

builder.Services.AddFleetTrackAuthorization();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CurrentUserService>();
builder.Services.AddScoped<IAuditActorProvider>(sp => sp.GetRequiredService<CurrentUserService>());
builder.Services.AddScoped<PermissionService>();

// ---------------------------------------------------------------------------
// Application services
// ---------------------------------------------------------------------------

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<UserAdminService>();
builder.Services.AddScoped<VehicleService>();
builder.Services.AddScoped<ReferenceDataService>();
builder.Services.AddScoped<ServiceLogService>();
builder.Services.AddScoped<RenewalService>();
builder.Services.AddScoped<HistoryService>();

// Uploaded documents are stored outside wwwroot (see DocumentStorageOptions).
var storageOptions = builder.Configuration.GetSection("Storage").Get<DocumentStorageOptions>() ?? new DocumentStorageOptions();
builder.Services.AddSingleton<IDocumentStorage>(new LocalDocumentStorage(
    Path.Combine(builder.Environment.ContentRootPath, storageOptions.DocumentsPath)));
builder.Services.AddScoped<DocumentService>();

// Sign-in cookies are encrypted with keys ASP.NET Core creates itself. On a server those keys must be kept
// in a fixed folder (DataProtection__KeysPath), or everyone is signed out whenever the app restarts.
var keysPath = builder.Configuration["DataProtection:KeysPath"];
var dataProtection = builder.Services.AddDataProtection().SetApplicationName("FleetTrack");
if (!string.IsNullOrWhiteSpace(keysPath))
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keysPath));

// Account lockout stops guessing one account's password. This limit stops one computer trying a common
// password against every account: at most 10 sign-in attempts per minute from each IP address.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    // A short message (rather than an empty response, which would be turned into a redirect) explains the wait.
    options.OnRejected = (context, cancellationToken) => new ValueTask(context.HttpContext.Response.WriteAsync(
        "Too many sign-in attempts from this network. Please wait a minute and try again.", cancellationToken));
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        HttpMethods.IsPost(context.Request.Method) && context.Request.Path.StartsWithSegments("/Account/Login")
            ? RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) })
            : RateLimitPartition.GetNoLimiter("everything-else"));
});

builder.Services.AddHsts(options =>
{
    options.MaxAge = TimeSpan.FromDays(365);
    options.IncludeSubDomains = true;
});

var app = builder.Build();

// ---------------------------------------------------------------------------
// Start-up data
// ---------------------------------------------------------------------------

await using (var scope = app.Services.CreateAsyncScope())
{
    var services = scope.ServiceProvider;
    var db = services.GetRequiredService<ApplicationDbContext>();

    if (app.Environment.IsDevelopment())
    {
        // Development only: bring the local database up to date and load the fictional demo data.
        // In production, migrations are applied deliberately with a reviewed SQL script.
        await db.Database.MigrateAsync();
        await DevDataSeeder.SeedAsync(db, SouthAfricanTime.Today(TimeProvider.System));
    }

    await IdentitySeeder.SeedRolesAsync(services.GetRequiredService<RoleManager<IdentityRole>>());
    var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
    if (app.Environment.IsDevelopment())
        await IdentitySeeder.SeedDemoUsersAsync(userManager);
    else
        await IdentitySeeder.SeedBootstrapAdminAsync(userManager, app.Configuration, app.Logger);
}

// ---------------------------------------------------------------------------
// HTTP request pipeline
// ---------------------------------------------------------------------------

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts(); // tells browsers to only ever use HTTPS for this site
}

// Order matters: upgrade to HTTPS *before* anything else, so that even the redirect
// to the sign-in page (and therefore the password) never travels over plain HTTP.
app.UseHttpsRedirection();
app.UseSecurityHeaders();
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery(); // CSRF protection for every form post

// CSS, JavaScript and fonts must load on the login page, before anyone is signed in.
app.MapStaticAssets().AllowAnonymous();
app.MapRazorComponents<App>()
    // Blazor adds its own "frame-ancestors 'self'" header; ours (in SecurityHeaders) is stricter, so turn Blazor's off.
    .AddInteractiveServerRenderMode(options => options.ContentSecurityFrameAncestorsPolicy = null);

app.MapAdditionalIdentityEndpoints();
app.MapDocumentEndpoints();
app.MapHistoryEndpoints();

app.Run();
