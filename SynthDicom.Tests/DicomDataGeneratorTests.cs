using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CsvHelper;

namespace SynthDicom.Tests;

public class DicomDataGeneratorTests
{
    [Test]
    public void Test_CreatingOnDisk_OneFile()
    {
        var r = new Random(500);
        using var generator = new DicomDataGenerator(r, TestContext.CurrentContext.WorkDirectory) { Layout = FileSystemLayout.StudyUID, MaximumImages = 1 };


        var person = new Person(r);

        //generates a study but because of maximum images 1 we should only get 1 image being generated
        var studyUid = (string?)generator.GenerateTestDataRow(person)[0];
        Assert.That(studyUid, Is.Not.Null, "Study UID should not be null");

        //should be a directory named after the Study UID
        Assert.That(Directory.Exists(Path.Combine(TestContext.CurrentContext.WorkDirectory, studyUid)));

        //should be a single file
        var f = new FileInfo(Directory.GetFiles(Path.Combine(TestContext.CurrentContext.WorkDirectory, studyUid)).Single());
        Assert.That(f.Exists);

        var datasetCreated = DicomFile.Open(f.FullName);

        Assert.Multiple(() =>
        {
            Assert.That(datasetCreated.Dataset.GetValues<DicomUID>(DicomTag.StudyInstanceUID)[0].UID, Is.EqualTo(studyUid),
                    "UID in the dicom file generated did not match the one output into the CSV inventory file"
                );

            Assert.That(datasetCreated.Dataset.GetSingleValue<string>(DicomTag.AccessionNumber), Is.Not.Empty);
        });
    }


    [Test]
    public void ExampleUsage()
    {
        //create a test person
        var r = new Random(23);
        var person = new Person(r);

        //create a generator
        using var generator = new DicomDataGenerator(r, null, "CT");
        //create a dataset in memory
        var dataset = generator.GenerateTestDataset(person, r);

        Assert.Multiple(() =>
        {
            //values should match the patient details
            Assert.That(dataset.GetValue<string>(DicomTag.PatientID, 0), Is.EqualTo(person.CHI));
            Assert.That(dataset.GetValue<DateTime>(DicomTag.StudyDate, 0), Is.GreaterThanOrEqualTo(person.DateOfBirth));

            //should have a study description
            Assert.That(dataset.GetValue<string>(DicomTag.StudyDescription, 0), Is.Not.Null);
            //should have a study time
            Assert.That(dataset.Contains(DicomTag.StudyTime));
        });
    }

    [Test]
    public void Test_CreatingInMemory_ModalityCT()
    {
        var r = new Random(23);
        var person = new Person(r);
        using var generator = new DicomDataGenerator(r, new string(TestContext.CurrentContext.WorkDirectory), "CT") { NoPixels = true };

        //generate 100 images
        for (var i = 0; i < 100; i++)
        {
            //all should be CT because we said CT only
            var ds = generator.GenerateTestDataset(person, r);
            Assert.That(ds.GetSingleValue<string>(DicomTag.Modality), Is.EqualTo("CT"));
        }
    }

    [Test]
    public void Test_Anonymise()
    {
        var r = new Random(23);
        var person = new Person(r);

        using var generator = new DicomDataGenerator(r, new string(TestContext.CurrentContext.WorkDirectory), "CT");

        // without anonymisation (default) we get the normal patient ID
        var ds = generator.GenerateTestDataset(person, r);

        Assert.Multiple(() =>
        {
            Assert.That(ds.Contains(DicomTag.PatientID));
            Assert.That(ds.GetValue<string>(DicomTag.PatientID, 0), Is.EqualTo(person.CHI));
        });

        // with anonymisation
        generator.Anonymise = true;

        var ds2 = generator.GenerateTestDataset(person, r);

        Assert.Multiple(() =>
        {
            // we get a blank patient ID
            Assert.That(ds2.Contains(DicomTag.PatientID));
            Assert.That(ds2.GetString(DicomTag.PatientID), Is.EqualTo(string.Empty));
        });
    }
    [Test]
    public void Test_CreatingInMemory_Modality_CTAndMR()
    {
        var r = new Random(23);
        var person = new Person(r);

        using var generator = new DicomDataGenerator(r, new string(TestContext.CurrentContext.WorkDirectory), "CT", "MR");

        //generate 100 images
        for (var i = 0; i < 100; i++)
        {
            //all should be CT because we said CT only
            var ds = generator.GenerateTestDataset(person, r);
            var modality = ds.GetSingleValue<string>(DicomTag.Modality);

            Assert.That(modality is "CT" or "MR", $"Unexpected modality {modality}");
        }
    }

    [Test]
    public void TestFail_CreatingInMemory_Modality_Unknown()
    {
        var r = new Random(23);
        Assert.Throws<ArgumentException>(() => _ = new DicomDataGenerator(r, new string(TestContext.CurrentContext.WorkDirectory), "LOLZ"));

    }

    [Test]
    public void Test_CsvOption()
    {
        var r = new Random(500);

        var outputDir = new DirectoryInfo(Path.Combine(TestContext.CurrentContext.WorkDirectory, nameof(Test_CsvOption)));
        if (outputDir.Exists)
            outputDir.Delete(true);
        outputDir.Create();

        var people = new PersonCollection();
        people.GeneratePeople(100, r);

        using var generator = new DicomDataGenerator(r, outputDir.FullName, "CT");
        generator.Csv = true;
        generator.NoPixels = true;
        generator.MaximumImages = 500;

        generator.GenerateTestDataFile(people, new FileInfo(Path.Combine(outputDir.FullName, "index.csv")), 500);

        //3 csv files + index.csv (the default one
        Assert.That(outputDir.GetFiles(), Has.Length.EqualTo(4));

        foreach (var f in outputDir.GetFiles())
        {
            using var reader = new CsvReader(new StreamReader(f.FullName), CultureInfo.CurrentCulture);
            var rowcount = 0;

            //confirms that the CSV is intact (no dodgy commas, unquoted newlines etc)
            while (reader.Read())
                rowcount++;

            //should be 1 row per image + 1 for header
            if (f.Name == DicomDataGenerator.ImageCsvFilename)
                Assert.That(rowcount, Is.EqualTo(501));
        }
    }

    [Test]
    public async Task Test_GenerateStudyImagesAsync_StreamsDatasets()
    {
        // Arrange
        var r = new Random(500);
        using var generator = new DicomDataGenerator(r, null, "CT") { NoPixels = true };
        var person = new Person(r);

        // Act
        var datasetCount = 0;
        Study? capturedStudy = null;
        await foreach (var (dataset, study) in generator.GenerateStudyImagesAsync(person))
        {
            // Assert each dataset
            Assert.That(dataset, Is.Not.Null);
            Assert.That(dataset.Contains(DicomTag.StudyInstanceUID));
            Assert.That(dataset.Contains(DicomTag.SeriesInstanceUID));
            Assert.That(dataset.Contains(DicomTag.SOPInstanceUID));
            Assert.That(dataset.GetSingleValue<string>(DicomTag.PatientID), Is.EqualTo(person.CHI));

            capturedStudy = study;
            datasetCount++;
        }

        // Assert
        Assert.That(datasetCount, Is.GreaterThan(0), "Should generate at least one dataset");
        Assert.That(capturedStudy, Is.Not.Null, "Study should be captured");
    }

    [Test]
    public async Task Test_GenerateStudyImagesAsync_SupportsCancellation()
    {
        // Arrange
        var r = new Random(500);
        using var generator = new DicomDataGenerator(r, null, "CT") { NoPixels = true };
        var person = new Person(r);
        using var cts = new CancellationTokenSource();

        // Act & Assert
        var datasetCount = 0;
        var cancelled = false;

        try
        {
            await foreach (var (dataset, study) in generator.GenerateStudyImagesAsync(person, cts.Token))
            {
                datasetCount++;

                // Cancel after first dataset
                if (datasetCount == 1)
                {
                    cts.Cancel();
                }
            }
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
        }

        // Assert - cancellation should have occurred and we processed at least one dataset
        Assert.Multiple(() =>
        {
            Assert.That(cancelled, Is.True, "Cancellation should have been triggered");
            Assert.That(datasetCount, Is.GreaterThanOrEqualTo(1), "Should process at least one dataset before cancellation");
        });
    }

    [Test]
    public async Task Test_GenerateTestDatasetAsync_CreatesValidDataset()
    {
        // Arrange
        var r = new Random(23);
        var person = new Person(r);
        using var generator = new DicomDataGenerator(r, null, "CT") { NoPixels = true };

        // Act
        var dataset = await generator.GenerateTestDatasetAsync(person, r);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(dataset, Is.Not.Null);
            Assert.That(dataset.GetValue<string>(DicomTag.PatientID, 0), Is.EqualTo(person.CHI));
            Assert.That(dataset.GetValue<DateTime>(DicomTag.StudyDate, 0), Is.GreaterThanOrEqualTo(person.DateOfBirth));
            Assert.That(dataset.GetSingleValue<string>(DicomTag.Modality), Is.EqualTo("CT"));
            Assert.That(dataset.GetValue<string>(DicomTag.StudyDescription, 0), Is.Not.Null);
            Assert.That(dataset.Contains(DicomTag.StudyTime));
        });
    }

    [Test]
    public async Task Test_GenerateStudyImagesAsync_ConsistentWithSynchronousVersion()
    {
        // Arrange - use same seed for both
        var r1 = new Random(42);
        var r2 = new Random(42);
        var person1 = new Person(r1);
        var person2 = new Person(r2);

        using var generator1 = new DicomDataGenerator(r1, null, "CT") { NoPixels = true };
        using var generator2 = new DicomDataGenerator(r2, null, "CT") { NoPixels = true };

        // Act - Generate synchronously
        var syncDatasets = generator1.GenerateStudyImages(person1, out var syncStudy);

        // Act - Generate asynchronously
        var asyncDatasets = new System.Collections.Generic.List<DicomDataset>();
        await foreach (var (dataset, study) in generator2.GenerateStudyImagesAsync(person2))
        {
            asyncDatasets.Add(dataset);
        }

        // Assert - Both should generate the same number of datasets
        Assert.That(asyncDatasets.Count, Is.EqualTo(syncDatasets.Length),
            "Async and sync versions should generate the same number of datasets");
    }
}