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
/// <param name="random">The Random pseudo-random number generator to be used</param>
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
    /// The Random pseudo-random number generator to be used
    /// </summary>
    private Random Rng { get; } = random;
}