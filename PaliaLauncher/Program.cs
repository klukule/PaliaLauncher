using System.Net;
using PaliaLauncher;

const string BUNDLE = "Palia";
const string CHANNEL = "live";
const string PLATFORM = "windows";
const string GAME_ROOT = "C:\\Users\\klukule\\AppData\\Local\\Palia\\Client"; // TODO: Pull of of magic hat or wherever it's stored
// TODO: Factor out the base URL here as it's used in multiple places

long runningTotal = 0;
var sw = System.Diagnostics.Stopwatch.StartNew();
Console.WriteLine("Fetching channel info...");
var resp = UpdateServer.FetchChannelInfo(BUNDLE, CHANNEL).Result;
if (!resp.Ok)
{
    Console.WriteLine("Error fetching manifest: " + (resp.Data as UpdateServerResponse.ErrorResponse)!.Message);
    return;
}

sw.Stop();
runningTotal += sw.ElapsedMilliseconds;
Console.WriteLine("Channel info OK - " + sw.Elapsed.TotalSeconds + " seconds");

var channelInfo = (resp.Data as UpdateServerResponse.ChannelInfoResponse)!;

Console.WriteLine("Palia version: " + channelInfo.Version);

if (args.Length >= 2 && args[0] == "--override")
{
    channelInfo.Version = args[1];
    Console.WriteLine("Overriding version to " + channelInfo.Version);
}

sw.Restart();
Console.WriteLine("Fetching manifest...");
var manifestData = UpdateServer.FetchManifest(BUNDLE, channelInfo.Version, PLATFORM);
sw.Stop();
runningTotal += sw.ElapsedMilliseconds;
Console.WriteLine("Manifest fetch OK - " + sw.Elapsed.TotalSeconds + " seconds");

sw.Restart();
Console.WriteLine("Parsing manifest...");
var manifest = ManifestParser.ParseManifest(manifestData);

if (manifest == null)
{
    Console.WriteLine("Error parsing manifest - Exit");
    return;
}

sw.Stop();
runningTotal += sw.ElapsedMilliseconds;
Console.WriteLine("Manifest OK - " + sw.Elapsed.TotalSeconds + " seconds");

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

Parallel.ForEach(map.Entries.Values, (file) =>
{
    if (file.LocalFile == null)
    {
        Console.WriteLine("Downloading " + file.RemoteFile.Path);
        var url = $"https://dl.palia.com/bundle/{BUNDLE}/v/{channelInfo.Version}/{PLATFORM}/file/{file.RemoteFile.Path}";
        var response = client.GetByteArrayAsync(url).Result;
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
            var url = $"https://dl.palia.com/bundle/{BUNDLE}/v/{channelInfo.Version}/{PLATFORM}/file/{file.RemoteFile.Path}";
            var response = client.GetByteArrayAsync(url).Result; // TODO: Use async/await so we do not eat the exceptions
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