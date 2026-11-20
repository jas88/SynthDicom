using System.IO;

namespace SynthDicom.Tests;

internal sealed class MaximumImagesTests
{
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
}
