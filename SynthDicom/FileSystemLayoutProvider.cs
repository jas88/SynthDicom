using System.IO;

namespace SynthDicom;

/// <summary>
/// Provides file system path generation for DICOM files based on the configured layout strategy.
/// </summary>
/// <param name="layout">The file system layout strategy to use</param>
internal class FileSystemLayoutProvider(FileSystemLayout layout)
{
    /// <summary>
    /// Gets the configured file system layout strategy.
    /// </summary>
    public FileSystemLayout Layout { get; } = layout;

    /// <summary>
    /// Generates the file path for a DICOM dataset based on the configured layout strategy.
    /// </summary>
    /// <param name="root">Root directory where DICOM files are stored</param>
    /// <param name="ds">DICOM dataset containing UIDs and metadata</param>
    /// <returns>FileInfo representing the full path where the DICOM file should be written</returns>
    public FileInfo GetPath(DirectoryInfo root, DicomDataset ds)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(ds);

        var sopUID = ds.GetSingleValue<DicomUID>(DicomTag.SOPInstanceUID).UID;
        var filename = string.Create(sopUID.Length + 4, sopUID, static (span, uid) =>
        {
            uid.AsSpan().CopyTo(span);
            ".dcm".AsSpan().CopyTo(span[uid.Length..]);
        });
        var date = ds.GetValues<DateTime>(DicomTag.StudyDate);

        return Layout switch
        {
            FileSystemLayout.Flat => new FileInfo(Path.Combine(root.FullName, filename)),

            FileSystemLayout.StudyYearMonthDay when date.Length > 0 => new FileInfo(Path.Combine(
                root.FullName,
                date[0].Year.ToString(),
                date[0].Month.ToString(),
                date[0].Day.ToString(),
                filename)),

            FileSystemLayout.StudyYearMonthDayAccession when date.Length > 0 && !string.IsNullOrWhiteSpace(ds.GetSingleValue<string>(DicomTag.AccessionNumber)) => new FileInfo(Path.Combine(
                root.FullName,
                date[0].Year.ToString(),
                date[0].Month.ToString(),
                date[0].Day.ToString(),
                ds.GetSingleValue<string>(DicomTag.AccessionNumber),
                filename)),

            FileSystemLayout.StudyUID => new FileInfo(Path.Combine(
                root.FullName,
                ds.GetSingleValue<DicomUID>(DicomTag.StudyInstanceUID).UID,
                filename)),

            _ => new FileInfo(Path.Combine(root.FullName, filename))
        };
    }

}