using FleetTrack.Core.Entities;
using FleetTrack.Core.Security;
using FleetTrack.Web.Data;
using FleetTrack.Web.Security;
using Microsoft.EntityFrameworkCore;

namespace FleetTrack.Web.Services;

public record AuditLogQuery(
    string? Search = null,
    AuditAction? Action = null,
    string? EntityType = null,
    DateOnly? From = null,
    DateOnly? To = null,
    int Page = 0,
    int PageSize = 25);

public record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount);

/// <summary>
/// Creates and reads the audit log.
/// Creates, updates and deletes are logged automatically by <see cref="ApplicationDbContext"/>;
/// this service is for events that change nothing in the database, such as viewing a document.
/// </summary>
public class AuditService(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    CurrentUserService currentUser,
    PermissionService permissions)
{
    public async Task LogAsync(AuditAction action, string entityType, string? entityId, string? details = null)
    {
        var actor = await currentUser.GetActorAsync();
        await using var db = await dbFactory.CreateDbContextAsync();
        db.AuditLogs.Add(new AuditLog
        {
            TimestampUtc = DateTime.UtcNow,
            UserId = actor.UserId,
            UserName = actor.UserName,
            IpAddress = actor.IpAddress,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Details = details,
        });
        await db.SaveChangesAsync();
    }

    public async Task<PagedResult<AuditLog>> SearchAsync(AuditLogQuery query)
    {
        await permissions.DemandAsync(Permission.ViewAuditLog);

        await using var db = await dbFactory.CreateDbContextAsync();
        var logs = db.AuditLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            // EF Core turns this into a parameterised LIKE query, so the search text can't inject SQL.
            var term = query.Search.Trim().ToUpper();
            logs = logs.Where(l => (l.UserName ?? "").ToUpper().Contains(term) || (l.EntityId ?? "").ToUpper().Contains(term));
        }
        if (query.Action is { } action)
            logs = logs.Where(l => l.Action == action);
        if (!string.IsNullOrEmpty(query.EntityType))
            logs = logs.Where(l => l.EntityType == query.EntityType);
        if (query.From is { } from)
        {
            var fromUtc = from.ToDateTime(TimeOnly.MinValue);
            logs = logs.Where(l => l.TimestampUtc >= fromUtc);
        }
        if (query.To is { } to)
        {
            var toUtc = to.AddDays(1).ToDateTime(TimeOnly.MinValue);
            logs = logs.Where(l => l.TimestampUtc < toUtc);
        }

        var total = await logs.CountAsync();
        var items = await logs
            .OrderByDescending(l => l.TimestampUtc).ThenByDescending(l => l.Id)
            .Skip(query.Page * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync();
        return new PagedResult<AuditLog>(items, total);
    }

    public async Task<List<string>> GetEntityTypesAsync()
    {
        await permissions.DemandAsync(Permission.ViewAuditLog);
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.AuditLogs.Select(l => l.EntityType).Distinct().OrderBy(t => t).ToListAsync();
    }
}
