namespace FleetTrack.Core.Rules;

public enum LicenceStatus
{
    Valid,
    DueSoon,
    Expired
}

/// <summary>
/// Works out a licence disk's status from its expiry date. The status is never stored
/// in the database because it changes with the calendar, not when a row is edited.
/// </summary>
public static class LicenceStatusRules
{
    public const int DueSoonDays = 30;

    /// <param name="today">Passed in rather than read from the clock so the rule is easy to test.</param>
    public static LicenceStatus GetStatus(DateOnly expiryDate, DateOnly today)
    {
        // A disk is still valid on its expiry date itself.
        if (expiryDate < today)
            return LicenceStatus.Expired;

        if (expiryDate <= today.AddDays(DueSoonDays))
            return LicenceStatus.DueSoon;

        return LicenceStatus.Valid;
    }
}
