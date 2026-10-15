using FleetTrack.Core.Entities;

namespace FleetTrack.Core.Rules;

/// <summary>What happens to a vehicle's licence expiry date when renewals are recorded or removed.</summary>
public static class RenewalRules
{
    /// <summary>
    /// A new disk normally runs for a year from the old disk's expiry date (not from the purchase date),
    /// so that is the suggested new expiry.
    /// </summary>
    public static DateOnly SuggestNewExpiry(DateOnly currentExpiry) => currentExpiry.AddYears(1);

    /// <summary>Checks the dates on a renewal. Returns null when they make sense, otherwise a message.</summary>
    public static string? Validate(DateOnly datePurchased, DateOnly newExpiryDate, DateOnly today)
    {
        if (datePurchased > today)
            return "The purchase date can't be in the future.";
        if (newExpiryDate <= datePurchased)
            return "The new expiry date must be after the purchase date.";
        if (newExpiryDate > datePurchased.AddMonths(25))
            return "A disk is valid for about a year. Please check the new expiry date.";
        return null;
    }

    /// <summary>
    /// Records a renewal against a vehicle. The renewal remembers the expiry date it replaced, and the
    /// vehicle's expiry date moves forward. Entering an older renewal for the history never moves it backwards.
    /// </summary>
    /// <returns>True if the vehicle's expiry date changed.</returns>
    public static bool Apply(Vehicle vehicle, LicenceRenewal renewal)
    {
        renewal.PreviousExpiryDate = vehicle.LicenceExpiryDate;
        if (renewal.NewExpiryDate <= vehicle.LicenceExpiryDate)
            return false;

        vehicle.LicenceExpiryDate = renewal.NewExpiryDate;
        return true;
    }

    /// <summary>
    /// Undoes a renewal that is being deleted (e.g. entered by mistake). If it is the renewal that set the
    /// vehicle's current expiry date, the date goes back to what it was before; otherwise nothing changes.
    /// </summary>
    /// <returns>True if the vehicle's expiry date changed.</returns>
    public static bool Revert(Vehicle vehicle, LicenceRenewal renewal)
    {
        if (vehicle.LicenceExpiryDate != renewal.NewExpiryDate || renewal.PreviousExpiryDate is not { } previous)
            return false;

        vehicle.LicenceExpiryDate = previous;
        return true;
    }
}
