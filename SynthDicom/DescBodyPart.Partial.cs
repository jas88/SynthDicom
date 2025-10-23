using System.Collections.ObjectModel;

namespace SynthDicom;

/// <summary>
/// <para>
/// Describes a commonly seen occurrence of a given triplet of values
/// StudyDescription, BodyPartExamined and SeriesDescription in scottish medical imaging data.
/// </para>
/// <para>
/// This class (and its corresponding DicomDataGeneratorDescBodyPart.csv) allow
/// synthetic data in the description tags to make sense when comparing to the other
/// 2 tags listed.  It prevents for example a study being generated called CT Head with
/// a Series Description of 'Foot Scan'
/// </para>
/// </summary>
public readonly partial record struct DescBodyPart
{
    /// <summary>
    /// A known value of StudyDescription which is consistent with BodyPartExamined and SeriesDescription
    /// </summary>
    public string? StudyDescription { get; init; }

    /// <summary>
    /// A known value of BodyPartExamined which is consistent with StudyDescription and SeriesDescription
    /// </summary>
    public string? BodyPartExamined { get; init; }

    /// <summary>
    /// A known value of SeriesDescription which is consistent with BodyPartExamined and StudyDescription
    /// </summary>
    public string? SeriesDescription { get; init; }

    /// <summary>
    /// Creates a new DescBodyPart instance
    /// </summary>
    /// <param name="studyDescription">The study description value</param>
    /// <param name="bodyPartExamined">The body part examined value</param>
    /// <param name="seriesDescription">The series description value</param>
    public DescBodyPart(string? studyDescription, string? bodyPartExamined, string? seriesDescription)
    {
        StudyDescription = studyDescription;
        BodyPartExamined = bodyPartExamined;
        SeriesDescription = seriesDescription;
    }
}
