using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.IO.Buffer;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace SynthDicom;

/// <summary>
/// Provides functionality to convert bitmap image sequences to multi-frame DICOM datasets
/// </summary>
internal static class BitmapToDicom
{
    /// <summary>
    /// Converts a sequence of bitmap frames to a multi-frame DICOM dataset with embedded pixel data.
    /// The method handles grayscale conversion and sets appropriate DICOM tags for multi-frame images.
    /// </summary>
    /// <param name="frames">Array of RGBA32 images representing individual frames to be encoded</param>
    /// <param name="dataset">The DICOM dataset to populate with pixel data and multi-frame tags</param>
    /// <exception cref="ArgumentNullException">Thrown when frames array or dataset is null</exception>
    /// <exception cref="ArgumentException">Thrown when frames array is empty or contains null frames</exception>
    /// <remarks>
    /// This method:
    /// - Converts each frame from RGBA32 to 8-bit grayscale
    /// - Sets PhotometricInterpretation to MONOCHROME2
    /// - Configures BitsAllocated, BitsStored, and HighBit to 8-bit values
    /// - Sets NumberOfFrames tag to match the frame count
    /// - Encodes all frames sequentially into the PixelData element
    /// </remarks>
    public static void ConvertToMultiFrame(Image<Rgba32>[] frames, DicomDataset dataset)
    {
        ArgumentNullException.ThrowIfNull(frames);
        ArgumentNullException.ThrowIfNull(dataset);

        if (frames.Length == 0)
            throw new ArgumentException("Frames array cannot be empty", nameof(frames));

        // Validate all frames are non-null
        for (int i = 0; i < frames.Length; i++)
        {
            if (frames[i] == null)
                throw new ArgumentException($"Frame at index {i} is null", nameof(frames));
        }

        var firstFrame = frames[0];
        int width = firstFrame.Width;
        int height = firstFrame.Height;

        // Set DICOM image descriptor tags
        dataset.AddOrUpdate(DicomTag.PhotometricInterpretation, PhotometricInterpretation.Monochrome2.Value);
        dataset.AddOrUpdate(DicomTag.Rows, (ushort)height);
        dataset.AddOrUpdate(DicomTag.Columns, (ushort)width);
        dataset.AddOrUpdate(DicomTag.BitsAllocated, (ushort)8);
        dataset.AddOrUpdate(DicomTag.BitsStored, (ushort)8);
        dataset.AddOrUpdate(DicomTag.HighBit, (ushort)7);
        dataset.AddOrUpdate(DicomTag.PixelRepresentation, (ushort)0);
        dataset.AddOrUpdate(DicomTag.SamplesPerPixel, (ushort)1);
        dataset.AddOrUpdate(DicomTag.NumberOfFrames, frames.Length.ToString());

        // Create pixel data element
        var pixelData = DicomPixelData.Create(dataset, true);

        // Convert and add each frame
        foreach (var frame in frames)
        {
            // Verify frame dimensions match
            if (frame.Width != width || frame.Height != height)
                throw new ArgumentException(
                    $"All frames must have the same dimensions. Expected {width}x{height}, got {frame.Width}x{frame.Height}",
                    nameof(frames));

            // Convert RGBA32 to grayscale (8-bit)
            var grayscaleBuffer = ConvertToGrayscale(frame);

            // Add frame to pixel data
            pixelData.AddFrame(new MemoryByteBuffer(grayscaleBuffer));
        }
    }

    /// <summary>
    /// Converts an RGBA32 image to 8-bit grayscale using luminosity method
    /// </summary>
    /// <param name="image">Source RGBA32 image</param>
    /// <returns>Byte array containing grayscale pixel data</returns>
    private static byte[] ConvertToGrayscale(Image<Rgba32> image)
    {
        int width = image.Width;
        int height = image.Height;
        var grayscale = new byte[width * height];

        image.ProcessPixelRows(accessor =>
        {
            int index = 0;
            for (int y = 0; y < height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (int x = 0; x < width; x++)
                {
                    var pixel = row[x];
                    // Convert to grayscale using luminosity method (ITU-R BT.709)
                    // Y = 0.2126*R + 0.7152*G + 0.0722*B
                    grayscale[index++] = (byte)(
                        0.2126 * pixel.R +
                        0.7152 * pixel.G +
                        0.0722 * pixel.B
                    );
                }
            }
        });

        return grayscale;
    }

    /// <summary>
    /// Imports the first frame into a DICOM dataset with proper pixel data attributes.
    /// </summary>
    /// <param name="ds">DICOM dataset to populate</param>
    /// <param name="bitmap">Bitmap image for the first frame</param>
    /// <param name="totalFrames">Total number of frames that will be in this dataset</param>
    public static void ImportImage(DicomDataset ds, Image<Rgb24> bitmap, int totalFrames = 1)
    {
        ArgumentNullException.ThrowIfNull(ds);
        ArgumentNullException.ThrowIfNull(bitmap);

        // Set basic image attributes
        ds.AddOrUpdate(DicomTag.PhotometricInterpretation, "RGB");
        ds.AddOrUpdate(DicomTag.Rows, (ushort)bitmap.Height);
        ds.AddOrUpdate(DicomTag.Columns, (ushort)bitmap.Width);
        ds.AddOrUpdate(DicomTag.BitsAllocated, (ushort)8);
        ds.AddOrUpdate(DicomTag.BitsStored, (ushort)8);
        ds.AddOrUpdate(DicomTag.HighBit, (ushort)7);
        ds.AddOrUpdate(DicomTag.PixelRepresentation, (ushort)0);
        ds.AddOrUpdate(DicomTag.SamplesPerPixel, (ushort)3);
        ds.AddOrUpdate(DicomTag.PlanarConfiguration, (ushort)0);

        if (totalFrames > 1)
        {
            ds.AddOrUpdate(DicomTag.NumberOfFrames, totalFrames.ToString());
            // Frame time in milliseconds: 1000ms / totalFrames (for fps calculation)
            var frameTime = 1000.0m / totalFrames;
            ds.AddOrUpdate(DicomTag.FrameTime, frameTime.ToString("F2"));
        }

        // For multi-frame, we need to use DicomPixelData to create a CompositeByteBuffer
        // Convert first frame to pixel data
        var pixelData = DicomPixelData.Create(ds, true);
        pixelData.BitsStored = 8;
        pixelData.SamplesPerPixel = 3;
        pixelData.HighBit = 7;
        pixelData.PixelRepresentation = 0;
        pixelData.PlanarConfiguration = 0;
        
        var frameData = ConvertBitmapToPixelData(bitmap);
        pixelData.AddFrame(frameData);
    }

    /// <summary>
    /// Appends an additional frame to an existing multi-frame DICOM dataset.
    /// Appends an additional frame to an existing multi-frame DICOM dataset.
    /// </summary>
    /// <param name="ds">DICOM dataset to append frame to</param>
    /// <param name="bitmap">Bitmap image for the additional frame</param>
    public static void AddFrame(DicomDataset ds, Image<Rgb24> bitmap)
    {
        ArgumentNullException.ThrowIfNull(ds);
        ArgumentNullException.ThrowIfNull(bitmap);

        // Get existing pixel data
        var pixelData = DicomPixelData.Create(ds, false);
        if (pixelData == null)
            throw new InvalidOperationException("Cannot add frame: No existing pixel data found");

        // Convert new frame
        var newFrameData = ConvertBitmapToPixelData(bitmap);

        // Add the new frame
        pixelData.AddFrame(newFrameData);
    }

    /// <summary>
    /// Converts an ImageSharp bitmap to DICOM pixel data byte buffer.
    /// </summary>
    /// <summary>
    /// Converts an ImageSharp bitmap to DICOM pixel data byte buffer.
    /// </summary>
    private static IByteBuffer ConvertBitmapToPixelData(Image<Rgb24> bitmap)
    {
        var pixels = new byte[bitmap.Width * bitmap.Height * 3];
        int index = 0;

        bitmap.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < bitmap.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (int x = 0; x < bitmap.Width; x++)
                {
                    var pixel = row[x];
                    pixels[index++] = pixel.R;
                    pixels[index++] = pixel.G;
                    pixels[index++] = pixel.B;
                }
            }
        });

        return new MemoryByteBuffer(pixels);
    }
}
