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
        Assert.That(Directory.Exists(Path.Join(TestContext.CurrentContext.WorkDirectory, studyUid)));

        //should be a single file
        var f = new FileInfo(Directory.GetFiles(Path.Join(TestContext.CurrentContext.WorkDirectory, studyUid)).Single());
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

        var outputDir = new DirectoryInfo(Path.Join(TestContext.CurrentContext.WorkDirectory, nameof(Test_CsvOption)));
        if (outputDir.Exists)
            outputDir.Delete(true);
        outputDir.Create();

        var people = new PersonCollection();
        people.GeneratePeople(100, r);

        using var generator = new DicomDataGenerator(r, outputDir.FullName, "CT");
        generator.Csv = true;
        generator.NoPixels = true;
        generator.MaximumImages = 500;

        generator.GenerateTestDataFile(people, new FileInfo(Path.Join(outputDir.FullName, "index.csv")), 500);

        //3 csv files + index.csv (the default one
        Assert.That(outputDir.GetFiles(), Has.Length.EqualTo(4));

        foreach (var f in outputDir.GetFiles())
        {
            using var reader = new CsvReader(new StreamReader(f.FullName), CultureInfo.InvariantCulture);
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
        var asyncDatasets = new List<DicomDataset>();
        await foreach (var (dataset, study) in generator2.GenerateStudyImagesAsync(person2))
        {
            asyncDatasets.Add(dataset);
        }

        // Assert - Both should generate the same number of datasets
        Assert.That(asyncDatasets.Count, Is.EqualTo(syncDatasets.Length),
            "Async and sync versions should generate the same number of datasets");
    }

    [Test]
    public void Test_MultiFrame_GeneratesTwoFrames()
    {
        var r = new Random(500);
        var person = new Person(r);
        using var generator = new DicomDataGenerator(r, null, "CT") { NoPixels = false, NumberOfFrames = 2 };

        var ds = generator.GenerateTestDataset(person, r);

        Assert.Multiple(() =>
        {
            Assert.That(ds.Contains(DicomTag.NumberOfFrames), "Should have NumberOfFrames tag");
            Assert.That(ds.GetSingleValue<int>(DicomTag.NumberOfFrames), Is.EqualTo(2), "Should have 2 frames");
            Assert.That(ds.Contains(DicomTag.FrameTime), "Should have FrameTime tag");
        });
    }

    [Test]
    public void Test_MultiFrame_GeneratesTenFrames()
    {
        var r = new Random(500);
        var person = new Person(r);
        using var generator = new DicomDataGenerator(r, null, "CT") { NoPixels = false, NumberOfFrames = 10 };

        var ds = generator.GenerateTestDataset(person, r);

        Assert.Multiple(() =>
        {
            Assert.That(ds.Contains(DicomTag.NumberOfFrames), "Should have NumberOfFrames tag");
            Assert.That(ds.GetSingleValue<int>(DicomTag.NumberOfFrames), Is.EqualTo(10), "Should have 10 frames");
            Assert.That(ds.GetSingleValue<decimal>(DicomTag.FrameTime), Is.EqualTo(100.0m).Within(0.1m), "Frame time should be 100ms (10fps)");
        });
    }

    [Test]
    public void Test_MultiFrame_SingleFrameHasNoFrameTag()
    {
        var r = new Random(500);
        var person = new Person(r);
        using var generator = new DicomDataGenerator(r, null, "CT") { NoPixels = false, NumberOfFrames = 1 };

        var ds = generator.GenerateTestDataset(person, r);

        Assert.That(ds.Contains(DicomTag.NumberOfFrames), Is.False, "Single frame should not have NumberOfFrames tag");
    }

    [Test]
    public void Test_MultiFrame_WithNoPixels()
    {
        var r = new Random(500);
        var person = new Person(r);
        using var generator = new DicomDataGenerator(r, null, "CT") { NoPixels = true, NumberOfFrames = 5 };

        var ds = generator.GenerateTestDataset(person, r);

        Assert.That(ds.Contains(DicomTag.PixelData), Is.False, "NoPixels should bypass all frame generation");
    }

    [Test]
    public void Test_MultiFrame_GeneratesFiveFrames()
    {
        var r = new Random(500);
        var person = new Person(r);
        using var generator = new DicomDataGenerator(r, null, "CT") { NoPixels = false, NumberOfFrames = 5 };

        var ds = generator.GenerateTestDataset(person, r);

        Assert.Multiple(() =>
        {
            Assert.That(ds.Contains(DicomTag.NumberOfFrames), "Should have NumberOfFrames tag");
            Assert.That(ds.GetSingleValue<int>(DicomTag.NumberOfFrames), Is.EqualTo(5), "Should have 5 frames");
            Assert.That(ds.Contains(DicomTag.FrameTime), "Should have FrameTime tag for multi-frame");
        });
    }

    [Test]
    public void Test_MultiFrame_VerifyPixelDataContainsMultipleFrames()
    {
        var r = new Random(500);
        var person = new Person(r);
        using var generator = new DicomDataGenerator(r, null, "CT") { NoPixels = false, NumberOfFrames = 3 };

        var ds = generator.GenerateTestDataset(person, r);

        Assert.Multiple(() =>
        {
            Assert.That(ds.Contains(DicomTag.PixelData), "Should have pixel data");
            Assert.That(ds.GetSingleValue<int>(DicomTag.NumberOfFrames), Is.EqualTo(3));

            // Verify pixel data buffer is sized correctly for multiple frames
            var pixelData = ds.GetDicomItem<DicomElement>(DicomTag.PixelData);
            Assert.That(pixelData, Is.Not.Null, "Pixel data element should exist");

            var rows = ds.GetSingleValue<int>(DicomTag.Rows);
            var cols = ds.GetSingleValue<int>(DicomTag.Columns);
            var samplesPerPixel = ds.GetSingleValue<int>(DicomTag.SamplesPerPixel);
            var expectedSize = rows * cols * samplesPerPixel * 3; // 3 frames

            Assert.That(pixelData.Buffer.Size, Is.EqualTo(expectedSize),
                "Pixel data size should match rows * cols * samplesPerPixel * numberOfFrames");
        });
    }

    [Test]
    public void Test_MultiFrame_BackwardCompatibility_DefaultIsSingleFrame()
    {
        var r = new Random(500);
        var person = new Person(r);
        // Don't set NumberOfFrames - should default to 1
        using var generator = new DicomDataGenerator(r, null, "CT") { NoPixels = false };

        var ds = generator.GenerateTestDataset(person, r);

        Assert.Multiple(() =>
        {
            Assert.That(ds.Contains(DicomTag.NumberOfFrames), Is.False,
                "Single frame (default) should not have NumberOfFrames tag");
            Assert.That(ds.Contains(DicomTag.PixelData), "Should still have pixel data");
        });
    }

    [Test]
    public void Test_MultiFrame_FrameTimeCalculation()
    {
        var r = new Random(500);
        var person = new Person(r);
        using var generator = new DicomDataGenerator(r, null, "CT") { NoPixels = false, NumberOfFrames = 30 };

        var ds = generator.GenerateTestDataset(person, r);

        Assert.Multiple(() =>
        {
            Assert.That(ds.Contains(DicomTag.FrameTime), "Should have FrameTime tag");
            var frameTime = ds.GetSingleValue<decimal>(DicomTag.FrameTime);
            // 30 fps = 33.33ms per frame
            Assert.That(frameTime, Is.EqualTo(33.33m).Within(0.01m),
                "Frame time for 30 frames should be ~33.33ms (30fps)");
        });
    }

    [Test]
    public void Test_MultiFrame_WithDifferentModalities()
    {
        var r = new Random(500);
        var person = new Person(r);

        // Test MR modality with multi-frame
        using var mrGenerator = new DicomDataGenerator(r, null, "MR") { NoPixels = false, NumberOfFrames = 4 };
        var mrDs = mrGenerator.GenerateTestDataset(person, r);

        // Test CT modality with multi-frame
        using var ctGenerator = new DicomDataGenerator(r, null, "CT") { NoPixels = false, NumberOfFrames = 4 };
        var ctDs = ctGenerator.GenerateTestDataset(person, r);

        Assert.Multiple(() =>
        {
            Assert.That(mrDs.GetSingleValue<string>(DicomTag.Modality), Is.EqualTo("MR"));
            Assert.That(mrDs.GetSingleValue<int>(DicomTag.NumberOfFrames), Is.EqualTo(4));

            Assert.That(ctDs.GetSingleValue<string>(DicomTag.Modality), Is.EqualTo("CT"));
            Assert.That(ctDs.GetSingleValue<int>(DicomTag.NumberOfFrames), Is.EqualTo(4));
        });
    }

    [Test]
    public async Task Test_MultiFrame_AsyncGeneration()
    {
        var r = new Random(500);
        var person = new Person(r);
        using var generator = new DicomDataGenerator(r, null, "CT") { NoPixels = false, NumberOfFrames = 3 };

        var ds = await generator.GenerateTestDatasetAsync(person, r);

        Assert.Multiple(() =>
        {
            Assert.That(ds.Contains(DicomTag.NumberOfFrames), "Async generated dataset should have NumberOfFrames tag");
            Assert.That(ds.GetSingleValue<int>(DicomTag.NumberOfFrames), Is.EqualTo(3));
        });
    }

    [Test]
    public async Task Test_MultiFrame_StudyImagesAsync()
    {
        var r = new Random(500);
        var person = new Person(r);
        using var generator = new DicomDataGenerator(r, null, "CT") { NoPixels = false, NumberOfFrames = 2 };

        var frameCountsFound = new HashSet<int>();

        await foreach (var (dataset, study) in generator.GenerateStudyImagesAsync(person))
        {
            if (dataset.Contains(DicomTag.NumberOfFrames))
            {
                frameCountsFound.Add(dataset.GetSingleValue<int>(DicomTag.NumberOfFrames));
            }
        }

        Assert.That(frameCountsFound, Contains.Item(2),
            "All images in study should have 2 frames when NumberOfFrames is set to 2");
    }

    [Test]
    public void Test_MultiFrame_OnDiskGeneration()
    {
        var r = new Random(500);
        var person = new Person(r);
        using var generator = new DicomDataGenerator(r, TestContext.CurrentContext.WorkDirectory)
        {
            Layout = FileSystemLayout.StudyUID,
            MaximumImages = 1,
            NumberOfFrames = 5,
            NoPixels = false
        };

        var studyUid = (string?)generator.GenerateTestDataRow(person)[0];
        Assert.That(studyUid, Is.Not.Null, "Study UID should not be null");

        var f = new FileInfo(Directory.GetFiles(Path.Join(TestContext.CurrentContext.WorkDirectory, studyUid)).Single());
        var datasetCreated = DicomFile.Open(f.FullName);

        Assert.Multiple(() =>
        {
            Assert.That(datasetCreated.Dataset.Contains(DicomTag.NumberOfFrames),
                "Saved file should contain NumberOfFrames tag");
            Assert.That(datasetCreated.Dataset.GetSingleValue<int>(DicomTag.NumberOfFrames), Is.EqualTo(5),
                "Saved file should have correct number of frames");
            Assert.That(datasetCreated.Dataset.Contains(DicomTag.PixelData),
                "Saved file should contain pixel data");
        });
    }

    [Test]
    public void Test_MultiFrame_GenerateStudyImages()
    {
        var r = new Random(500);
        var person = new Person(r);
        using var generator = new DicomDataGenerator(r, null, "CT") { NoPixels = false, NumberOfFrames = 7 };

        var datasets = generator.GenerateStudyImages(person, out var study);

        Assert.That(datasets.Length, Is.GreaterThan(0), "Should generate at least one dataset");

        // All images in the study should have the same frame count
        foreach (var ds in datasets)
        {
            Assert.Multiple(() =>
            {
                Assert.That(ds.Contains(DicomTag.NumberOfFrames), "Each dataset should have NumberOfFrames tag");
                Assert.That(ds.GetSingleValue<int>(DicomTag.NumberOfFrames), Is.EqualTo(7),
                    "Each dataset should have 7 frames");
            });
        }
    }

    [Test]
    public void Test_MultiFrame_LargeFrameCount()
    {
        var r = new Random(500);
        var person = new Person(r);
        using var generator = new DicomDataGenerator(r, null, "CT") { NoPixels = false, NumberOfFrames = 100 };

        var ds = generator.GenerateTestDataset(person, r);

        Assert.Multiple(() =>
        {
            Assert.That(ds.GetSingleValue<int>(DicomTag.NumberOfFrames), Is.EqualTo(100));
            Assert.That(ds.Contains(DicomTag.PixelData), "Should have pixel data even with 100 frames");

            // Verify frame time for 100 frames
            var frameTime = ds.GetSingleValue<decimal>(DicomTag.FrameTime);
            Assert.That(frameTime, Is.EqualTo(10.0m).Within(0.1m),
                "Frame time for 100 frames should be 10ms (100fps)");
        });
    }

    [Test]
    public void Test_MultiFrame_WithAnonymization()
    {
        var r = new Random(500);
        var person = new Person(r);
        using var generator = new DicomDataGenerator(r, null, "CT")
        {
            NoPixels = false,
            NumberOfFrames = 3,
            Anonymise = true
        };

        var ds = generator.GenerateTestDataset(person, r);

        Assert.Multiple(() =>
        {
            // Multi-frame tags should still be present
            Assert.That(ds.Contains(DicomTag.NumberOfFrames), "NumberOfFrames should be present even with anonymization");
            Assert.That(ds.GetSingleValue<int>(DicomTag.NumberOfFrames), Is.EqualTo(3));

            // Patient data should be anonymized
            Assert.That(ds.GetString(DicomTag.PatientID), Is.EqualTo(string.Empty),
                "Patient ID should be anonymized");
        });
    }
}