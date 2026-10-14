namespace FleetTrack.Core.Rules;

/// <summary>
/// "Today" for licence purposes is the date in South Africa, even if the server runs in UTC.
/// Taking a <see cref="TimeProvider"/> lets tests pretend it is any date they like.
/// </summary>
public static class SouthAfricanTime
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Africa/Johannesburg");

    public static DateOnly Today(TimeProvider clock) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), Zone).DateTime);

    /// <summary>Converts a UTC timestamp from the database to South African time for display.</summary>
    public static DateTime ToLocal(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone);
}
