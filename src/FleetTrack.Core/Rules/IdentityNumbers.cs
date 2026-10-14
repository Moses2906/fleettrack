using System.Globalization;
using System.Text.RegularExpressions;

namespace FleetTrack.Core.Rules;

/// <summary>Validation for the identifiers a licence holder can have.</summary>
public static partial class IdentityNumbers
{
    [GeneratedRegex(@"^\d{13}$")]
    private static partial Regex ThirteenDigits();

    // CIPC format: year of registration / six digits / entity type, e.g. 2014/208815/07
    [GeneratedRegex(@"^(18|19|20)\d{2}/\d{6}/\d{2}$")]
    private static partial Regex CompanyFormat();

    /// <summary>
    /// A South African ID number is YYMMDD SSSS C A Z:
    /// date of birth, sequence (gender), citizenship (0 or 1, or 2 for refugees), a legacy digit, and a check digit.
    /// The check digit uses the Luhn algorithm, which catches any single mistyped digit.
    /// </summary>
    public static bool IsValidSaIdNumber(string? value)
    {
        var id = Clean(value);
        if (!ThirteenDigits().IsMatch(id))
            return false;

        // The first six digits must be a real date (any century).
        if (!DateTime.TryParseExact(id[..6], "yyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            return false;

        if (id[10] is not ('0' or '1' or '2'))
            return false;

        return PassesLuhnCheck(id);
    }

    public static bool IsValidCompanyRegistrationNumber(string? value) =>
        CompanyFormat().IsMatch(Clean(value));

    /// <summary>Removes spaces, so "850412 5123 086" is accepted as typed from a document.</summary>
    public static string Clean(string? value) => (value ?? "").Replace(" ", "").Trim();

    /// <summary>Shows only the start and end, e.g. "8504•••••••86", so a full ID number isn't on every screen.</summary>
    public static string Mask(string? value)
    {
        var id = Clean(value);
        return id.Length <= 6 ? id : id[..4] + new string('•', id.Length - 6) + id[^2..];
    }

    private static bool PassesLuhnCheck(string digits)
    {
        var sum = 0;
        var doubleIt = false;
        // Work from the right: double every second digit, and subtract 9 if that gives more than 9.
        for (var i = digits.Length - 1; i >= 0; i--)
        {
            var digit = digits[i] - '0';
            if (doubleIt)
            {
                digit *= 2;
                if (digit > 9)
                    digit -= 9;
            }
            sum += digit;
            doubleIt = !doubleIt;
        }
        return sum % 10 == 0;
    }
}
