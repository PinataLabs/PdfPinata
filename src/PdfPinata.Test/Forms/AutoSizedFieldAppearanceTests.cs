using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using ImageMagick;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.AcroForms;
using PdfPinata.Test.Helpers;
using TUnit.Core;
using static PdfPinata.Test.Helpers.PageInk;

namespace PdfPinata.Test.Forms;

/// <summary>
///   A font size of 0 in <c>/DA</c> asks for the text to be sized to the box (ISO 32000-1
///   12.7.3.3). It used to be drawn at a fixed 10 points whatever the box, so a value was a
///   sliver in a tall field and ran past the edge of a narrow one. Issue #354.
/// </summary>
/// <remarks>
///   What a reader shows is counted in pixels. Only the floor and the sizes that must not move are
///   read from the <c>Tf</c> the appearance stream sets, where the number itself is the point.
/// </remarks>
[Rasterizing]
public sealed class AutoSizedFieldAppearanceTests : IDisposable
{
    private const string OutDir = "Out/AutoSizedFieldAppearances";

    private const string AutoSize = "/Helv 0 Tf 0 g";

    private readonly Rasterizations _rasterized = new(OutDir);

    public void Dispose() => _rasterized.Dispose();

    private static T OnAPage<T>(T field, XRect box, string defaultAppearance = AutoSize)
        where T : PdfAcroField
    {
        var document = field.Owner;
        var page = document.AddPage();
        document.GetOrCreateAcroForm().Fields.Add(field);
        field.DefaultAppearance = defaultAppearance;

        var gfx = XGraphics.FromPdfPage(page);
        field.AddWidget(page, new PdfRectangle(gfx.Transformer.WorldToDefaultPage(box)));
        gfx.Dispose();
        return field;
    }

    private static PdfTextField AText(XRect box, string defaultAppearance = AutoSize) =>
        OnAPage(new PdfTextField(new PdfDocument()) { Name = "text" }, box, defaultAppearance);

    [Test, GoldenImage]
    public void ASingleLineFillsMostOfTheHeightOfATallBox()
    {
        var box = new XRect(60, 60, 300, 60);
        var field = AText(box);
        field.Text = "HHHH";

        var ink = InkBounds(_rasterized.FirstPageOf(field.Owner, "tall-line"), box);

        ink.Height.Should().BeGreaterThan(box.Height / 2,
            "an auto-sized line is as large as the box lets it be, not 10 points");
        ink.Bottom.Should().BeLessThanOrEqualTo(box.Bottom, "and stays inside the box");
    }

    [Test, GoldenImage]
    public void ALineTooLongForTheBoxShrinksUntilAllOfItFits()
    {
        // Sixteen H's are 115 points wide at 10 points, and the box has 96 inside its margins:
        // at the old fixed size, the clip cut the last three off.
        var box = new XRect(60, 60, 100, 30);
        var field = AText(box);
        field.Text = new string('H', 16);

        var image = _rasterized.FirstPageOf(field.Owner, "narrow-line");

        StemsAcross(image, box).Should().Be(32, "every H of the value is drawn, two stems each");
    }

    [Test, GoldenImage]
    public void AMultiLineValueTooTallForTheBoxShrinksUntilEveryLineFits()
    {
        // Eight lines are 110 points tall at 12 points and 92 at 10, and the box has 86 inside
        // its margins: at the old fixed size the last line was cut off.
        var box = new XRect(60, 60, 160, 90);
        var field = AText(box);
        field.MultiLine = true;
        field.Text = string.Join("\n", Enumerable.Repeat("HHH", 8));

        var image = _rasterized.FirstPageOf(field.Owner, "multi-line");

        LinesDown(image, box).Should().Be(8, "every line of the value is drawn");
    }

    [Test, GoldenImage]
    public void ACombFieldSizesEachCharacterToItsCell()
    {
        // Five cells, each 20 points wide and 60 tall. Sized to the height alone an H would be
        // twice the width of its cell and run into the next.
        var box = new XRect(60, 60, 100, 60);
        var field = AText(box);
        field.MaxLength = 5;
        field.Flags |= PdfAcroFieldFlags.Comb;
        field.Text = "HHHHH";

        var image = _rasterized.FirstPageOf(field.Owner, "comb");

        InkBounds(image, box).Height.Should().BeGreaterThan(14, "larger than the 7-point H of the old 10-point size");
        StemsAcross(image, box).Should().Be(10, "each H stays inside its own cell");
    }

    [Test, GoldenImage]
    public void AComboBoxFillsMostOfTheHeightOfATallBox()
    {
        var box = new XRect(60, 60, 300, 60);
        var combo = OnAPage(new PdfComboBoxField(new PdfDocument()) { Name = "combo", Options = ["HHHH"] }, box);
        combo.SelectedIndex = 0;

        var ink = InkBounds(_rasterized.FirstPageOf(combo.Owner, "tall-combo"), box);

        ink.Height.Should().BeGreaterThan(box.Height / 2, "a closed combo box shows one line, sized as a text field's");
    }

    [Test, GoldenImage]
    public void AListBoxShrinksUntilEveryOptionHasARow()
    {
        // Eight rows are 92 points tall at 10 points, and the box is 80: at the old fixed size
        // the last option had no row.
        var box = new XRect(60, 60, 160, 80);
        var list = OnAPage(new PdfListBoxField(new PdfDocument())
        {
            Name = "list",
            Options = ["AAA", "BBB", "CCC", "DDD", "EEE", "FFF", "HHH", "III"]
        }, box);

        var image = _rasterized.FirstPageOf(list.Owner, "list");

        LinesDown(image, box).Should().Be(8, "every option is drawn");
    }

    [Test]
    public void AMultiLineValueThatFitsIsDrawnAtTwelvePoints()
    {
        // Several lines start at 12 points and only ever shrink: a short value in a big box is
        // not blown up to fill it, as a single line is.
        var field = AText(new XRect(60, 60, 300, 200));
        field.MultiLine = true;
        field.Text = "Short";

        FontSizesSetBy(field).Should().Equal(12);
    }

    [Test, GoldenImage]
    public void ALineFeedInASingleLineValueIsMeasuredAsTheJoinedLineItIsDrawnAs()
    {
        // DrawString drops the line feed and draws sixteen H's on one line. Measured as two
        // lines of eight, the value was sized for half its width and the clip cut the rest off.
        var box = new XRect(60, 60, 100, 30);
        var field = AText(box);
        field.Text = "HHHHHHHH\nHHHHHHHH";

        var image = _rasterized.FirstPageOf(field.Owner, "joined-line");

        StemsAcross(image, box).Should().Be(32, "every H of the joined line is drawn");
    }

    [Test, GoldenImage]
    public void AListBoxShrinksUntilItsWidestOptionFitsAcross()
    {
        // Twelve H's are 104 points wide at 12 points, and the box has less than 100 inside its
        // border: one row fits the height easily, so the height alone left the option clipped.
        var box = new XRect(60, 60, 100, 30);
        var list = OnAPage(new PdfListBoxField(new PdfDocument())
        {
            Name = "list",
            Options = [new string('H', 12)]
        }, box);

        var image = _rasterized.FirstPageOf(list.Owner, "wide-option");

        StemsAcross(image, box).Should().Be(24, "every H of the option is drawn");
    }

    [Test]
    public void ACombFieldOfAHugeMaximumLengthDrawsAShortValue()
    {
        // /MaxLen comes from the file. The cells were collected into a list sized for every one
        // of them, so a short value in a field claiming two billion cells asked for an array
        // larger than any the runtime will make.
        var field = AText(new XRect(60, 60, 300, 30), "/Helv 12 Tf 0 g");
        field.MaxLength = int.MaxValue;
        field.Flags |= PdfAcroFieldFlags.Comb;

        var drawing = () => field.Text = "A";

        drawing.Should().NotThrow();
    }

    [Test]
    public void ALineNeverShrinksBelowFourPoints()
    {
        // Forty H's across 36 points would be a point and a quarter: unreadable, and no use to
        // anyone. The clip cuts off what does not fit at the floor instead.
        var field = AText(new XRect(60, 60, 40, 30));
        field.Text = new string('H', 40);

        FontSizesSetBy(field).Should().Equal(4);
    }

    [Test]
    public void ASizeTheDefaultAppearanceNamesIsDrawnAtThatSizeEvenWhenItDoesNotFit()
    {
        var field = AText(new XRect(60, 60, 40, 30), "/Helv 9 Tf 0 g");
        field.Text = new string('H', 40);

        FontSizesSetBy(field).Should().Equal(9);
    }

    [Test]
    public void AFontSetOnTheFieldIsNotAutoSized()
    {
        var field = AText(new XRect(60, 60, 300, 60));
        field.Font = new XFont("Arial", 14);
        field.Text = "HHHH";

        FontSizesSetBy(field).Should().Equal(14);
    }

    /// <summary>
    ///   The size each <c>Tf</c> in the field's first appearance stream sets, in order.
    /// </summary>
    private static double[] FontSizesSetBy(PdfAcroField field)
    {
        var appearances = field.Widgets[0].Elements.GetDictionary("/AP");
        var normal = (PdfDictionary)appearances.Elements.GetObject("/N");
        var content = System.Text.Encoding.Latin1.GetString(normal.Stream.UnfilteredValue);
        return [..Regex.Matches(content, @"([0-9.]+)\s+Tf\b")
            .Select(match => double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture))];
    }

    /// <summary>
    ///   How many separate runs of ink a row a quarter of the way down the ink crosses - above the
    ///   bar of an H, so each H is two.
    /// </summary>
    private static int StemsAcross(IMagickImage<byte> image, XRect box)
    {
        var ink = InkBounds(image, box);
        var scale = ScaleOf(image);
        var y = (int)((ink.Top + ink.Height / 4) * scale);

        using var pixels = image.GetPixels();
        var runs = 0;
        var inRun = false;
        for (var x = (int)(box.X * scale); x < (int)(box.Right * scale); x++)
        {
            var c = pixels.GetPixel(x, y).ToColor();
            var isInk = c != null && IsInk(c);
            if (isInk && !inRun)
                runs++;
            inRun = isInk;
        }
        return runs;
    }

    /// <summary>
    ///   How many separate bands of rows with ink in them the box holds - one for each line of
    ///   capitals, which have no descenders to run into the line below.
    /// </summary>
    private static int LinesDown(IMagickImage<byte> image, XRect box)
    {
        var scale = ScaleOf(image);
        using var pixels = image.GetPixels();
        var bands = 0;
        var inBand = false;
        for (var y = (int)(box.Y * scale); y < (int)(box.Bottom * scale); y++)
        {
            var hasInk = false;
            for (var x = (int)(box.X * scale); x < (int)(box.Right * scale) && !hasInk; x++)
            {
                var c = pixels.GetPixel(x, y).ToColor();
                hasInk = c != null && IsInk(c);
            }
            if (hasInk && !inBand)
                bands++;
            inBand = hasInk;
        }
        return bands;
    }

    /// <summary>
    ///   The bounds of the dark ink inside a box, in world points.
    /// </summary>
    private static XRect InkBounds(IMagickImage<byte> image, XRect box)
    {
        var scale = ScaleOf(image);
        int left = int.MaxValue, top = int.MaxValue, right = -1, bottom = -1;
        using var pixels = image.GetPixels();
        for (var y = (int)(box.Y * scale); y < (int)(box.Bottom * scale); y++)
        {
            for (var x = (int)(box.X * scale); x < (int)(box.Right * scale); x++)
            {
                var c = pixels.GetPixel(x, y).ToColor();
                if (c == null || !IsInk(c))
                    continue;
                left = Math.Min(left, x);
                right = Math.Max(right, x);
                top = Math.Min(top, y);
                bottom = Math.Max(bottom, y);
            }
        }

        right.Should().BeGreaterThan(-1, "something is drawn in the box");
        return new XRect(left / scale, top / scale, (right - left + 1) / scale, (bottom - top + 1) / scale);
    }

    private static bool IsInk(IMagickColor<byte> c) => c.R < 110 && c.G < 110 && c.B < 110;
}
