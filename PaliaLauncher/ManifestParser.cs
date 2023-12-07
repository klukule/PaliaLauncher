using FlexBuffers;

namespace PaliaLauncher;

public static class ManifestParser
{
    public static UpdateManifest ParseManifest(byte[] rawManifest)
    {
        try
        {
            var raw = FlxValue.FromBytes(rawManifest);
            var manifest = new UpdateManifest
            {
                Bundle = raw["bundle"].AsString,
                Version = raw["version"].AsString,
                Platform = raw["platform"].AsString,
                Contents = new List<UpdateManifest.ManifestEntry>()
            };
            
            FlattenManifest(manifest.Contents, raw["contents"].AsMap);

            return manifest;
        }
        catch
        {
            return null;
        }
    }

    private static void FlattenManifest(ICollection<UpdateManifest.ManifestEntry> entries, FlxMap rawEntry)
    {
        if (rawEntry.KeyIndex("files") >= 0)
        {
            var files = rawEntry["files"].AsVector;
            foreach (var file in files)
            {
                FlattenManifest(entries, file.AsMap);
            }

            return;
        }

        var manifestEntry = new UpdateManifest.ManifestEntry();
        foreach (var (key, value) in rawEntry)
        {
            switch (key)
            {
                case "path":
                    manifestEntry.Path = value.AsString;
                    break;
                case "size":
                    manifestEntry.Size = value.AsULong;
                    break;
                case "hash":
                    manifestEntry.Hash = value.AsBlob;
                    break;
                case "chunks":
                    manifestEntry.Chunks = new List<UpdateManifest.ManifestEntry.ChunkHash>();
                    foreach (var chunk in value.AsVector)
                    {
                        var chunkHash = new UpdateManifest.ManifestEntry.ChunkHash
                        {
                            Offset = chunk[0].AsULong,
                            Size = chunk[1].AsULong,
                            Hash = chunk[2].AsBlob
                        };

                        manifestEntry.Chunks.Add(chunkHash);
                    }

                    break;
            }
        }

        entries.Add(manifestEntry);
    }
}

public class UpdateManifest
{
    public string Bundle { get; set; }
    public string Version { get; set; }
    public string Platform { get; set; }

    // Flattened manifest entries - containing only the leaf nodes
    public List<ManifestEntry> Contents { get; set; }

    public class ManifestEntry
    {
        // File path relative to game root
        public string Path { get; set; }

        // File size in bytes
        public ulong Size { get; set; }

        // File hash
        public byte[] Hash { get; set; }

        // File chunk hashes
        public List<ChunkHash> Chunks { get; set; }

        public class ChunkHash
        {
            // Starting offset into the file
            public ulong Offset { get; set; }

            // Size of the chunk in bytes
            public ulong Size { get; set; }

            // Hash of the chunk
            public byte[] Hash { get; set; }
        }
    }
}