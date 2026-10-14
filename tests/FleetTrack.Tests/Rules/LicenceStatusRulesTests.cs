using FleetTrack.Core.Rules;

namespace FleetTrack.Tests.Rules;

public class LicenceStatusRulesTests
{
    private static readonly DateOnly Today = new(2026, 10, 14);

    [Theory]
    [InlineData(-365, LicenceStatus.Expired)]
    [InlineData(-1, LicenceStatus.Expired)]
    [InlineData(0, LicenceStatus.DueSoon)]   // still valid on the expiry date itself
    [InlineData(1, LicenceStatus.DueSoon)]
    [InlineData(30, LicenceStatus.DueSoon)]  // boundary: exactly 30 days away
    [InlineData(31, LicenceStatus.Valid)]
    [InlineData(300, LicenceStatus.Valid)]
    public void GetStatus_ReturnsExpectedStatus(int daysUntilExpiry, LicenceStatus expected)
    {
        var status = LicenceStatusRules.GetStatus(Today.AddDays(daysUntilExpiry), Today);

        Assert.Equal(expected, status);
    }
}
