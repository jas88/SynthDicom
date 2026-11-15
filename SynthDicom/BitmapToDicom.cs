using System;
using System.Globalization;
using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.IO.Buffer;

namespace SynthDicom;

/// <summary>
/// Writes interleaved RGB24 frames into multi-frame DICOM datasets
/// </summary>
internal static class BitmapToDicom
{
    /// <summary>
    /// Imports the first frame into a DICOM dataset with proper pixel data attributes.
    /// </summary>
    /// <param name="ds">DICOM dataset to populate</param>
    /// <param name="rgb">Interleaved RGB24 pixel data for the first frame</param>
    /// <param name="width">Frame width in pixels</param>
    /// <param name="height">Frame height in pixels</param>
    /// <param name="totalFrames">Total number of frames that will be in this dataset</param>
    public static void ImportImage(DicomDataset ds, byte[] rgb, int width, int height, int totalFrames = 1)
    {
        ArgumentNullException.ThrowIfNull(ds);
        ValidateFrame(rgb, width, height);

        // Set basic image attributes
        ds.AddOrUpdate(DicomTag.PhotometricInterpretation, "RGB");
        ds.AddOrUpdate(DicomTag.Rows, (ushort)height);
        ds.AddOrUpdate(DicomTag.Columns, (ushort)width);
        ds.AddOrUpdate(DicomTag.BitsAllocated, (ushort)8);
        ds.AddOrUpdate(DicomTag.BitsStored, (ushort)8);
        ds.AddOrUpdate(DicomTag.HighBit, (ushort)7);
        ds.AddOrUpdate(DicomTag.PixelRepresentation, (ushort)0);
        ds.AddOrUpdate(DicomTag.SamplesPerPixel, (ushort)3);
        ds.AddOrUpdate(DicomTag.PlanarConfiguration, (ushort)0);

        if (totalFrames > 1)
        {
            ds.AddOrUpdate(DicomTag.NumberOfFrames, totalFrames.ToString(CultureInfo.InvariantCulture));
            // Frame time in milliseconds: 1000ms / totalFrames (for fps calculation)
            var frameTime = 1000.0m / totalFrames;
            ds.AddOrUpdate(DicomTag.FrameTime, frameTime.ToString("F2", CultureInfo.InvariantCulture));
        }

        var pixelData = DicomPixelData.Create(ds, true);
        pixelData.BitsStored = 8;
        pixelData.SamplesPerPixel = 3;
        pixelData.HighBit = 7;
        pixelData.PixelRepresentation = 0;
        pixelData.PlanarConfiguration = 0;
        pixelData.AddFrame(new MemoryByteBuffer(rgb));
    }

    /// <summary>
    /// Appends an additional frame to an existing multi-frame DICOM dataset.
    /// </summary>
    /// <param name="ds">DICOM dataset to append frame to</param>
    /// <param name="rgb">Interleaved RGB24 pixel data for the additional frame</param>
    /// <param name="width">Frame width in pixels</param>
    /// <param name="height">Frame height in pixels</param>
    public static void AddFrame(DicomDataset ds, byte[] rgb, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(ds);
        ValidateFrame(rgb, width, height);

        var pixelData = DicomPixelData.Create(ds, false)
                        ?? throw new InvalidOperationException("Cannot add frame: No existing pixel data found");
        pixelData.AddFrame(new MemoryByteBuffer(rgb));
    }

    private static void ValidateFrame(byte[] rgb, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(rgb);
        if (rgb.Length != width * height * 3)
            throw new ArgumentException($"Expected {width * height * 3} bytes for a {width}x{height} RGB frame, got {rgb.Length}", nameof(rgb));
    }
}
