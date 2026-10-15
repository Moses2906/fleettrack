using FleetTrack.Core.Rules;

namespace FleetTrack.Core.Entities;

/// <summary>
/// Remembers that a reminder email went out for a vehicle's milestone (e.g. "disk expiring CA 123-456, 7 days to go"),
/// so the same reminder is never sent twice. See <see cref="ReminderRules"/>.
/// </summary>
public class ReminderSent
{
    public long Id { get; set; }
    public ReminderKind Kind { get; set; }
    public int VehicleId { get; set; }
    public Vehicle Vehicle { get; set; } = null!;

    /// <summary>The expiry or service date the reminder was about. A new date (after a renewal) starts fresh.</summary>
    public DateOnly DueDate { get; set; }

    public string Milestone { get; set; } = string.Empty;
    public DateTime SentAtUtc { get; set; }
    public int RecipientCount { get; set; }
}
