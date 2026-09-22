using System.IO.Compression;

namespace CollisionLinkDownloader.Services;

public class ExtractService
{
    private readonly Action<string> _log;
    private void Log(string message = "") => _log(message);

    private readonly string _destinationFolder;

    public ExtractService(Action<string>? log = null)
    {
        _log = log ?? Console.WriteLine;
        _destinationFolder = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.UserProfile),
            "Downloads",
            "CollisionLink");
    }

    public void Extract(string zipPath)
    {
        if (!File.Exists(zipPath))
            throw new FileNotFoundException(
                "Downloaded ZIP file was not found.",
                zipPath);

        Directory.CreateDirectory(_destinationFolder);

        Log("Extracting EMS file...");

        ZipFile.ExtractToDirectory(
            zipPath,
            _destinationFolder,
            overwriteFiles: true);

        Log($"Extracted to: {_destinationFolder}");

        File.Delete(zipPath);

        Log("Downloaded ZIP deleted.");
    }
}
