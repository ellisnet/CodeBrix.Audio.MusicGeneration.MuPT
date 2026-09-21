using System;
using System.IO;
using System.Security.Cryptography;

namespace CodeBrix.Audio.MusicGeneration.MuPT.ModelStager;

/// <summary>
/// One file of what ships, measured: the name it carries in the output folder, its size, and its
/// sha256. It is what the run checks itself against and what the provenance file records.
/// </summary>
internal sealed class ArtifactFile
{
    /// <summary>
    /// Initializes a measured file.
    /// </summary>
    /// <param name="name">The file's name inside the output folder.</param>
    /// <param name="path">The file's full path.</param>
    /// <param name="bytes">Its size in bytes.</param>
    /// <param name="sha256">Its sha256 as lower-case hexadecimal.</param>
    private ArtifactFile(string name, string path, long bytes, string sha256)
    {
        Name = name;
        Path = path;
        Bytes = bytes;
        Sha256 = sha256;
    }

    /// <summary>The file's name inside the output folder.</summary>
    internal string Name { get; }

    /// <summary>The file's full path.</summary>
    internal string Path { get; }

    /// <summary>The file's size in bytes.</summary>
    internal long Bytes { get; }

    /// <summary>The file's sha256, as lower-case hexadecimal without a prefix.</summary>
    internal string Sha256 { get; }

    /// <summary>
    /// Measures a file on disk.
    /// </summary>
    /// <param name="path">The file's full path.</param>
    /// <returns>Its name, size and sha256.</returns>
    /// <exception cref="FileNotFoundException">There is no file at that path.</exception>
    internal static ArtifactFile Measure(string path)
    {
        var file = new FileInfo(path);
        if (!file.Exists)
        {
            throw new FileNotFoundException("The staged artifact is not there.", path);
        }

        return new ArtifactFile(file.Name, file.FullName, file.Length, ComputeSha256(file.FullName));
    }

    /// <summary>
    /// Whether this file is the size and the content that was expected of it.
    /// </summary>
    /// <param name="expectedBytes">The size recorded for this artifact.</param>
    /// <param name="expectedSha256">The sha256 recorded for this artifact.</param>
    /// <returns><see langword="true"/> when both agree.</returns>
    internal bool Matches(long expectedBytes, string expectedSha256)
    {
        return Bytes == expectedBytes
            && string.Equals(Sha256, expectedSha256, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Reads a file and returns its sha256.
    /// </summary>
    /// <param name="path">The file's full path.</param>
    /// <returns>The digest as lower-case hexadecimal.</returns>
    private static string ComputeSha256(string path)
    {
        using FileStream stream = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20, FileOptions.SequentialScan);
        using var algorithm = SHA256.Create();
        return Convert.ToHexString(algorithm.ComputeHash(stream)).ToLowerInvariant();
    }
}
