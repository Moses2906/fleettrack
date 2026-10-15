using System.Security.Claims;
using FleetTrack.Web.Data;
using FleetTrack.Web.Documents;
using FleetTrack.Web.Security;
using FleetTrack.Web.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FleetTrack.Tests;

/// <summary>
/// Builds the app's real services (permission checks, auditing, EF Core) around an in-memory database,
/// signed in as a user with the given role. Lets tests call services exactly as a page would.
/// </summary>
internal sealed class TestServices : IAsyncDisposable
{
    public static readonly DateOnly Today = new(2026, 10, 14);

    private readonly ServiceProvider _root;
    private readonly AsyncServiceScope _scope;

    /// <summary>A temporary folder standing in for the document store; deleted when the test ends.</summary>
    public string StoragePath { get; }

    private readonly string _databaseName;
    private readonly bool _ownsStorage;

    private TestServices(ServiceProvider root, string storagePath, string databaseName, bool ownsStorage)
    {
        _root = root;
        _scope = root.CreateAsyncScope();
        StoragePath = storagePath;
        _databaseName = databaseName;
        _ownsStorage = ownsStorage;
    }

    /// <param name="shareWith">Another instance whose database and document folder this one should use,
    /// e.g. to have an admin upload a file and a staff member open it.</param>
    public static async Task<TestServices> CreateAsync(string role, TestServices? shareWith = null)
    {
        var databaseName = shareWith?._databaseName ?? Guid.NewGuid().ToString();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddFleetTrackAuthorization();
        services.AddSingleton<AuthenticationStateProvider>(new SignedInAs(role));
        services.AddHttpContextAccessor();
        services.AddScoped<CurrentUserService>();
        services.AddScoped<IAuditActorProvider>(sp => sp.GetRequiredService<CurrentUserService>());
        services.AddScoped<PermissionService>();
        services.AddDbContextFactory<ApplicationDbContext>(o => o.UseInMemoryDatabase(databaseName), ServiceLifetime.Scoped);
        services.AddSingleton<TimeProvider>(new FixedClock(Today));
        services.AddScoped<VehicleService>();
        services.AddScoped<ReferenceDataService>();
        services.AddScoped<ServiceLogService>();
        services.AddScoped<RenewalService>();
        services.AddScoped<FleetTrack.Web.History.HistoryService>();
        services.AddScoped<AuditService>();
        var storagePath = shareWith?.StoragePath ?? Path.Combine(Path.GetTempPath(), "fleettrack-tests", Guid.NewGuid().ToString("N"));
        services.AddSingleton<IDocumentStorage>(new LocalDocumentStorage(storagePath));
        services.AddScoped<DocumentService>();
        services.Configure<FleetTrack.Web.Reminders.EmailOptions>(o =>
        {
            o.Host = "smtp.test";
            o.FromAddress = "fleettrack@test";
            o.AppBaseUrl = "https://fleettrack.test";
        });
        services.AddSingleton<FakeMailSender>();
        services.AddSingleton<FleetTrack.Web.Reminders.IMailSender>(sp => sp.GetRequiredService<FakeMailSender>());
        services.AddScoped<FleetTrack.Web.Reminders.ReminderService>();

        var result = new TestServices(services.BuildServiceProvider(), storagePath, databaseName, ownsStorage: shareWith is null);
        await using var db = await result.CreateDbAsync();
        await db.Database.EnsureCreatedAsync();
        return result;
    }

    public T Get<T>() where T : notnull => _scope.ServiceProvider.GetRequiredService<T>();

    public Task<ApplicationDbContext> CreateDbAsync() =>
        Get<IDbContextFactory<ApplicationDbContext>>().CreateDbContextAsync();

    public string[] StoredFiles() => Directory.GetFiles(StoragePath);

    public async ValueTask DisposeAsync()
    {
        await _scope.DisposeAsync();
        await _root.DisposeAsync();
        if (_ownsStorage && Directory.Exists(StoragePath))
            Directory.Delete(StoragePath, recursive: true);
    }

    private sealed class SignedInAs(string role) : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, $"{role.ToLowerInvariant()}-id"),
                new Claim(ClaimTypes.Name, $"{role.ToLowerInvariant()}@fleettrack.local"),
                new Claim(ClaimTypes.Role, role),
            ], authenticationType: "Test");
            return Task.FromResult(new AuthenticationState(new ClaimsPrincipal(identity)));
        }
    }

    private sealed class FixedClock(DateOnly today) : TimeProvider
    {
        // Midday in South Africa is 10:00 UTC.
        public override DateTimeOffset GetUtcNow() => new(today.ToDateTime(new TimeOnly(10, 0)), TimeSpan.Zero);
    }
}

/// <summary>Records emails instead of sending them.</summary>
internal sealed class FakeMailSender : FleetTrack.Web.Reminders.IMailSender
{
    public List<FleetTrack.Web.Reminders.EmailMessage> Sent { get; } = [];
    public bool Fail { get; set; }

    public Task SendAsync(FleetTrack.Web.Reminders.EmailMessage message, CancellationToken cancellationToken = default)
    {
        if (Fail)
            throw new InvalidOperationException("Mail server unavailable");
        Sent.Add(message);
        return Task.CompletedTask;
    }
}
