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

public class ServiceForm : IValidatableObject
{
    [Required(ErrorMessage = "Choose the vehicle.")]
    public int? VehicleId { get; set; }

    [Required(ErrorMessage = "Enter the date of the service.")]
    public DateTime? ServiceDate { get; set; }

    [Required(ErrorMessage = "Enter the odometer reading."), Range(0, 3_000_000, ErrorMessage = "Enter a reading between 0 and 3 000 000 km.")]
    public int? OdometerKm { get; set; }

    public WorkType WorkType { get; set; } = WorkType.RoutineService;

    [Required(ErrorMessage = "Describe the work done."), StringLength(250)]
    public string Description { get; set; } = "";

    [Required(ErrorMessage = "Enter the workshop or supplier."), StringLength(100)]
    public string Workshop { get; set; } = "";

    [Required(ErrorMessage = "Enter the cost (0 if it was free)."), Range(0, 10_000_000, ErrorMessage = "Enter a cost between R0 and R10 000 000.")]
    public decimal? Cost { get; set; }

    [StringLength(2000)]
    public string? Notes { get; set; }

    public DateTime? NextServiceDueDate { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ServiceDate > DateTime.Today)
            yield return new ValidationResult("The service date can't be in the future.", [nameof(ServiceDate)]);
        if (NextServiceDueDate is not null && ServiceDate is not null && NextServiceDueDate <= ServiceDate)
            yield return new ValidationResult("The next service must be after this one.", [nameof(NextServiceDueDate)]);
    }
}

public class RenewalForm
{
    [Required(ErrorMessage = "Enter the date the disk was bought.")]
    public DateTime? DatePurchased { get; set; }

    [Required(ErrorMessage = "Enter the new expiry date printed on the disk.")]
    public DateTime? NewExpiryDate { get; set; }

    [Required(ErrorMessage = "Enter the amount paid."), Range(0, 1_000_000, ErrorMessage = "Enter an amount between R0 and R1 000 000.")]
    public decimal? AmountPaid { get; set; }

    [StringLength(50)]
    public string? ReferenceNumber { get; set; }

    [Required(ErrorMessage = "Enter who processed the renewal."), StringLength(100)]
    public string ProcessedBy { get; set; } = "";

    [StringLength(2000)]
    public string? Notes { get; set; }
}

public class RenewalStepForm
{
    public RenewalStepType StepType { get; set; } = RenewalStepType.DiskHandedToDriver;

    [Required(ErrorMessage = "Enter the date.")]
    public DateTime? StepDate { get; set; } = DateTime.Today;

    [StringLength(1000)]
    public string? Notes { get; set; }
}

public static class DisplayNames
{
    public static string Of(WorkType type) => type switch
    {
        WorkType.RoutineService => "Routine service",
        WorkType.Roadworthy => "Roadworthy test",
        _ => type.ToString(),
    };

    public static string Of(RenewalStepType type) => type switch
    {
        RenewalStepType.DiskHandedToDriver => "Disk handed to driver",
        RenewalStepType.DiskFittedToVehicle => "Disk fitted to vehicle",
        RenewalStepType.CopyFiled => "Copy filed",
        _ => "Other",
    };

    /// <summary>Short labels for small tags: "Handed over", "Fitted", "Filed".</summary>
    public static string Short(RenewalStepType type) => type switch
    {
        RenewalStepType.DiskHandedToDriver => "Handed over",
        RenewalStepType.DiskFittedToVehicle => "Fitted",
        RenewalStepType.CopyFiled => "Filed",
        _ => "Note",
    };

    /// <summary>Rand amounts, e.g. "R 2,400.00". Fixed format so it looks the same on every server.</summary>
    public static string Money(decimal amount) =>
        "R " + amount.ToString("#,##0.00", System.Globalization.CultureInfo.InvariantCulture);
}
