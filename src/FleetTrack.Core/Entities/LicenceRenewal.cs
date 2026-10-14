namespace FleetTrack.Core.Entities;

public class LicenceRenewal
{
    public int Id { get; set; }
    public int VehicleId { get; set; }
    public Vehicle Vehicle { get; set; } = null!;

    public DateOnly DatePurchased { get; set; }

    /// <summary>The vehicle's expiry date before this renewal, kept so a mistaken renewal can be reversed.</summary>
    public DateOnly? PreviousExpiryDate { get; set; }

    public DateOnly NewExpiryDate { get; set; }
    public decimal AmountPaid { get; set; }
    public string? ReferenceNumber { get; set; }
    public string ProcessedBy { get; set; } = string.Empty;
    public string? Notes { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    /// <summary>Login name as text, not a foreign key, so history survives the user account being removed.</summary>
    public string CreatedBy { get; set; } = string.Empty;

    public ICollection<RenewalStep> Steps { get; set; } = [];
}
