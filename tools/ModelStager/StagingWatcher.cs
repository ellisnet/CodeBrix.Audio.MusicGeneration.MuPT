using System;
using System.Threading;
using System.Threading.Tasks;

namespace CodeBrix.Audio.MusicGeneration.MuPT.ModelStager;

/// <summary>
/// Watches what the run costs while it runs. The peak of a staging run happens INSIDE a step - a
/// conversion holds the checkpoint and the file it is writing at the same time - so the cost is sampled
/// on a timer rather than measured between steps.
/// </summary>
internal sealed class StagingWatcher : IDisposable
{
    /// <summary>How often the folder and the volume are measured.</summary>
    private static readonly TimeSpan SampleInterval = TimeSpan.FromSeconds(2);

    private readonly StagingLayout _layout;
    private readonly CancellationTokenSource _stopping = new CancellationTokenSource();
    private readonly Task _sampling;
    private long _peakStagingBytes;
    private long _lowestFreeBytes;

    /// <summary>
    /// Starts watching a staging folder.
    /// </summary>
    /// <param name="layout">The folders to watch.</param>
    internal StagingWatcher(StagingLayout layout)
    {
        _layout = layout;
        StartingFreeBytes = DiskUsage.AvailableFreeBytes(layout.StagingDirectory);
        _lowestFreeBytes = StartingFreeBytes;
        _peakStagingBytes = layout.MeasureStagingBytes();
        _sampling = Task.Run(SampleAsync);
    }

    /// <summary>How much the volume had free when the run began, or -1 when the platform would not say.</summary>
    internal long StartingFreeBytes { get; }

    /// <summary>The largest apparent size the staging folder reached, hard links counted once per link.</summary>
    internal long PeakStagingBytes
    {
        get { return Interlocked.Read(ref _peakStagingBytes); }
    }

    /// <summary>
    /// How much of the volume the run took at its worst: the difference between the free space it
    /// started with and the least it ever had. It counts anything else running on the machine too, so it
    /// is a ceiling rather than a measurement of this tool alone.
    /// </summary>
    internal long PeakVolumeBytes
    {
        get
        {
            long lowest = Interlocked.Read(ref _lowestFreeBytes);
            return StartingFreeBytes < 0 || lowest < 0 ? -1 : StartingFreeBytes - lowest;
        }
    }

    /// <summary>
    /// Takes one more sample, which the caller does at the end of each step so that the report never
    /// misses a peak that came and went between two ticks.
    /// </summary>
    internal void Sample()
    {
        Record(_layout.MeasureStagingBytes(), DiskUsage.AvailableFreeBytes(_layout.StagingDirectory));
    }

    /// <summary>
    /// Stops sampling and waits for the sampler to finish.
    /// </summary>
    public void Dispose()
    {
        _stopping.Cancel();
        try
        {
            _sampling.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
        }

        _stopping.Dispose();
    }

    /// <summary>
    /// Samples until the watcher is disposed.
    /// </summary>
    /// <returns>A task that completes when sampling stops.</returns>
    private async Task SampleAsync()
    {
        while (!_stopping.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(SampleInterval, _stopping.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            Sample();
        }
    }

    /// <summary>
    /// Keeps a sample when it is worse than every sample before it.
    /// </summary>
    /// <param name="stagingBytes">What the staging folder holds now.</param>
    /// <param name="freeBytes">What the volume has free now, or -1.</param>
    private void Record(long stagingBytes, long freeBytes)
    {
        long peak = Interlocked.Read(ref _peakStagingBytes);
        if (stagingBytes > peak)
        {
            Interlocked.Exchange(ref _peakStagingBytes, stagingBytes);
        }

        long lowest = Interlocked.Read(ref _lowestFreeBytes);
        if (freeBytes >= 0 && (lowest < 0 || freeBytes < lowest))
        {
            Interlocked.Exchange(ref _lowestFreeBytes, freeBytes);
        }
    }
}
