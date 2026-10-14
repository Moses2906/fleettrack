using System.Text.Json;
using System.Text.Json.Serialization;
using FleetTrack.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace FleetTrack.Web.Data;

/// <summary>
/// Turns EF Core's change tracker entries into audit log rows.
///
/// POPIA rule: the audit log must not become a second copy of personal information,
/// otherwise deleting someone's data on request would leave it behind in the log.
/// So values are only recorded for record types that hold no personal information,
/// and never for free-text or file-name fields. For everything else only the
/// names of the changed fields are recorded.
/// </summary>
internal sealed class AuditEntryBuilder
{
    private static readonly HashSet<Type> TypesSafeToLogValues =
    [
        typeof(Vehicle), typeof(VehicleCategory), typeof(ServiceRecord),
        typeof(LicenceRenewal), typeof(RenewalStep), typeof(VehicleDocument)
    ];

    // Free text could contain names or ID numbers; an uploaded file name could be "Thabo Mokoena ID.pdf".
    private static readonly HashSet<string> SensitiveProperties = ["Notes", nameof(VehicleDocument.OriginalFileName)];

    // Bookkeeping columns that change on every save and would only add noise.
    private static readonly HashSet<string> IgnoredProperties =
        [nameof(Vehicle.ConcurrencyStamp), nameof(Vehicle.CreatedAtUtc), nameof(Vehicle.UpdatedAtUtc), nameof(Vehicle.RegistrationKey)];

    private static readonly JsonSerializerOptions JsonOptions = new() { Converters = { new JsonStringEnumConverter() } };

    private readonly EntityEntry _entry;
    private readonly AuditAction _action;
    private readonly string? _details;
    private readonly string? _keyBeforeSave;

    private AuditEntryBuilder(EntityEntry entry, AuditAction action)
    {
        _entry = entry;
        _action = action;
        // Changes must be read before saving, because afterwards the "original" values are overwritten.
        // New rows are described after saving instead, once generated IDs and foreign keys are known.
        _details = action == AuditAction.Create ? null : BuildDetails();
        _keyBeforeSave = action == AuditAction.Create ? null : ReadKey();
    }

    /// <summary>Collects every pending create/update/delete of FleetTrack's own records.</summary>
    public static List<AuditEntryBuilder> Collect(ChangeTracker changeTracker) =>
        changeTracker.Entries()
            .Where(e => e.Entity is not AuditLog && e.Metadata.ClrType.Namespace == typeof(Vehicle).Namespace)
            .Select(e => e.State switch
            {
                EntityState.Added => new AuditEntryBuilder(e, AuditAction.Create),
                EntityState.Modified when HasMeaningfulChanges(e) => new AuditEntryBuilder(e, AuditAction.Update),
                EntityState.Deleted => new AuditEntryBuilder(e, AuditAction.Delete),
                _ => null
            })
            .OfType<AuditEntryBuilder>()
            .ToList();

    /// <summary>Called after saving, when database-generated IDs of new rows are known.</summary>
    public AuditLog ToAuditLog(AuditActor actor, DateTime timestampUtc) => new()
    {
        TimestampUtc = timestampUtc,
        UserId = actor.UserId,
        UserName = actor.UserName,
        IpAddress = actor.IpAddress,
        Action = _action,
        EntityType = _entry.Metadata.ClrType.Name,
        EntityId = _keyBeforeSave ?? ReadKey(),
        Details = _action == AuditAction.Create ? BuildDetails() : _details,
    };

    private string? BuildDetails()
    {
        var safeType = TypesSafeToLogValues.Contains(_entry.Metadata.ClrType);
        var properties = _entry.Properties
            .Where(p => !p.Metadata.IsPrimaryKey() && !IgnoredProperties.Contains(p.Metadata.Name));

        if (_action == AuditAction.Update)
        {
            var changes = properties
                .Where(IsRealChange)
                .ToDictionary(
                    p => p.Metadata.Name,
                    p => safeType && !SensitiveProperties.Contains(p.Metadata.Name)
                        ? new { from = p.OriginalValue, to = p.CurrentValue }
                        : (object)"changed");
            return JsonSerializer.Serialize(changes, JsonOptions);
        }

        // Create and Delete: a snapshot of the values, but only for record types without personal information.
        if (!safeType)
            return null;

        var snapshot = properties
            .Where(p => !SensitiveProperties.Contains(p.Metadata.Name))
            .ToDictionary(p => p.Metadata.Name, p => _action == AuditAction.Delete ? p.OriginalValue : p.CurrentValue);
        return JsonSerializer.Serialize(snapshot, JsonOptions);
    }

    private static bool HasMeaningfulChanges(EntityEntry entry) =>
        entry.Properties.Any(p => !IgnoredProperties.Contains(p.Metadata.Name) && IsRealChange(p));

    private static bool IsRealChange(PropertyEntry p) => p.IsModified && !Equals(p.OriginalValue, p.CurrentValue);

    private string ReadKey() =>
        string.Join(",", _entry.Properties.Where(p => p.Metadata.IsPrimaryKey()).Select(p => p.CurrentValue));
}
