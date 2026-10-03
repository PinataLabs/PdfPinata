using System;
using System.Linq;
using AwesomeAssertions;
using ImageMagick;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.AcroForms;
using PdfPinata.Test.Helpers;
using Xunit;
using static PdfPinata.Test.Helpers.PageInk;

namespace PdfPinata.Test.Forms;

/// <summary>
///   A text field draws its value the way a viewer lays it out while the field is being edited,
///   so the text does not move as the field gains and loses the focus. It used to draw every value
///   as one line from the top left, at a fixed 10 points whatever <c>/DA</c> said. Issue #155.
/// </summary>
[Collection(RasterizingCollection.Name)]
public sealed class TextFieldAppearanceTests : IDisposable
{
    private const string OutDir = "Out/TextFieldAppearances";

    private readonly Rasterizations _rasterized = new(OutDir);

    static TextFieldAppearanceTests()
    {
        GhostscriptSetup.Configure();
    }

    public void Dispose() => _rasterized.Dispose();

    /// <summary>A single-line box, in world space from the top left of an A4 page.</summary>
    private static readonly XRect Line = new(60, 60, 300, 30);

    /// <summary>A box several lines tall.</summary>
    private static readonly XRect Tall = new(60, 60, 160, 90);

    private static PdfTextField OnAPage(XRect box, Action<PdfTextField> describe = null)
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        var form = document.GetOrCreateAcroForm();
        var field = new PdfTextField(document) { Name = "text" };
        form.Fields.Add(field);
        field.DefaultAppearance = "/Helv 12 Tf 0 g";
        describe?.Invoke(field);

        var gfx = XGraphics.FromPdfPage(page);
        field.AddWidget(page, new PdfRectangle(gfx.Transformer.WorldToDefaultPage(box)));
        gfx.Dispose();
        return field;
    }

    [Fact]
    public void TheFontSizeAndColourComeFromTheDefaultAppearance()
    {
        var field = OnAPage(Line, f => f.DefaultAppearance = "/Helv 9 Tf 0 0 1 rg");

        field.Font.Size.Should().Be(9);
        field.ForeColor.B.Should().Be(255);
        field.ForeColor.R.Should().Be(0);
    }

    [Fact]
    public void AFontOrColourSetOnTheFieldWinsOverTheDefaultAppearance()
    {
        var field = OnAPage(Line);

        field.Font = new XFont("Arial", 14);
        field.ForeColor = XColors.Red;

        field.Font.Size.Should().Be(14);
        field.ForeColor.Should().Be(XColors.Red);
    }

    [GoldenImageFact]
    public void OneLineIsCentredVertically()
    {
        var field = OnAPage(Line);
        field.Text = "Ada Lovelace";

        var ink = InkBounds(Rasterize("centred", field), Line);

        var middle = (ink.Top + ink.Bottom) / 2;
        middle.Should().BeApproximately(Line.Y + Line.Height / 2, 2.5,
            "a viewer centres a single line vertically while editing it, so the drawing has to");
        (ink.Left - Line.X).Should().BeInRange(1.5, 4, "two points in from the left, as a viewer draws it");
    }

    [GoldenImageFact]
    public void AValueIsDrawnAtTheSizeTheDefaultAppearanceNames()
    {
        var small = InkBounds(Rasterize("size-9", OnAPage(Line, f =>
        {
            f.DefaultAppearance = "/Helv 9 Tf 0 g";
            f.Text = "HHHH";
        })), Line);
        var large = InkBounds(Rasterize("size-18", OnAPage(Line, f =>
        {
            f.DefaultAppearance = "/Helv 18 Tf 0 g";
            f.Text = "HHHH";
        })), Line);

        (large.Height / small.Height).Should().BeApproximately(2, 0.3);
    }

    [GoldenImageFact]
    public void AMultiLineValueIsWrappedFromTheTop()
    {
        var field = OnAPage(Tall, f => f.MultiLine = true);
        field.Text = "A value long enough that it has to wrap onto several lines of this narrow box.";

        var ink = InkBounds(Rasterize("wrapped", field), Tall);

        ink.Height.Should().BeGreaterThan(30, "the value runs to several lines");
        (ink.Top - Tall.Y).Should().BeLessThan(8, "wrapped text starts at the top");
        ink.Right.Should().BeLessThanOrEqualTo(Tall.Right, "and stays inside the box");
    }

    [GoldenImageFact]
    public void ACombFieldPutsOneCharacterInEachCell()
    {
        // Five cells across 300 points; "11111" puts a narrow stroke in the middle of each.
        var field = OnAPage(Line, f =>
        {
            f.MaxLength = 5;
            f.Flags |= PdfAcroFieldFlags.Comb;
        });
        field.Text = "11111";

        var image = Rasterize("comb", field);
        var cell = Line.Width / 5;
        for (var index = 0; index < 5; index++)
        {
            var middle = new XRect(Line.X + index * cell + cell / 2 - 8, Line.Y, 16, Line.Height);
            Count(image, middle, IsInk).Should().BeGreaterThan(5, "cell {0} has its character in the middle", index);
        }
    }

    /// <summary>
    ///   A comb field counts characters as a reader of the value sees them: a character outside the
    ///   Basic Multilingual Plane is two UTF-16 code units, and a base letter with a combining mark
    ///   after it is two code points, and each is one cell. Split by code unit, each half of the
    ///   surrogate pair was drawn alone - a lone surrogate has no glyph - in a cell of its own.
    /// </summary>
    [Theory]
    [InlineData("a\U0001F600b", new[] { 0, 1, 2 })]
    [InlineData("e\u0301x", new[] { 0, 1 })]
    [InlineData("\U00020000\U00020001\U00020002", new[] { 0, 1, 2 })]
    public void ACombFieldPutsOneCharacterAsTheReaderSeesItInEachCell(string value, int[] cells)
    {
        var field = OnAPage(Line, f =>
        {
            f.MaxLength = 4;
            f.Flags |= PdfAcroFieldFlags.Comb;
        });

        field.Text = value;

        CellsOf(field).Should().Equal(cells);
    }

    [Theory]
    [InlineData("\U0001F600")]
    [InlineData("e\u0301")]
    public void APasswordDrawsOneMaskCharacterForEachCharacterAsTheReaderSeesIt(string value)
    {
        var masked = OnAPage(Line, f => f.Password = true);
        var single = OnAPage(Line, f => f.Password = true);

        masked.Text = value;
        single.Text = "x";

        TextOperators.ShownStrings(AppearanceBytes(masked))
            .Should().Equal(TextOperators.ShownStrings(AppearanceBytes(single)), "one character, so one asterisk");
    }

    /// <summary>
    ///   A comb field described once, on a parent that only groups: <c>/MaxLen</c> and the comb
    ///   flag are on <c>code</c>, and <c>code.digits</c>, which has the widget, carries neither.
    /// </summary>
    private static (PdfDocument Document, PdfTextField Parent, PdfTextField Kid) ACombGroup()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        var form = document.GetOrCreateAcroForm();
        var parent = new PdfTextField(document) { Name = "code" };
        form.Fields.Add(parent);
        parent.DefaultAppearance = "/Helv 12 Tf 0 g";
        parent.MaxLength = 6;
        parent.Flags |= PdfAcroFieldFlags.Comb;

        var kid = new PdfTextField(document) { Name = "digits" };
        parent.Fields.Add(kid);
        kid.AddWidget(page, new PdfRectangle(new XRect(60, 700, 300, 30)));
        return (document, parent, kid);
    }

    [Fact]
    public void AKidInheritsItsMaximumLength()
    {
        var (_, _, kid) = ACombGroup();

        kid.Elements.ContainsKey(PdfTextField.Keys.MaxLen).Should().BeFalse("the kid says none of its own");
        kid.MaxLength.Should().Be(6, "/MaxLen is inheritable, ISO 32000-1 Table 229");
    }

    [Fact]
    public void AKidsOwnMaximumLengthIsWrittenOnTheKid()
    {
        var (_, parent, kid) = ACombGroup();

        kid.MaxLength = 4;

        kid.MaxLength.Should().Be(4);
        kid.Elements.GetInteger(PdfTextField.Keys.MaxLen).Should().Be(4);
        parent.MaxLength.Should().Be(6, "the parent is untouched");
    }

    [Fact]
    public void ACombKidDrawsTheCellsItsParentDescribes()
    {
        var (_, _, kid) = ACombGroup();

        kid.Text = "123456";

        CellsOf(kid).Should().Equal([0, 1, 2, 3, 4, 5], "one character in each of the six cells the parent's /MaxLen makes");
    }

    [Fact]
    public void ACombKidReadFromAFileDrawsTheCellsItsParentDescribes()
    {
        var (document, _, _) = ACombGroup();
        var reopened = document.Reopened();
        var kid = (PdfTextField)reopened.AcroForm.Fields["code.digits"];

        kid.MaxLength.Should().Be(6);
        kid.Text = "654321";

        CellsOf(kid).Should().Equal([0, 1, 2, 3, 4, 5]);
    }

    /// <summary>
    ///   Which comb cell each show-text operator of a field's appearance draws in, counted from the
    ///   left - the cell the middle of the drawn character falls in is good enough, since every
    ///   string is centred in its cell.
    /// </summary>
    private static int[] CellsOf(PdfTextField field)
    {
        var width = field.Widgets[0].Rectangle.Width;
        var cell = width / field.MaxLength;
        return [..TextOperators.ShownWithPositions(AppearanceBytes(field))
            .Select(shown => (int)Math.Floor((shown.X + 2) / cell))];
    }

    [GoldenImageFact]
    public void APasswordIsNotDrawnInTheClear()
    {
        // Two values of the same length draw the same: a mask character for each.
        var first = Rasterize("password-a", OnAPage(Line, f =>
        {
            f.Password = true;
            f.Text = "iiiiiii";
        }));
        var second = Rasterize("password-b", OnAPage(Line, f =>
        {
            f.Password = true;
            f.Text = "WWWWWWW";
        }));

        Count(first, Line, IsInk).Should().BeGreaterThan(20, "something is drawn");
        Count(first, Line, IsInk).Should().Be(Count(second, Line, IsInk));
    }

    [GoldenImageFact]
    public void QuaddingAlignsTheValue()
    {
        var right = InkBounds(Rasterize("right", OnAPage(Line, f =>
        {
            f.Elements.SetInteger(PdfAcroField.Keys.Q, 2);
            f.Text = "Ada";
        })), Line);

        (Line.Right - right.Right).Should().BeInRange(1.5, 4, "aligned right, two points in");
    }

    [Theory]
    [InlineData("Ada Lovelace")]
    [InlineData("")]
    public void OnlyTheTextIsMarkedAsVariableText(string value)
    {
        // A viewer that edits the field replaces what is between /Tx BMC and EMC with its own
        // text. The box has to be outside, or the first edit takes it away.
        var field = OnAPage(Line, f =>
        {
            f.BackColor = XColors.LightGray;
            f.BorderColor = XColors.Gray;
        });
        field.Text = value;

        var content = AppearanceContent(field);
        var open = content.IndexOf("/Tx BMC", StringComparison.Ordinal);
        var close = content.IndexOf("EMC", open + 7, StringComparison.Ordinal);

        open.Should().BeGreaterThan(-1, "the text is marked, even when there is none yet");
        close.Should().BeGreaterThan(open);
        var inside = content[open..close];
        var before = content[..open];
        before.Should().Contain(Fill, "the background is filled before the text");
        before.Should().Contain(Stroke, "and the border is stroked before it");
        inside.Should().NotContain(Fill).And.NotContain(Stroke, "neither is inside the bracket");
    }

    [Fact]
    public void AChoiceFieldMarksOnlyItsTextToo()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        var form = document.GetOrCreateAcroForm();
        var combo = new PdfComboBoxField(document) { Name = "c", Options = ["one", "two"], BackColor = XColors.LightGray };
        form.Fields.Add(combo);
        combo.AddWidget(page, new PdfRectangle(new XRect(60, 700, 200, 20)));
        combo.SelectedIndex = 1;

        var content = AppearanceContent(combo);
        var open = content.IndexOf("/Tx BMC", StringComparison.Ordinal);
        open.Should().BeGreaterThan(content.IndexOf(Fill, StringComparison.Ordinal));
    }

    /// <summary>A rectangle filled, and a rectangle stroked, as the renderer writes them.</summary>
    private const string Fill = " re\nf";

    private const string Stroke = " re\nS";

    private static string AppearanceContent(PdfAcroField field) =>
        System.Text.Encoding.Latin1.GetString(AppearanceBytes(field));

    private static byte[] AppearanceBytes(PdfAcroField field)
    {
        var appearances = field.Widgets[0].Elements.GetDictionary("/AP");
        var normal = (PdfDictionary)appearances.Elements.GetObject("/N");
        return normal.Stream.UnfilteredValue;
    }

    private IMagickImage<byte> Rasterize(string name, PdfTextField field)
    {
        return _rasterized.FirstPageOf(field.Owner, name);
    }

    /// <summary>
    ///   The bounds of the ink inside a box, in world points - leaving out a two-point band at the
    ///   edges, where the border is.
    /// </summary>
    private static XRect InkBounds(IMagickImage<byte> image, XRect box)
    {
        var scale = ScaleOf(image);
        int left = int.MaxValue, top = int.MaxValue, right = -1, bottom = -1;
        using var pixels = image.GetPixels();
        for (var y = (int)((box.Y + 2) * scale); y < (int)((box.Bottom - 2) * scale); y++)
        {
            for (var x = (int)((box.X + 2) * scale); x < (int)((box.Right - 2) * scale); x++)
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
