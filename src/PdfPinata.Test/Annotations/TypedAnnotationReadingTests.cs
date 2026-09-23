using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using ImageMagick;
using PdfPinata.Drawing;
using PdfPinata.Fonts;
using PdfPinata.Pdf;
using PdfPinata.Pdf.AcroForms;
using PdfPinata.Pdf.Annotations;
using PdfPinata.Pdf.IO;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PdfPinata.Test.Annotations;

/// <summary>
///   An annotation read out of a file is handed back as the class its <c>/Subtype</c> names, and
///   handing it back changes nothing in it.
/// </summary>
/// <remarks>
///   The second half is the one that could go wrong. Four of these classes draw their own
///   appearance and write defaults when they are made, so constructing one over a dictionary read
///   from a file - the way a new one is constructed - would replace the appearance the file carries
///   with one this library made up. Wrapping takes the dictionary over and writes nothing.
/// </remarks>
[Collection(RasterizingCollection.Name)]
public sealed class TypedAnnotationReadingTests : IDisposable
{
    private const string OutDir = "Out/TypedAnnotationReading";

    private readonly List<MagickImageCollection> _rasterized = new();

    static TypedAnnotationReadingTests()
    {
        GhostscriptSetup.Configure();
    }

    public void Dispose()
    {
        foreach (var collection in _rasterized)
            collection.Dispose();

        _rasterized.Clear();
    }

    private static readonly PdfRectangle Somewhere = new(new XPoint(100, 500), new XPoint(300, 600));

    public static TheoryData<string, Type> Subtypes => new()
    {
        { "/Text", typeof(PdfTextAnnotation) },
        { "/Link", typeof(PdfLinkAnnotation) },
        { "/FreeText", typeof(PdfFreeTextAnnotation) },
        { "/Line", typeof(PdfLineAnnotation) },
        { "/Square", typeof(PdfSquareAnnotation) },
        { "/Circle", typeof(PdfCircleAnnotation) },
        { "/Highlight", typeof(PdfHighlightAnnotation) },
        { "/Underline", typeof(PdfUnderlineAnnotation) },
        { "/StrikeOut", typeof(PdfStrikeOutAnnotation) },
        { "/Squiggly", typeof(PdfSquigglyAnnotation) },
        { "/Stamp", typeof(PdfRubberStampAnnotation) },
        { "/FileAttachment", typeof(PdfFileAttachmentAnnotation) },
        { "/Widget", typeof(PdfWidgetAnnotation) },
        { "/Ink", typeof(PdfInkAnnotation) },
        { "/Polygon", typeof(PdfPolygonAnnotation) },
        { "/PolyLine", typeof(PdfPolyLineAnnotation) },
        { "/Popup", typeof(PdfPopupAnnotation) },
        { "/Caret", typeof(PdfCaretAnnotation) },
        { "/Redact", typeof(PdfRedactAnnotation) },
        { "/Wibble", typeof(PdfGenericAnnotation) },
    };

    [Theory]
    [MemberData(nameof(Subtypes))]
    public void AnAnnotationIsReadBackAsTheClassItsSubtypeNames(string subtype, Type expected)
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        page.Annotations.Add(new PdfGenericAnnotation(subtype) { Rectangle = Somewhere });

        var read = ReadBack(document).Pages[0].Annotations[0];

        read.Should().BeOfType(expected);
        read.Elements.GetName("/Subtype").Should().Be(subtype);
    }

    [Fact]
    public void TheSameObjectIsHandedBackEveryTimeItIsAskedFor()
    {
        var document = new PdfDocument();
        document.AddPage().Annotations.Add(new PdfSquareAnnotation { Rectangle = Somewhere });

        var annotations = ReadBack(document).Pages[0].Annotations;

        annotations[0].Should().BeSameAs(annotations[0]);
    }

    [Fact]
    public void ASquareReportsTheInteriorAndBorderItWasWrittenWith()
    {
        var document = new PdfDocument();
        var square = new PdfSquareAnnotation();
        document.AddPage().Annotations.Add(square);
        square.Rectangle = Somewhere;
        square.Interior = XColor.FromArgb(127, 0, 255);
        square.BorderWidth = 3.5;

        var read = (PdfSquareAnnotation)ReadBack(document).Pages[0].Annotations[0];

        read.Interior.R.Should().Be(127);
        read.Interior.G.Should().Be(0);
        read.Interior.B.Should().Be(255);
        read.BorderWidth.Should().Be(3.5);
    }

    [Fact]
    public void ACircleWithNoInteriorReportsNone()
    {
        var document = new PdfDocument();
        var circle = new PdfCircleAnnotation();
        document.AddPage().Annotations.Add(circle);
        circle.Rectangle = Somewhere;

        var read = (PdfCircleAnnotation)ReadBack(document).Pages[0].Annotations[0];

        read.Interior.Should().Be(XColor.Empty);
        read.BorderWidth.Should().Be(1);
    }

    [Fact]
    public void ALineReportsItsEndpointsAndEndings()
    {
        var document = new PdfDocument();
        var line = new PdfLineAnnotation();
        document.AddPage().Annotations.Add(line);
        line.SetLine(new XPoint(100, 100), new XPoint(300, 200));
        line.EndEnding = PdfLineEnding.ClosedArrow;

        var read = (PdfLineAnnotation)ReadBack(document).Pages[0].Annotations[0];

        read.Start.Should().Be(new XPoint(100, 100));
        read.End.Should().Be(new XPoint(300, 200));
        read.EndEnding.Should().Be(PdfLineEnding.ClosedArrow);
    }

    [Fact]
    public void AFreeTextReportsTheColourAndSizeItsDefaultAppearanceNames()
    {
        GlobalFontSettings.FontResolver ??= new PinnedFontResolver();

        var document = new PdfDocument();
        var note = new PdfFreeTextAnnotation();
        document.AddPage().Annotations.Add(note);
        note.Rectangle = Somewhere;
        note.Font = new XFont(GlobalFontSettings.FontResolver.DefaultFontName, 14);
        note.TextColor = XColor.FromArgb(0, 128, 0);
        note.Contents = "read me";

        var read = (PdfFreeTextAnnotation)ReadBack(document).Pages[0].Annotations[0];

        read.TextColor.G.Should().Be(128);
        read.TextColor.R.Should().Be(0);
        read.Font.Size.Should().Be(14);
        read.Contents.Should().Be("read me");
    }

    [Fact]
    public void AMarkupAnnotationReportsItsQuads()
    {
        var document = new PdfDocument();
        var highlight = new PdfHighlightAnnotation();
        document.AddPage().Annotations.Add(highlight);
        highlight.AddQuad(new PdfRectangle(new XPoint(10, 10), new XPoint(90, 30)));
        highlight.AddQuad(new PdfRectangle(new XPoint(10, 40), new XPoint(60, 60)));

        var read = (PdfHighlightAnnotation)ReadBack(document).Pages[0].Annotations[0];

        read.Quads.Should().HaveCount(2);
    }

    /// <summary>
    ///   The appearance is left exactly where the file put it: the same object, the same bytes.
    ///   Reading one is not a change to it.
    /// </summary>
    [Theory]
    [InlineData("/Square")]
    [InlineData("/Circle")]
    [InlineData("/Line")]
    [InlineData("/FreeText")]
    [InlineData("/Highlight")]
    [InlineData("/Ink")]
    [InlineData("/Polygon")]
    [InlineData("/PolyLine")]
    [InlineData("/Caret")]
    [InlineData("/Redact")]
    public void ReadingAnAnnotationThatDrawsItselfLeavesItsAppearanceAlone(string subtype)
    {
        var original = WithForeignAppearance(subtype);
        var before = AppearanceBytes(original.Pages[0].Annotations[0]);

        var reopened = ReadBack(original);
        var annotation = reopened.Pages[0].Annotations[0];
        annotation.Should().NotBeOfType<PdfGenericAnnotation>();

        var again = ReadBack(reopened);
        var after = AppearanceBytes(again.Pages[0].Annotations[0]);

        after.Should().Equal(before);
    }

    /// <summary>
    ///   And a reader still paints what the file said, rather than what this library would have
    ///   drawn for the same entries: a green cross where a square would be a black frame.
    /// </summary>
    [Fact]
    public void ASquareReadFromAFileStillPaintsTheAppearanceTheFileCarried()
    {
        var reopened = ReadBack(WithForeignAppearance("/Square"));
        reopened.Pages[0].Annotations[0].Should().BeOfType<PdfSquareAnnotation>();

        var images = PdfHelper.Rasterize(ReadBack(reopened)).ImageCollection;
        _rasterized.Add(images);
        PdfHelper.WriteImageCollection(images, OutDir, "foreign-square");

        Count(images[0], IsGreen).Should().BeGreaterThan(200);
    }

    [Fact]
    public void ChangingWhatAReadSquareIsDrawnFromRedrawsIt()
    {
        var reopened = ReadBack(WithForeignAppearance("/Square"));
        var square = (PdfSquareAnnotation)reopened.Pages[0].Annotations[0];

        square.Interior = XColors.Blue;

        var images = PdfHelper.Rasterize(ReadBack(reopened)).ImageCollection;
        _rasterized.Add(images);
        PdfHelper.WriteImageCollection(images, OutDir, "redrawn-square");

        Count(images[0], IsGreen).Should().Be(0);
        Count(images[0], IsBlue).Should().BeGreaterThan(1000);
    }

    /// <summary>
    ///   A link this library makes is given a zero-width border, because older readers drew a
    ///   one-point one otherwise. A link read from a file has whatever the file says, and saying
    ///   nothing is a one-point border by ISO 32000-1 - so the default is not added on the way out.
    /// </summary>
    [Fact]
    public void ALinkReadFromAFileIsNotGivenABorderItDidNotHave()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        var link = new PdfGenericAnnotation("/Link") { Rectangle = Somewhere };
        page.Annotations.Add(link);

        var reopened = ReadBack(document);
        reopened.Pages[0].Annotations[0].Should().BeOfType<PdfLinkAnnotation>();

        var saved = ReadBack(reopened).Pages[0].Annotations[0];

        saved.Elements.ContainsKey("/BS").Should().BeFalse();
        saved.Elements.ContainsKey("/Border").Should().BeFalse();
    }

    [Fact]
    public void AMergedFieldAndWidgetCanStillBeFilledInAfterItsWidgetIsRead()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        var form = document.GetOrCreateAcroForm();
        var field = new PdfTextField(document) { Name = "merged" };
        form.Fields.Add(field);

        // Merged: the field dictionary is also the page's widget annotation.
        field.Elements.SetName("/Type", "/Annot");
        field.Elements.SetName("/Subtype", "/Widget");
        field.Elements.SetRectangle("/Rect", Somewhere);
        page.Elements.GetValue("/Annots", VCF.Create);
        page.Elements.GetArray("/Annots")!.Elements.Add(field.Reference);

        var reopened = ReadBack(document);
        reopened.Pages[0].Annotations[0].Should().BeOfType<PdfWidgetAnnotation>();

        var read = (PdfTextField)reopened.AcroForm.Fields["merged"];
        read.Text = "filled";

        var again = ReadBack(reopened);
        again.Pages[0].Annotations[0].Elements.GetString("/V").Should().Be("filled");
        ((PdfTextField)again.AcroForm.Fields["merged"]).Text.Should().Be("filled");
    }

    /// <summary>
    ///   Wrapping is not a change. A document opened for appending and saved incrementally with
    ///   nothing touched appends no annotation.
    /// </summary>
    [Fact]
    public void ReadingAnAnnotationDoesNotMarkItChanged()
    {
        var document = new PdfDocument();
        var square = new PdfSquareAnnotation();
        document.AddPage().Annotations.Add(square);
        square.Rectangle = Somewhere;

        var reopened = ReadBack(document, PdfDocumentOpenMode.Append);
        var read = reopened.Pages[0].Annotations[0];

        read.IsDirty.Should().BeFalse();
    }

    // ----- helpers ------------------------------------------------------------------------------------

    /// <summary>
    ///   A document whose one annotation carries an appearance this library would never draw for
    ///   its entries - a green cross - written through the generic class, so that nothing typed
    ///   has touched it before it is read.
    /// </summary>
    private static PdfDocument WithForeignAppearance(string subtype)
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        var annotation = new PdfGenericAnnotation(subtype) { Rectangle = Somewhere };
        page.Annotations.Add(annotation);

        switch (subtype)
        {
            case "/Line":
                annotation.Elements["/L"] = new PdfArray(document,
                    new PdfReal(100), new PdfReal(500), new PdfReal(300), new PdfReal(600));
                break;
            case "/FreeText":
                annotation.Elements.SetString("/DA", "/Helv 12 Tf 0 0 1 rg");
                annotation.Elements.SetString("/Contents", "not what is drawn");
                break;
            case "/Highlight":
                annotation.Elements["/QuadPoints"] = new PdfArray(document,
                    new PdfReal(100), new PdfReal(600), new PdfReal(300), new PdfReal(600),
                    new PdfReal(100), new PdfReal(500), new PdfReal(300), new PdfReal(500));
                break;
        }

        var form = new XForm(document, new XSize(200, 100));
        using (var gfx = XGraphics.FromForm(form))
        {
            var pen = new XPen(XColors.Lime, 8);
            gfx.DrawLine(pen, 0, 0, 200, 100);
            gfx.DrawLine(pen, 0, 100, 200, 0);
        }

        annotation.SetAppearance(form);
        return document;
    }

    private static byte[] AppearanceBytes(PdfAnnotation annotation)
    {
        var appearance = annotation.Elements.GetDictionary("/AP");
        appearance.Should().NotBeNull();

        var normal = appearance.Elements.GetDictionary("/N");
        normal.Should().NotBeNull();

        return normal.Stream.UnfilteredValue;
    }

    private static PdfDocument ReadBack(PdfDocument document, PdfDocumentOpenMode mode = PdfDocumentOpenMode.Modify)
    {
        using var output = new MemoryStream();
        document.Save(output, false);
        return PdfPinata.Pdf.IO.PdfReader.Open(new MemoryStream(output.ToArray()), mode);
    }

    private static bool IsGreen(IMagickColor<byte> c) => c.G > 180 && c.R < 100 && c.B < 100;

    private static bool IsBlue(IMagickColor<byte> c) => c.B > 150 && c.R < 120 && c.G < 150;

    private static int Count(IMagickImage<byte> image, Func<IMagickColor<byte>, bool> match)
    {
        using var pixels = image.GetPixels();
        return pixels.Count(p =>
        {
            var c = p.ToColor();
            return c != null && match(c);
        });
    }
}
