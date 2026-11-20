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
    /// <see cref="IOException"/> or <see cref="UnauthorizedAccessException"/>.
    /// </summary>
    public void Load()
    {
        // unlikely but if someone else has pre queued some stuff, this replaces that
        UIDAllocator.StudyUIDs.Clear();
        UIDAllocator.SeriesUIDs.Clear();
        UIDAllocator.SOPUIDs.Clear();

        foreach (var u in GetUIDsFrom(StudyInstanceUIDs))
            UIDAllocator.StudyUIDs.Enqueue(u);

        foreach (var u in GetUIDsFrom(SeriesInstanceUIDs))
            UIDAllocator.SeriesUIDs.Enqueue(u);

        foreach (var u in GetUIDsFrom(SOPInstanceUIDs))
            UIDAllocator.SOPUIDs.Enqueue(u);
    }

    private static IEnumerable<string> GetUIDsFrom(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return [];

        // Read the file rather than asking File.Exists first: File.Exists also returns false for a file the
        // caller isn't allowed to read, which would skip it silently. Reading it all now raises any failure here.
        try
        {
            return File.ReadAllLines(path).Where(static l => !string.IsNullOrWhiteSpace(l));
        }
        // A UID file that doesn't exist is skipped, as documented; any other failure goes to the caller
        catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException)
        {
            return [];
        }
    }
}
