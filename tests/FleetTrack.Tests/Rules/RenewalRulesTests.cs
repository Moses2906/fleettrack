using FleetTrack.Core.Entities;
using FleetTrack.Core.Rules;

namespace FleetTrack.Tests.Rules;

public class RenewalRulesTests
{
    private static readonly DateOnly Today = new(2026, 10, 15);

    private static Vehicle VehicleExpiring(DateOnly expiry) => new() { RegistrationNumber = "CA 123-456", LicenceExpiryDate = expiry };

    [Fact]
    public void Renewal_MovesTheExpiryDateForward_AndRemembersTheOldOne()
    {
        var vehicle = VehicleExpiring(new DateOnly(2026, 10, 31));
        var renewal = new LicenceRenewal { DatePurchased = Today, NewExpiryDate = new DateOnly(2027, 10, 31) };

        var changed = RenewalRules.Apply(vehicle, renewal);

        Assert.True(changed);
        Assert.Equal(new DateOnly(2027, 10, 31), vehicle.LicenceExpiryDate);
        Assert.Equal(new DateOnly(2026, 10, 31), renewal.PreviousExpiryDate);
    }

    [Fact]
    public void RenewingAnExpiredDisk_MakesTheVehicleValidAgain()
    {
        var vehicle = VehicleExpiring(new DateOnly(2026, 8, 30));
        Assert.Equal(LicenceStatus.Expired, vehicle.GetLicenceStatus(Today));

        RenewalRules.Apply(vehicle, new LicenceRenewal { DatePurchased = Today, NewExpiryDate = new DateOnly(2027, 8, 30) });

        Assert.Equal(LicenceStatus.Valid, vehicle.GetLicenceStatus(Today));
    }

    [Fact]
    public void EnteringAnOldRenewalForTheHistory_DoesNotMoveTheExpiryDateBackwards()
    {
        var vehicle = VehicleExpiring(new DateOnly(2027, 5, 31));
        var oldRenewal = new LicenceRenewal { DatePurchased = new DateOnly(2024, 5, 20), NewExpiryDate = new DateOnly(2025, 5, 31) };

        var changed = RenewalRules.Apply(vehicle, oldRenewal);

        Assert.False(changed);
        Assert.Equal(new DateOnly(2027, 5, 31), vehicle.LicenceExpiryDate);
    }

    [Fact]
    public void DeletingTheLatestRenewal_PutsTheOldExpiryDateBack()
    {
        var vehicle = VehicleExpiring(new DateOnly(2026, 10, 31));
        var mistake = new LicenceRenewal { DatePurchased = Today, NewExpiryDate = new DateOnly(2027, 10, 31) };
        RenewalRules.Apply(vehicle, mistake);

        var changed = RenewalRules.Revert(vehicle, mistake);

        Assert.True(changed);
        Assert.Equal(new DateOnly(2026, 10, 31), vehicle.LicenceExpiryDate);
    }

    [Fact]
    public void DeletingAnOlderRenewal_LeavesTheCurrentExpiryDateAlone()
    {
        var vehicle = VehicleExpiring(new DateOnly(2027, 10, 31));
        var older = new LicenceRenewal { NewExpiryDate = new DateOnly(2026, 10, 31), PreviousExpiryDate = new DateOnly(2025, 10, 31) };

        Assert.False(RenewalRules.Revert(vehicle, older));
        Assert.Equal(new DateOnly(2027, 10, 31), vehicle.LicenceExpiryDate);
    }

    [Fact]
    public void SuggestedExpiry_IsOneYearAfterTheOldDiskExpires()
    {
        Assert.Equal(new DateOnly(2027, 11, 30), RenewalRules.SuggestNewExpiry(new DateOnly(2026, 11, 30)));
    }

    [Theory]
    [InlineData("2026-10-16", "2027-10-31")] // bought tomorrow
    [InlineData("2026-10-15", "2026-10-15")] // expires the day it was bought
    [InlineData("2026-10-15", "2029-10-31")] // three years: a typo
    public void ImpossibleDates_AreRejected(string purchased, string newExpiry)
    {
        Assert.NotNull(RenewalRules.Validate(DateOnly.Parse(purchased), DateOnly.Parse(newExpiry), Today));
    }

    [Fact]
    public void NormalDates_AreAccepted()
    {
        Assert.Null(RenewalRules.Validate(new DateOnly(2026, 10, 10), new DateOnly(2027, 10, 31), Today));
    }
}
