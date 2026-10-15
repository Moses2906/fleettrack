using System.Globalization;

namespace FleetTrack.Core.Rules;

/// <summary>A calendar month, e.g. November 2026. Written as "2026-11" in web addresses.</summary>
public readonly record struct YearMonth(int Year, int Month) : IComparable<YearMonth>
{
    public static YearMonth Of(DateOnly date) => new(date.Year, date.Month);

    public DateOnly FirstDay => new(Year, Month, 1);
    public DateOnly LastDay => FirstDay.AddMonths(1).AddDays(-1);

    public bool Contains(DateOnly date) => date.Year == Year && date.Month == Month;

    public YearMonth AddMonths(int months) => Of(FirstDay.AddMonths(months));

    /// <summary>"November 2026"</summary>
    public string Display => FirstDay.ToString("MMMM yyyy", CultureInfo.InvariantCulture);

    /// <summary>"Nov 2026"</summary>
    public string ShortDisplay => FirstDay.ToString("MMM yyyy", CultureInfo.InvariantCulture);

    /// <summary>"2026-11", the form used in web addresses.</summary>
    public override string ToString() => $"{Year:0000}-{Month:00}";

    public static bool TryParse(string? value, out YearMonth result)
    {
        result = default;
        if (!DateOnly.TryParseExact(value + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return false;
        result = Of(date);
        return true;
    }

    public int CompareTo(YearMonth other) => FirstDay.CompareTo(other.FirstDay);
}

public record MonthCount(YearMonth Month, int Count);

public record ExpiryOutlook(int AlreadyExpired, IReadOnlyList<MonthCount> Months);

public static class ExpiryCalendar
{
    /// <summary>
    /// How many disks expire in each of the coming months, starting with this one. Disks that have already
    /// expired are counted separately, so this month's figure only includes disks that are still valid today.
    /// </summary>
    public static ExpiryOutlook ByMonth(IEnumerable<DateOnly> expiryDates, DateOnly today, int months = 12)
    {
        var dates = expiryDates.ToList();
        var thisMonth = YearMonth.Of(today);
        var counts = Enumerable.Range(0, months)
            .Select(i => thisMonth.AddMonths(i))
            .Select(month => new MonthCount(month, dates.Count(d => d >= today && month.Contains(d))))
            .ToList();
        return new ExpiryOutlook(dates.Count(d => d < today), counts);
    }
}
