using System;
using System.Globalization;

namespace CodeBrix.Audio.MusicGeneration.MuPT.ModelStager;

/// <summary>
/// The stager's console output: one timestamped line per event, so that a run that takes minutes says
/// what it is doing while it does it and reads back afterwards as a record of what happened.
/// </summary>
internal static class Log
{
    /// <summary>How often a step that reports a percentage is allowed to print.</summary>
    internal static readonly TimeSpan ProgressInterval = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Writes a timestamped line.
    /// </summary>
    /// <param name="message">The text of the line.</param>
    internal static void Line(string message)
    {
        Console.WriteLine(Stamp() + message);
    }

    /// <summary>
    /// Writes a blank line, which separates the sections of a run.
    /// </summary>
    internal static void Blank()
    {
        Console.WriteLine();
    }

    /// <summary>
    /// Announces a step of the run.
    /// </summary>
    /// <param name="number">Which step this is, counting from one.</param>
    /// <param name="total">How many steps the run has.</param>
    /// <param name="title">What the step does.</param>
    internal static void Step(int number, int total, string title)
    {
        Console.WriteLine();
        Console.WriteLine(Stamp() + string.Format(
            CultureInfo.InvariantCulture, "STEP {0}/{1}  {2}", number, total, title));
    }

    /// <summary>
    /// Writes an indented line underneath the step that produced it.
    /// </summary>
    /// <param name="message">The text of the line.</param>
    internal static void Detail(string message)
    {
        Console.WriteLine(Stamp() + "    " + message);
    }

    /// <summary>
    /// Writes a failure to the error stream.
    /// </summary>
    /// <param name="message">What went wrong.</param>
    internal static void Error(string message)
    {
        Console.Error.WriteLine(Stamp() + "ERROR  " + message);
    }

    /// <summary>
    /// Formats a byte count the way this tool reports sizes: the exact number, and the same number in
    /// mebibytes for a human.
    /// </summary>
    /// <param name="bytes">The count.</param>
    /// <returns>The formatted text.</returns>
    internal static string Bytes(long bytes)
    {
        return string.Format(
            CultureInfo.InvariantCulture, "{0:N0} bytes ({1:F1} MiB)", bytes, bytes / (1024.0 * 1024.0));
    }

    /// <summary>
    /// Formats a duration the way this tool reports times.
    /// </summary>
    /// <param name="elapsed">The duration.</param>
    /// <returns>The formatted text.</returns>
    internal static string Duration(TimeSpan elapsed)
    {
        return elapsed.TotalSeconds < 90
            ? string.Format(CultureInfo.InvariantCulture, "{0:F1} s", elapsed.TotalSeconds)
            : string.Format(
                CultureInfo.InvariantCulture, "{0:F1} min ({1:F0} s)",
                elapsed.TotalMinutes, elapsed.TotalSeconds);
    }

    /// <summary>
    /// The timestamp every line carries.
    /// </summary>
    /// <returns>The local time of day in brackets.</returns>
    private static string Stamp()
    {
        return DateTime.Now.ToString("[HH:mm:ss] ", CultureInfo.InvariantCulture);
    }
}
