namespace PaliaLauncher;

public class FileMap
{
    public Dictionary<string, FileEntry> Entries { get; set; } = new Dictionary<string, FileEntry>();

    public void AddManifestFiles(UpdateManifest manifest)
    {
        foreach (var entry in manifest.Contents)
        {
            Entries.Add(entry.Path, new FileEntry
            {
                RemoteFile = entry,
                LocalFile = null
            });
        }
    }

    public void AddLocalFiles(string rootPath)
    {
        var root = new DirectoryInfo(rootPath);
        var files = root.GetFiles("*", SearchOption.AllDirectories);
        foreach (var file in files)
        {
            var relativePath = file.FullName[(rootPath.Length + 1)..].Replace('\\', '/');
            if (Entries.TryGetValue(relativePath, out var entry))
            {
                entry.LocalFile = file;
            }
            else
            {
                Entries.Add(relativePath, new FileEntry
                {
                    RemoteFile = null,
                    LocalFile = file
                });
            }
        }
    }

    public class FileEntry
    {
        public UpdateManifest.ManifestEntry RemoteFile { get; set; }
        public FileInfo LocalFile { get; set; }
    }
}