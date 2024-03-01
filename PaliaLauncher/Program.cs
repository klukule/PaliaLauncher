using System.Net;
using PaliaLauncher;

var GAME_ROOT = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "Palia\\Client"); // TODO: Pull of of magic hat or wherever it's stored

long runningTotal = 0;
var sw = System.Diagnostics.Stopwatch.StartNew();
Console.WriteLine("Fetching channel info...");
var resp = await UpdateServer.FetchChannelInfo(Configuration.Bundle, Configuration.Channel);
if (!resp.Ok)
{
    Console.WriteLine("Error fetching channel info: " + resp.Data);
    return;
}

sw.Stop();
runningTotal += sw.ElapsedMilliseconds;
Console.WriteLine("Channel info OK - " + sw.Elapsed.TotalSeconds + " seconds");

var channelInfo = (resp.Data as UpdateServerResponse.ChannelInfoResponse)!;

Console.WriteLine("Latest palia version: " + channelInfo.Version);

// TODO: Proper CLI interface
if (args.Length >= 2 && args[0] == "--override")
{
    channelInfo.Version = args[1];
    Console.WriteLine("Overriding version to " + channelInfo.Version);
}

// TODO: if channelInfo.Version == currentVersion, exit

sw.Restart();
Console.WriteLine("Fetching manifest...");
var manifestData = await UpdateServer.FetchManifest(Configuration.Bundle, channelInfo.Version, Configuration.Platform);

if (!manifestData.Ok)
{
    Console.WriteLine("Error fetching manifest: " + manifestData.Data);
    return;
}

sw.Stop();
runningTotal += sw.ElapsedMilliseconds;
Console.WriteLine("Manifest fetch OK - " + sw.Elapsed.TotalSeconds + " seconds");

sw.Restart();
Console.WriteLine("Parsing manifest...");
var manifest = ManifestParser.ParseManifest(manifestData.Data as byte[]);

if (manifest == null)
{
    Console.WriteLine("Error parsing manifest - Exit");
    return;
}

sw.Stop();
runningTotal += sw.ElapsedMilliseconds;
Console.WriteLine("Manifest OK - " + sw.Elapsed.TotalSeconds + " seconds");
Console.WriteLine("Manifest version: " + manifest.Version + " (" + manifest.Platform + ") - " + manifest.Contents.Count + " files");

sw.Restart();
Console.WriteLine("Building file map...");
var map = new FileMap();
map.AddManifestFiles(manifest);
map.AddLocalFiles(GAME_ROOT);
sw.Stop();
runningTotal += sw.ElapsedMilliseconds;
Console.WriteLine("File map OK - " + sw.Elapsed.TotalSeconds + " seconds");

sw.Restart();
Console.WriteLine("Checking files... (" + map.Entries.Count + " files to check)");
var sameFiles = 0;
var removedFiles = 0;
var addedFiles = 0;
var updatedFiles = 0;

// NOTE: We can use HTTP 2.0 which is more efficient as cloudflare's S3 buckets support it
var client = new HttpClient
{
    DefaultRequestVersion = HttpVersion.Version20,
    DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower
};

await Parallel.ForEachAsync(map.Entries.Values, async (file, ct) =>
{
    if (file.LocalFile == null)
    {
        Console.WriteLine("Downloading " + file.RemoteFile.Path);
        var url = $"{Configuration.DownloadServer}/bundle/{manifest.Bundle}/v/{manifest.Version}/{manifest.Platform}/file/{file.RemoteFile.Path}";
        var response = client.GetByteArrayAsync(url, ct).Result;
        var fullPath = Path.Combine(GAME_ROOT, file.RemoteFile.Path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllBytes(fullPath, response);
        Interlocked.Increment(ref addedFiles);
    }
    else if (file.RemoteFile == null && file.LocalFile != null)
    {
        Console.WriteLine("Deleting " + file.LocalFile.FullName);
        File.Delete(file.LocalFile.FullName);
        Interlocked.Increment(ref removedFiles);
    }
    else
    {
        var localHash = HashUtils.Sha256(file.LocalFile.FullName);
        if (!localHash.SequenceEqual(file.RemoteFile.Hash))
        {
            // TODO: Compare chunks and download only the chunks that are different
            Console.WriteLine("Updating " + file.RemoteFile.Path);
            var url = $"{Configuration.DownloadServer}/bundle/{manifest.Bundle}/v/{manifest.Version}/{manifest.Platform}/file/{file.RemoteFile.Path}";
            var response = await client.GetByteArrayAsync(url, ct);
            File.WriteAllBytes(file.LocalFile.FullName, response);
            Interlocked.Increment(ref updatedFiles);
        }
        else
        {
            Interlocked.Increment(ref sameFiles);
        }
    }
});

sw.Stop();
runningTotal += sw.ElapsedMilliseconds;
Console.WriteLine("Update OK - " + sw.Elapsed.TotalSeconds + " seconds");
Console.WriteLine("\t" + sameFiles + " files are up to date");
Console.WriteLine("\t" + removedFiles + " files were removed");
Console.WriteLine("\t" + addedFiles + " files were added");
Console.WriteLine("\t" + updatedFiles + " files were updated");

Console.WriteLine("Total time: " + runningTotal / 1000.0 + " seconds");

// TODO: Launch palia
// TODO: Pass through the args?