using FleetTrack.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace FleetTrack.Tests;

/// <summary>Creates an in-memory database for tests, so they run without Oracle.</summary>
internal static class TestDb
{
    public static async Task<ApplicationDbContext> CreateAsync(AuditActor? actor = null, string? name = null)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(name ?? Guid.NewGuid().ToString())
            .Options;
        var db = new ApplicationDbContext(options, actor is null ? null : new FixedActor(actor));
        await db.Database.EnsureCreatedAsync(); // applies the seeded categories
        return db;
    }

    private sealed class FixedActor(AuditActor actor) : IAuditActorProvider
    {
        public Task<AuditActor> GetActorAsync() => Task.FromResult(actor);
    }
}
