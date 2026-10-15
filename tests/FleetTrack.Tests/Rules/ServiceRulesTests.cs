using FleetTrack.Core.Entities;
using FleetTrack.Core.Rules;

namespace FleetTrack.Tests.Rules;

public class ServiceRulesTests
{
    private static readonly DateOnly Today = new(2026, 10, 15);

    [Theory]
    [InlineData(null, ServiceReminder.None)]
    [InlineData(-1, ServiceReminder.Overdue)]
    [InlineData(0, ServiceReminder.DueSoon)]
    [InlineData(30, ServiceReminder.DueSoon)]
    [InlineData(31, ServiceReminder.Scheduled)]
    public void Reminder_DependsOnHowFarAwayTheServiceIs(int? daysAway, ServiceReminder expected)
    {
        DateOnly? due = daysAway is null ? null : Today.AddDays(daysAway.Value);
        Assert.Equal(expected, ServiceRules.GetReminder(due, Today));
    }

    [Fact]
    public void NextServiceDue_ComesFromTheMostRecentService()
    {
        var services = new[]
        {
            new ServiceRecord { Id = 1, ServiceDate = new DateOnly(2026, 1, 10), NextServiceDueDate = new DateOnly(2026, 7, 10) },
            new ServiceRecord { Id = 2, ServiceDate = new DateOnly(2026, 7, 8), NextServiceDueDate = new DateOnly(2027, 1, 8) },
        };

        Assert.Equal(new DateOnly(2027, 1, 8), ServiceRules.NextServiceDue(services));
    }

    [Fact]
    public void LoggingARepairWithoutANextDate_ClearsTheOldReminder()
    {
        var services = new[]
        {
            new ServiceRecord { Id = 1, ServiceDate = new DateOnly(2026, 1, 10), NextServiceDueDate = new DateOnly(2026, 7, 10) },
            new ServiceRecord { Id = 2, ServiceDate = new DateOnly(2026, 7, 8), NextServiceDueDate = null },
        };

        Assert.Null(ServiceRules.NextServiceDue(services));
    }

    [Fact]
    public void OdometerLowerThanAnEarlierReading_IsRefused()
    {
        var earlier = new[] { new ServiceRecord { ServiceDate = new DateOnly(2026, 1, 10), OdometerKm = 85_000 } };

        Assert.NotNull(ServiceRules.CheckOdometer(8_500, new DateOnly(2026, 7, 1), earlier)); // a missing digit
        Assert.Null(ServiceRules.CheckOdometer(92_000, new DateOnly(2026, 7, 1), earlier));
    }

    [Fact]
    public void OdometerHigherThanALaterReading_IsRefused()
    {
        var later = new[] { new ServiceRecord { ServiceDate = new DateOnly(2026, 7, 10), OdometerKm = 92_000 } };

        Assert.NotNull(ServiceRules.CheckOdometer(950_000, new DateOnly(2026, 1, 1), later));
    }

    [Theory]
    [InlineData(NextServiceBooking.SameDateNextYear, "2027-10-15")]
    [InlineData(NextServiceBooking.InSixMonths, "2027-04-15")]
    [InlineData(NextServiceBooking.DontBook, null)]
    public void NextService_IsBookedFromTheDayItWasDone(NextServiceBooking booking, string? expected)
    {
        DateOnly? expectedDate = expected is null ? null : DateOnly.Parse(expected);
        Assert.Equal(expectedDate, ServiceRules.NextServiceDate(booking, Today));
    }

    [Fact]
    public void PickADate_UsesThePickedDate()
    {
        Assert.Equal(new DateOnly(2027, 3, 1), ServiceRules.NextServiceDate(NextServiceBooking.PickADate, Today, new DateOnly(2027, 3, 1)));
    }

    [Fact]
    public void SameDateNextYear_OnTheTwentyNinthOfFebruary_FallsOnTheTwentyEighth()
    {
        Assert.Equal(new DateOnly(2029, 2, 28), ServiceRules.NextServiceDate(NextServiceBooking.SameDateNextYear, new DateOnly(2028, 2, 29)));
    }
}
