namespace FleetTrack.Core.Entities;

public class RenewalStep
{
    public int Id { get; set; }
    public int LicenceRenewalId { get; set; }
    public LicenceRenewal LicenceRenewal { get; set; } = null!;

    public RenewalStepType StepType { get; set; }
    public DateOnly StepDate { get; set; }
    public string? Notes { get; set; }
    /// <summary>Login name as text, not a foreign key, so history survives the user account being removed.</summary>
    public string RecordedBy { get; set; } = string.Empty;
}
