namespace FleetTrack.Core.Entities;

public class AuditLog
{
    public long Id { get; set; }
    public DateTime TimestampUtc { get; set; }
    public string? UserId { get; set; }
    public string? UserName { get; set; }
    public AuditAction Action { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public string? EntityId { get; set; }

    /// <summary>Which fields changed. Never contains the values of sensitive fields such as ID numbers.</summary>
    public string? Details { get; set; }

    public string? IpAddress { get; set; }
}
