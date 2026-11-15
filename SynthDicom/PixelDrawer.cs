using System;
using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.IO.Buffer;

namespace SynthDicom;

/// <summary>
/// Handles drawing image data directly into the dicom file
/// </summary>
internal static class PixelDrawer
{
    /// <summary>
    /// Draws a black box with white text as DICOM pixel data, supporting multi-frame generation.
    /// </summary>
    /// <param name="ds">DICOM dataset to populate with pixel data</param>
    /// <param name="width">Width of the image in pixels</param>
    /// <param name="height">Height of the image in pixels</param>
    /// <param name="msg">Text message to draw on the image</param>
    /// <param name="numberOfFrames">Number of frames to generate (1-1000). Default is 1 for single-frame images.</param>
    internal static void DrawBlackBoxWithWhiteText(DicomDataset ds, int width, int height, string msg, int numberOfFrames = 1)
    {
        if (numberOfFrames < 1 || numberOfFrames > 1000)
            throw new ArgumentOutOfRangeException(nameof(numberOfFrames), "Number of frames must be between 1 and 1000");

        var firstFrame = RenderFrame(width, height, msg);

        if (numberOfFrames == 1)
        {
            ds.Add(DicomTag.PhotometricInterpretation, PhotometricInterpretation.Rgb.Value);
            ds.Add(DicomTag.Rows, (ushort)height);
            ds.Add(DicomTag.Columns, (ushort)width);
            ds.Add(DicomTag.BitsAllocated, (ushort)8);

            var pixelData = DicomPixelData.Create(ds, true);
            pixelData.BitsStored = 8;
            pixelData.SamplesPerPixel = 3;
            pixelData.HighBit = 7;
            pixelData.PixelRepresentation = 0;
            pixelData.PlanarConfiguration = 0;
            pixelData.AddFrame(new MemoryByteBuffer(firstFrame));

            // Remove NumberOfFrames tag for single-frame images (backward compatibility)
            // FellowOakDicom may automatically add this tag, but DICOM standard says it's optional for single-frame
            ds.Remove(DicomTag.NumberOfFrames);
        }
        else
        {
            BitmapToDicom.ImportImage(ds, firstFrame, width, height, numberOfFrames);

            // Add additional frames with frame number annotations
            for (var frameNum = 2; frameNum <= numberOfFrames; frameNum++)
                BitmapToDicom.AddFrame(ds, RenderFrame(width, height, $"{msg}\n[Frame {frameNum}]"), width, height);
        }
    }

    /// <summary>
    /// Renders white text centred on a black background as an interleaved RGB24 buffer.
    /// </summary>
    private static byte[] RenderFrame(int width, int height, string text)
    {
        // New arrays are zero-filled, i.e. already black
        var rgb = new byte[width * height * 3];
        var scale = Math.Max(1, Math.Min(width, height) / 250);
        BitmapFont.DrawCentredText(rgb, width, height, text, scale);
        return rgb;
    }
}
