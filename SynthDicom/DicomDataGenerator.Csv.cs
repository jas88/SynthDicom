using System.Globalization;
using System.IO;
using CsvHelper;

namespace SynthDicom;

/// <summary>
/// The <see cref="Csv"/> output of <see cref="DicomDataGenerator"/>: Study, Series and Image level CSV files
/// </summary>
public partial class DicomDataGenerator
{
    private static readonly List<DicomTag> StudyTags =
    [
        DicomTag.PatientID,
        DicomTag.StudyInstanceUID,
        DicomTag.StudyDate,
        DicomTag.StudyTime,
        DicomTag.ModalitiesInStudy,
        DicomTag.StudyDescription,
        DicomTag.PatientAge,
        DicomTag.NumberOfStudyRelatedInstances,
        DicomTag.PatientBirthDate
    ];
    private static readonly List<DicomTag> SeriesTags =
    [
        DicomTag.StudyInstanceUID,
        DicomTag.SeriesInstanceUID,
        DicomTag.SeriesDate,
        DicomTag.SeriesTime,
        DicomTag.Modality,
        DicomTag.ImageType,
        DicomTag.SourceApplicationEntityTitle,
        DicomTag.InstitutionName,
        DicomTag.ProcedureCodeSequence,
        DicomTag.ProtocolName,
        DicomTag.PerformedProcedureStepID,
        DicomTag.PerformedProcedureStepDescription,
        DicomTag.SeriesDescription,
        DicomTag.BodyPartExamined,
        DicomTag.DeviceSerialNumber,
        DicomTag.NumberOfSeriesRelatedInstances,
        DicomTag.SeriesNumber
    ];
    private static readonly List<DicomTag> ImageTags =
        [
            DicomTag.SeriesInstanceUID,
            DicomTag.SOPInstanceUID,
            DicomTag.BurnedInAnnotation,
            DicomTag.SliceLocation,
            DicomTag.SliceThickness,
            DicomTag.SpacingBetweenSlices,
            DicomTag.SpiralPitchFactor,
            DicomTag.KVP,
            DicomTag.ExposureTime,
            DicomTag.Exposure,
            DicomTag.ManufacturerModelName,
            DicomTag.Manufacturer,
            DicomTag.XRayTubeCurrent,
            DicomTag.PhotometricInterpretation,
            DicomTag.ContrastBolusRoute,
            DicomTag.ContrastBolusAgent,
            DicomTag.AcquisitionNumber,
            DicomTag.AcquisitionDate,
            DicomTag.AcquisitionTime,
            DicomTag.ImagePositionPatient,
            DicomTag.PixelSpacing,
            DicomTag.FieldOfViewDimensions,
            DicomTag.FieldOfViewDimensionsInFloat,
            DicomTag.DerivationDescription,
            DicomTag.TransferSyntaxUID,
            DicomTag.LossyImageCompression,
            DicomTag.LossyImageCompressionMethod,
            DicomTag.LossyImageCompressionRatio,
            DicomTag.ScanOptions
        ];

    private string _lastStudyUID = "";
    private string _lastSeriesUID = "";
    private CsvWriter? _studyWriter, _seriesWriter, _imageWriter;

    /// <summary>
    /// Name of the file that contains distinct Study level records for all images when <see cref="Csv"/> is true
    /// </summary>
    public const string StudyCsvFilename = "study.csv";

    /// <summary>
    /// Name of the file that contains distinct Series level records for all images when <see cref="Csv"/> is true
    /// </summary>
    public const string SeriesCsvFilename = "series.csv";

    /// <summary>
    /// Name of the file that contains distinct Image level records for all images when <see cref="Csv"/> is true
    /// </summary>
    public const string ImageCsvFilename = "image.csv";

    private bool _csvInitialized;

    // ACH - Methods for CSV output added below

    private void InitialiseCSVOutput()
    {
        // Write the headers
        if (_csvInitialized)
            return;
        _csvInitialized = true;

        if (OutputDir == null)
            return;
        // Create/open CSV files with proper disposal via using statements
        _studyWriter = new CsvWriter(new StreamWriter(Path.Join(OutputDir.FullName, StudyCsvFilename)), CultureInfo.InvariantCulture);
        _seriesWriter = new CsvWriter(new StreamWriter(Path.Join(OutputDir.FullName, SeriesCsvFilename)), CultureInfo.InvariantCulture);
        _imageWriter = new CsvWriter(new StreamWriter(Path.Join(OutputDir.FullName, ImageCsvFilename)), CultureInfo.InvariantCulture);

        // Write header
        WriteData(_studyWriter, StudyTags.Select(i => i.DictionaryEntry.Keyword));
        WriteData(_seriesWriter, SeriesTags.Select(i => i.DictionaryEntry.Keyword));
        WriteData(_imageWriter, ImageTags.Select(i => i.DictionaryEntry.Keyword));
    }

    private static void WriteData(CsvWriter sw, IEnumerable<string> data)
    {
        foreach (var s in data)
            sw.WriteField(s);

        sw.NextRecord();
    }

    private void AddDicomDatasetToCSV(DicomDataset ds, CsvWriter studies, CsvWriter series, CsvWriter images)
    {
        if (_lastStudyUID != ds.GetString(DicomTag.StudyInstanceUID))
        {
            _lastStudyUID = ds.GetString(DicomTag.StudyInstanceUID);

            WriteTags(studies, StudyTags, ds);
        }

        if (_lastSeriesUID != ds.GetString(DicomTag.SeriesInstanceUID))
        {
            _lastSeriesUID = ds.GetString(DicomTag.SeriesInstanceUID);

            WriteTags(series, SeriesTags, ds);
        }

        WriteTags(images, ImageTags, ds);
    }

    private static void WriteTags(CsvWriter sw, IEnumerable<DicomTag> tags, DicomDataset ds)
    {
        var columnData = tags.Select(tag => ds.Contains(tag) ? ds.GetString(tag) : "NULL");
        WriteData(sw, columnData);
        sw.Flush();
    }
}
