using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using CodeBrix.Ollama.ModelManager;
using Microsoft.Win32;

namespace CodeBrix.Audio.MusicGeneration.MuPT.ModelStager;

/// <summary>
/// Writes MODEL-PROVENANCE.json at the repository root: where the model came from and at which commit,
/// under which licence, what every step of the staging was asked for, what the artifact weighs and
/// hashes to, and which machine produced it.
/// </summary>
/// <remarks>
/// THE MACHINE IS PART OF THE RECORD. Re-staging on the same machine reproduces the same bytes and that
/// is the gate; bytes are never pinned across platforms, so the hash means nothing without the operating
/// system and the processor it was measured on.
/// </remarks>
internal static class ProvenanceFile
{
    /// <summary>What this file's own shape is called, so that a reader can tell one version from another.</summary>
    private const string Schema = "codebrix.model-provenance/1";

    /// <summary>
    /// Writes the provenance file.
    /// </summary>
    /// <param name="path">Where to write it.</param>
    /// <param name="modelName">The upstream model's name in the store's name grammar.</param>
    /// <param name="repository">The upstream repository.</param>
    /// <param name="requestedRevision">The commit the pull was pinned to.</param>
    /// <param name="source">What the store says about the model that was pulled.</param>
    /// <param name="sourceFiles">The files that pull brought down, in manifest order.</param>
    /// <param name="steps">Every step that ran after the pull, in order.</param>
    /// <param name="artifacts">Every file that ships, measured.</param>
    /// <param name="outputFolder">The folder the artifacts sit in, relative to the repository root.</param>
    internal static void Write(
        string path,
        string modelName,
        string repository,
        string requestedRevision,
        ModelInfo source,
        IReadOnlyList<ResolvedFile> sourceFiles,
        IReadOnlyList<ProvenanceStep> steps,
        IReadOnlyList<ArtifactFile> artifacts,
        string outputFolder)
    {
        //Nothing here is ever put in a web page, and a special token reads far better as <bos> than as
        //its escaped form, so the relaxed encoder is the right one for a file a maintainer reads.
        var options = new JsonWriterOptions { Indented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        using FileStream stream = File.Create(path);
        using var writer = new Utf8JsonWriter(stream, options);

        writer.WriteStartObject();
        writer.WriteString("schema", Schema);
        writer.WriteString("stagedAtUtc", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));

        WriteUpstream(writer, modelName, repository, requestedRevision, source, sourceFiles);
        WriteSteps(writer, steps);
        WriteArtifacts(writer, artifacts, outputFolder);
        WriteMachine(writer);

        writer.WriteEndObject();
        writer.Flush();
        stream.Write(new byte[] { (byte)'\n' }, 0, 1);
    }

    /// <summary>
    /// Writes where the model came from, as the store recorded it when it was pulled.
    /// </summary>
    /// <param name="writer">The writer.</param>
    /// <param name="modelName">The upstream model's name in the store's name grammar.</param>
    /// <param name="repository">The upstream repository.</param>
    /// <param name="requestedRevision">The commit the pull was pinned to.</param>
    /// <param name="source">What the store says about the model that was pulled.</param>
    /// <param name="sourceFiles">The files that pull brought down, in manifest order.</param>
    private static void WriteUpstream(
        Utf8JsonWriter writer,
        string modelName,
        string repository,
        string requestedRevision,
        ModelInfo source,
        IReadOnlyList<ResolvedFile> sourceFiles)
    {
        writer.WriteStartObject("upstream");
        writer.WriteString("name", modelName);
        writer.WriteString("repository", repository);
        writer.WriteString("address", "https://huggingface.co/" + repository);
        writer.WriteString("requestedRevision", requestedRevision);
        writer.WriteString("resolvedRevision", Setting(source, ModelConfigKeys.Revision));
        writer.WriteString("source", Setting(source, ModelConfigKeys.Source));
        writer.WriteString("pulledAtUtc", Setting(source, ModelConfigKeys.PulledAt));
        writer.WriteString("licenseId", source.License == null ? null : source.License.LicenseId);
        writer.WriteString("licenseSource", source.License == null ? null : source.License.LicenseSource);
        writer.WriteString("format", source.Format);

        writer.WriteStartArray("files");
        foreach (ResolvedFile file in sourceFiles)
        {
            writer.WriteStartObject();
            writer.WriteString("path", file.Name);
            writer.WriteNumber("bytes", file.Size);
            writer.WriteString("sha256", Sha256Of(file.Digest));
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    /// <summary>
    /// Writes each step of the staging with the settings the store recorded for it.
    /// </summary>
    /// <param name="writer">The writer.</param>
    /// <param name="steps">The steps, in order.</param>
    private static void WriteSteps(Utf8JsonWriter writer, IReadOnlyList<ProvenanceStep> steps)
    {
        writer.WriteStartArray("steps");
        foreach (ProvenanceStep step in steps)
        {
            writer.WriteStartObject();
            writer.WriteString("step", step.Name);
            writer.WriteString("operation", step.Operation);
            writer.WriteString("storedName", step.StoredName);
            writer.WriteString("derivedFrom", step.DerivedFrom);
            writer.WriteString("tool", step.Tool);
            writer.WriteString("toolVersion", step.ToolVersion);

            writer.WriteStartObject("settings");
            foreach (KeyValuePair<string, string> setting in step.Settings)
            {
                writer.WriteString(setting.Key, setting.Value);
            }

            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    /// <summary>
    /// Writes what ships, with the size and the digest of every file of it.
    /// </summary>
    /// <param name="writer">The writer.</param>
    /// <param name="artifacts">The measured files.</param>
    /// <param name="outputFolder">The folder they sit in, relative to the repository root.</param>
    private static void WriteArtifacts(
        Utf8JsonWriter writer, IReadOnlyList<ArtifactFile> artifacts, string outputFolder)
    {
        writer.WriteStartObject("artifact");
        writer.WriteString("folder", outputFolder);

        long total = 0;
        writer.WriteStartArray("files");
        foreach (ArtifactFile file in artifacts)
        {
            total += file.Bytes;
            writer.WriteStartObject();
            writer.WriteString("name", file.Name);
            writer.WriteNumber("bytes", file.Bytes);
            writer.WriteString("sha256", file.Sha256);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteNumber("totalBytes", total);
        writer.WriteEndObject();
    }

    /// <summary>
    /// Writes the machine the artifact was staged on, without which a pinned hash means nothing.
    /// </summary>
    /// <param name="writer">The writer.</param>
    private static void WriteMachine(Utf8JsonWriter writer)
    {
        writer.WriteStartObject("stagedOn");
        writer.WriteString("operatingSystem", RuntimeInformation.OSDescription);
        writer.WriteString("runtimeIdentifier", RuntimeInformation.RuntimeIdentifier);
        writer.WriteString("processArchitecture", RuntimeInformation.ProcessArchitecture.ToString());
        writer.WriteString("processor", ProcessorName());
        writer.WriteNumber("processorCount", Environment.ProcessorCount);
        writer.WriteString("framework", RuntimeInformation.FrameworkDescription);
        writer.WriteEndObject();
    }

    /// <summary>
    /// The processor's own name, read from where the operating system publishes it: /proc/cpuinfo on
    /// Linux, the registry on Windows, the kernel's brand string on macOS. Each operating system has its
    /// own branch, and the Linux one is reached on every platform that is neither of the other two.
    /// </summary>
    /// <returns>The name, or <see langword="null"/> when this platform does not publish one.</returns>
    private static string ProcessorName()
    {
        if (OperatingSystem.IsWindows())
        {
            return WindowsProcessorName();
        }

        if (OperatingSystem.IsMacOS())
        {
            return MacProcessorName();
        }

        const string CpuInfo = "/proc/cpuinfo";
        if (!File.Exists(CpuInfo))
        {
            return null;
        }

        try
        {
            foreach (string line in File.ReadLines(CpuInfo))
            {
                if (!line.StartsWith("model name", StringComparison.Ordinal))
                {
                    continue;
                }

                int colon = line.IndexOf(':');
                if (colon >= 0 && colon + 1 < line.Length)
                {
                    return line.Substring(colon + 1).Trim();
                }
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        return null;
    }

    /// <summary>
    /// The processor's name on Windows, which has no /proc/cpuinfo: the registry's own description of the
    /// first processor ("12th Gen Intel(R) Core(TM) ..."), or the PROCESSOR_IDENTIFIER environment variable
    /// ("Intel64 Family 6 Model ...") when the registry cannot be read.
    /// </summary>
    /// <returns>The name, or <see langword="null"/> when Windows publishes neither.</returns>
    [SupportedOSPlatform("windows")]
    private static string WindowsProcessorName()
    {
        const string ProcessorKey = @"HARDWARE\DESCRIPTION\System\CentralProcessor\0";

        try
        {
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(ProcessorKey))
            {
                if (key != null)
                {
                    string name = key.GetValue("ProcessorNameString") as string;
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        return name.Trim();
                    }
                }
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
        catch (System.Security.SecurityException)
        {
        }

        string identifier = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER");
        return string.IsNullOrWhiteSpace(identifier) ? null : identifier.Trim();
    }

    /// <summary>
    /// The processor's name on macOS, which has no /proc/cpuinfo: the kernel's own brand string
    /// ("Apple M2 Pro", "Intel(R) Core(TM) i9-..."), asked for the way the sysctl command asks.
    /// </summary>
    /// <returns>The name, or <see langword="null"/> when the kernel does not publish one.</returns>
    [SupportedOSPlatform("macos")]
    private static string MacProcessorName()
    {
        const string BrandString = "machdep.cpu.brand_string";

        try
        {
            //Asked twice, as the call is meant to be: once for the length, once for the text.
            nuint length = 0;
            if (SysctlByName(BrandString, null, ref length, IntPtr.Zero, 0) != 0 || length == 0)
            {
                return null;
            }

            byte[] buffer = new byte[(int)length];
            if (SysctlByName(BrandString, buffer, ref length, IntPtr.Zero, 0) != 0)
            {
                return null;
            }

            string name = Encoding.UTF8.GetString(buffer, 0, (int)length).TrimEnd('\0').Trim();
            return name.Length == 0 ? null : name;
        }
        catch (DllNotFoundException)
        {
        }
        catch (EntryPointNotFoundException)
        {
        }

        return null;
    }

    [DllImport("libc", EntryPoint = "sysctlbyname")]
    private static extern int SysctlByName(
        [MarshalAs(UnmanagedType.LPStr)] string name,
        byte[] oldValue,
        ref nuint oldLength,
        IntPtr newValue,
        nuint newLength);

    /// <summary>
    /// One of the config-layer properties the store wrote when it pulled the model.
    /// </summary>
    /// <param name="info">What the store says about the model.</param>
    /// <param name="key">The property's name.</param>
    /// <returns>Its value, or <see langword="null"/> when the config states none.</returns>
    private static string Setting(ModelInfo info, string key)
    {
        if (info.Config == null || info.Config.AdditionalProperties == null)
        {
            return null;
        }

        JsonElement value;
        if (!info.Config.AdditionalProperties.TryGetValue(key, out value))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }

    /// <summary>
    /// The hexadecimal of a digest the store spells with a prefix.
    /// </summary>
    /// <param name="digest">The digest, as <c>sha256:&lt;hex&gt;</c> or as plain hexadecimal.</param>
    /// <returns>The hexadecimal alone, or <see langword="null"/> when there is no digest.</returns>
    private static string Sha256Of(string digest)
    {
        if (string.IsNullOrEmpty(digest))
        {
            return null;
        }

        int separator = digest.IndexOf(':');
        return separator < 0 ? digest : digest.Substring(separator + 1);
    }
}
