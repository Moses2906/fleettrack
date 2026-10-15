using System.Globalization;
using FleetTrack.Core.Entities;

namespace FleetTrack.Core.Rules;

public enum ServiceReminder
{
    None,
    Scheduled,
    DueSoon,
    Overdue
}

/// <summary>What to do about the next service when one is marked as done.</summary>
public enum NextServiceBooking
{
    SameDateNextYear,
    InSixMonths,
    PickADate,
    DontBook
}

public static class ServiceRules
{
    /// <summary>
    /// The next-service date to remember (which drives the reminder email). "Same date next year"
    /// counts from the day the service was done.
    /// </summary>
    public static DateOnly? NextServiceDate(NextServiceBooking booking, DateOnly serviceDate, DateOnly? pickedDate = null) => booking switch
    {
        NextServiceBooking.SameDateNextYear => serviceDate.AddYears(1),
        NextServiceBooking.InSixMonths => serviceDate.AddMonths(6),
        NextServiceBooking.PickADate => pickedDate,
        _ => null,
    };

    public const int DueSoonDays = 30;

    public static ServiceReminder GetReminder(DateOnly? nextServiceDue, DateOnly today)
    {
        if (nextServiceDue is not { } due)
            return ServiceReminder.None;
        if (due < today)
            return ServiceReminder.Overdue;
        if (due <= today.AddDays(DueSoonDays))
            return ServiceReminder.DueSoon;
        return ServiceReminder.Scheduled;
    }

    /// <summary>
    /// The vehicle's next service date comes from its most recent service. Logging a new service
    /// (with or without a next date) replaces the old reminder, so a done service stops nagging.
    /// </summary>
    public static DateOnly? NextServiceDue(IEnumerable<ServiceRecord> services) =>
        services.OrderByDescending(s => s.ServiceDate).ThenByDescending(s => s.Id).FirstOrDefault()?.NextServiceDueDate;

    /// <summary>
    /// Odometer readings only go up. A reading lower than one recorded on an earlier date is almost
    /// always a typo (an extra or missing digit), so it is refused.
    /// </summary>
    /// <returns>Null if the reading fits, otherwise a message explaining the conflict.</returns>
    public static string? CheckOdometer(int odometerKm, DateOnly serviceDate, IEnumerable<ServiceRecord> otherServices)
    {
        var earlier = otherServices.Where(s => s.ServiceDate <= serviceDate).MaxBy(s => s.OdometerKm);
        if (earlier is not null && odometerKm < earlier.OdometerKm)
            return $"The odometer reading is lower than the {Km(earlier.OdometerKm)} recorded on {Date(earlier.ServiceDate)}. Please check it.";

        var later = otherServices.Where(s => s.ServiceDate > serviceDate).MinBy(s => s.OdometerKm);
        if (later is not null && odometerKm > later.OdometerKm)
            return $"The odometer reading is higher than the {Km(later.OdometerKm)} recorded later, on {Date(later.ServiceDate)}. Please check it.";

        return null;
    }

    // Fixed (invariant) formats, so messages read the same whatever the server's regional settings are.
    private static string Km(int km) => km.ToString("#,##0", CultureInfo.InvariantCulture) + " km";
    private static string Date(DateOnly date) => date.ToString("dd MMM yyyy", CultureInfo.InvariantCulture);
}
