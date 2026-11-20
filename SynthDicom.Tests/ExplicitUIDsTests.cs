using System.IO;
using BadDicom.Configuration;

namespace SynthDicom.Tests;

/// <summary>
/// Tests for BadDicom's <see cref="ExplicitUIDs"/>, which queues the UIDs listed in files for <see cref="UIDAllocator"/>
/// </summary>
internal sealed class ExplicitUIDsTests
{
    [TearDown]
    public void ClearUIDQueues()
    {
        // UIDAllocator's queues are static, so empty them for the tests that generate their own UIDs
        UIDAllocator.StudyUIDs.Clear();
        UIDAllocator.SeriesUIDs.Clear();
        UIDAllocator.SOPUIDs.Clear();
    }

    [Test]
    public void Load_SkipsBlankLines()
    {
        var path = Path.Join(TestContext.CurrentContext.WorkDirectory, $"{nameof(Load_SkipsBlankLines)}.csv");
        File.WriteAllText(path, "1.2.3.4.1\n\n1.2.3.4.2\n   \n\t\n1.2.3.4.3\n");
        string[] nonBlankLines = ["1.2.3.4.1", "1.2.3.4.2", "1.2.3.4.3"];

        new ExplicitUIDs { StudyInstanceUIDs = path }.Load();

        Assert.That(UIDAllocator.StudyUIDs, Is.EqualTo(nonBlankLines));
    }
}
