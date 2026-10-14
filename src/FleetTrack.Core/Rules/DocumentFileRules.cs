namespace FleetTrack.Core.Rules;

public sealed record DetectedFileType(string ContentType, string Description);

/// <summary>
/// Rules for uploaded document copies: PDF, JPG or PNG only, at most 10 MB.
///
/// The type is decided from the file's first bytes (its "magic number"), never from the file name
/// or the type the browser reports, because both are chosen by the uploader. A web page renamed to
/// "rc1.pdf" is rejected because it doesn't start with the PDF signature.
/// </summary>
public static class DocumentFileRules
{
    public const long MaxBytes = 10 * 1024 * 1024;
    public const int HeaderBytesNeeded = 8;

    public static readonly string[] AllowedExtensions = [".pdf", ".jpg", ".jpeg", ".png"];

    private static readonly byte[] PdfSignature = "%PDF-"u8.ToArray();
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] JpegSignature = [0xFF, 0xD8, 0xFF];

    /// <returns>The real file type, or null if it isn't an allowed PDF, PNG or JPEG.</returns>
    public static DetectedFileType? DetectType(ReadOnlySpan<byte> header)
    {
        if (header.StartsWith(PdfSignature)) return new("application/pdf", "PDF");
        if (header.StartsWith(PngSignature)) return new("image/png", "PNG image");
        if (header.StartsWith(JpegSignature)) return new("image/jpeg", "JPG image");
        return null;
    }

    public static bool HasAllowedExtension(string? fileName) =>
        AllowedExtensions.Contains(Path.GetExtension(fileName ?? "").ToLowerInvariant());

    /// <summary>
    /// Makes an uploaded file name safe to store and show: drops any folder path the browser sent,
    /// removes control characters and limits the length. It is only ever displayed, never used as a path.
    /// </summary>
    public static string CleanFileName(string? fileName)
    {
        var name = Path.GetFileName((fileName ?? "").Replace('\\', '/'));
        name = new string(name.Where(c => !char.IsControl(c)).ToArray()).Trim();
        if (name.Length == 0)
            name = "document";
        return name.Length <= 200 ? name : name[..(200 - Path.GetExtension(name).Length)] + Path.GetExtension(name);
    }

    public static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:0} KB",
        _ => $"{bytes / (1024.0 * 1024):0.0} MB",
    };
}
