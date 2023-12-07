namespace PaliaLauncher;

public static class HashUtils
{
    public static byte[] Sha256(string path)
    {
        using var stream = File.OpenRead(path);
        using var sha = System.Security.Cryptography.SHA256.Create();

        return sha.ComputeHash(stream);
    }
}