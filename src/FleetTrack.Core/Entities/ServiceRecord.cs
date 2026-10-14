namespace FleetTrack.Core.Entities;

public class ServiceRecord
{
    public int Id { get; set; }
    public int VehicleId { get; set; }
    public Vehicle Vehicle { get; set; } = null!;

    public DateOnly ServiceDate { get; set; }
    public int OdometerKm { get; set; }
    public WorkType WorkType { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Workshop { get; set; } = string.Empty;
    public decimal Cost { get; set; }
    public string? Notes { get; set; }
    public DateOnly? NextServiceDueDate { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    /// <summary>Login name as text, not a foreign key, so history survives the user account being removed.</summary>
    public string CreatedBy { get; set; } = string.Empty;
}
