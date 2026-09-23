using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using CodeBrix.Audio.MusicGeneration.Models;

namespace CodeBrix.Audio.MusicGeneration.MuPT;

/// <summary>Descriptor, paths and lazy registration for the packaged MuPT Q4_K_M GGUF music model.</summary>
/// <remarks>
/// Register explicitly, then select GeneratorName in MusicGenerationOptions. Registering loads no
/// model. PreloadAsync or the first generation loads it; Release returns its memory. Both model
/// packages may be installed together because their output directories and registry names differ.
/// </remarks>
public static class MuPTModel
{
    /// <summary>The name used to register and select the packaged generator.</summary>
    public const string GeneratorName = "MuPT";

    /// <summary>The model family.</summary>
    public const string Family = "MuPT";

    /// <summary>The NuGet package carrying this model.</summary>
    public const string PackageId = "CodeBrix.Audio.MusicGeneration.MuPT.ApacheLicenseForever";

    /// <summary>The licence of the packaged model and wrapper.</summary>
    public const string LicenseId = "Apache-2.0";

    /// <summary>Collision-free model directory relative to application build/publish output.</summary>
    public const string RelativeModelDirectory = "Models/CodeBrix.Audio.MusicGeneration.MuPT";

    private static readonly IReadOnlyList<string> Names = Array.AsReadOnly(new[]
    {
        "MuPT-v1-8192-190M-Q4_K_M.gguf"
    });

    private static readonly MuPTMusicGenerator Shared = new MuPTMusicGenerator(GeneratorName, ModelPath);

    /// <summary>The immutable list of inference artifacts, excluding notices and provenance.</summary>
    public static IReadOnlyList<string> FileNames => Names;

    /// <summary>Absolute model directory beside the running application.</summary>
    public static string ModelDirectory => ResolveDirectory();

    /// <summary>Absolute path of the packaged quantized GGUF model.</summary>
    public static string ModelPath => Path.Combine(ModelDirectory, "MuPT-v1-8192-190M-Q4_K_M.gguf");

    /// <summary>The shared lazy generator; inspecting it opens no files.</summary>
    public static MuPTMusicGenerator Instance => Shared;

    /// <summary>Whether this exact instance is registered under its model name.</summary>
    public static bool IsRegistered => MusicGeneratorRegistry.Registered.Any(item => ReferenceEquals(item, Shared));

    /// <summary>Whether the model is currently loaded in memory.</summary>
    public static bool IsLoaded => Shared.IsLoaded;

    /// <summary>Whether every packaged inference artifact exists at its expected output path.</summary>
    public static bool IsAvailable => FileNames.All(file => File.Exists(Path.Combine(ModelDirectory, file)));

    /// <summary>Registers the shared generator idempotently. No model file is opened.</summary>
    /// <exception cref="InvalidOperationException">A different generator already owns the name.</exception>
    public static void Register() => MusicGeneratorRegistry.Register(Shared);

    /// <summary>Resolves the model directory below an application output directory without opening files.</summary>
    /// <param name="applicationDirectory">Output root; null uses AppContext.BaseDirectory.</param>
    /// <returns>An absolute directory path.</returns>
    public static string ResolveDirectory(string applicationDirectory = null) =>
        Path.GetFullPath(Path.Combine(applicationDirectory ?? AppContext.BaseDirectory, RelativeModelDirectory));

    /// <summary>Resolves the immutable logical-filename-to-path map, without loading the model.</summary>
    /// <param name="applicationDirectory">Output root; null uses AppContext.BaseDirectory.</param>
    /// <returns>All inference artifact paths.</returns>
    public static IReadOnlyDictionary<string, string> ResolveFiles(string applicationDirectory = null)
    {
        var directory = ResolveDirectory(applicationDirectory);
        return new ReadOnlyDictionary<string, string>(FileNames.ToDictionary(file => file,
            file => Path.Combine(directory, file), StringComparer.Ordinal));
    }
}
