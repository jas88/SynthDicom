using System.Collections.Concurrent;
using System.IO;
using YamlDotNet.Serialization;

namespace BadDicom.Configuration;

/// <summary>
/// Config section for loading explicit UIDs from disk and using those in file creation
/// </summary>
[YamlSerializable]
public sealed class ExplicitUIDs
{
    /// <summary>
    /// Path to a file containing a list of study instance UIDs to use
    /// </summary>
    public string? StudyInstanceUIDs { get; set; }

    /// <summary>
    /// Path to a file containing a list of series instance UIDs to use
    /// </summary>
    public string? SeriesInstanceUIDs { get; set; }

    /// <summary>
    /// Path to a file containing a list of SOP instance UIDs to use
    /// </summary>
    public string? SOPInstanceUIDs { get; set; }

    /// <summary>
    /// Loads the UID files referenced (if they exist) in the configuration
    /// and populates <see cref="UIDAllocator"/> with the values.
    /// A file that doesn't exist is skipped; one that exists but can't be read throws an
    /// <see cref="IOException"/> or <see cref="UnauthorizedAccessException"/>, and a path that can't name a file
    /// throws an <see cref="ArgumentException"/> or <see cref="PathTooLongException"/>.
    /// </summary>
    public void Load()
    {
        // unlikely but if someone else has pre queued some stuff, this replaces that
        UIDAllocator.StudyUIDs.Clear();
        UIDAllocator.SeriesUIDs.Clear();
        UIDAllocator.SOPUIDs.Clear();

        EnqueueUIDsFrom(StudyInstanceUIDs, UIDAllocator.StudyUIDs);
        EnqueueUIDsFrom(SeriesInstanceUIDs, UIDAllocator.SeriesUIDs);
        EnqueueUIDsFrom(SOPInstanceUIDs, UIDAllocator.SOPUIDs);
    }

    /// <summary>
    /// Queues the UIDs in the file at <paramref name="path"/>, one per non-blank line, reading the file as a stream
    /// rather than holding all of it in memory as well as in the queue.
    /// </summary>
    private static void EnqueueUIDsFrom(string? path, ConcurrentQueue<string> queue)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        // Read the file rather than asking File.Exists first: File.Exists also returns false for a file the
        // caller isn't allowed to read, which would skip it silently. File.ReadLines opens the file when it is
        // called and reads it while it is enumerated, so the try covers the whole loop.
        try
        {
            foreach (var line in File.ReadLines(path))
            {
                if (!string.IsNullOrWhiteSpace(line))
                    queue.Enqueue(line);
            }
        }
        catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException)
        {
            // A UID file that doesn't exist is skipped, as documented; any other failure goes to the caller
        }
    }
}
