using FellowOakDicom;
using System;
using System.IO;

namespace SynthDicom;

internal class FileSystemLayoutProvider(FileSystemLayout layout)
{
    public FileSystemLayout Layout { get; } = layout;

    public FileInfo GetPath(DirectoryInfo root,DicomDataset ds)
    {
        var sopUID = ds.GetSingleValue<DicomUID>(DicomTag.SOPInstanceUID).UID;
        var filename = string.Create(sopUID.Length + 4, sopUID, static (span, uid) =>
        {
            uid.AsSpan().CopyTo(span);
            ".dcm".AsSpan().CopyTo(span[uid.Length..]);
        });
        var date = ds.GetValues<DateTime>(DicomTag.StudyDate);

        switch(Layout)
        {
            case FileSystemLayout.Flat:
                return  new FileInfo(Path.Combine(root.FullName,filename));

            case FileSystemLayout.StudyYearMonthDay:

                if(date.Length > 0)
                {
                    return  new FileInfo(Path.Combine(
                        root.FullName,
                        date[0].Year.ToString(),
                        date[0].Month.ToString(),
                        date[0].Day.ToString(),
                        filename));
                }
                break;

            case FileSystemLayout.StudyYearMonthDayAccession:

                var acc = ds.GetSingleValue<string>(DicomTag.AccessionNumber);

                if(date.Length > 0 && !string.IsNullOrWhiteSpace(acc))
                {
                    return  new FileInfo(Path.Combine(
                        root.FullName,
                        date[0].Year.ToString(),
                        date[0].Month.ToString(),
                        date[0].Day.ToString(),
                        acc,
                        filename));
                }
                break;

            case FileSystemLayout.StudyUID:

                return  new FileInfo(Path.Combine(
                    root.FullName,
                    ds.GetSingleValue<DicomUID>(DicomTag.StudyInstanceUID).UID,
                    filename));

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(Layout),
                    Layout,
                    $"Unsupported file system layout: {Layout}. Valid layouts are: Flat, StudyYearMonthDay, StudyYearMonthDayAccession, StudyUID");
        }

        return  new FileInfo(Path.Combine(root.FullName,filename));
    }

}