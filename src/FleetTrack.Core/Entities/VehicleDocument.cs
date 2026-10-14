namespace FleetTrack.Core.Entities;

public class VehicleDocument
{
    public int Id { get; set; }
    public int VehicleId { get; set; }
    public Vehicle Vehicle { get; set; } = null!;

    public DocumentType DocumentType { get; set; }

    /// <summary>The name the file had on the uploader's computer. Shown to users, never used as a path.</summary>
    public string OriginalFileName { get; set; } = string.Empty;

    /// <summary>Random GUID-based name on disk, so file names can't be guessed or used for path tricks.</summary>
    public string StoredFileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }

    /// <summary>Hash of the file contents, used to detect if a stored file has been altered.</summary>
    public string Sha256 { get; set; } = string.Empty;

    /// <summary>Free-text notes, e.g. "certified copy, original kept in office safe".</summary>
    public string? Notes { get; set; }

    public DateTime UploadedAtUtc { get; set; }
    /// <summary>Login name as text, not a foreign key, so history survives the user account being removed.</summary>
    public string UploadedBy { get; set; } = string.Empty;
}
