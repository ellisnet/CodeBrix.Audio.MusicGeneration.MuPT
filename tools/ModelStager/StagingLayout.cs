using System;
using System.Collections.Generic;
using System.IO;

namespace CodeBrix.Audio.MusicGeneration.MuPT.ModelStager;

/// <summary>
/// Where the stager keeps everything, and the rule that all of it is inside this repository: the model
/// store, the downloads, the temporary folders the convert and quantize steps work in, and the artifact
/// that ships. The default store under the user's home directory is never opened and never touched.
/// </summary>
internal sealed class StagingLayout
{
    /// <summary>The file at the repository root that marks the root.</summary>
    private const string RepositoryMarker = "CodeBrix.Audio.MusicGeneration.MuPT.slnx";

    /// <summary>
    /// Initializes the layout of one repository.
    /// </summary>
    /// <param name="repositoryRoot">The absolute path of the repository root.</param>
    private StagingLayout(string repositoryRoot)
    {
        RepositoryRoot = repositoryRoot;
        StagingDirectory = Path.Combine(repositoryRoot, "staging");
        DownloadDirectory = Path.Combine(StagingDirectory, "download");
        StoreDirectory = Path.Combine(StagingDirectory, "store");
        OutputDirectory = Path.Combine(StagingDirectory, "output");
        TemporaryDirectory = Path.Combine(StagingDirectory, "tmp");
        ProvenancePath = Path.Combine(repositoryRoot, "MODEL-PROVENANCE.json");
    }

    /// <summary>The absolute path of the repository root.</summary>
    internal string RepositoryRoot { get; }

    /// <summary>The staging folder, which holds the four working folders.</summary>
    internal string StagingDirectory { get; }

    /// <summary>
    /// Where a downloader that separates its downloads from its store would put them. The model store
    /// does not separate them - a partial download is a sidecar beside the blob it will become - so this
    /// folder exists, is ignored by git and normally stays empty.
    /// </summary>
    internal string DownloadDirectory { get; }

    /// <summary>The model store, which <c>ModelStoreOptions.StoreDirectory</c> is pointed at.</summary>
    internal string StoreDirectory { get; }

    /// <summary>The artifact that ships. Nothing in this tool ever clears it.</summary>
    internal string OutputDirectory { get; }

    /// <summary>The temporary directory the model library lays its working folders out under.</summary>
    internal string TemporaryDirectory { get; }

    /// <summary>The provenance file the stager writes at the repository root.</summary>
    internal string ProvenancePath { get; }

    /// <summary>
    /// Finds the repository this assembly was built inside by walking up from the assembly's own folder
    /// until the marker file appears.
    /// </summary>
    /// <returns>The layout of that repository.</returns>
    /// <exception cref="InvalidOperationException">No folder above the assembly holds the marker file.</exception>
    internal static StagingLayout Discover()
    {
        DirectoryInfo directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, RepositoryMarker)))
            {
                return new StagingLayout(directory.FullName);
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            "No folder above " + AppContext.BaseDirectory + " holds " + RepositoryMarker + ", so the"
                + " repository root cannot be found. Run the stager from inside its own repository.");
    }

    /// <summary>
    /// Creates the staging folders that are missing. An existing folder is left exactly as it is.
    /// </summary>
    internal void CreateDirectories()
    {
        Directory.CreateDirectory(StagingDirectory);
        Directory.CreateDirectory(DownloadDirectory);
        Directory.CreateDirectory(StoreDirectory);
        Directory.CreateDirectory(OutputDirectory);
        Directory.CreateDirectory(TemporaryDirectory);
    }

    /// <summary>
    /// Points the process's temporary directory at <see cref="TemporaryDirectory"/> and proves that it
    /// took, because everything after this depends on it: the conversion and the quantization each lay
    /// hundreds of megabytes out under the temporary directory, and the machine this is staged on has a
    /// small in-memory /tmp.
    /// </summary>
    /// <exception cref="InvalidOperationException">The runtime still reports another temporary directory.</exception>
    internal void RedirectTemporaryDirectory()
    {
        //TMPDIR is what the runtime reads on Unix; TEMP and TMP are what it reads on Windows. Setting all
        //three keeps the tool honest wherever it is run.
        Environment.SetEnvironmentVariable("TMPDIR", TemporaryDirectory);
        Environment.SetEnvironmentVariable("TEMP", TemporaryDirectory);
        Environment.SetEnvironmentVariable("TMP", TemporaryDirectory);

        string reported = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
        string wanted = Path.TrimEndingDirectorySeparator(Path.GetFullPath(TemporaryDirectory));
        if (!string.Equals(reported, wanted, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The temporary directory could not be redirected into the repository: it was pointed at "
                    + wanted + " but the runtime reports " + reported + ". Staging would write outside the"
                    + " repository, so it stops here.");
        }
    }

    /// <summary>
    /// Adds up the apparent size of everything under the staging folder.
    /// </summary>
    /// <returns>
    /// The sum of the lengths of every file under <see cref="StagingDirectory"/>. Hard links are counted
    /// once per link, so this is at or above what the volume actually gives up.
    /// </returns>
    internal long MeasureStagingBytes()
    {
        return DiskUsage.DirectoryBytes(StagingDirectory);
    }

    /// <summary>
    /// Clears the three working folders - the downloads, the store and the temporary directory - and
    /// leaves <see cref="OutputDirectory"/> untouched.
    /// </summary>
    /// <returns>How many bytes the folders held before they were cleared.</returns>
    internal long ClearWorkingDirectories()
    {
        long before = 0;
        foreach (string directory in new[] { DownloadDirectory, StoreDirectory, TemporaryDirectory })
        {
            before += DiskUsage.DirectoryBytes(directory);
            Clear(directory);
        }

        return before;
    }

    /// <summary>
    /// Empties one working folder and re-creates it, refusing anything that is not one of the three.
    /// </summary>
    /// <param name="directory">The folder to empty.</param>
    /// <exception cref="InvalidOperationException">The folder is not one of the three working folders.</exception>
    private void Clear(string directory)
    {
        //A removal this tool performs is limited, by name, to the three folders it made itself. Nothing
        //else in the repository and nothing at all outside it can be reached from here.
        IReadOnlyList<string> allowed = new[] { DownloadDirectory, StoreDirectory, TemporaryDirectory };
        bool isAllowed = false;
        foreach (string candidate in allowed)
        {
            isAllowed |= string.Equals(candidate, directory, StringComparison.Ordinal);
        }

        if (!isAllowed)
        {
            throw new InvalidOperationException(
                "The stager only ever clears its own download, store and temporary folders, and " + directory
                    + " is none of them.");
        }

        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, true);
        }

        Directory.CreateDirectory(directory);
    }
}
