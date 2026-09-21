using System;
using System.Collections.Generic;
using System.Globalization;

namespace CodeBrix.Audio.MusicGeneration.MuPT.ModelStager;

/// <summary>
/// What the run cost: how long each step took, how much was downloaded, and how much disk the staging
/// folder held at its worst. Staging a model spends bandwidth, time and disk, so a run says what it
/// spent rather than leaving it to be guessed at.
/// </summary>
internal sealed class UsageReport
{
    private readonly List<KeyValuePair<string, TimeSpan>> _steps =
        new List<KeyValuePair<string, TimeSpan>>();

    /// <summary>How many bytes the pull reported fetching.</summary>
    internal long DownloadedBytes { get; set; }

    /// <summary>
    /// Records what one step took.
    /// </summary>
    /// <param name="title">What the step did.</param>
    /// <param name="elapsed">How long it took.</param>
    internal void Record(string title, TimeSpan elapsed)
    {
        _steps.Add(new KeyValuePair<string, TimeSpan>(title, elapsed));
    }

    /// <summary>
    /// Writes the report.
    /// </summary>
    /// <param name="watcher">The watcher that sampled the run.</param>
    /// <param name="total">How long the whole run took.</param>
    internal void Write(StagingWatcher watcher, TimeSpan total)
    {
        Log.Blank();
        Log.Line("USAGE REPORT");
        Log.Detail("downloaded              " + Log.Bytes(DownloadedBytes));
        Log.Detail("peak staging/ size      " + Log.Bytes(watcher.PeakStagingBytes)
            + "  (apparent; hard links counted once per link)");

        if (watcher.PeakVolumeBytes >= 0)
        {
            Log.Detail("peak volume taken       " + Log.Bytes(watcher.PeakVolumeBytes)
                + "  (free space at its lowest, this machine as a whole)");
        }

        if (watcher.StartingFreeBytes >= 0)
        {
            Log.Detail("free space now          " + Log.Bytes(
                DiskUsage.AvailableFreeBytes(Environment.CurrentDirectory)));
        }

        Log.Detail("elapsed, per step:");
        foreach (KeyValuePair<string, TimeSpan> step in _steps)
        {
            Log.Detail(string.Format(
                CultureInfo.InvariantCulture, "    {0,-34} {1}", step.Key, Log.Duration(step.Value)));
        }

        Log.Detail(string.Format(
            CultureInfo.InvariantCulture, "    {0,-34} {1}", "TOTAL", Log.Duration(total)));
    }
}
