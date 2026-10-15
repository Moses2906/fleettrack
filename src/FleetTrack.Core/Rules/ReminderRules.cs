namespace FleetTrack.Core.Rules;

public enum ReminderKind
{
    LicenceDisk,
    Service
}

/// <summary>
/// Decides when a reminder email is due. Dates are grouped into "milestones" (30 days to go, 7 days to go,
/// overdue week 1, week 2, ...). One email is sent per milestone, which gives three properties:
/// - nobody gets the same reminder twice (the sent milestone is remembered);
/// - if the server was off on the exact day, the reminder still goes out the next day;
/// - renewing a disk or logging a service changes the due date, which starts the milestones afresh.
/// </summary>
public static class ReminderRules
{
    // Days-before-due milestones, largest first.
    private static readonly int[] LicenceMilestones = [30, 7];
    private static readonly int[] ServiceMilestones = [7];

    /// <returns>The milestone the due date has reached today, e.g. "30d", "7d", "overdue-w0", or null if none yet.</returns>
    public static string? CurrentMilestone(ReminderKind kind, DateOnly dueDate, DateOnly today)
    {
        var daysLeft = dueDate.DayNumber - today.DayNumber;

        // A disk is still valid on its expiry date, so "expired" starts the day after.
        // A service due today counts as due, not overdue.
        if (daysLeft < 0)
        {
            var weeksOverdue = (-daysLeft - 1) / 7; // week 0 = the first seven days overdue
            return $"overdue-w{weeksOverdue}";
        }
        if (kind == ReminderKind.LicenceDisk && daysLeft == 0)
            return "today";

        var milestones = kind == ReminderKind.LicenceDisk ? LicenceMilestones : ServiceMilestones;
        foreach (var days in milestones.Reverse())
        {
            if (daysLeft <= days)
                return $"{days}d";
        }
        return null;
    }

    /// <summary>Wording for the email, e.g. "expires in 6 days", "expired 9 days ago".</summary>
    public static string Describe(ReminderKind kind, DateOnly dueDate, DateOnly today)
    {
        var days = dueDate.DayNumber - today.DayNumber;
        var (soon, overdue) = kind == ReminderKind.LicenceDisk ? ("expires", "expired") : ("service due", "service overdue");
        return days switch
        {
            0 => kind == ReminderKind.LicenceDisk ? "expires today" : "service due today",
            1 => $"{soon} tomorrow",
            > 1 => $"{soon} in {days} days",
            -1 => kind == ReminderKind.LicenceDisk ? "expired yesterday" : "service overdue by 1 day",
            _ => kind == ReminderKind.LicenceDisk ? $"{overdue} {-days} days ago" : $"{overdue} by {-days} days",
        };
    }
}
