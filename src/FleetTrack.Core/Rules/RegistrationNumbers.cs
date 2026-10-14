using System.Text.RegularExpressions;

namespace FleetTrack.Core.Rules;

/// <summary>
/// Normalises and validates South African vehicle registration numbers.
/// </summary>
public static partial class RegistrationNumbers
{
    // Gauteng, Eastern Cape, Free State, Limpopo, Mpumalanga, North West, Northern Cape, KZN (new style)
    private const string ProvinceSuffix = "(GP|EC|FS|L|MP|NW|NC|ZN)";

    // "HJ 45 KL GP" (current Gauteng style) or "BCD 123 MP" (older suffix style)
    [GeneratedRegex($"^([A-Z]{{2}} ?\\d{{2}} ?[A-Z]{{2}}|[A-Z]{{3}} ?\\d{{3}}) ?{ProvinceSuffix}$")]
    private static partial Regex SuffixStyle();

    // Western Cape and older KZN prefix style: "CA 123-456", "CY 12345", "ND 123-456"
    [GeneratedRegex("^[CN][A-Z]{1,2} ?(\\d{1,6}|\\d{1,3}-\\d{1,3})$")]
    private static partial Regex PrefixStyle();

    // Personalised plates: "JOHN-GP", "FLEET 1-WP"
    [GeneratedRegex("^[A-Z0-9]([A-Z0-9 ]{0,6}[A-Z0-9])?-(GP|EC|FS|L|MP|NW|NC|ZN|WP|KZN)$")]
    private static partial Regex Personalised();

    [GeneratedRegex("\\s+")]
    private static partial Regex Whitespace();

    [GeneratedRegex("[^A-Z0-9]")]
    private static partial Regex NonAlphanumeric();

    /// <summary>Uppercases, trims and tidies spacing: " ca 123 - 456 " becomes "CA 123-456".</summary>
    public static string Normalise(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        var value = Whitespace().Replace(input.Trim().ToUpperInvariant(), " ");
        return value.Replace(" -", "-").Replace("- ", "-");
    }

    /// <summary>
    /// Letters and digits only: "CA 123-456" and "CA123456" both become "CA123456".
    /// A unique index on this key stops the same plate being captured twice with different spacing.
    /// </summary>
    public static string ToKey(string? input) => NonAlphanumeric().Replace(Normalise(input), "");

    public static bool IsValid(string? input)
    {
        var value = Normalise(input);
        return value.Length is > 0 and <= 15
            && (SuffixStyle().IsMatch(value) || PrefixStyle().IsMatch(value) || Personalised().IsMatch(value));
    }
}
