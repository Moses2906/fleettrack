using System.Security.Cryptography;
using FleetTrack.Core.Rules;

namespace FleetTrack.Web.Documents;

/// <summary>
/// An uploaded file that has passed the upload rules: PDF, JPG or PNG by its real content, at most 10 MB.
/// Shared by vehicle documents and service invoices, so both follow exactly the same rules.
/// </summary>
public sealed class UploadedFile
{
    private UploadedFile(byte[] content, string fileName, string contentType, string sha256)
    {
        Content = content;
        FileName = fileName;
        ContentType = contentType;
        Sha256 = sha256;
    }

    public byte[] Content { get; }
    public string FileName { get; }
    public string ContentType { get; }
    public string Sha256 { get; }
    public long Size => Content.Length;

    /// <summary>Reads and checks an upload. Returns the file, or an error message that is safe to show.</summary>
    public static async Task<(UploadedFile? File, string? Error)> ReadAsync(Stream content, string fileName, CancellationToken cancellationToken = default)
    {
        if (!DocumentFileRules.HasAllowedExtension(fileName))
            return (null, "Only PDF, JPG and PNG files can be uploaded.");

        // Read at most 10 MB, refusing to go further even if the browser lied about the size.
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await content.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > DocumentFileRules.MaxBytes)
                return (null, "That file is larger than 10 MB. Please scan at a lower resolution or compress it.");
            buffer.Write(chunk, 0, read);
        }
        if (buffer.Length == 0)
            return (null, "That file is empty.");

        var bytes = buffer.ToArray();
        // The real type comes from the file's first bytes, never from its name.
        var detected = DocumentFileRules.DetectType(bytes.AsSpan(0, Math.Min(bytes.Length, DocumentFileRules.HeaderBytesNeeded)));
        if (detected is null)
            return (null, "That file isn't a real PDF, JPG or PNG. If it is a scan, save it again as PDF and retry.");

        return (new UploadedFile(bytes, DocumentFileRules.CleanFileName(fileName), detected.ContentType,
            Convert.ToHexStringLower(SHA256.HashData(bytes))), null);
    }

    /// <summary>True if stored content still matches the fingerprint taken at upload.</summary>
    public static bool MatchesFingerprint(byte[] content, string sha256) =>
        string.Equals(Convert.ToHexStringLower(SHA256.HashData(content)), sha256, StringComparison.Ordinal);
}
