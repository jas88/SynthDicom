#!/usr/bin/env python3
"""
Convert DescBodyPart.cs from BucketList to optimized binary search arrays
"""

import re

# Read the file
with open('/Users/jas88/Developer/SynthDicom/BadMedicine.Dicom/DescBodyPart.cs', 'r') as f:
    content = f.read()

# Replace the static field declaration
old_declaration = '    internal static readonly ReadOnlyDictionary<string, BucketList<DescBodyPart>> d =\n        new Dictionary<string, BucketList<DescBodyPart>>'
new_declaration = '''    internal static readonly ReadOnlyDictionary<string, (int MaxWeight, (int CumulativeWeight, DescBodyPart Value)[] Items)> d = InitializeDescBodyParts();

    private static ReadOnlyDictionary<string, (int MaxWeight, (int CumulativeWeight, DescBodyPart Value)[] Items)> InitializeDescBodyParts()
    {
        var tempDict = new Dictionary<string, BucketList<DescBodyPart>>'''

content = content.replace(old_declaration, new_declaration)

# Find the end of the dictionary (before .AsReadOnly())
pattern = r'(\s+}\s+}\s+)\.AsReadOnly\(\);'
replacement = r'''\1.AsReadOnly();

        // Convert all BucketLists to optimized format
        var result = new Dictionary<string, (int MaxWeight, (int CumulativeWeight, DescBodyPart Value)[] Items)>();
        foreach (var kvp in tempDict)
        {
            var list = new List<(int CumulativeWeight, DescBodyPart Value)>();
            int cumulative = 0;
            foreach (var (item, probability) in kvp.Value)
            {
                cumulative += probability;
                list.Add((cumulative, item));
            }
            result[kvp.Key] = (cumulative, list.ToArray());
        }
        return new ReadOnlyDictionary<string, (int MaxWeight, (int CumulativeWeight, DescBodyPart Value)[] Items)>(result);
    }

    /// <summary>
    /// Binary search helper for getting random DescBodyPart - O(log n) performance
    /// </summary>
    public static DescBodyPart GetRandom((int MaxWeight, (int CumulativeWeight, DescBodyPart Value)[] Items) data, Random r)
    {
        var target = r.Next(data.MaxWeight);
        int left = 0, right = data.Items.Length - 1;

        while (left < right)
        {
            int mid = left + (right - left) / 2;
            if (data.Items[mid].CumulativeWeight <= target)
                left = mid + 1;
            else
                right = mid;
        }

        return data.Items[left].Value;
    }'''

content = re.sub(pattern, replacement, content)

# Write the modified content
with open('/Users/jas88/Developer/SynthDicom/BadMedicine.Dicom/DescBodyPart.cs', 'w') as f:
    f.write(content)

print("DescBodyPart.cs has been successfully converted to use binary search optimization!")
