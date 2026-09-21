using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.Ollama.ModelManager;
using CodeBrix.Ollama.ModelRunner;
using Runner = CodeBrix.Ollama.ModelRunner.ModelRunner;

namespace CodeBrix.Audio.MusicGeneration.MuPT.ModelStager;

/// <summary>
/// Reproduces this repository's shipped model artifact from upstream: the MuPT checkpoint is pulled at a
/// pinned commit, converted to GGUF at the checkpoint's own precision, quantized to Q4_K_M through the
/// inference engine's own quantizer, checked against what was recorded for this machine, and written to
/// <c>staging/output</c> with a provenance file beside the repository's LICENSE.
/// </summary>
/// <remarks>
/// <para>
/// NOTHING HAS TO BE INSTALLED. Every step is managed code plus the native quantizer the inference-engine
/// package already carries, so a machine with nothing but .NET on it can reproduce the artifact. No step
/// of this tool has a Python road, and if one ever found itself on one it stops and says so.
/// </para>
/// <para>
/// EVERYTHING STAYS INSIDE THE REPOSITORY: the store, the downloads, the temporary folders the conversion
/// and the quantization work in, and the artifact. The default store under the user's home directory is
/// never opened.
/// </para>
/// <para>
/// The <c>Runner</c> alias is needed because the inference-engine package carries a class and a namespace
/// of the same name, and a plain <c>ModelRunner</c> would reach the namespace.
/// </para>
/// </remarks>
internal static class Program
{
    /// <summary>The upstream model, in the store's own name grammar.</summary>
    private const string UpstreamName = "hf.co/m-a-p/MuPT-v1-8192-190M";

    /// <summary>The Hugging Face repository the files come from.</summary>
    private const string UpstreamRepository = "m-a-p/MuPT-v1-8192-190M";

    /// <summary>
    /// The commit the pull is PINNED to. A branch moves; a staged artifact that cannot say which bytes it
    /// was made from is not provenance, so the revision is named here and recorded in the provenance file.
    /// </summary>
    private const string UpstreamRevision = "bf8f270d11683f65aa83ccebc2e756b782b795d8";

    /// <summary>The name the unquantized conversion is stored under.</summary>
    private const string ConvertedName = UpstreamName + ":gguf-auto";

    /// <summary>The name the quantized model is stored under.</summary>
    private const string QuantizedName = UpstreamName + ":gguf-q4_k_m";

    /// <summary>The quantization, in the spelling the store records and names it by.</summary>
    private const string QuantizationTag = "q4_k_m";

    /// <summary>The one file this repository ships.</summary>
    private const string OutputFileName = "MuPT-v1-8192-190M-Q4_K_M.gguf";

    /// <summary>The size of that file, measured on the machine the artifact of record was staged on.</summary>
    /// <remarks>
    /// IT IS 32 BYTES LARGER THAN THE FILE AN EARLIER HAND-RUN SPIKE LEFT BEHIND, and the whole of the
    /// difference is two metadata strings. The conversion derives <c>general.name</c> and
    /// <c>general.basename</c> from the model identifier, which the store takes from the model part of
    /// the stored name - so this road writes "MuPT v1 8192 190M" and "MuPT-v1-8192", the publisher's own
    /// model name, where a conversion pointed at a folder somebody had called <c>mupt-190m</c> wrote
    /// "Mupt 190m" and "mupt". The two strings are 16 bytes longer together, and the alignment padding in
    /// front of the tensor data takes that to 32. THE QUANTIZED TENSOR DATA IS BYTE FOR BYTE THE SAME.
    /// Naming <c>ConvertOptions.ModelId</c> "mupt-190m" would reproduce the older file exactly, at the
    /// cost of putting a scratch folder's name into what ships.
    /// </remarks>
    private const long ExpectedBytes = 123_149_152L;

    /// <summary>
    /// The sha256 of that file, measured on the same machine. RE-STAGING TO THIS HASH ON THIS MACHINE IS
    /// THE GATE; bytes are never pinned across platforms, and a different operating system or processor
    /// may legitimately produce a different file. MODEL-PROVENANCE.json records which machine this is.
    /// </summary>
    private const string ExpectedSha256 = "6de624aa478f7beb99d025334e9655e2f782c6441a5b1ec108d2df84bfff4983";

    /// <summary>
    /// What the run needs free before it starts: the checkpoint, the converted model, the quantized file
    /// and the working copies the conversion holds while it writes, with room over.
    /// </summary>
    private const long RequiredFreeBytes = 2_684_354_560L;

    /// <summary>How many steps the run reports.</summary>
    private const int StepCount = 6;

    /// <summary>
    /// The four special tokens MuPT's own files do not declare: its tokenizer class names them as default
    /// arguments inside the publisher's Python, which a conversion that reads files never sees and never
    /// runs, so the caller supplies the declaration the files are missing.
    /// </summary>
    private static readonly string[] SpecialTokens = { "<pad>", "<unk>", "<bos>", "<eos>" };

    /// <summary>
    /// Runs the staging.
    /// </summary>
    /// <param name="args">The command line.</param>
    /// <returns>0 when the artifact was staged and matched, 1 on a failure, 2 on a mismatch.</returns>
    private static async Task<int> Main(string[] args)
    {
        //THE TEMPORARY DIRECTORY IS REDIRECTED BEFORE THE FIRST CALL INTO THE MODEL LIBRARIES. The
        //conversion and the quantization each lay hundreds of megabytes out under it, and /tmp is a small
        //in-memory file system on the machine this is staged on. Only this tool's own path arithmetic runs
        //ahead of it.
        StagingLayout layout;
        try
        {
            layout = StagingLayout.Discover();
            layout.CreateDirectories();
            layout.RedirectTemporaryDirectory();
        }
        catch (InvalidOperationException error)
        {
            Log.Error(error.Message);
            return 1;
        }

        bool cleanAfter = false;
        bool keep = false;
        foreach (string argument in args)
        {
            switch (argument)
            {
                case "--clean-after":
                    cleanAfter = true;
                    break;
                case "--keep":
                    keep = true;
                    break;
                case "--help":
                case "-h":
                    WriteUsage();
                    return 0;
                default:
                    Log.Error("Unknown argument " + argument + ".");
                    WriteUsage();
                    return 1;
            }
        }

        Log.Line("MuPT model stager - " + UpstreamName);
        Log.Detail("repository root   " + layout.RepositoryRoot);
        Log.Detail("store             " + layout.StoreDirectory);
        Log.Detail("temporary         " + Path.TrimEndingDirectorySeparator(Path.GetTempPath()));
        Log.Detail("output            " + layout.OutputDirectory);

        var total = Stopwatch.StartNew();
        using var watcher = new StagingWatcher(layout);
        var report = new UsageReport();

        try
        {
            int result = await StageAsync(layout, watcher, report, CancellationToken.None)
                .ConfigureAwait(false);
            total.Stop();
            report.Write(watcher, total.Elapsed);

            if (result == 0)
            {
                Clean(layout, cleanAfter, keep);
                Log.Blank();
                Log.Line("DONE. The artifact is in " + layout.OutputDirectory + ".");
            }

            return result;
        }
        catch (PythonNotAvailableException error)
        {
            return StopOnPython(error);
        }
        catch (PythonModuleNotInstalledException error)
        {
            return StopOnPython(error);
        }
        catch (PythonScriptException error)
        {
            return StopOnPython(error);
        }
        catch (Exception error)
        {
            total.Stop();
            Log.Blank();
            Log.Error(error.GetType().Name + ": " + error.Message);
            Log.Detail("The staging folders are left exactly as they are, so a second run resumes.");
            return 1;
        }
    }

    /// <summary>
    /// Pulls, converts, quantizes, copies the artifact out and writes the provenance.
    /// </summary>
    /// <param name="layout">Where everything goes.</param>
    /// <param name="watcher">The watcher that samples what the run costs.</param>
    /// <param name="report">Where the timings and the byte counts are collected.</param>
    /// <param name="cancellationToken">A token that cancels the run.</param>
    /// <returns>0 when the artifact matched what was recorded, 1 on a failure, 2 on a mismatch.</returns>
    private static async Task<int> StageAsync(
        StagingLayout layout,
        StagingWatcher watcher,
        UsageReport report,
        CancellationToken cancellationToken)
    {
        var step = Stopwatch.StartNew();

        Log.Step(1, StepCount, "free space");
        long free = DiskUsage.AvailableFreeBytes(layout.StagingDirectory);
        Log.Detail("needed     " + Log.Bytes(RequiredFreeBytes));
        Log.Detail("available  " + (free < 0 ? "unknown on this platform" : Log.Bytes(free)));
        if (free >= 0 && free < RequiredFreeBytes)
        {
            Log.Error("There is not enough free space to stage this model. Free some and run again.");
            return 1;
        }

        report.Record("free space", step.Elapsed);

        using var store = new ModelStore(new ModelStoreOptions { StoreDirectory = layout.StoreDirectory });

        //PULL, pinned to the commit. A file already in the store costs no request, so a second run of
        //   the stager over a store that was kept starts at the conversion.
        step.Restart();
        Log.Step(2, StepCount, "pull " + UpstreamName + " at " + UpstreamRevision);
        var pullOptions = new PullOptions
        {
            Source = PullSource.HuggingFaceFiles,
            Repository = UpstreamRepository,
            Revision = UpstreamRevision,
            Filter = FileFilter.ExcludeTrainingArtifacts
        };

        long downloaded = 0;
        var seen = new Dictionary<string, long>(StringComparer.Ordinal);
        DateTime next = DateTime.MinValue;
        await foreach (PullProgress progress in store
            .PullAsync(UpstreamName, pullOptions, cancellationToken)
            .ConfigureAwait(false))
        {
            //A file reports repeatedly as it comes down; only its last count is part of the total.
            if (progress.Digest != null || progress.TotalBytes > 0)
            {
                seen[progress.Status] = progress.CompletedBytes;
            }

            if (DateTime.UtcNow >= next || progress.TotalBytes == 0)
            {
                Log.Detail(Describe(progress));
                next = DateTime.UtcNow + Log.ProgressInterval;
            }
        }

        foreach (KeyValuePair<string, long> file in seen)
        {
            downloaded += file.Value;
        }

        report.DownloadedBytes = downloaded;
        watcher.Sample();
        report.Record("pull", step.Elapsed);

        ModelInfo source = await store.ShowAsync(UpstreamName, cancellationToken).ConfigureAwait(false);
        ResolvedModel resolvedSource = await store.ResolveAsync(UpstreamName, cancellationToken)
            .ConfigureAwait(false);
        Log.Detail("licence    " + (source.License == null ? "none stated" : source.License.LicenseId));
        Log.Detail("files      " + resolvedSource.Files.Count + ", " + Log.Bytes(source.Size));

        //CONVERT. The checkpoint's OWN precision is kept - the "auto" source, never f16 - and the four
        //   special tokens the publisher declares only in Python are named by this caller.
        step.Restart();
        Log.Step(3, StepCount, "convert to GGUF at the checkpoint's own precision");
        var convertOptions = new ConvertOptions
        {
            OutputType = GgufOutputType.Auto,
            OutputName = ConvertedName,
            Overwrite = true,
            AddedSpecialTokens = SpecialTokens
        };

        ConvertResult converted = await store
            .ConvertToGgufAsync(UpstreamName, convertOptions, Watch(), cancellationToken)
            .ConfigureAwait(false);
        Log.Detail("wrote      " + converted.TensorCount + " tensors at " + converted.TypeWritten
            + ", " + Log.Bytes(converted.OutputBytes));
        watcher.Sample();
        report.Record("convert", step.Elapsed);

        //QUANTIZE. The store has no quantizer of its own on purpose; the inference engine's is handed
        //   to it as a delegate, which is the one place the two packages meet.
        step.Restart();
        Log.Step(4, StepCount, "quantize to " + QuantizationTag.ToUpperInvariant());
        var quantizeOptions = new QuantizeGgufOptions
        {
            Type = QuantizationTag,
            OutputName = QuantizedName,
            Overwrite = true,
            Tool = "CodeBrix.Ollama.ModelRunner",
            ToolVersion = Runner.GetNativeRuntimeInfo().BuildInfo,
            Quantizer = (input, output, token) => Runner.QuantizeAsync(
                input, output, GgufQuantizationType.Q4_K_M, null, token)
        };

        QuantizeGgufResult quantized = await store
            .QuantizeGgufAsync(ConvertedName, quantizeOptions, Watch(), cancellationToken)
            .ConfigureAwait(false);
        Log.Detail("wrote      " + Log.Bytes(quantized.OutputBytes) + ", "
            + string.Format(
                CultureInfo.InvariantCulture, "{0:P1} of the converted model",
                (double)quantized.OutputBytes / quantized.SourceBytes));
        watcher.Sample();
        report.Record("quantize", step.Elapsed);

        //THE ARTIFACT. The quantized blob is copied out under the name the package ships it as.
        step.Restart();
        Log.Step(5, StepCount, "copy the artifact out and check it");
        ResolvedModel resolved = await store.ResolveAsync(QuantizedName, cancellationToken)
            .ConfigureAwait(false);
        string artifactPath = Path.Combine(layout.OutputDirectory, OutputFileName);
        if (File.Exists(artifactPath))
        {
            File.Delete(artifactPath);
        }

        File.Copy(resolved.ModelPath, artifactPath);
        ArtifactFile artifact = ArtifactFile.Measure(artifactPath);
        Log.Detail("file       " + artifact.Name);
        Log.Detail("bytes      " + artifact.Bytes.ToString("N0", CultureInfo.InvariantCulture)
            + "   expected " + ExpectedBytes.ToString("N0", CultureInfo.InvariantCulture));
        Log.Detail("sha256     " + artifact.Sha256);
        Log.Detail("expected   " + ExpectedSha256);
        watcher.Sample();
        report.Record("copy out and hash", step.Elapsed);

        bool matched = artifact.Matches(ExpectedBytes, ExpectedSha256);
        Log.Detail(matched
            ? "MATCH - this run reproduced the artifact of record."
            : "MISMATCH - this run did NOT reproduce the artifact of record.");

        //PROVENANCE. It is written whatever the comparison said, because a file that does not match is
        //   exactly the one whose settings and machine somebody will want to read.
        step.Restart();
        Log.Step(6, StepCount, "write MODEL-PROVENANCE.json");
        ModelInfo convertedInfo = await store.ShowAsync(ConvertedName, cancellationToken)
            .ConfigureAwait(false);
        ModelInfo quantizedInfo = await store.ShowAsync(QuantizedName, cancellationToken)
            .ConfigureAwait(false);

        ProvenanceFile.Write(
            layout.ProvenancePath,
            UpstreamName,
            UpstreamRepository,
            UpstreamRevision,
            source,
            resolvedSource.Files,
            new[]
            {
                ProvenanceStep.FromModel("convert", "IModelStore.ConvertToGgufAsync", convertedInfo),
                ProvenanceStep.FromModel("quantize", "IModelStore.QuantizeGgufAsync", quantizedInfo)
            },
            new[] { artifact },
            "staging/output");
        Log.Detail("wrote      " + layout.ProvenancePath);
        report.Record("provenance", step.Elapsed);

        if (!matched)
        {
            Log.Blank();
            Log.Error("The staged file is not the one recorded for this machine.");
            Log.Detail("Bytes are never pinned across platforms: a different operating system or processor"
                + " may legitimately produce a different file, and MODEL-PROVENANCE.json says which machine"
                + " the recorded hash belongs to. On the SAME machine this is a real difference and wants"
                + " investigating before anything is published.");
            return 2;
        }

        return 0;
    }

    /// <summary>
    /// A progress sink that prints the library's own statuses, no more often than the tool prints anything.
    /// </summary>
    /// <returns>The sink.</returns>
    private static IProgress<PullProgress> Watch()
    {
        DateTime next = DateTime.MinValue;
        return new Progress<PullProgress>(progress =>
        {
            if (DateTime.UtcNow < next && progress.TotalBytes > 0)
            {
                return;
            }

            Log.Detail(Describe(progress));
            next = DateTime.UtcNow + Log.ProgressInterval;
        });
    }

    /// <summary>
    /// One progress report as a line of text.
    /// </summary>
    /// <param name="progress">The report.</param>
    /// <returns>The line.</returns>
    private static string Describe(PullProgress progress)
    {
        if (progress.TotalBytes <= 0)
        {
            return progress.Status;
        }

        return string.Format(
            CultureInfo.InvariantCulture,
            "{0}  {1:F1}%  ({2:N0} / {3:N0} bytes)",
            progress.Status,
            100.0 * progress.CompletedBytes / progress.TotalBytes,
            progress.CompletedBytes,
            progress.TotalBytes);
    }

    /// <summary>
    /// Clears the working folders when the run is allowed to, and never touches the output folder.
    /// </summary>
    /// <param name="layout">Where everything is.</param>
    /// <param name="cleanAfter">Whether the command line asked for it.</param>
    /// <param name="keep">Whether the command line forbade it.</param>
    private static void Clean(StagingLayout layout, bool cleanAfter, bool keep)
    {
        if (keep)
        {
            return;
        }

        bool clear = cleanAfter;
        if (!clear && !Console.IsInputRedirected && !Console.IsOutputRedirected)
        {
            Log.Blank();
            Console.Write(
                "Clear staging/download, staging/store and staging/tmp now? staging/output is kept. [y/N] ");
            string answer = Console.ReadLine();
            clear = answer != null && answer.Trim().StartsWith("y", StringComparison.OrdinalIgnoreCase);
        }

        if (!clear)
        {
            return;
        }

        long freed = layout.ClearWorkingDirectories();
        Log.Line("Cleared the working folders: " + Log.Bytes(freed) + " given back. staging/output is kept.");
    }

    /// <summary>
    /// Stops the run because something asked for Python, which neither stager may ever need.
    /// </summary>
    /// <param name="error">What the library said.</param>
    /// <returns>The exit code.</returns>
    private static int StopOnPython(Exception error)
    {
        Log.Blank();
        Log.Error("STOPPED: a step of this stager found itself on a PYTHON road.");
        Log.Detail("This tool reproduces its artifact with managed code and the inference engine's own"
            + " native quantizer, and nothing it does needs an interpreter. Something has changed, and"
            + " the run stops rather than quietly requiring Python on a build machine.");
        Log.Detail(error.GetType().Name + ": " + error.Message);
        return 1;
    }

    /// <summary>
    /// Writes what the tool does and what it takes.
    /// </summary>
    private static void WriteUsage()
    {
        Console.WriteLine();
        Console.WriteLine("ModelStager - reproduces this repository's shipped model artifact from upstream.");
        Console.WriteLine();
        Console.WriteLine("  dotnet run --project tools/ModelStager -c Release [options]");
        Console.WriteLine();
        Console.WriteLine("  --clean-after   clear staging/download, staging/store and staging/tmp when the");
        Console.WriteLine("                  run succeeds. staging/output is always kept.");
        Console.WriteLine("  --keep          never ask and never clear.");
        Console.WriteLine("  --help, -h      this text.");
        Console.WriteLine();
        Console.WriteLine("Everything the tool writes stays inside the repository. See staging/README.txt.");
        Console.WriteLine();
    }
}
