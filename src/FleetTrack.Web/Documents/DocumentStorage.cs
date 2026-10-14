using System.Text.RegularExpressions;

namespace FleetTrack.Web.Documents;

/// <summary>Where uploaded document files live. An interface so tests (or later, cloud storage) can swap it.</summary>
public interface IDocumentStorage
{
    /// <summary>Saves the content under a new random name and returns that name.</summary>
    Task<string> SaveAsync(Stream content, CancellationToken cancellationToken = default);
    Task<byte[]> ReadAsync(string storedName, CancellationToken cancellationToken = default);
    void Delete(string storedName);
}

public class DocumentStorageOptions
{
    /// <summary>
    /// Folder for uploaded files. Relative paths are inside the app's content root (App_Data/documents),
    /// which is NOT under wwwroot, so files can never be downloaded directly by URL.
    /// On a server, point this at a folder outside the application, e.g. Storage__DocumentsPath=/var/fleettrack/documents.
    /// </summary>
    public string DocumentsPath { get; set; } = "App_Data/documents";
}

/// <summary>Stores files on the server's disk with random names.</summary>
public partial class LocalDocumentStorage : IDocumentStorage
{
    private readonly string _root;

    public LocalDocumentStorage(string root)
    {
        _root = Path.GetFullPath(root);
        Directory.CreateDirectory(_root);
    }

    // Stored names are always 32 hex characters (a GUID). Anything else is refused,
    // so a crafted name like "../../appsettings.json" can never reach the file system.
    [GeneratedRegex("^[0-9a-f]{32}$")]
    private static partial Regex StoredNameFormat();

    public async Task<string> SaveAsync(Stream content, CancellationToken cancellationToken = default)
    {
        var storedName = Guid.NewGuid().ToString("N");
        var finalPath = PathFor(storedName);
        var tempPath = finalPath + ".uploading";

        // Write to a temporary name first and rename at the end, so a half-written file never looks complete.
        await using (var file = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            await content.CopyToAsync(file, cancellationToken);
        }
        File.Move(tempPath, finalPath);
        return storedName;
    }

    public Task<byte[]> ReadAsync(string storedName, CancellationToken cancellationToken = default) =>
        File.ReadAllBytesAsync(PathFor(storedName), cancellationToken);

    public void Delete(string storedName)
    {
        var path = PathFor(storedName);
        if (File.Exists(path))
            File.Delete(path);
    }

    private string PathFor(string storedName)
    {
        if (!StoredNameFormat().IsMatch(storedName))
            throw new ArgumentException("Invalid stored file name.", nameof(storedName));
        return Path.Combine(_root, storedName);
    }
}
