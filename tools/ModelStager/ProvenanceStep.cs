using System;
using System.Collections.Generic;
using CodeBrix.Ollama.ModelManager;

namespace CodeBrix.Audio.MusicGeneration.MuPT.ModelStager;

/// <summary>
/// One step of the staging road, as the store itself recorded it. The settings are the store's own
/// strings, copied into the provenance file verbatim rather than restated - what is written down is
/// then what actually ran, and not what this tool believes it asked for.
/// </summary>
internal sealed class ProvenanceStep
{
    /// <summary>
    /// Initializes a step.
    /// </summary>
    /// <param name="name">A short name for the step, such as "convert".</param>
    /// <param name="operation">The library call that did it.</param>
    /// <param name="storedName">The name the result is stored under.</param>
    /// <param name="derivedFrom">The model the result was derived from.</param>
    /// <param name="tool">The tool the store recorded as having done the work.</param>
    /// <param name="toolVersion">That tool's version.</param>
    /// <param name="settings">The settings the store recorded, verbatim.</param>
    private ProvenanceStep(
        string name,
        string operation,
        string storedName,
        string derivedFrom,
        string tool,
        string toolVersion,
        IReadOnlyDictionary<string, string> settings)
    {
        Name = name;
        Operation = operation;
        StoredName = storedName;
        DerivedFrom = derivedFrom;
        Tool = tool;
        ToolVersion = toolVersion;
        Settings = settings ?? new Dictionary<string, string>(StringComparer.Ordinal);
    }

    /// <summary>A short name for the step.</summary>
    internal string Name { get; }

    /// <summary>The library call that did it.</summary>
    internal string Operation { get; }

    /// <summary>The name the result is stored under.</summary>
    internal string StoredName { get; }

    /// <summary>The model the result was derived from.</summary>
    internal string DerivedFrom { get; }

    /// <summary>The tool the store recorded as having done the work.</summary>
    internal string Tool { get; }

    /// <summary>That tool's version.</summary>
    internal string ToolVersion { get; }

    /// <summary>The settings the store recorded, verbatim.</summary>
    internal IReadOnlyDictionary<string, string> Settings { get; }

    /// <summary>
    /// Reads a step out of what the store says about the model it produced.
    /// </summary>
    /// <param name="name">A short name for the step, such as "convert".</param>
    /// <param name="operation">The library call that did it.</param>
    /// <param name="info">What the store says about the result.</param>
    /// <returns>The step.</returns>
    internal static ProvenanceStep FromModel(string name, string operation, ModelInfo info)
    {
        return new ProvenanceStep(
            name, operation, info.DisplayName, info.DerivedFrom, info.Tool, info.ToolVersion, info.Settings);
    }
}
