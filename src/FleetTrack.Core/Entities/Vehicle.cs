using FleetTrack.Core.Rules;

namespace FleetTrack.Core.Entities;

public class Vehicle
{
    public int Id { get; set; }

    // EF Core finds this backing field by naming convention and uses it when loading rows,
    // so the normalising setter below only runs when our own code assigns a value.
    private string _registrationNumber = string.Empty;

    public string RegistrationNumber
    {
        get => _registrationNumber;
        set
        {
            _registrationNumber = RegistrationNumbers.Normalise(value);
            RegistrationKey = RegistrationNumbers.ToKey(value);
        }
    }

    /// <summary>Letters and digits only; carries the unique index. See <see cref="RegistrationNumbers.ToKey"/>.</summary>
    public string RegistrationKey { get; private set; } = string.Empty;

    public string Make { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int? Year { get; set; }
    public string? Vin { get; set; }

    public int CategoryId { get; set; }
    public VehicleCategory Category { get; set; } = null!;

    public int LicenceHolderId { get; set; }
    public LicenceHolder LicenceHolder { get; set; } = null!;

    public int? CurrentDriverId { get; set; }
    public Driver? CurrentDriver { get; set; }

    public DateOnly LicenceExpiryDate { get; set; }

    // Document checklist. Ticked manually by an admin, or automatically when a copy is uploaded.
    public bool HasRc1 { get; set; }
    public bool HasBrnc { get; set; }
    public bool HasProxyLetter { get; set; }
    public bool HasProxyId { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    /// <summary>
    /// Changed on every save. If two admins edit the same vehicle at once, the second save
    /// fails instead of silently overwriting the first person's changes.
    /// </summary>
    public string ConcurrencyStamp { get; set; } = Guid.NewGuid().ToString();

    public ICollection<VehicleDocument> Documents { get; set; } = [];
    public ICollection<ServiceRecord> ServiceRecords { get; set; } = [];
    public ICollection<LicenceRenewal> LicenceRenewals { get; set; } = [];

    public LicenceStatus GetLicenceStatus(DateOnly today) =>
        LicenceStatusRules.GetStatus(LicenceExpiryDate, today);

    public bool HasAllDocuments => HasRc1 && HasBrnc && HasProxyLetter && HasProxyId;

    public bool HasDocument(DocumentType type) => type switch
    {
        DocumentType.Rc1 => HasRc1,
        DocumentType.Brnc => HasBrnc,
        DocumentType.ProxyLetter => HasProxyLetter,
        DocumentType.ProxyId => HasProxyId,
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    public void SetDocumentHeld(DocumentType type, bool held)
    {
        switch (type)
        {
            case DocumentType.Rc1: HasRc1 = held; break;
            case DocumentType.Brnc: HasBrnc = held; break;
            case DocumentType.ProxyLetter: HasProxyLetter = held; break;
            case DocumentType.ProxyId: HasProxyId = held; break;
            default: throw new ArgumentOutOfRangeException(nameof(type));
        }
    }
}
