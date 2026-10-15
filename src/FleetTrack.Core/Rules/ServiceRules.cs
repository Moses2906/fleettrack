using FleetTrack.Core.Entities;

namespace FleetTrack.Core.Rules;

public enum ServiceReminder
{
    None,
    Scheduled,
    DueSoon,
    Overdue
}

public static class ServiceRules
{
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
            return $"The odometer reading is lower than the {earlier.OdometerKm:N0} km recorded on {earlier.ServiceDate:dd MMM yyyy}. Please check it.";

        var later = otherServices.Where(s => s.ServiceDate > serviceDate).MinBy(s => s.OdometerKm);
        if (later is not null && odometerKm > later.OdometerKm)
            return $"The odometer reading is higher than the {later.OdometerKm:N0} km recorded later, on {later.ServiceDate:dd MMM yyyy}. Please check it.";

        return null;
    }
}
