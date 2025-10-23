using System.Collections.Concurrent;

namespace SynthDicom;

/// <summary>
/// Allocates <see cref="DicomUID"/> values from an explicit list(s) or
/// by calling <see cref="DicomUID.Generate"/>.
/// </summary>
public class UIDAllocator
{
    /// <summary>
    /// Explicit <see cref="DicomUID"/> string values to use when allocating uids for studies
    /// </summary>
    public static readonly ConcurrentQueue<string> StudyUIDs = new ();


    /// <summary>
    /// Explicit <see cref="DicomUID"/> string values to use when allocating uids for series
    /// </summary>
    public static readonly ConcurrentQueue<string> SeriesUIDs = new();

    /// <summary>
    /// Explicit <see cref="DicomUID"/> string values to use when allocating uids for images
    /// </summary>
    public static readonly ConcurrentQueue<string> SOPUIDs = new();

    /// <summary>
    /// Generates a new Study Instance UID, either from the <see cref="StudyUIDs"/> queue or by calling <see cref="DicomUID.Generate"/>.
    /// </summary>
    /// <returns>A new unique <see cref="DicomUID"/> for a DICOM study</returns>
    public static DicomUID GenerateStudyInstanceUID() =>
        StudyUIDs.TryDequeue(out var result)
            ? new DicomUID(result, "Local UID", DicomUidType.Unknown)
            : DicomUID.Generate();

    /// <summary>
    /// Generates a new Series Instance UID, either from the <see cref="SeriesUIDs"/> queue or by calling <see cref="DicomUID.Generate"/>.
    /// </summary>
    /// <returns>A new unique <see cref="DicomUID"/> for a DICOM series</returns>
    public static DicomUID GenerateSeriesInstanceUID() => SeriesUIDs.TryDequeue(out var result)
        ? new DicomUID(result, "Local UID", DicomUidType.Unknown)
        : DicomUID.Generate();

    /// <summary>
    /// Generates a new SOP Instance UID, either from the <see cref="SOPUIDs"/> queue or by calling <see cref="DicomUID.Generate"/>.
    /// </summary>
    /// <returns>A new unique <see cref="DicomUID"/> for a DICOM image instance</returns>
    public static DicomUID GenerateSOPInstanceUID() => SOPUIDs.TryDequeue(out var result)
        ? new DicomUID(result, "Local UID", DicomUidType.Unknown)
        : DicomUID.Generate();
}