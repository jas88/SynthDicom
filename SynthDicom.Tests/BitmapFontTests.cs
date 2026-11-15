namespace SynthDicom.Tests;

internal sealed class BitmapFontTests
{
    // font8x8_basic glyph for 'A'; bit 0 of each row byte is the leftmost pixel
    private static readonly byte[] GlyphA = [0x0C, 0x1E, 0x33, 0x33, 0x3F, 0x33, 0x33, 0x00];

    [Test]
    public void EmptyText_LeavesBufferBlack()
    {
        var rgb = Render("", 32, 32, 1);

        Assert.That(rgb, Is.All.EqualTo(0));
    }

    [Test]
    public void SingleGlyph_MatchesFontBitmap()
    {
        var rgb = Render("A", 8, 8, 1);

        for (var y = 0; y < 8; y++)
        {
            for (var x = 0; x < 8; x++)
            {
                var expected = (GlyphA[y] & (1 << x)) != 0 ? (byte)0xFF : (byte)0x00;
                var i = (y * 8 + x) * 3;
                Assert.That(new[] { rgb[i], rgb[i + 1], rgb[i + 2] }, Is.All.EqualTo(expected), $"Pixel ({x},{y})");
            }
        }
    }

    [Test]
    public void Scale_ExpandsEachFontPixelToBlock()
    {
        var reference = Render("A", 8, 8, 1);
        var scaled = Render("A", 16, 16, 2);

        for (var y = 0; y < 16; y++)
        {
            for (var x = 0; x < 16; x++)
                Assert.That(scaled[(y * 16 + x) * 3], Is.EqualTo(reference[(y / 2 * 8 + x / 2) * 3]), $"Pixel ({x},{y})");
        }
    }

    [Test]
    public void SingleGlyph_IsCentred()
    {
        var reference = Render("A", 8, 8, 1);
        var rgb = Render("A", 24, 24, 1);

        // One 8x8 glyph centred in 24x24 starts at (8,8); everything else stays black
        Assert.That(Block(rgb, 24, 8, 8, 8), Is.EqualTo(Block(reference, 8, 0, 0, 8)));
        Assert.That(LitPixelCount(rgb), Is.EqualTo(LitPixelCount(reference)));
    }

    [Test]
    public void Newline_StartsNewLineBelow()
    {
        var a = Block(Render("A", 8, 8, 1), 8, 0, 0, 8);
        var b = Block(Render("B", 8, 8, 1), 8, 0, 0, 8);

        // Two lines of height 8 with 2px spacing = 18px block, centred in 26px => top at 4
        var rgb = Render("A\nB", 8, 26, 1);

        Assert.Multiple(() =>
        {
            Assert.That(Block(rgb, 8, 0, 4, 8), Is.EqualTo(a));
            Assert.That(Block(rgb, 8, 0, 14, 8), Is.EqualTo(b));
        });
    }

    [Test]
    public void LongText_WrapsToImageWidth()
    {
        // 16px wide at scale 1 fits two characters per line
        var wrapped = Render("ABCD", 16, 40, 1);
        var explicitLines = Render("AB\nCD", 16, 40, 1);

        Assert.Multiple(() =>
        {
            Assert.That(LitPixelCount(wrapped), Is.GreaterThan(0));
            Assert.That(wrapped, Is.EqualTo(explicitLines));
        });
    }

    [Test]
    public void UnsupportedCharacter_RendersAsQuestionMark()
    {
        var unsupported = Render("é", 8, 8, 1);
        var question = Render("?", 8, 8, 1);

        Assert.Multiple(() =>
        {
            Assert.That(LitPixelCount(question), Is.GreaterThan(0));
            Assert.That(unsupported, Is.EqualTo(question));
        });
    }

    [Test]
    public void TextLargerThanImage_IsClippedToBuffer()
    {
        const int width = 10;
        const int height = 10;
        const byte sentinel = 0xAB;

        // Buffer extends past the image so writes outside the image bounds would be detected
        var buffer = new byte[width * height * 3 + 64];
        buffer.AsSpan(width * height * 3).Fill(sentinel);

        Assert.DoesNotThrow(() => BitmapFont.DrawCentredText(buffer, width, height, "WWWWWWWW\nWWWWWWWW\nWWWWWWWW", 4));

        Assert.Multiple(() =>
        {
            Assert.That(buffer.Skip(width * height * 3), Is.All.EqualTo(sentinel));
            Assert.That(LitPixelCount(buffer.AsSpan(0, width * height * 3).ToArray()), Is.GreaterThan(0));
        });
    }

    [Test]
    public void InvalidArguments_Throw()
    {
        Assert.Multiple(() =>
        {
            Assert.Throws<ArgumentException>(() => BitmapFont.DrawCentredText(new byte[10], 8, 8, "A", 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => BitmapFont.DrawCentredText(new byte[192], 8, 8, "A", 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => BitmapFont.DrawCentredText(new byte[192], 0, 8, "A", 1));
            Assert.Throws<ArgumentNullException>(() => BitmapFont.DrawCentredText(new byte[192], 8, 8, null!, 1));
        });
    }

    [Test]
    public void GeneratedDataset_PixelDataIsBlackAndWhiteText()
    {
        var r = new Random(500);
        using var generator = new DicomDataGenerator(r, null, "CT") { NoPixels = false };

        var ds = generator.GenerateTestDataset(new Person(r), r);
        var frame = FellowOakDicom.Imaging.DicomPixelData.Create(ds).GetFrame(0).Data;

        Assert.Multiple(() =>
        {
            Assert.That(frame, Has.Length.EqualTo(500 * 500 * 3));
            Assert.That(frame, Is.All.EqualTo(0x00).Or.EqualTo(0xFF));
            Assert.That(frame, Has.Some.EqualTo(0xFF), "Text should be drawn");
        });
    }

    private static byte[] Render(string text, int width, int height, int scale)
    {
        var rgb = new byte[width * height * 3];
        BitmapFont.DrawCentredText(rgb, width, height, text, scale);
        return rgb;
    }

    /// <summary>Extracts the red channel of a square block, row by row.</summary>
    private static byte[] Block(byte[] rgb, int width, int x0, int y0, int size)
    {
        var block = new byte[size * size];
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
                block[y * size + x] = rgb[((y0 + y) * width + x0 + x) * 3];
        }
        return block;
    }

    private static int LitPixelCount(byte[] rgb)
    {
        var count = 0;
        for (var i = 0; i < rgb.Length; i += 3)
            if (rgb[i] != 0)
                count++;
        return count;
    }
}
