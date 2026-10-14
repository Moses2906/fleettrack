namespace FleetTrack.Web.Data;

/// <summary>Who performed an audited action.</summary>
public sealed record AuditActor(string? UserId, string? UserName, string? IpAddress)
{
    /// <summary>Used for changes made by the application itself, e.g. start-up seeding.</summary>
    public static readonly AuditActor System = new(null, "system", null);
}

public interface IAuditActorProvider
{
    Task<AuditActor> GetActorAsync();
}
