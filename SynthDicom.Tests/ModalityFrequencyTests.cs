namespace SynthDicom.Tests;

/// <summary>
/// Pins the modality table that <see cref="DicomDataGenerator"/> picks from. Modalities are weighted by cumulative
/// frequency in table order, so the order decides which modality a seeded Random picks. Each seeded pick below
/// changes if the table is sorted alphabetically; the first four also change in the earlier row order of
/// DicomDataGeneratorModalities.csv, and the SC+KO picks change if SC and KO (both frequency 1) swap places.
/// </summary>
internal sealed class ModalityFrequencyTests
{
    private static readonly (string Modality, int Frequency, double SeriesMean, double SeriesSd, double ImagesMean, double ImagesSd)[] ExpectedTable =
    [
        ("CT", 182846, 2, 0, 100, 0),
        ("MR", 53161, 1, 0, 100, 10),
        ("US", 3805, 1, 0, 100, 10),
        ("PT", 2639, 1, 0, 100, 10),
        ("CR", 1949, 1, 0, 100, 10),
        ("XA", 1511, 1, 0, 100, 10),
        ("RF", 1206, 1, 0, 100, 10),
        ("DX", 485, 1, 0, 100, 10),
        ("OT", 391, 1, 0, 100, 10),
        ("MG", 149, 1, 0, 100, 10),
        ("IO", 92, 1, 0, 100, 10),
        ("NM", 58, 1, 0, 100, 10),
        ("PR", 23, 1, 0, 100, 10),
        ("PX", 8, 1, 0, 100, 10),
        ("SC", 1, 1, 0, 100, 10),
        ("KO", 1, 1, 0, 100, 10)
    ];

    [Test]
    public void Test_ModalityTable_HasPinnedOrderFrequenciesAndParameters()
    {
        var items = DicomDataGeneratorStats.Instance.ModalityFrequency.Items;
        var actual = items.Select((item, i) => (
            item.Value.Modality,
            item.CumulativeWeight - (i == 0 ? 0 : items[i - 1].CumulativeWeight),
            item.Value.SeriesPerStudyAverage,
            item.Value.SeriesPerStudyStandardDeviation,
            item.Value.ImagesPerSeriesAverage,
            item.Value.ImagesPerSeriesStandardDeviation));

        Assert.That(actual, Is.EqualTo(ExpectedTable));
    }

    [TestCase(3, "", "RF")]
    [TestCase(13, "", "XA")]
    [TestCase(1, "CR,XA,RF,DX", "RF")]
    [TestCase(3, "MR,US,CR", "CR")]
    [TestCase(0, "SC,KO", "KO")]
    [TestCase(2, "SC,KO", "SC")]
    public void Test_SeededGenerator_PicksPinnedModality(int seed, string modalities, string expected)
    {
        var r = new Random(seed);
        using var generator = new DicomDataGenerator(r, null, modalities.Split(',', StringSplitOptions.RemoveEmptyEntries)) { NoPixels = true };
        var person = new Person(r);

        generator.GenerateStudyImages(person, out var study);

        Assert.That(study.Series[0].Modality, Is.EqualTo(expected));
    }

    /// <summary>
    /// Every modality's series count has a standard deviation of 0, so it is exactly the mean. Image counts are only
    /// fixed where their standard deviation is 0 (CT), so other modalities leave them unpinned (null).
    /// </summary>
    [TestCase("CT", 2, 100)]
    [TestCase("MR", 1, null)]
    [TestCase("US", 1, null)]
    [TestCase("PT", 1, null)]
    [TestCase("CR", 1, null)]
    [TestCase("XA", 1, null)]
    [TestCase("RF", 1, null)]
    [TestCase("DX", 1, null)]
    [TestCase("OT", 1, null)]
    [TestCase("MG", 1, null)]
    [TestCase("IO", 1, null)]
    [TestCase("NM", 1, null)]
    [TestCase("PR", 1, null)]
    [TestCase("PX", 1, null)]
    [TestCase("SC", 1, null)]
    [TestCase("KO", 1, null)]
    public void Test_SeededStudy_HasPinnedSeriesAndImageCounts(string modality, int series, int? imagesPerSeries)
    {
        var r = new Random(7);
        using var generator = new DicomDataGenerator(r, null, modality) { NoPixels = true };
        var person = new Person(r);

        generator.GenerateStudyImages(person, out var study);

        Assert.That(study.Series, Has.Count.EqualTo(series));
        if (imagesPerSeries != null)
            Assert.That(study.Series.Select(static s => s.Datasets.Count), Is.All.EqualTo(imagesPerSeries));
    }
}
