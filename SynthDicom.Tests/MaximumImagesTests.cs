using System.IO;

namespace SynthDicom.Tests;

internal sealed class MaximumImagesTests
{
    // (images written, NumberOfSeriesRelatedInstances recorded) for each series, largest first
    private static readonly (int Images, int Recorded)[] ExpectedSeries = [(100, 100), (50, 50)];

    /// <summary>
    /// When <see cref="DicomDataGenerator.MaximumImages"/> cuts a study short, the images written must record how many
    /// of them were written, not how many images the whole study had.
    /// </summary>
    [Test]
    public void Test_StudyCutShort_RecordsTheImagesWritten()
    {
        var outputDir = new DirectoryInfo(Path.Join(TestContext.CurrentContext.WorkDirectory, nameof(Test_StudyCutShort_RecordsTheImagesWritten)));
        if (outputDir.Exists)
            outputDir.Delete(true);

        var r = new Random(500);
        using var generator = new DicomDataGenerator(r, outputDir.FullName, "MR") { Layout = FileSystemLayout.StudyUID, MaximumImages = 5, NoPixels = true };

        var studyUid = (string?)generator.GenerateTestDataRow(new Person(r))[0];

        var counts = Directory.GetFiles(Path.Join(outputDir.FullName, studyUid))
            .Select(static f => DicomFile.Open(f).Dataset.GetSingleValue<int>(DicomTag.NumberOfStudyRelatedInstances))
            .ToArray();

        Assert.That(counts, Has.Length.EqualTo(5).And.All.EqualTo(5));
    }

    /// <summary>
    /// When <see cref="DicomDataGenerator.MaximumImages"/> cuts a series short, the images written of that series must
    /// record how many of them were written; a series written whole keeps its count. This CT study has two series of 100
    /// images, cut at 150.
    /// </summary>
    [Test]
    public void Test_SeriesCutShort_RecordsTheImagesWritten()
    {
        var outputDir = new DirectoryInfo(Path.Join(TestContext.CurrentContext.WorkDirectory, nameof(Test_SeriesCutShort_RecordsTheImagesWritten)));
        if (outputDir.Exists)
            outputDir.Delete(true);

        var r = new Random(1);
        using var generator = new DicomDataGenerator(r, outputDir.FullName, "CT") { Layout = FileSystemLayout.StudyUID, MaximumImages = 150, NoPixels = true };

        var studyUid = (string?)generator.GenerateTestDataRow(new Person(r))[0];

        var series = Directory.GetFiles(Path.Join(outputDir.FullName, studyUid))
            .Select(static f => DicomFile.Open(f).Dataset)
            .GroupBy(static ds => ds.GetSingleValue<string>(DicomTag.SeriesInstanceUID))
            .Select(static g => (Images: g.Count(), Recorded: g.Select(static ds => ds.GetSingleValue<int>(DicomTag.NumberOfSeriesRelatedInstances)).Distinct().Single()))
            .OrderByDescending(static s => s.Images)
            .ToArray();

        Assert.That(series, Is.EqualTo(ExpectedSeries));
    }
}
