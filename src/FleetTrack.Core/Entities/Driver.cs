namespace FleetTrack.Core.Entities;

public class Driver
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? DriverLicenceNumber { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Vehicle> AssignedVehicles { get; set; } = [];
}
