using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace SynthDicom.SourceGenerators;

/// <summary>
/// Helper class for parsing CSV files in source generators
/// </summary>
internal static class CsvHelper
{
    /// <summary>
    /// Parse CSV content into rows and columns
    /// </summary>
    public static List<Dictionary<string, string>> ParseCsv(SourceText text)
    {
        var lines = text.Lines
            .Select(line => line.ToString().Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();

        if (lines.Count == 0)
            return new List<Dictionary<string, string>>();

        // Parse header
        var headers = ParseCsvLine(lines[0]);
        var result = new List<Dictionary<string, string>>();

        // Parse data rows
        for (int i = 1; i < lines.Count; i++)
        {
            var values = ParseCsvLine(lines[i]);
            if (values.Count == 0) continue;

            var row = new Dictionary<string, string>();
            for (int j = 0; j < Math.Min(headers.Count, values.Count); j++)
            {
                row[headers[j]] = values[j];
            }
            result.Add(row);
        }

        return result;
    }

    private static List<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        result.Add(current.ToString());
        return result;
    }

    /// <summary>
    /// Escape string for C# raw string literal
    /// </summary>
    public static string EscapeForRawString(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        // Count consecutive quotes to determine raw string delimiter
        int maxQuotes = 0;
        int currentQuotes = 0;
        foreach (char c in input)
        {
            if (c == '"')
            {
                currentQuotes++;
                maxQuotes = Math.Max(maxQuotes, currentQuotes);
            }
            else
            {
                currentQuotes = 0;
            }
        }

        // Use enough quotes to escape the content
        string delimiter = new string('"', Math.Max(3, maxQuotes + 1));
        return delimiter + input + delimiter;
    }

    /// <summary>
    /// Convert string to safe C# identifier
    /// </summary>
    public static string ToSafeIdentifier(string input)
    {
        if (string.IsNullOrEmpty(input))
            return "_";

        var sb = new StringBuilder();
        foreach (char c in input)
        {
            if (char.IsLetterOrDigit(c))
                sb.Append(c);
            else
                sb.Append('_');
        }

        var result = sb.ToString();
        if (char.IsDigit(result[0]))
            result = "_" + result;

        return result;
    }

    /// <summary>
    /// Get value from dictionary with default fallback (.NET Standard 2.0 compatible)
    /// </summary>
    public static string GetValueOrDefault(this Dictionary<string, string> dict, string key, string defaultValue = "")
    {
        return dict.TryGetValue(key, out var value) ? value : defaultValue;
    }
}
