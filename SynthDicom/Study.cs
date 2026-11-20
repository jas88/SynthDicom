using System.Collections;
using SynthDicom.Statistics.Distributions;

namespace SynthDicom;

/// <summary>
/// Represents a whole DICOM Study (a collection of Series objects).
/// Stores the DICOM tags that fit at the study/patient level hierarchy
/// (and are modelled by SynthDicom).
/// </summary>
public class Study : IEnumerable<Series>
{
    /// <summary>
    /// The Series objects within this Study
    /// </summary>
    public IReadOnlyList<Series> Series => _series.AsReadOnly();

    /// <summary>
    /// The DicomDataGenerator which created this Study
    /// </summary>
    public DicomDataGenerator Parent { get; }

    /// <summary>
    /// The DICOM UID of this Study
    /// </summary>
    public DicomUID StudyUID { get; }

    /// <summary>
    /// The timestamp on this Study
    /// </summary>
    public DateTime StudyDate { get; internal set; }

    /// <summary>
    /// Free-text description of this Study
    /// </summary>
    public string? StudyDescription { get; }
    /// <summary>
    /// The Accession Number for this Study, usually used to associate the study with clinical data in the RIS
    /// </summary>
    public string? AccessionNumber { get; }
    /// <summary>
    /// Starting time of the Study, empty if unknown
    /// </summary>
    public TimeSpan StudyTime { get; }

    /// <summary>
    /// Count of Instances within this Study
    /// </summary>
    public int NumberOfStudyRelatedInstances { get; }

    private readonly List<Series> _series;

    /// <summary>
    /// Constructor for a new Study on a specified Person
    /// </summary>
    /// <param name="parent">The DicomDataGenerator creating this Study</param>
    /// <param name="person">The Person representing this patient</param>
    /// <param name="modalityStats">Statistical distributions to use</param>
    /// <param name="r">Seeded PRNG to use</param>
    public Study(DicomDataGenerator parent, Person person, ModalityStats modalityStats, Random r)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(person);

        /////////////////////// Generate all the Study Values ////////////////////
        Parent = parent;
        StudyUID = UIDAllocator.GenerateStudyInstanceUID();
        StudyDate = person.GetRandomDateDuringLifetime(r).Date;

        var stats = DicomDataGeneratorStats.GetInstance();

        string imageType;
        var seriesCount = 1;
        var imageCount = 2;

        //if we know about the frequency of StudyDescription values for this modality?
        if (stats.TagValuesByModalityAndTag.TryGetValue(modalityStats.Modality, out var descriptions))
            StudyDescription = DicomDataGeneratorStats.GetRandomStudyDescription(modalityStats.Modality, r);

        AccessionNumber = DicomDataGeneratorStats.GetRandomAccessionNumber(r);
        StudyTime = DicomDataGeneratorStats.Instance.GetRandomTimeOfDay(r);

        /////////////////////  Generate all the Series (will also generate images) /////////////////////

        //have a random number of series (based on average and standard deviation for that modality)
        //but have at least 1 series! The counts come from r, so the same seed gives the same counts

        if (modalityStats.Modality == "CT")
        {
            // Set ImageType
            imageType = DicomDataGeneratorStats.Instance.GetRandomImageType(r);
            if (imageType == "ORIGINAL\\PRIMARY\\AXIAL")
            {
                seriesCount = Math.Max(1, SampleCount(modalityStats.SeriesPerStudyNormal, r));
                imageCount = Math.Max(1, SampleCount(modalityStats.ImagesPerSeriesNormal, r));
            }
        }
        else
        {
            imageType = "ORIGINAL\\PRIMARY";
            seriesCount = Math.Max(1, SampleCount(modalityStats.SeriesPerStudyNormal, r));
            imageCount = Math.Max(1, SampleCount(modalityStats.ImagesPerSeriesNormal, r));
        }

        // see if we have a better StudyDescription / SeriesDescription / BodyPart value set for
        // this modality
        DescBodyPart? part = null;

        if (stats.DescBodyPartsByModality.TryGetValue(modalityStats.Modality, out var stat))
        {
            part = DicomDataGeneratorStats.GetRandomDescBodyPart(stat, r);
            StudyDescription = part?.StudyDescription;
        }

        // Pre-size series list with series count (not image count)
        _series = new List<Series>(seriesCount);
        for (var i = 0; i < seriesCount; i++)
            _series.Add(new Series(this, person, modalityStats.Modality, imageType, imageCount, part));

        // Calculate total instance count across all series
        NumberOfStudyRelatedInstances = _series.Sum(s => s.NumberOfSeriesRelatedInstances);
    }

    /// <summary>
    /// Draws a series or image count from <paramref name="r"/>, the Random this study is generated with. A distribution
    /// with a standard deviation of 0 always gives its mean, so it takes no draw and leaves <paramref name="r"/> as it was.
    /// </summary>
    // Normal rejects a negative standard deviation, so StdDev <= 0 means it is exactly 0: a fixed count
    private static int SampleCount(Normal distribution, Random r) =>
        (int)(distribution.StdDev <= 0 ? distribution.Mean : distribution.Sample(r));

    /// <summary>
    /// Returns enumeration of <see cref="Series"/>
    /// </summary>
    /// <returns></returns>
    public IEnumerator<Series> GetEnumerator()
    {
        return _series.GetEnumerator();
    }
    /// <summary>
    /// Returns IEnumerable of <see cref="Series"/>
    /// </summary>
    /// <returns></returns>

    IEnumerator IEnumerable.GetEnumerator()
    {
        return _series.GetEnumerator();
    }
}
