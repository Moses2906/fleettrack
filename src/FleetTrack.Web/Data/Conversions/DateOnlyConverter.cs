using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FleetTrack.Web.Data.Conversions;

/// <summary>
/// The Oracle provider stores C#'s DateOnly as text by default. Converting through DateTime
/// lets it be stored as a real Oracle DATE, so SQL like "WHERE licence_expiry_date &lt; SYSDATE" works.
/// </summary>
public class DateOnlyConverter() : ValueConverter<DateOnly, DateTime>(
    dateOnly => dateOnly.ToDateTime(TimeOnly.MinValue),
    dateTime => DateOnly.FromDateTime(dateTime));
