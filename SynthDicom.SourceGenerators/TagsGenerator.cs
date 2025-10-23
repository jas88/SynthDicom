using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace SynthDicom.SourceGenerators;

/// <summary>
/// Source generator for DICOM tags data from CSV
/// Generates tag value collections grouped by modality
/// </summary>
[Generator]
public class TagsGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var csvFiles = context.AdditionalTextsProvider
            .Where(file => file.Path.EndsWith("DicomDataGeneratorTags.csv"))
            .Select((file, ct) => file.GetText(ct))
            .Where(text => text != null)
            .Collect();

        context.RegisterSourceOutput(csvFiles, GenerateTags!);
    }

    private void GenerateTags(SourceProductionContext context, ImmutableArray<SourceText?> csvTexts)
    {
        if (csvTexts.Length == 0 || csvTexts[0] == null)
            return;

        var csvText = csvTexts[0]!;
        var rows = CsvHelper.ParseCsv(csvText);

        if (rows.Count == 0)
            return;

        // Group by Modality and Tag
        var grouped = rows
            .Where(r => r.ContainsKey("Modality") && r.ContainsKey("Tag") && r.ContainsKey("Value") && r.ContainsKey("Frequency"))
            .GroupBy(r => (Modality: r["Modality"], Tag: r["Tag"]))
            .OrderBy(g => g.Key.Modality)
            .ThenBy(g => g.Key.Tag);

        var sb = new StringBuilder();
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Collections.ObjectModel;");
        sb.AppendLine();
        sb.AppendLine("namespace SynthDicom;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Generated from DicomDataGeneratorTags.csv");
        sb.AppendLine("/// Contains DICOM tag values grouped by modality and tag name");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("internal static partial class GeneratedTags");
        sb.AppendLine("{");
        sb.AppendLine("    internal static readonly ReadOnlyDictionary<(string Modality, string Tag), BucketList<string>> TagValues =");
        sb.AppendLine("        new Dictionary<(string, string), BucketList<string>>");
        sb.AppendLine("        {");

        bool firstGroup = true;
        foreach (var group in grouped)
        {
            if (!firstGroup)
                sb.AppendLine(",");
            firstGroup = false;

            var modality = group.Key.Modality;
            var tag = group.Key.Tag;

            sb.AppendLine("            {");
            sb.AppendLine($"                (\"{modality}\", \"{tag}\"),");
            sb.AppendLine("                new BucketList<string>");
            sb.AppendLine("                {");

            bool firstEntry = true;
            foreach (var row in group)
            {
                if (!firstEntry)
                    sb.AppendLine(",");
                firstEntry = false;

                var value = row.GetValueOrDefault("Value", "");
                var frequency = row.GetValueOrDefault("Frequency", "1");

                var valueStr = string.IsNullOrEmpty(value) ? "\"\"" : CsvHelper.EscapeForRawString(value);
                sb.Append($"                    {{{frequency}, {valueStr}}}");
            }

            sb.AppendLine();
            sb.AppendLine("                }");
            sb.Append("            }");
        }

        sb.AppendLine();
        sb.AppendLine("        }.AsReadOnly();");
        sb.AppendLine("}");

        context.AddSource("GeneratedTags.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
    }
}
