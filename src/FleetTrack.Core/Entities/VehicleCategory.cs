namespace FleetTrack.Core.Entities;

public class VehicleCategory
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>Inactive categories stay on existing vehicles but can't be chosen for new ones.</summary>
    public bool IsActive { get; set; } = true;

    public int SortOrder { get; set; }

    public ICollection<Vehicle> Vehicles { get; set; } = [];
}
