namespace SynthDicom.Tests;

/// <summary>
/// The same seed must give the same images, image counts included. UIDs are never seeded, so they are left out.
/// Seed 1 gives non-CT studies for each mixed list, whose image counts used to come from an unseeded Random.
/// </summary>
internal sealed class SeededRunTests
{
    [TestCase(1, "CT")]
    [TestCase(1, "MR")]
    [TestCase(1, "CT,MR,US")]
    [TestCase(1, "")]
    public void Test_SameSeed_GivesIdenticalDatasets(int seed, string modalities)
    {
        var first = Generate(seed, modalities);
        var second = Generate(seed, modalities);

        Assert.That(second, Is.EqualTo(first));
    }

    /// <summary>
    /// Makes five studies from one seeded Random, as BadDicom does, and describes every image they hold.
    /// </summary>
    private static List<string> Generate(int seed, string modalities)
    {
        var r = new Random(seed);
        using var generator = new DicomDataGenerator(r, null, modalities.Split(',', StringSplitOptions.RemoveEmptyEntries)) { NoPixels = true };
        var people = Enumerable.Range(0, 5).Select(_ => new Person(r)).ToArray();

        return [.. people.SelectMany(person => generator.GenerateStudyImages(person, out _)).Select(Describe)];
    }

    private static string Describe(DicomDataset ds) =>
        string.Join('\n', ds
            .Where(static item => item.Tag != DicomTag.StudyInstanceUID && item.Tag != DicomTag.SeriesInstanceUID && item.Tag != DicomTag.SOPInstanceUID)
            .Select(item => $"{item.Tag} {(ds.TryGetString(item.Tag, out var value) ? value : "<binary>")}"));
}
