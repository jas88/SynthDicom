using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace SynthDicom.SourceGenerators;

/// <summary>
/// Source generator for DescBodyPart data from CSV
/// Generates the ReadOnlyDictionary of BucketList data structures
/// </summary>
[Generator]
public class DescBodyPartGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Find CSV files
        var csvFiles = context.AdditionalTextsProvider
            .Where(file => file.Path.EndsWith("DicomDataGeneratorDescBodyPart.csv"))
            .Select((file, ct) => file.GetText(ct))
            .Where(text => text != null)
            .Collect();

        // Generate source
        context.RegisterSourceOutput(csvFiles, GenerateDescBodyPart!);
    }

    private void GenerateDescBodyPart(SourceProductionContext context, ImmutableArray<SourceText?> csvTexts)
    {
        if (csvTexts.Length == 0 || csvTexts[0] == null)
            return;

        var csvText = csvTexts[0]!;
        var rows = CsvHelper.ParseCsv(csvText);

        if (rows.Count == 0)
            return;

        // Group by Modality
        var grouped = rows
            .Where(r => r.ContainsKey("Modality") && r.ContainsKey("series_count"))
            .GroupBy(r => r["Modality"])
            .OrderBy(g => g.Key);

        var sb = new StringBuilder();
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Collections.ObjectModel;");
        sb.AppendLine();
        sb.AppendLine("namespace SynthDicom;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Generated from DicomDataGeneratorDescBodyPart.csv");
        sb.AppendLine("/// Optimized with binary search for O(log n) performance");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public readonly partial record struct DescBodyPart");
        sb.AppendLine("{");
        sb.AppendLine("    internal static readonly ReadOnlyDictionary<string, (int MaxWeight, (int CumulativeWeight, DescBodyPart Value)[] Items)> d =");
        sb.AppendLine("        new Dictionary<string, (int MaxWeight, (int CumulativeWeight, DescBodyPart Value)[] Items)>");
        sb.AppendLine("        {");

        bool firstModality = true;
        foreach (var modalityGroup in grouped)
        {
            if (!firstModality)
                sb.AppendLine(",");
            firstModality = false;

            // Calculate cumulative weights for this modality
            var entries = new List<(int cumulativeWeight, string desc)>();
            int cumulative = 0;
            foreach (var row in modalityGroup)
            {
                var studyDesc = row.GetValueOrDefault("StudyDescription", "");
                var bodyPart = row.GetValueOrDefault("BodyPartExamined", "");
                var seriesDesc = row.GetValueOrDefault("SeriesDescription", "");
                var count = int.Parse(row.GetValueOrDefault("series_count", "0"));

                cumulative += count;

                // Use raw string literals with proper escaping
                var studyDescStr = string.IsNullOrEmpty(studyDesc) ? "\"\"" : CsvHelper.EscapeForRawString(studyDesc);
                var bodyPartStr = string.IsNullOrEmpty(bodyPart) ? "\"\"" : $"\"{bodyPart}\"";
                var seriesDescStr = string.IsNullOrEmpty(seriesDesc) ? "\"\"" : CsvHelper.EscapeForRawString(seriesDesc);

                entries.Add((cumulative, $"({cumulative}, new DescBodyPart({studyDescStr},{bodyPartStr},{seriesDescStr}))"));
            }

            sb.AppendLine("            {");
            sb.AppendLine($"                \"{modalityGroup.Key}\",");
            sb.AppendLine($"                ({cumulative}, new (int, DescBodyPart)[]");
            sb.AppendLine("                {");

            bool firstEntry = true;
            foreach (var (_, desc) in entries)
            {
                if (!firstEntry)
                    sb.AppendLine(",");
                firstEntry = false;
                sb.Append($"                    {desc}");
            }

            sb.AppendLine();
            sb.AppendLine("                })");
            sb.Append("            }");
        }

        sb.AppendLine();
        sb.AppendLine("        }.AsReadOnly();");
        sb.AppendLine("}");

        context.AddSource("DescBodyPart.Generated.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
    }
}
