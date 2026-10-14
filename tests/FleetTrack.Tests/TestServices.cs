using System.Security.Claims;
using FleetTrack.Web.Data;
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

    private TestServices(ServiceProvider root)
    {
        _root = root;
        _scope = root.CreateAsyncScope();
    }

    public static async Task<TestServices> CreateAsync(string role, string? databaseName = null)
    {
        databaseName ??= Guid.NewGuid().ToString();
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
        services.AddScoped<AuditService>();

        var result = new TestServices(services.BuildServiceProvider());
        await using var db = await result.CreateDbAsync();
        await db.Database.EnsureCreatedAsync();
        return result;
    }

    public T Get<T>() where T : notnull => _scope.ServiceProvider.GetRequiredService<T>();

    public Task<ApplicationDbContext> CreateDbAsync() =>
        Get<IDbContextFactory<ApplicationDbContext>>().CreateDbContextAsync();

    public async ValueTask DisposeAsync()
    {
        await _scope.DisposeAsync();
        await _root.DisposeAsync();
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
