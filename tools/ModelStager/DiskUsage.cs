using System;
using System.IO;

namespace CodeBrix.Audio.MusicGeneration.MuPT.ModelStager;

/// <summary>
/// What the volume has left, and how much of it this run is using. Staging a model is mostly a disk
/// budget, so both numbers are measured rather than assumed.
/// </summary>
internal static class DiskUsage
{
    /// <summary>
    /// How many bytes the volume a directory sits on still has free for this user.
    /// </summary>
    /// <param name="directory">A directory on the volume. It must exist.</param>
    /// <returns>The free bytes, or -1 when the platform will not say.</returns>
    internal static long AvailableFreeBytes(string directory)
    {
        string full = Path.GetFullPath(directory);
        try
        {
            return new DriveInfo(full).AvailableFreeSpace;
        }
        catch (ArgumentException)
        {
            return AvailableFromMountPoints(full);
        }
        catch (DriveNotFoundException)
        {
            return AvailableFromMountPoints(full);
        }
        catch (IOException)
        {
            return AvailableFromMountPoints(full);
        }
    }

    /// <summary>
    /// Adds up the apparent size of every file under a directory.
    /// </summary>
    /// <param name="directory">The directory to measure. A missing one measures zero.</param>
    /// <returns>
    /// The sum of the file lengths. A file reached through more than one hard link is counted once per
    /// link, so this is at or above what the volume actually gives up for the tree.
    /// </returns>
    internal static long DirectoryBytes(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return 0;
        }

        long total = 0;
        try
        {
            foreach (string path in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
            {
                try
                {
                    total += new FileInfo(path).Length;
                }
                catch (FileNotFoundException)
                {
                    //A working folder is being written while this walks it; a file that has just gone is
                    //simply not part of the measurement.
                }
                catch (IOException)
                {
                }
            }
        }
        catch (DirectoryNotFoundException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        return total;
    }

    /// <summary>
    /// Finds the free space of the mount point a path sits under, for a platform whose
    /// <see cref="DriveInfo"/> will not take a plain directory.
    /// </summary>
    /// <param name="fullPath">The absolute path.</param>
    /// <returns>The free bytes of the longest matching mount point, or -1 when none matches.</returns>
    private static long AvailableFromMountPoints(string fullPath)
    {
        long free = -1;
        int longest = -1;

        foreach (DriveInfo drive in DriveInfo.GetDrives())
        {
            string mount;
            try
            {
                mount = Path.TrimEndingDirectorySeparator(Path.GetFullPath(drive.Name));
            }
            catch (ArgumentException)
            {
                continue;
            }

            if (mount.Length <= longest || !fullPath.StartsWith(mount, StringComparison.Ordinal))
            {
                continue;
            }

            try
            {
                free = drive.AvailableFreeSpace;
                longest = mount.Length;
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        return free;
    }
}
