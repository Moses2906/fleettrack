using System.Security.Cryptography;
using System.Text.Json;
using FleetTrack.Core.Entities;
using FleetTrack.Core.Rules;
using FleetTrack.Core.Security;
using FleetTrack.Web.Data;
using FleetTrack.Web.Security;
using FleetTrack.Web.Services;
using Microsoft.EntityFrameworkCore;

namespace FleetTrack.Web.Documents;

public record DocumentInfo(
    int Id,
    int VehicleId,
    DocumentType DocumentType,
    string OriginalFileName,
    string ContentType,
    long FileSizeBytes,
    DateTime UploadedAtUtc,
    string UploadedBy,
    string? Notes);

/// <summary>One row of the Documents overview: what we hold for each vehicle.</summary>
public record VehicleDocumentStatus(
    int VehicleId,
    string RegistrationNumber,
    string MakeAndModel,
    IReadOnlyDictionary<DocumentType, DocumentSlotState> Slots)
{
    public int UploadedCount => Slots.Values.Count(s => s == DocumentSlotState.Uploaded);
}

public enum DocumentSlotState
{
    /// <summary>A copy is stored in FleetTrack.</summary>
    Uploaded,
    /// <summary>Ticked on the checklist (e.g. the paper copy is in the office) but no copy uploaded.</summary>
    TickedOnly,
    Missing
}

public record DocumentFile(byte[] Content, string ContentType, string FileName);

public static class DocumentTypes
{
    public static readonly DocumentType[] All = [DocumentType.Rc1, DocumentType.Brnc, DocumentType.ProxyLetter, DocumentType.ProxyId];

    public static string Name(DocumentType type) => type switch
    {
        DocumentType.Rc1 => "RC1",
        DocumentType.Brnc => "BRNC",
        DocumentType.ProxyLetter => "Proxy letter",
        DocumentType.ProxyId => "Proxy ID",
        _ => type.ToString(),
    };

    public static string Description(DocumentType type) => type switch
    {
        DocumentType.Rc1 => "Registration certificate",
        DocumentType.Brnc => "Business registration number certificate",
        DocumentType.ProxyLetter => "Letter appointing the proxy",
        DocumentType.ProxyId => "Copy of the proxy's ID",
        _ => "",
    };
}

/// <summary>Uploading, viewing and deleting stored copies of vehicle documents.</summary>
public class DocumentService(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    IDocumentStorage storage,
    PermissionService permissions,
    CurrentUserService currentUser,
    AuditService audit,
    ILogger<DocumentService> logger)
{
    public async Task<List<DocumentInfo>> ListForVehicleAsync(int vehicleId)
    {
        await permissions.DemandAsync(Permission.ViewDocuments);
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.VehicleDocuments.AsNoTracking()
            .Where(d => d.VehicleId == vehicleId)
            .Select(d => new DocumentInfo(d.Id, d.VehicleId, d.DocumentType, d.OriginalFileName, d.ContentType,
                d.FileSizeBytes, d.UploadedAtUtc, d.UploadedBy, d.Notes))
            .ToListAsync();
    }

    public async Task<List<VehicleDocumentStatus>> OverviewAsync()
    {
        await permissions.DemandAsync(Permission.ViewDocuments);
        await using var db = await dbFactory.CreateDbContextAsync();

        var vehicles = await db.Vehicles.AsNoTracking().OrderBy(v => v.RegistrationNumber)
            .Select(v => new { v.Id, v.RegistrationNumber, v.Make, v.Model, v.HasRc1, v.HasBrnc, v.HasProxyLetter, v.HasProxyId })
            .ToListAsync();
        var uploaded = (await db.VehicleDocuments.AsNoTracking().Select(d => new { d.VehicleId, d.DocumentType }).ToListAsync())
            .Select(d => (d.VehicleId, d.DocumentType))
            .ToHashSet();

        return vehicles.Select(v =>
        {
            var ticked = new Dictionary<DocumentType, bool>
            {
                [DocumentType.Rc1] = v.HasRc1, [DocumentType.Brnc] = v.HasBrnc,
                [DocumentType.ProxyLetter] = v.HasProxyLetter, [DocumentType.ProxyId] = v.HasProxyId,
            };
            var slots = DocumentTypes.All.ToDictionary(t => t, t =>
                uploaded.Contains((v.Id, t)) ? DocumentSlotState.Uploaded
                : ticked[t] ? DocumentSlotState.TickedOnly
                : DocumentSlotState.Missing);
            return new VehicleDocumentStatus(v.Id, v.RegistrationNumber, $"{v.Make} {v.Model}", slots);
        }).ToList();
    }

    /// <summary>
    /// Stores a copy, replacing any existing copy of the same type, and ticks it on the vehicle's checklist.
    /// </summary>
    public async Task<OperationResult> UploadAsync(int vehicleId, DocumentType type, Stream content, string fileName, string? notes,
        CancellationToken cancellationToken = default)
    {
        await permissions.DemandAsync(Permission.UploadDocuments);

        if (!DocumentFileRules.HasAllowedExtension(fileName))
            return OperationResult.Fail("Only PDF, JPG and PNG files can be uploaded.");

        // Read into memory (at most 10 MB), refusing to read past the limit even if the browser lied about the size.
        using var buffer = new MemoryStream();
        var limited = new byte[81920];
        int read;
        while ((read = await content.ReadAsync(limited, cancellationToken)) > 0)
        {
            if (buffer.Length + read > DocumentFileRules.MaxBytes)
                return OperationResult.Fail("That file is larger than 10 MB. Please scan at a lower resolution or compress it.");
            buffer.Write(limited, 0, read);
        }
        if (buffer.Length == 0)
            return OperationResult.Fail("That file is empty.");

        var detected = DocumentFileRules.DetectType(buffer.GetBuffer().AsSpan(0, (int)Math.Min(buffer.Length, DocumentFileRules.HeaderBytesNeeded)));
        if (detected is null)
            return OperationResult.Fail("That file isn't a real PDF, JPG or PNG. If it is a scan, save it again as PDF and retry.");

        var hash = Convert.ToHexStringLower(SHA256.HashData(buffer.GetBuffer().AsSpan(0, (int)buffer.Length)));

        await using var db = await dbFactory.CreateDbContextAsync();
        var vehicle = await db.Vehicles.SingleOrDefaultAsync(v => v.Id == vehicleId, cancellationToken);
        if (vehicle is null)
            return OperationResult.Fail("This vehicle has been deleted.");

        // 1. Save the new file first...
        buffer.Position = 0;
        var storedName = await storage.SaveAsync(buffer, cancellationToken);

        var actor = await currentUser.GetActorAsync();
        var document = await db.VehicleDocuments.SingleOrDefaultAsync(d => d.VehicleId == vehicleId && d.DocumentType == type, cancellationToken);
        var oldStoredName = document?.StoredFileName;
        if (document is null)
        {
            document = new VehicleDocument { VehicleId = vehicleId, DocumentType = type };
            db.VehicleDocuments.Add(document);
        }
        document.OriginalFileName = DocumentFileRules.CleanFileName(fileName);
        document.StoredFileName = storedName;
        document.ContentType = detected.ContentType;
        document.FileSizeBytes = buffer.Length;
        document.Sha256 = hash;
        document.UploadedAtUtc = DateTime.UtcNow;
        document.UploadedBy = actor.UserName ?? "unknown";
        document.Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();

        // Uploading a copy ticks it off on the checklist.
        vehicle.SetDocumentHeld(type, true);

        // 2. ...then record it in the database. If that fails, remove the new file so nothing is left behind.
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            storage.Delete(storedName);
            throw;
        }

        // 3. Only now that the database points at the new file is the replaced copy removed.
        if (oldStoredName is not null)
            TryDeleteFile(oldStoredName);

        return OperationResult.Created(document.Id);
    }

    public async Task<OperationResult> UpdateNotesAsync(int documentId, string? notes)
    {
        await permissions.DemandAsync(Permission.UploadDocuments);
        if (notes?.Length > 2000)
            return OperationResult.Fail("Notes can be at most 2000 characters.");

        await using var db = await dbFactory.CreateDbContextAsync();
        var document = await db.VehicleDocuments.FindAsync(documentId);
        if (document is null)
            return OperationResult.Fail("This document has been deleted.");
        document.Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        await db.SaveChangesAsync();
        return OperationResult.Ok;
    }

    /// <param name="untick">Also untick the checklist item (leave it ticked if the paper copy is still held).</param>
    public async Task<OperationResult> DeleteAsync(int documentId, bool untick)
    {
        await permissions.DemandAsync(Permission.DeleteRecords);
        await using var db = await dbFactory.CreateDbContextAsync();
        var document = await db.VehicleDocuments.Include(d => d.Vehicle).SingleOrDefaultAsync(d => d.Id == documentId);
        if (document is null)
            return OperationResult.Ok;

        db.VehicleDocuments.Remove(document);
        if (untick)
            document.Vehicle.SetDocumentHeld(document.DocumentType, false);
        await db.SaveChangesAsync();

        TryDeleteFile(document.StoredFileName);
        return OperationResult.Ok;
    }

    /// <summary>
    /// Returns a stored file for viewing or downloading, and records that in the audit log.
    /// Returns null if the document doesn't exist or the file fails its integrity check.
    /// </summary>
    public async Task<DocumentFile?> OpenAsync(int documentId, bool download, CancellationToken cancellationToken = default)
    {
        await permissions.DemandAsync(Permission.ViewDocuments);
        await using var db = await dbFactory.CreateDbContextAsync();
        var document = await db.VehicleDocuments.AsNoTracking().SingleOrDefaultAsync(d => d.Id == documentId, cancellationToken);
        if (document is null)
            return null;

        byte[] content;
        try
        {
            content = await storage.ReadAsync(document.StoredFileName, cancellationToken);
        }
        catch (FileNotFoundException)
        {
            logger.LogError("Document {DocumentId} is in the database but its file {StoredName} is missing.", documentId, document.StoredFileName);
            return null;
        }

        // The hash recorded at upload must still match, otherwise the file on disk has been altered or corrupted.
        if (!string.Equals(Convert.ToHexStringLower(SHA256.HashData(content)), document.Sha256, StringComparison.Ordinal))
        {
            logger.LogError("Document {DocumentId} failed its integrity check: the stored file has changed since upload.", documentId);
            return null;
        }

        await audit.LogAsync(download ? AuditAction.Download : AuditAction.View, nameof(VehicleDocument), document.Id.ToString(),
            JsonSerializer.Serialize(new { document.VehicleId, DocumentType = document.DocumentType.ToString() }));

        return new DocumentFile(content, document.ContentType, document.OriginalFileName);
    }

    /// <summary>Removes the files of a vehicle that is being deleted (called after the database delete succeeds).</summary>
    public void DeleteFiles(IEnumerable<string> storedNames)
    {
        foreach (var storedName in storedNames)
            TryDeleteFile(storedName);
    }

    private void TryDeleteFile(string storedName)
    {
        try
        {
            storage.Delete(storedName);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The database no longer points at this file, so it's harmless, but log it so it can be cleaned up.
            logger.LogWarning(ex, "Couldn't delete stored document file {StoredName}.", storedName);
        }
    }
}
