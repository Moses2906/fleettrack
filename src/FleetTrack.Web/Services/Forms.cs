using System.ComponentModel.DataAnnotations;
using FleetTrack.Core.Entities;
using FleetTrack.Core.Rules;

namespace FleetTrack.Web.Services;

// Form models: what an edit dialog binds to. Kept separate from the database entities so a form
// can only ever set the fields it shows (no sneaking in e.g. a CreatedAtUtc value).

public class VehicleForm : IValidatableObject
{
    [Required(ErrorMessage = "Enter the registration number.")]
    [StringLength(15)]
    public string RegistrationNumber { get; set; } = "";

    [Required(ErrorMessage = "Enter the make."), StringLength(50)]
    public string Make { get; set; } = "";

    [Required(ErrorMessage = "Enter the model."), StringLength(50)]
    public string Model { get; set; } = "";

    [Range(1950, 2100, ErrorMessage = "Enter a valid year.")]
    public int? Year { get; set; }

    [RegularExpression("^[A-HJ-NPR-Za-hj-npr-z0-9]{6,17}$", ErrorMessage = "A VIN has 6 to 17 letters and numbers (no I, O or Q).")]
    public string? Vin { get; set; }

    [Required(ErrorMessage = "Choose a category.")]
    public int? CategoryId { get; set; }

    [Required(ErrorMessage = "Choose who the vehicle is licensed to.")]
    public int? LicenceHolderId { get; set; }

    public int? CurrentDriverId { get; set; }

    [Required(ErrorMessage = "Enter the licence disk expiry date.")]
    public DateTime? LicenceExpiryDate { get; set; }

    public bool HasRc1 { get; set; }
    public bool HasBrnc { get; set; }
    public bool HasProxyLetter { get; set; }
    public bool HasProxyId { get; set; }

    [StringLength(2000)]
    public string? Notes { get; set; }

    /// <summary>Sent back unchanged so the save can detect if someone else edited the vehicle meanwhile.</summary>
    public string? ConcurrencyStamp { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!string.IsNullOrWhiteSpace(RegistrationNumber) && !RegistrationNumbers.IsValid(RegistrationNumber))
            yield return new ValidationResult(
                "This doesn't look like a South African registration number, e.g. CA 123-456, HJ 45 KL GP or JOHN-GP.",
                [nameof(RegistrationNumber)]);

        if (Year > DateTime.Today.Year + 1)
            yield return new ValidationResult("The year can't be in the future.", [nameof(Year)]);

        // A disk is valid for a year, so anything far in the future is almost certainly a typo.
        if (LicenceExpiryDate > DateTime.Today.AddYears(2))
            yield return new ValidationResult("That expiry date is more than two years away. Please check it.", [nameof(LicenceExpiryDate)]);
    }
}

public class LicenceHolderForm : IValidatableObject
{
    public HolderType HolderType { get; set; } = HolderType.Company;

    [Required(ErrorMessage = "Enter the name."), StringLength(150)]
    public string Name { get; set; } = "";

    [Required(ErrorMessage = "Enter the registration or ID number."), StringLength(20)]
    public string Identifier { get; set; } = "";

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(Identifier))
            yield break;

        if (HolderType == HolderType.Company && !IdentityNumbers.IsValidCompanyRegistrationNumber(Identifier))
            yield return new ValidationResult("Company registration numbers look like 2014/208815/07.", [nameof(Identifier)]);

        if (HolderType == HolderType.Individual && !IdentityNumbers.IsValidSaIdNumber(Identifier))
            yield return new ValidationResult("That isn't a valid 13-digit South African ID number. Please check for a typo.", [nameof(Identifier)]);
    }
}

public class DriverForm
{
    [Required(ErrorMessage = "Enter the driver's name."), StringLength(100)]
    public string FullName { get; set; } = "";

    [RegularExpression(@"^\+?[0-9 ]{10,15}$", ErrorMessage = "Enter a phone number like 082 555 0101 or +27 82 555 0101.")]
    public string? Phone { get; set; }

    [EmailAddress(ErrorMessage = "Enter a valid email address."), StringLength(150)]
    public string? Email { get; set; }

    [StringLength(20)]
    public string? DriverLicenceNumber { get; set; }

    public bool IsActive { get; set; } = true;
}

public class CategoryForm
{
    [Required(ErrorMessage = "Enter a name."), StringLength(60)]
    public string Name { get; set; } = "";

    [StringLength(250)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    [Range(0, 999)]
    public int SortOrder { get; set; }
}
