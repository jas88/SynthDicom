using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace SynthDicom.SourceGenerators;

/// <summary>
/// Source generator for modality statistics from CSV
/// Generates modality frequency data and series/image statistics
/// </summary>
[Generator]
public class ModalitiesGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var csvFiles = context.AdditionalTextsProvider
            .Where(file => file.Path.EndsWith("DicomDataGeneratorModalities.csv"))
            .Select((file, ct) => file.GetText(ct))
            .Where(text => text != null)
            .Collect();

        context.RegisterSourceOutput(csvFiles, GenerateModalities!);
    }

    private void GenerateModalities(SourceProductionContext context, ImmutableArray<SourceText?> csvTexts)
    {
        if (csvTexts.Length == 0 || csvTexts[0] == null)
            return;

        var csvText = csvTexts[0]!;
        var rows = CsvHelper.ParseCsv(csvText);

        if (rows.Count == 0)
            return;

        var validRows = rows
            .Where(r => r.ContainsKey("Modality") && r.ContainsKey("Frequency"))
            .Where(r => !string.IsNullOrWhiteSpace(r["Modality"]))
            .OrderBy(r => r["Modality"]);

        var sb = new StringBuilder();
        sb.AppendLine("namespace SynthDicom;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Generated from DicomDataGeneratorModalities.csv");
        sb.AppendLine("/// Contains modality frequency and statistical data");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("internal static partial class GeneratedModalities");
        sb.AppendLine("{");

        // Generate BucketList of modalities by frequency
        sb.AppendLine("    internal static readonly BucketList<string> ModalityFrequencies = new()");
        sb.AppendLine("    {");

        bool firstEntry = true;
        foreach (var row in validRows)
        {
            if (!firstEntry)
                sb.AppendLine(",");
            firstEntry = false;

            var modality = row["Modality"];
            var frequency = row.GetValueOrDefault("Frequency", "1");

            sb.Append($"        {{{frequency}, \"{modality}\"}}");
        }

        sb.AppendLine();
        sb.AppendLine("    };");
        sb.AppendLine();

        // Generate statistics dictionary with simpler data structure
        sb.AppendLine("    internal static readonly System.Collections.Generic.Dictionary<string, GeneratedModalityStatData> Statistics = new()");
        sb.AppendLine("    {");

        firstEntry = true;
        foreach (var row in validRows)
        {
            if (!firstEntry)
                sb.AppendLine(",");
            firstEntry = false;

            var modality = row["Modality"];
            var avgSeries = row.GetValueOrDefault("AverageSeriesPerStudy", "1");
            var stdDevSeries = row.GetValueOrDefault("StandardDeviationSeriesPerStudy", "0");
            var avgImages = row.GetValueOrDefault("AverageImagesPerSeries", "100");
            var stdDevImages = row.GetValueOrDefault("StandardDeviationImagesPerSeries", "10");

            sb.Append($"        {{\"{modality}\", new GeneratedModalityStatData({avgSeries}, {stdDevSeries}, {avgImages}, {stdDevImages})}}");
        }

        sb.AppendLine();
        sb.AppendLine("    };");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Simple statistics data structure for generated modality data");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("internal readonly record struct GeneratedModalityStatData(");
        sb.AppendLine("    double AverageSeriesPerStudy,");
        sb.AppendLine("    double StandardDeviationSeriesPerStudy,");
        sb.AppendLine("    double AverageImagesPerSeries,");
        sb.AppendLine("    double StandardDeviationImagesPerSeries);");

        context.AddSource("GeneratedModalities.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
    }
}
