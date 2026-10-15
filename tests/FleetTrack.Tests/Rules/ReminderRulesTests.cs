using FleetTrack.Core.Rules;

namespace FleetTrack.Tests.Rules;

public class ReminderRulesTests
{
    private static readonly DateOnly Today = new(2026, 10, 15);

    [Theory]
    [InlineData(45, null)]
    [InlineData(31, null)]
    [InlineData(30, "30d")]
    [InlineData(29, "30d")]   // still the 30-day milestone if the server was off yesterday
    [InlineData(8, "30d")]
    [InlineData(7, "7d")]
    [InlineData(1, "7d")]
    [InlineData(0, "today")]
    [InlineData(-1, "overdue-w0")]
    [InlineData(-7, "overdue-w0")]
    [InlineData(-8, "overdue-w1")] // a weekly nag while the disk stays expired
    [InlineData(-15, "overdue-w2")]
    public void LicenceDisk_Milestones(int daysLeft, string? expected)
    {
        Assert.Equal(expected, ReminderRules.CurrentMilestone(ReminderKind.LicenceDisk, Today.AddDays(daysLeft), Today));
    }

    [Theory]
    [InlineData(30, null)]  // services only get a one-week warning
    [InlineData(8, null)]
    [InlineData(7, "7d")]
    [InlineData(0, "7d")]
    [InlineData(-1, "overdue-w0")]
    [InlineData(-8, "overdue-w1")]
    public void Service_Milestones(int daysLeft, string? expected)
    {
        Assert.Equal(expected, ReminderRules.CurrentMilestone(ReminderKind.Service, Today.AddDays(daysLeft), Today));
    }

    [Fact]
    public void EachDayWithinAMilestone_GivesTheSameMilestone_SoTheEmailIsSentOnce()
    {
        var expiry = Today.AddDays(20);
        var milestones = Enumerable.Range(0, 10)
            .Select(d => ReminderRules.CurrentMilestone(ReminderKind.LicenceDisk, expiry, Today.AddDays(d)))
            .Distinct();

        Assert.Equal(["30d"], milestones);
    }

    [Theory]
    [InlineData(ReminderKind.LicenceDisk, 6, "expires in 6 days")]
    [InlineData(ReminderKind.LicenceDisk, 0, "expires today")]
    [InlineData(ReminderKind.LicenceDisk, -9, "expired 9 days ago")]
    [InlineData(ReminderKind.Service, 1, "service due tomorrow")]
    [InlineData(ReminderKind.Service, -3, "service overdue by 3 days")]
    public void Describe_UsesPlainWording(ReminderKind kind, int daysLeft, string expected)
    {
        Assert.Equal(expected, ReminderRules.Describe(kind, Today.AddDays(daysLeft), Today));
    }
}
