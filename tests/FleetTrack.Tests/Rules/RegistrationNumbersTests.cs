using FleetTrack.Core.Rules;

namespace FleetTrack.Tests.Rules;

public class RegistrationNumbersTests
{
    [Theory]
    [InlineData("HJ 45 KL GP")]  // current Gauteng
    [InlineData("BCD 123 MP")]   // older suffix style
    [InlineData("GZD 731 L")]    // Limpopo
    [InlineData("CA 123-456")]   // Western Cape
    [InlineData("CY 51207")]
    [InlineData("ND 304-118")]   // older KZN
    [InlineData("JOHN-GP")]      // personalised
    [InlineData("FLEET 1-WP")]
    [InlineData("  ca 123 - 456 ")] // messy input is accepted after normalising
    public void IsValid_AcceptsSouthAfricanFormats(string input)
    {
        Assert.True(RegistrationNumbers.IsValid(input));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("12345")]
    [InlineData("HJ 45 KL XX")]  // unknown province
    [InlineData("ABC")]
    [InlineData("<script>")]
    public void IsValid_RejectsInvalidInput(string? input)
    {
        Assert.False(RegistrationNumbers.IsValid(input));
    }

    [Fact]
    public void Normalise_UppercasesAndTidiesSpacing()
    {
        Assert.Equal("CA 123-456", RegistrationNumbers.Normalise("  ca  123 - 456 "));
    }

    [Fact]
    public void ToKey_TreatsDifferentSpacingAsTheSamePlate()
    {
        Assert.Equal(RegistrationNumbers.ToKey("CA 123-456"), RegistrationNumbers.ToKey("ca123456"));
    }
}
