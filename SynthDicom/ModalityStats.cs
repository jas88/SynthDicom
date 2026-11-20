using SynthDicom.Statistics.Distributions;

namespace SynthDicom;

/// <summary>
/// A set of statistical distribution parameters for a specific Modality
/// </summary>
/// <param name="modalityCode">Which Modality this relates to, for example 'MR'</param>
/// <param name="averageSeriesPerStudy">The mean number of Series in a Study of this Modality</param>
/// <param name="standardDeviationSeriesPerStudy">The standard deviation of the number of Series in a Study of this Modality</param>
/// <param name="averageImagesPerSeries">The mean number of Images in a Series of this Modality</param>
/// <param name="standardDeviationImagesPerSeries">The standard deviation of the number of Images in a Series of this Modality</param>
/// <param name="random">Kept for compatibility. <see cref="Study"/> draws the series and image counts from the Random it is
/// given, so that the same seed gives the same counts, not from this one</param>
public readonly record struct ModalityStats(
    string modalityCode,
    double averageSeriesPerStudy,
    double standardDeviationSeriesPerStudy,
    double averageImagesPerSeries,
    double standardDeviationImagesPerSeries,
    Random random)
{
    /// <summary>
    /// Which Modality this relates to, for example 'MR'
    /// </summary>
    public string Modality { get; } = modalityCode;

    /// <summary>
    /// The mean number of Series in a Study of this Modality
    /// </summary>
    public double SeriesPerStudyAverage => SeriesPerStudyNormal.Mean;

    /// <summary>
    /// The standard deviation of the number of Series in a Study of this Modality
    /// </summary>
    public double SeriesPerStudyStandardDeviation => SeriesPerStudyNormal.StdDev;

    /// <summary>
    /// The parameterised Normal distribution used for the number of series per study
    /// </summary>
    internal Normal SeriesPerStudyNormal { get; } = new Normal(averageSeriesPerStudy, standardDeviationSeriesPerStudy, random);

    /// <summary>
    /// The mean number of Images in a Series of this Modality
    /// </summary>
    public double ImagesPerSeriesAverage => ImagesPerSeriesNormal.Mean;

    /// <summary>
    /// The standard deviation of the number of Images in a Series of this Modality
    /// </summary>
    public double ImagesPerSeriesStandardDeviation => ImagesPerSeriesNormal.StdDev;

    /// <summary>
    /// The Normal distribution of the number of Images per Series for this Modality
    /// </summary>
    internal Normal ImagesPerSeriesNormal { get; } = new Normal(averageImagesPerSeries, standardDeviationImagesPerSeries, random);

    /// <summary>
    /// The Random passed to the constructor; <see cref="Study"/> does not draw from it
    /// </summary>
    private Random Rng { get; } = random;
}
