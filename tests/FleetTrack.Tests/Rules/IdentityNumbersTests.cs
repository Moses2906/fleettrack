using FleetTrack.Core.Rules;

namespace FleetTrack.Tests.Rules;

public class IdentityNumbersTests
{
    [Theory]
    [InlineData("8504125123086")]
    [InlineData("9207300154087")]
    [InlineData("850412 5123 086")] // spaces as printed on an ID book
    public void ValidSaIdNumbers_AreAccepted(string id)
    {
        Assert.True(IdentityNumbers.IsValidSaIdNumber(id));
    }

    [Theory]
    [InlineData("8504125123087")]  // wrong check digit (one digit mistyped)
    [InlineData("8513125123086")]  // month 13
    [InlineData("850412512308")]   // 12 digits
    [InlineData("85041251230861")] // 14 digits
    [InlineData("85041251230AB")]
    [InlineData("")]
    [InlineData(null)]
    public void InvalidSaIdNumbers_AreRejected(string? id)
    {
        Assert.False(IdentityNumbers.IsValidSaIdNumber(id));
    }

    [Theory]
    [InlineData("2014/208815/07", true)]
    [InlineData("2009/045512/23", true)]
    [InlineData("2014-208815-07", false)]
    [InlineData("14/208815/07", false)]
    [InlineData("2014/20881/07", false)]
    public void CompanyRegistrationNumbers_MustMatchCipcFormat(string value, bool expected)
    {
        Assert.Equal(expected, IdentityNumbers.IsValidCompanyRegistrationNumber(value));
    }

    [Fact]
    public void Mask_HidesTheMiddleOfAnIdNumber()
    {
        Assert.Equal("8504•••••••86", IdentityNumbers.Mask("8504125123086"));
    }
}
