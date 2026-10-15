using FleetTrack.Core.Rules;

namespace FleetTrack.Tests.Rules;

public class YearMonthTests
{
    private static readonly DateOnly Today = new(2026, 10, 15);

    [Fact]
    public void FirstAndLastDay_CoverTheWholeMonth()
    {
        var february = new YearMonth(2028, 2); // a leap year

        Assert.Equal(new DateOnly(2028, 2, 1), february.FirstDay);
        Assert.Equal(new DateOnly(2028, 2, 29), february.LastDay);
        Assert.True(february.Contains(new DateOnly(2028, 2, 29)));
        Assert.False(february.Contains(new DateOnly(2028, 3, 1)));
    }

    [Fact]
    public void AddMonths_CrossesTheYearEnd()
    {
        Assert.Equal(new YearMonth(2027, 2), new YearMonth(2026, 11).AddMonths(3));
    }

    [Theory]
    [InlineData("2026-11", true)]
    [InlineData("2026-13", false)]
    [InlineData("November", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void TryParse_AcceptsTheWebAddressFormat(string? value, bool valid)
    {
        Assert.Equal(valid, YearMonth.TryParse(value, out var month));
        if (valid)
            Assert.Equal(value, month.ToString());
    }

    [Fact]
    public void Display_IsPlainEnglish()
    {
        Assert.Equal("November 2026", new YearMonth(2026, 11).Display);
        Assert.Equal("Nov 2026", new YearMonth(2026, 11).ShortDisplay);
    }

    [Fact]
    public void ExpiryOutlook_CountsDisksPerComingMonth_AndExpiredSeparately()
    {
        DateOnly[] expiries =
        [
            new(2026, 10, 2),   // already expired: not counted in October
            new(2026, 10, 15),  // expires today: still valid, counts in October
            new(2026, 10, 31),
            new(2026, 11, 10),
            new(2027, 9, 30),   // the 12th month
            new(2027, 10, 1),   // beyond the 12 months shown
        ];

        var outlook = ExpiryCalendar.ByMonth(expiries, Today);

        Assert.Equal(1, outlook.AlreadyExpired);
        Assert.Equal(12, outlook.Months.Count);
        Assert.Equal(new MonthCount(new YearMonth(2026, 10), 2), outlook.Months[0]);
        Assert.Equal(new MonthCount(new YearMonth(2026, 11), 1), outlook.Months[1]);
        Assert.Equal(new MonthCount(new YearMonth(2027, 9), 1), outlook.Months[11]);
        Assert.Equal(4, outlook.Months.Sum(m => m.Count));
    }
}
