namespace SynthDicom.Tests;

internal sealed class StudyTests
{
    [Test]
    public void Test_CreatingNewStudy_HasSomeImages()
    {
        var r = new Random(100);

        using var generator = new DicomDataGenerator(r, null) { NoPixels = true };

        var p = new Person(r);

        Study study = new(generator, p, new ModalityStats("MR", 2, 0, 50, 0, r), r);

        Assert.That(study.Series, Has.Count.EqualTo(2));
        Assert.That(study.Series[0].Datasets, Has.Count.EqualTo(50));


        foreach (var ds in study.Series[0])
        {
            Assert.Multiple(() =>
            {
                Assert.That(ds.GetValues<string>(DicomTag.Modality)[0], Is.EqualTo("MR"));
                Assert.That(ds.GetSingleValue<DateTime>(DicomTag.StudyTime).TimeOfDay, Is.EqualTo(study.StudyTime));
            });
        }
    }

    [Test]
    public void Test_UsingExplicitUIDs()
    {
        UIDAllocator.StudyUIDs.Enqueue("999");
        UIDAllocator.SeriesUIDs.Enqueue("888");
        UIDAllocator.SOPUIDs.Enqueue("777");

        var r = new Random(100);

        using var generator = new DicomDataGenerator(r, null) { NoPixels = true };

        var p = new Person(r);

        Study study = new(generator, p, new ModalityStats("MR", 2, 0, 50, 0, r), r);

        Assert.Multiple(() =>
        {
            Assert.That(study.StudyUID.UID, Is.EqualTo("999"));
            Assert.That(study.Series[0].SeriesUID.UID, Is.EqualTo("888"));
        });

        var image1 = study.Series[0].Datasets[0];
        Assert.Multiple(() =>
        {
            Assert.That(image1.GetSingleValue<DicomUID>(DicomTag.StudyInstanceUID).UID, Is.EqualTo("999"));
            Assert.That(image1.GetSingleValue<DicomUID>(DicomTag.SeriesInstanceUID).UID, Is.EqualTo("888"));
            Assert.That(image1.GetSingleValue<DicomUID>(DicomTag.SOPInstanceUID).UID, Is.EqualTo("777"));
        });
    }

    /// <summary>
    /// Pins the StudyDescription that a seeded Random picks. Each modality's descriptions are weighted by cumulative
    /// frequency in list order, so these change if that order does. The seeds are ones where the earlier row order of
    /// DicomDataGeneratorTags.csv, or sorting tied frequencies alphabetically (CR 21, XA 96), picks another description.
    /// </summary>
    [TestCase("CR", 0, "XR Pelvis")]
    [TestCase("CR", 21, "XR Neck soft tissue")]
    [TestCase("MR", 0, "NINEWELLS^Lumbar Spine")]
    [TestCase("SR", 0, "XR Abdomen")]
    [TestCase("US", 0, "US Kidney Both")]
    [TestCase("XA", 96, "Cateterismo y Angiocardiografía Izquierdos")]
    public void Test_SeededStudy_PicksPinnedStudyDescription(string modality, int seed, string expected)
    {
        var r = new Random(seed);

        using var generator = new DicomDataGenerator(r, null) { NoPixels = true };

        var p = new Person(r);

        Study study = new(generator, p, new ModalityStats(modality, 1, 0, 1, 0, r), r);

        Assert.That(study.StudyDescription, Is.EqualTo(expected));
    }

    [Test]
    public void Test_EveryImage_CarriesTheStudyInstanceCount()
    {
        var r = new Random(100);

        using var generator = new DicomDataGenerator(r, null, "CT") { NoPixels = true };

        var p = new Person(r);

        Study study = new(generator, p, new ModalityStats("MR", 2, 0, 50, 0, r), r);
        var images = generator.GenerateStudyImages(p, out var ctStudy);

        Assert.Multiple(() =>
        {
            Assert.That(study.NumberOfStudyRelatedInstances, Is.EqualTo(100));
            Assert.That(study.Series.SelectMany(static s => s.Datasets).Select(InstanceCount), Is.All.EqualTo(100));
            Assert.That(ctStudy.NumberOfStudyRelatedInstances, Is.EqualTo(images.Length));
            Assert.That(images.Select(InstanceCount), Is.All.EqualTo(images.Length));
        });
    }

    private static int InstanceCount(DicomDataset ds) => ds.GetSingleValue<int>(DicomTag.NumberOfStudyRelatedInstances);
}
