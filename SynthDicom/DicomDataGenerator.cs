using SynthEHR.Datasets;
using System.IO;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using CsvHelper;

namespace SynthDicom;

/// <summary>
/// <see cref="DataGenerator"/> which produces dicom files on disk and accompanying metadata
/// </summary>
public class DicomDataGenerator : DataGenerator,IDisposable
{
    /// <summary>
    /// Location on disk to output dicom files to
    /// </summary>
    public DirectoryInfo? OutputDir { get; }

    /// <summary>
    /// Set to true to generate <see cref="DicomDataset"/> without any pixel data.
    /// </summary>
    public bool NoPixels { get; set; }

    /// <summary>
    /// Set to true to discard the generated DICOM files, usually for testing.
    /// </summary>
    private bool DevNull { get; }

    private static readonly string DevNullPath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)?"NUL":"/dev/null";

    /// <summary>
    /// Set to true to run <see cref="DicomAnonymizer"/> on the generated <see cref="DicomDataset"/> before writing to disk.
    /// </summary>
    public bool Anonymise {get;set;}

    /// <summary>
    /// True to output Study / Series / Image level CSV files containing all the tag data.  Setting this option
    /// disables image file output
    /// </summary>
    public bool Csv { get; set; }

    /// <summary>
    /// The subdirectories layout to put dicom files into when writing to disk
    /// </summary>
    public FileSystemLayout Layout{
        get => _pathProvider.Layout;
        set => _pathProvider = new FileSystemLayoutProvider(value);
    }

    /// <summary>
    /// The maximum number of images to generate regardless of how many calls to <see cref="GenerateTestDataRow"/>,  Defaults to int.MaxValue
    /// </summary>
    public int MaximumImages { get; set; } = int.MaxValue;

    /// <summary>
    /// Number of frames to generate per image (1-1000)
    /// </summary>
    public int NumberOfFrames { get; set; } = 1;

    private FileSystemLayoutProvider _pathProvider = new(FileSystemLayout.StudyYearMonthDay);

    private readonly int[]? _modalities;

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
    private readonly DicomAnonymizer _anonymiser = new();

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

    private bool csvInitialized;

    /// <summary>
    /// Initializes a new instance of the <see cref="DicomDataGenerator"/> class for generating synthetic DICOM images.
    /// </summary>
    /// <param name="r">Random number generator for deterministic synthetic data generation</param>
    /// <param name="outputDir">Directory path where DICOM files will be written, or null/"/dev/null" to discard output</param>
    /// <param name="modalities">List of modalities to generate (e.g., "CT", "MR"). The frequency of images generated is based on
    /// the popularity of that modality in a clinical PACS. Passing no modalities results in all supported modalities being generated</param>
    /// <exception cref="ArgumentException">Thrown when an invalid modality is specified in <paramref name="modalities"/></exception>
    public DicomDataGenerator(Random r, string? outputDir, params string[] modalities) : base(r)
    {
        DevNull = outputDir?.Equals("/dev/null", StringComparison.InvariantCulture) != false;
        OutputDir = DevNull ? null : Directory.CreateDirectory(outputDir!);

        var stats = DicomDataGeneratorStats.GetInstance();

        var modalityList = new HashSet<string>(modalities);
        // Iterate through known modalities, listing their offsets within the BucketList
        _modalities = stats.ModalityFrequency.Select(static i => i.item.Modality).Select(static (m, i) => (m, i))
            .Where(i => modalityList.Count == 0 || modalityList.Contains(i.m)).Select(static i => i.i).ToArray();

        if (modalityList.Count != 0 && modalityList.Count != _modalities.Length)
        {
            var requestedModalities = string.Join(", ", modalities);
            var validModalities = string.Join(", ", stats.ModalityFrequency.Select(i => i.item.Modality));
            throw new ArgumentException(
                $"Invalid modality list provided: '{requestedModalities}'. " +
                $"Valid modalities are: {validModalities}",
                nameof(modalities));
        }
    }

    /// <summary>
    /// Generates a complete study with all series and images for a given person and writes them to disk or CSV.
    /// </summary>
    /// <param name="p">Person demographics and information for the patient</param>
    /// <returns>Array containing the generated Study UID as the single element</returns>
    public override object?[] GenerateTestDataRow(Person p)
    {
        ArgumentNullException.ThrowIfNull(p);

        if(!csvInitialized && Csv)
            InitialiseCSVOutput();

        //The currently extracting study
        string? studyUID = null;

        foreach (var ds in GenerateStudyImages(p, out var study))
        {
            //don't generate more than the maximum number of images
            if (MaximumImages-- <= 0)
            {
                break;
            }

            studyUID = study.StudyUID.UID; //all images will have the same study

            // ACH : additions to produce some CSV data
            if(Csv)
                AddDicomDatasetToCSV(
                    ds,
                    _studyWriter ?? throw new InvalidOperationException(),
                    _seriesWriter ?? throw new InvalidOperationException(),
                    _imageWriter ?? throw new InvalidOperationException());
            else
            {
                var f = new DicomFile(ds);

                FileInfo? fi=null;
                if (!DevNull)
                {
                    fi = _pathProvider.GetPath(OutputDir!, f.Dataset);
                    if (fi.Directory is { Exists: false })
                        fi.Directory.Create();
                }

                using var outFile = new FileStream(fi?.FullName ?? DevNullPath, FileMode.Create);
                f.Save(outFile);
            }
        }

        //in the CSV write only the StudyUID
        return [studyUID];
    }

    /// <summary>
    /// Returns headers for the inventory file produced during <see cref="GenerateTestDataset(Person,Random)"/>.
    /// </summary>
    /// <returns>Array containing column header names for the inventory CSV</returns>
    protected override string[] GetHeaders()
    {
        return ["Studies Generated"];
    }

    /// <summary>
    /// Creates a DICOM study for the specified person with tag values that make sense for that person. This call
    /// will generate an entire study with a random number of series and a random number of images per series
    /// based on modality statistics (e.g., for CT studies you might get 2 series of ~100 images each).
    /// </summary>
    /// <param name="p">Person demographics and information for the patient</param>
    /// <param name="study">The generated study containing all series and images</param>
    /// <returns>Array of all <see cref="DicomDataset"/> instances generated for the study</returns>
    public DicomDataset[] GenerateStudyImages(Person p, out Study study)
    {
        ArgumentNullException.ThrowIfNull(p);

        //generate a study
        study = new Study(this,p,GetRandomModality(r),r);

        // Calculate total image count for array pooling (sum of all series image counts)
        var totalImages = study.Series.Sum(s => s.NumberOfSeriesRelatedInstances);
        var buffer = ArrayPool<DicomDataset>.Shared.Rent(totalImages);
        try
        {
            var index = 0;
            foreach (var dataset in study.SelectMany(series => series))
            {
                buffer[index++] = dataset;
            }

            var result = new DicomDataset[index];
            Array.Copy(buffer, result, index);
            return result;
        }
        finally
        {
            ArrayPool<DicomDataset>.Shared.Return(buffer, clearArray: true);
        }
    }

    /// <summary>
    /// Asynchronously streams dicom study images for the <paramref name="p"/> with tag values that make sense for that person.
    /// This method generates images on-demand, reducing memory pressure for large datasets.
    /// The study parameter will be set after the first yield return.
    /// </summary>
    /// <param name="p">Person to generate study for</param>
    /// <param name="ct">Cancellation token for cooperative cancellation</param>
    /// <returns>Async enumerable of DICOM datasets</returns>
    public async IAsyncEnumerable<(DicomDataset Dataset, Study Study)> GenerateStudyImagesAsync(
        Person p,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(p);

        //generate a study
        var study = new Study(this,p,GetRandomModality(r),r);

        foreach (var series in study)
        {
            // Allow cooperative cancellation between series
            await Task.Yield();
            ct.ThrowIfCancellationRequested();

            foreach (var dataset in series)
            {
                ct.ThrowIfCancellationRequested();
                yield return (dataset, study);
            }
        }
    }

    /// <summary>
    /// Generates a new <see cref="DicomDataset"/> for the given <see cref="Person"/>. This will be a single image in a single series study.
    /// </summary>
    /// <param name="p">Person demographics and information for the patient</param>
    /// <param name="_r">Random number generator for this dataset</param>
    /// <returns>A single <see cref="DicomDataset"/> containing all DICOM tags for one image</returns>
    public DicomDataset GenerateTestDataset(Person p, Random _r)
    {
        ArgumentNullException.ThrowIfNull(p);

        //get a random modality
        var modality = GetRandomModality(_r);
        return GenerateTestDataset(p,new Study(this,p,modality,_r).Series[0]);
    }

    /// <summary>
    /// Asynchronously generates a new <see cref="DicomDataset"/> for the given <see cref="Person"/>.
    /// This will be a single image single series study.
    /// </summary>
    /// <param name="p">Person to generate dataset for</param>
    /// <param name="_r">Random number generator</param>
    /// <param name="ct">Cancellation token for cooperative cancellation</param>
    /// <returns>Task containing the generated DICOM dataset</returns>
    public Task<DicomDataset> GenerateTestDatasetAsync(Person p, Random _r, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(p);

        ct.ThrowIfCancellationRequested();

        //get a random modality
        var modality = GetRandomModality(_r);
        return Task.FromResult(GenerateTestDataset(p,new Study(this,p,modality,_r).Series[0]));
    }

    private ModalityStats GetRandomModality(Random _r) =>
        _modalities is null
            ? DicomDataGeneratorStats.GetInstance().ModalityFrequency.GetRandom(_r)
            : _modalities.Length == 1
                ? DicomDataGeneratorStats.GetInstance().ModalityFrequency.Skip(_modalities[0]).First().item
                : DicomDataGeneratorStats.GetInstance().ModalityFrequency.GetRandom(_modalities, _r);

    /// <summary>
    /// Generates a new DICOM image dataset for the specified person within the given series, with tag values appropriate for that person.
    /// </summary>
    /// <param name="p">Person demographics and information for the patient</param>
    /// <param name="series">Series this image belongs to, providing modality and timing information</param>
    /// <returns>A <see cref="DicomDataset"/> containing all DICOM tags for a single image</returns>
    public DicomDataset GenerateTestDataset(Person p, Series series)
    {
        ArgumentNullException.ThrowIfNull(p);
        ArgumentNullException.ThrowIfNull(series);

        var ds = new DicomDataset();

        ds.AddOrUpdate(DicomTag.StudyInstanceUID,series.Study.StudyUID);
        ds.AddOrUpdate(DicomTag.SeriesInstanceUID,series.SeriesUID);

        var sopInstanceUID = UIDAllocator.GenerateSOPInstanceUID();
        ds.AddOrUpdate(DicomTag.SOPInstanceUID,sopInstanceUID);
        ds.AddOrUpdate(DicomTag.SOPClassUID , DicomUID.SecondaryCaptureImageStorage);

        //patient details
        ds.AddOrUpdate(DicomTag.PatientID, p.CHI);
        ds.AddOrUpdate(DicomTag.PatientName, $"{p.Surname}^{p.Forename}");
        ds.AddOrUpdate(DicomTag.PatientBirthDate, p.DateOfBirth);

        if (p.Address != null)
        {
            var s =
                $"{p.Address.Line1} {p.Address.Line2} {p.Address.Line3} {p.Address.Line4} {p.Address.Postcode.Value}";

            ds.AddOrUpdate(DicomTag.PatientAddress,
                s[..Math.Min(s.Length,64)] //LO only allows 64 characters
            );
        }

        ds.AddOrUpdate(new DicomDate(DicomTag.StudyDate, series.Study.StudyDate));
        ds.AddOrUpdate(new DicomTime(DicomTag.StudyTime, DateTime.Today + series.Study.StudyTime));

        ds.AddOrUpdate(new DicomDate(DicomTag.SeriesDate, series.SeriesDate));
        ds.AddOrUpdate(new DicomTime(DicomTag.SeriesTime, DateTime.Today + series.SeriesTime));

        ds.AddOrUpdate(DicomTag.Modality,series.Modality);
        ds.AddOrUpdate(DicomTag.AccessionNumber, series.Study.AccessionNumber ?? "");

        if(series.Study.StudyDescription != null)
            ds.AddOrUpdate(DicomTag.StudyDescription,series.Study.StudyDescription);

        if(series.SeriesDescription != null)
            ds.AddOrUpdate(DicomTag.SeriesDescription, series.SeriesDescription);

        if (series.BodyPartExamined != null)
            ds.AddOrUpdate(DicomTag.BodyPartExamined, series.BodyPartExamined);

        // Calculate the age of the patient at the time the series was taken
        var age = series.SeriesDate.Year - p.DateOfBirth.Year;
        // Go back to the year the person was born in case of a leap year
        if (p.DateOfBirth.Date > series.SeriesDate.AddYears(-age)) age--;
        ds.AddOrUpdate(new DicomAgeString(DicomTag.PatientAge, $"{age:000}Y"));

        if(!NoPixels)
            PixelDrawer.DrawBlackBoxWithWhiteText(ds,500,500,sopInstanceUID.UID,NumberOfFrames);

        // Additional DICOM tags added for the generation of CSV files
        ds.AddOrUpdate(DicomTag.ModalitiesInStudy, series.Modality);
        ds.AddOrUpdate(DicomTag.NumberOfStudyRelatedInstances, series.Study.NumberOfStudyRelatedInstances);
        //// Series DICOM tags
        ds.AddOrUpdate(DicomTag.ImageType, series.ImageType);
        //ds.AddOrUpdate(DicomTag.ProcedureCodeSequence, "0"); //TODO
        ds.AddOrUpdate(DicomTag.PerformedProcedureStepID, "0");
        ds.AddOrUpdate(DicomTag.NumberOfSeriesRelatedInstances, series.NumberOfSeriesRelatedInstances);
        ds.AddOrUpdate(DicomTag.SeriesNumber, "0");
        //// Image DICOM tags
        ds.AddOrUpdate(DicomTag.BurnedInAnnotation, "NO");
        ds.AddOrUpdate(DicomTag.SliceLocation, "");
        ds.AddOrUpdate(DicomTag.SliceThickness, "");
        ds.AddOrUpdate(DicomTag.SpacingBetweenSlices, "");
        ds.AddOrUpdate(DicomTag.SpiralPitchFactor, "0");
        ds.AddOrUpdate(DicomTag.KVP, "0");
        ds.AddOrUpdate(DicomTag.ExposureTime, "0");
        ds.AddOrUpdate(DicomTag.Exposure, "0");
        ds.AddOrUpdate(DicomTag.XRayTubeCurrent, "0");
        ds.AddOrUpdate(DicomTag.PhotometricInterpretation, "");
        ds.AddOrUpdate(DicomTag.AcquisitionNumber, "0");
        ds.AddOrUpdate(DicomTag.AcquisitionDate, series.SeriesDate);
        ds.AddOrUpdate(new DicomTime(DicomTag.AcquisitionTime, DateTime.Today + series.SeriesTime));
        ds.AddOrUpdate(DicomTag.ImagePositionPatient, "0","0","0");
        ds.AddOrUpdate(new DicomDecimalString(DicomTag.PixelSpacing,"0.3","0.25"));
        ds.AddOrUpdate(DicomTag.FieldOfViewDimensions, "0");
        ds.AddOrUpdate(DicomTag.FieldOfViewDimensionsInFloat, "0");
        //ds.AddOrUpdate(DicomTag.TransferSyntaxUID, "1.2.840.10008.1.2"); this seems to break saving of files lets not set it
        ds.AddOrUpdate(DicomTag.LossyImageCompression, "00");
        ds.AddOrUpdate(DicomTag.LossyImageCompressionMethod, "ISO_10918_1");
        ds.AddOrUpdate(DicomTag.LossyImageCompressionRatio, "1");

        if (!Anonymise) return ds;
        _anonymiser.AnonymizeInPlace(ds);
        ds.AddOrUpdate(DicomTag.StudyInstanceUID,series.Study.StudyUID);
        ds.AddOrUpdate(DicomTag.SeriesInstanceUID, series.SeriesUID);
        return ds;
    }

    // ACH - Methods for CSV output added below

    private void InitialiseCSVOutput()
    {
        // Write the headers
        if(csvInitialized)
            return;
        csvInitialized = true;

        if (OutputDir == null) return;
        // Create/open CSV files with proper disposal via using statements
        _studyWriter = new CsvWriter(new StreamWriter(Path.Join(OutputDir.FullName, StudyCsvFilename)),CultureInfo.InvariantCulture);
        _seriesWriter = new CsvWriter(new StreamWriter(Path.Join(OutputDir.FullName, SeriesCsvFilename)),CultureInfo.InvariantCulture);
        _imageWriter = new CsvWriter(new StreamWriter(Path.Join(OutputDir.FullName, ImageCsvFilename)),CultureInfo.InvariantCulture);

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

    private void AddDicomDatasetToCSV(DicomDataset ds,CsvWriter studies,CsvWriter series,CsvWriter images)
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

    /// <summary>
    /// Closes all writers and flushes to disk
    /// </summary>
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _studyWriter?.Dispose();
        _seriesWriter?.Dispose();
        _imageWriter?.Dispose();
    }
}