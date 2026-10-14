namespace FleetTrack.Core.Entities;

public class LicenceHolder
{
    public int Id { get; set; }
    public HolderType HolderType { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>Company registration number (e.g. 2015/123456/07) or 13-digit SA ID number.</summary>
    public string Identifier { get; set; } = string.Empty;

    public ICollection<Vehicle> Vehicles { get; set; } = [];
}
