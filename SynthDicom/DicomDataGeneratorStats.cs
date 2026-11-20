using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using FellowOakDicom;
using SynthEHR;
using SynthEHR.Datasets;

namespace SynthDicom;

internal sealed class DicomDataGeneratorStats
{
    public static readonly DicomDataGeneratorStats Instance = new();


    /// <summary>
    /// Dictionary of Modality=>StudyDescription by frequency, from DicomDataGeneratorTags.csv
    /// Optimized with binary search for O(log n) performance
    /// </summary>
    public readonly Dictionary<string, (int MaxWeight, (int CumulativeWeight, string Value)[] Items)> TagValuesByModalityAndTag = InitializeTagValuesByModalityAndTag();

    public readonly (int MaxWeight, (int CumulativeWeight, ModalityStats Value)[] Items) ModalityFrequency = InitializeModalityFrequency(new Random());

    public readonly ReadOnlyDictionary<string, (int MaxWeight, (int CumulativeWeight, DescBodyPart Value)[] Items)> DescBodyPartsByModality = DescBodyPart.d;

    /// <summary>
    /// Distribution of time of day (in hours only) that tests were taken - Optimized with binary search
    /// select DATEPART(HOUR,StudyTime),work.dbo.get_aggregate_value(count(*)) from CT_GoDARTS_StudyTable group by DATEPART(HOUR,StudyTime)
    /// </summary>
    private readonly (int MaxWeight, (int CumulativeWeight, int Value)[] Items) _hourOfDay = InitializeHourOfDay();

    /// <summary>
    /// CT Image Type - Optimized with binary search
    /// </summary>
    private readonly (int MaxWeight, (int CumulativeWeight, string Value)[] Items) _imageType = InitializeImageType();


    /// <summary>
    /// Generates a random time of day with a frequency that matches the times when the most images are captured (e.g., more images are
    /// taken at 1pm than at 8pm).
    /// </summary>
    /// <param name="r">Random number generator</param>
    /// <returns>TimeSpan representing the time of day</returns>
    /// <exception cref="Exception">Thrown if the calculated time span has a non-zero day component</exception>
    public TimeSpan GetRandomTimeOfDay(Random r)
    {
        var hour = GetRandomValue(_hourOfDay, r);
        var ts = new TimeSpan(0, hour, r.Next(60), r.Next(60), 0);

        // JS 2024-05-21: This is a leftover from an old bug where probabilities and hours were confused,
        // leading to scans timed at 44 o'clock. I'm not sure if this is still necessary.
        //ts = ts.Subtract(new TimeSpan(ts.Days,0,0,0));

        if (ts.Days != 0)
            throw new InvalidOperationException("Generated time of day has invalid day component");

        return ts;
    }

    /// <summary>
    /// Gets a random DICOM ImageType value weighted by frequency in clinical data.
    /// </summary>
    /// <param name="r">Random number generator</param>
    /// <returns>ImageType string (e.g., "ORIGINAL\PRIMARY\AXIAL")</returns>
    public string GetRandomImageType(Random r) => GetRandomValue(_imageType, r);

    /// <summary>
    /// Generates a random accession number in the format T{region}{site}{hospital}H{number} (e.g., T101H12451352).
    /// </summary>
    /// <param name="r">Random number generator</param>
    /// <returns>Synthetic accession number string</returns>
    public static string GetRandomAccessionNumber(Random r) => $"T{r.Next(4)}{r.Next(2)}{r.Next(5)}H{r.Next(9999999)}";

    private static (int MaxWeight, (int CumulativeWeight, ModalityStats Value)[] Items) InitializeModalityFrequency(Random r)
    {
        var bucketList = new BucketList<ModalityStats>
        {
            { 182846, new ModalityStats("CT", 2, 0, 100, 0, r) },
            { 53161, new ModalityStats("MR", 1, 0, 100, 10, r) },
            { 3805, new ModalityStats("US", 1, 0, 100, 10, r) },
            { 2639, new ModalityStats("PT", 1, 0, 100, 10, r) },
            { 1949, new ModalityStats("CR", 1, 0, 100, 10, r) },
            { 1511, new ModalityStats("XA", 1, 0, 100, 10, r) },
            { 1206, new ModalityStats("RF", 1, 0, 100, 10, r) },
            { 485, new ModalityStats("DX", 1, 0, 100, 10, r) },
            { 391, new ModalityStats("OT", 1, 0, 100, 10, r) },
            { 149, new ModalityStats("MG", 1, 0, 100, 10, r) },
            { 92, new ModalityStats("IO", 1, 0, 100, 10, r) },
            { 58, new ModalityStats("NM", 1, 0, 100, 10, r) },
            { 23, new ModalityStats("PR", 1, 0, 100, 10, r) },
            { 8, new ModalityStats("PX", 1, 0, 100, 10, r) },
            { 1, new ModalityStats("SC", 1, 0, 100, 10, r) },
            { 1, new ModalityStats("KO", 1, 0, 100, 10, r) }
        };
        return ConvertToOptimized(bucketList);
    }

    private static (int MaxWeight, (int CumulativeWeight, int Value)[] Items) InitializeHourOfDay()
    {
        var bucketList = new BucketList<int>
        {
            { 57, 9 },
            { 55, 13 },
            { 54, 14 },
            { 51, 12 },
            { 44, 16 },
            { 42, 17 },
            { 42, 15 },
            { 41, 11 },
            { 36, 10 },
            { 33, 18 },
            { 15, 8 },
            { 8, 22 },
            { 7, 20 },
            { 5, 21 },
            { 1, 6 },
            { 1, 5 },
            { 1, 4 },
            { 1, 19 },
            { 1, 1 }
        };
        return ConvertToOptimized(bucketList);
    }

    private static (int MaxWeight, (int CumulativeWeight, string Value)[] Items) InitializeImageType()
    {
        var bucketList = new BucketList<string>
        {
            { 96, @"ORIGINAL\PRIMARY\AXIAL" },
            { 3, @"DERIVED\SECONDARY" },
            { 1, @"ORIGINAL\PRIMARY\LOCALIZER" }
        };
        return ConvertToOptimized(bucketList);
    }

    /// <summary>
    /// Returns the existing stats for tag popularity, modality frequencies etc.
    /// </summary>
    /// <returns></returns>
    public static DicomDataGeneratorStats GetInstance()
    {
        return Instance;
    }

    /// <summary>
    /// Converts a BucketList to optimized binary search arrays
    /// </summary>
    private static (int MaxWeight, (int CumulativeWeight, T Value)[] Items) ConvertToOptimized<T>(BucketList<T> bucketList)
    {
        var list = new List<(int CumulativeWeight, T Value)>();
        int cumulative = 0;

        foreach (var (item, probability) in bucketList)
        {
            cumulative += probability;
            list.Add((cumulative, item));
        }

        return (cumulative, list.ToArray());
    }

    /// <summary>
    /// Generic binary search implementation for getting random weighted values - O(log n) performance
    /// </summary>
    private static T GetRandomValue<T>((int MaxWeight, (int CumulativeWeight, T Value)[] Items) data, Random r)
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
    }

    /// <summary>
    /// Builds <see cref="TagValuesByModalityAndTag"/> from the StudyDescription rows of DicomDataGeneratorTags.csv, which the
    /// source generator compiles into <see cref="GeneratedTags.TagValues"/>. Each modality keeps the CSV's row order, and the
    /// cumulative weights follow that order, so reordering the CSV changes which description a seeded <see cref="Random"/> picks.
    /// </summary>
    private static Dictionary<string, (int MaxWeight, (int CumulativeWeight, string Value)[] Items)> InitializeTagValuesByModalityAndTag() =>
        GeneratedTags.TagValues
            .Where(static entry => entry.Key.Tag == "StudyDescription")
            .ToDictionary(static entry => entry.Key.Modality, static entry => ConvertToOptimized(entry.Value));

    /// <summary>
    /// Binary search implementation for getting random weighted values - O(log n) performance
    /// </summary>
    public static string GetRandomStudyDescription(string modality, Random r)
    {
        var instance = GetInstance();
        if (!instance.TagValuesByModalityAndTag.TryGetValue(modality, out var data))
            throw new ArgumentException($"Unknown modality: {modality}");

        return GetRandomValue(data, r);
    }

    /// <summary>
    /// Gets a random modality based on frequency distribution - O(log n) performance
    /// </summary>
    public ModalityStats GetRandomModality(Random r) => GetRandomValue(ModalityFrequency, r);

    /// <summary>
    /// Gets a random modality from a subset of indices - O(n) performance
    /// </summary>
    public ModalityStats GetRandomModality(int[] indices, Random r)
    {
        if (indices.Length == 1)
            return ModalityFrequency.Items[indices[0]].Value;

        // Calculate total weight for the subset
        int totalWeight = 0;
        foreach (var idx in indices)
        {
            int prevWeight = idx > 0 ? ModalityFrequency.Items[idx - 1].CumulativeWeight : 0;
            int currentWeight = ModalityFrequency.Items[idx].CumulativeWeight;
            totalWeight += currentWeight - prevWeight;
        }

        // Pick a random value within the subset
        var target = r.Next(totalWeight);
        int cumulative = 0;

        foreach (var idx in indices)
        {
            int prevWeight = idx > 0 ? ModalityFrequency.Items[idx - 1].CumulativeWeight : 0;
            int weight = ModalityFrequency.Items[idx].CumulativeWeight - prevWeight;
            cumulative += weight;

            if (target < cumulative)
                return ModalityFrequency.Items[idx].Value;
        }

        throw new InvalidOperationException("Could not GetRandomModality");
    }

    /// <summary>
    /// Gets a random DescBodyPart using binary search - O(log n) performance
    /// </summary>
    public static DescBodyPart GetRandomDescBodyPart((int MaxWeight, (int CumulativeWeight, DescBodyPart Value)[] Items) data, Random r)
    {
        return GetRandomValue(data, r);
    }
}
