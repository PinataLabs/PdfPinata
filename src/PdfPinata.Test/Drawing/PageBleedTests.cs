using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PdfPinata.Test.Drawing;

/// <summary>
///   What <see cref="PdfPage.TrimMargins"/> does: where it puts the drawing origin, what it does
///   to the size of the page, and which of the five page boxes it writes.
/// </summary>
/// <remarks>
///   <para>
///     There are three areas, nested: the <b>sheet</b> that goes through the press, the
///     <b>bleed</b> the artwork may run to, and the <b>trim</b> where the guillotine cuts, which
///     is the page the caller asked for. The room between the bleed and the sheet edge is where
///     the crop marks go.
///   </para>
///   <para>
///     The first version of this file was written against the behaviour as it stood, before
///     anything changed, because the box arithmetic was documented as the numbers InDesign wrote
///     for one particular page rather than as anything derived from the specification. It found
///     three defects, and the tests that recorded them are still here - now asserting that each
///     is fixed.
///   </para>
/// </remarks>
public class PageBleedTests
{
    /// <summary>A bleed of 3mm, which is what a printer asks for and what InDesign defaults to.</summary>
    static readonly XUnit Bleed = XUnit.FromMillimeter(3);

    /// <summary>The room outside the bleed for printer's marks, which is 5mm unless it is changed.</summary>
    static readonly XUnit Marks = XUnit.FromMillimeter(5);

    /// <summary>From the corner of the sheet to the corner of the trimmed page.</summary>
    static double Inset => Bleed.Point + Marks.Point;

    /// <summary>A5 in points, which is the trimmed size every page here is cut down to.</summary>
    const double A5Width = 420;
    const double A5Height = 595;

    // ----- the origin ---------------------------------------------------------------------------

    [Fact]
    public void TheOriginIsTheCornerOfTheTrimmedPageRatherThanOfTheSheet()
    {
        var page = Trimmed();
        Draw(page, gfx => gfx.DrawRectangle(XBrushes.Black, new XRect(0, 0, 20, 20)));

        var mark = Save(page).RectangleOnSheet;

        // The sheet is the trimmed page plus a bleed and a mark allowance on each edge, so its
        // top-left corner is both of those out from the origin on both axes.
        mark.Left.Should().BeApproximately(Inset, 0.001);
        mark.Top.Should().BeApproximately(Inset, 0.001);
    }

    [Fact]
    public void ANegativeCoordinateReachesTheEdgeOfTheBleed()
    {
        var page = Trimmed();
        Draw(page, gfx => gfx.DrawRectangle(XBrushes.Black,
            new XRect(-Bleed.Point, -Bleed.Point, 100, 100)));

        var saved = Save(page);
        var mark = saved.RectangleOnSheet;

        // Flush with the corner of the bleed, which is what bleeding means: ink that survives a
        // cut landing a fraction off the mark. Outside it is the press's margin, not the page's.
        mark.Left.Should().BeApproximately(Marks.Point, 0.001);
        mark.Top.Should().BeApproximately(Marks.Point, 0.001);
        mark.Left.Should().BeApproximately(saved.BleedBox.X1, 0.001);
    }

    [Fact]
    public void TheSameDrawingSitsInTheSamePlaceOnTheTrimmedPageEitherWay()
    {
        var trimmed = Trimmed();
        Draw(trimmed, gfx => gfx.DrawRectangle(XBrushes.Black, new XRect(30, 40, 20, 20)));

        var plain = Plain();
        Draw(plain, gfx => gfx.DrawRectangle(XBrushes.Black, new XRect(30, 40, 20, 20)));

        var onTrimmed = Save(trimmed).RectangleOnSheet;
        var onPlain = Save(plain).RectangleOnSheet;

        // Measured from the trim corner - which on the untrimmed page is the corner of the sheet -
        // every coordinate is the same. Bleeding is reaching past the origin, not rebuilding the
        // page.
        (onTrimmed.Left - Inset).Should().BeApproximately(onPlain.Left, 0.001);
        (onTrimmed.Top - Inset).Should().BeApproximately(onPlain.Top, 0.001);
    }

    // ----- the size of the page -----------------------------------------------------------------

    [Fact]
    public void SettingATrimMarginDoesNotChangeTheSizeOfThePageTheCallerDrawsOn()
    {
        var page = Trimmed();

        page.Width.Point.Should().Be(A5Width);
        page.Height.Point.Should().Be(A5Height);
    }

    [Fact]
    public void ThePageStillReportsItsOwnSizeAfterItHasBeenSaved()
    {
        var page = Trimmed();
        Draw(page, gfx => gfx.DrawRectangle(XBrushes.Black, new XRect(0, 0, 20, 20)));

        using var stream = new MemoryStream();
        page.Owner.Save(stream, false);

        // `Width` reads the media box, and saving rewrites the media box to the size of the
        // sheet - so this used to report the sheet, and every right-aligned thing measured off
        // it afterwards landed in the wrong place. Trimming does not resize the page; it puts
        // more paper around it.
        page.Width.Point.Should().Be(A5Width);
        page.Height.Point.Should().Be(A5Height);
    }

    [Fact]
    public void TheSheetIsLargerThanTheTrimmedPageByTheBleedAndTheMarkAllowance()
    {
        var page = Trimmed();
        Draw(page, gfx => gfx.DrawRectangle(XBrushes.Black, new XRect(0, 0, 20, 20)));

        var boxes = Save(page);

        boxes.MediaBox.Width.Should().BeApproximately(A5Width + 2 * Inset, 0.01);
        boxes.MediaBox.Height.Should().BeApproximately(A5Height + 2 * Inset, 0.01);
    }

    [Fact]
    public void SavingASecondTimeWritesTheSameSheet()
    {
        var page = Trimmed();
        Draw(page, gfx => gfx.DrawRectangle(XBrushes.Black, new XRect(0, 0, 20, 20)));

        using (var first = new MemoryStream())
            page.Owner.Save(first, false);

        using var second = new MemoryStream();
        page.Owner.Save(second, false);
        var mediaBox = Reread(second).MediaBox;

        // `PrepareForSave` used to add the margins to `Width`, and `Width` was the media box it
        // had just written - so every save added another sheet's worth. Saving to a stream and
        // then to a file, which is an ordinary thing to do, produced two files of different sizes.
        mediaBox.Width.Should().BeApproximately(A5Width + 2 * Inset, 0.01);
        mediaBox.Height.Should().BeApproximately(A5Height + 2 * Inset, 0.01);
    }

    // ----- the boxes ----------------------------------------------------------------------------

    [Fact]
    public void ATrimmedPageIsSavedWithAllFiveBoxes()
    {
        var page = Trimmed();
        Draw(page, gfx => gfx.DrawRectangle(XBrushes.Black, new XRect(0, 0, 20, 20)));

        var boxes = Save(page);

        boxes.Names.Should().BeEquivalentTo(new[] { "/MediaBox", "/CropBox", "/BleedBox", "/TrimBox", "/ArtBox" });
    }

    [Fact]
    public void TheTrimBoxIsTheSheetInsetByTheBleedAndTheMarksAndTheArtBoxMatchesIt()
    {
        var page = Trimmed();
        Draw(page, gfx => gfx.DrawRectangle(XBrushes.Black, new XRect(0, 0, 20, 20)));

        var boxes = Save(page);

        boxes.TrimBox.Width.Should().BeApproximately(A5Width, 0.01);
        boxes.TrimBox.Height.Should().BeApproximately(A5Height, 0.01);
        boxes.TrimBox.X1.Should().BeApproximately(Inset, 0.01);
        boxes.TrimBox.Y1.Should().BeApproximately(Inset, 0.01);

        boxes.ArtBox.ToString().Should().Be(boxes.TrimBox.ToString());
    }

    [Fact]
    public void TheBleedBoxLeavesTheMarkAllowanceOutsideIt()
    {
        var page = Trimmed();
        Draw(page, gfx => gfx.DrawRectangle(XBrushes.Black, new XRect(0, 0, 20, 20)));

        var boxes = Save(page);

        // The whole point of the change: there is now somewhere on the sheet that is not artwork,
        // and it is exactly the mark allowance wide. This box used to equal the media box, which
        // satisfied the nesting rule and left nowhere for a crop mark to go.
        boxes.BleedBox.X1.Should().BeApproximately(Marks.Point, 0.01);
        boxes.BleedBox.Y1.Should().BeApproximately(Marks.Point, 0.01);
        boxes.BleedBox.Width.Should().BeApproximately(A5Width + 2 * Bleed.Point, 0.01);
        boxes.BleedBox.Height.Should().BeApproximately(A5Height + 2 * Bleed.Point, 0.01);
    }

    [Fact]
    public void TheBoxesNest()
    {
        var page = Trimmed();
        Draw(page, gfx => gfx.DrawRectangle(XBrushes.Black, new XRect(0, 0, 20, 20)));

        var boxes = Save(page);

        Encloses(boxes.MediaBox, boxes.CropBox).Should().BeTrue("the crop box lies within the media box");
        Encloses(boxes.MediaBox, boxes.BleedBox).Should().BeTrue("the bleed box lies within the media box");
        Encloses(boxes.BleedBox, boxes.TrimBox).Should().BeTrue("the trim box lies within the bleed box");
        Encloses(boxes.TrimBox, boxes.ArtBox).Should().BeTrue("the art box lies within the trim box");
    }

    [Fact]
    public void AnUnevenTrimMarginInsetsEachEdgeByItsOwnMargin()
    {
        var top = XUnit.FromMillimeter(10);
        var bottom = XUnit.FromMillimeter(1);

        var page = Plain();
        page.TrimMargins.Left = Bleed;
        page.TrimMargins.Right = Bleed;
        page.TrimMargins.Top = top;
        page.TrimMargins.Bottom = bottom;
        page.MarkMargins.All = 0;
        Draw(page, gfx => gfx.DrawRectangle(XBrushes.Black, new XRect(0, 0, 20, 20)));

        var saved = Save(page);

        // The drawing origin is one top margin below the sheet's top edge...
        saved.RectangleOnSheet.Top.Should().BeApproximately(top.Point, 0.001);

        // ...and so is the trim box's top edge. `PrepareForSave` used to inset Y1 by `Top` and Y2
        // by `Bottom`, and Y1 is the bottom edge in PDF space, so the two were swapped and the
        // trim box disagreed with the origin. An even margin - the usual case, and the case
        // InDesign's numbers came from - hid it completely.
        (saved.MediaBox.Y2 - saved.TrimBox.Y2).Should().BeApproximately(top.Point, 0.01);
        (saved.TrimBox.Y1 - saved.MediaBox.Y1).Should().BeApproximately(bottom.Point, 0.01);
    }

    [Fact]
    public void APageWithNoTrimMarginCarriesNoneOfTheExtraBoxes()
    {
        var page = Plain();
        Draw(page, gfx => gfx.DrawRectangle(XBrushes.Black, new XRect(0, 0, 20, 20)));

        var boxes = Save(page);

        // A page with no bleed is a page going nowhere near a guillotine, so the mark allowance
        // that is set by default does nothing at all to it. The whole feature stays invisible to
        // every document that does not ask for it.
        boxes.Names.Should().BeEquivalentTo(new[] { "/MediaBox" });
        boxes.MediaBox.Width.Should().Be(A5Width);
    }

    [Fact]
    public void ClearingTheMarkAllowanceGivesBackTheBoxesTheLibraryUsedToWrite()
    {
        var page = Trimmed();
        page.MarkMargins.All = 0;
        Draw(page, gfx => gfx.DrawRectangle(XBrushes.Black, new XRect(0, 0, 20, 20)));

        var boxes = Save(page);

        // For a caller whose downstream tooling expects the old boxes: no room outside the bleed
        // means the bleed is the sheet, which is what this library wrote before crop marks.
        boxes.MediaBox.Width.Should().BeApproximately(A5Width + 2 * Bleed.Point, 0.01);
        boxes.BleedBox.ToString().Should().Be(boxes.MediaBox.ToString());
        boxes.TrimBox.X1.Should().BeApproximately(Bleed.Point, 0.01);
    }

    // ----- assigning the margins ------------------------------------------------------------------

    [Fact]
    public void AssigningTrimMarginsCopiesTheValuesRatherThanHoldingTheReference()
    {
        var page = Plain();
        var shared = new TrimMargins { All = Bleed };

        page.TrimMargins = shared;
        shared.Left = XUnit.FromMillimeter(20);

        // The page holds margins of its own and the setter copies the four values into them, so
        // the page is not moved by a later change to the object it was given.
        page.TrimMargins.Left.Point.Should().BeApproximately(Bleed.Point, 0.01);
        page.TrimMargins.Should().NotBeSameAs(shared);
    }

    [Fact]
    public void AssigningMarkMarginsCopiesTheValuesRatherThanHoldingTheReference()
    {
        var page = Plain();
        var shared = new TrimMargins { All = Marks };

        page.MarkMargins = shared;
        shared.Top = XUnit.FromMillimeter(20);

        page.MarkMargins.Top.Point.Should().BeApproximately(Marks.Point, 0.01);
        page.MarkMargins.Should().NotBeSameAs(shared);
    }

    [Fact]
    public void EveryPageGetsItsOwnCopyOfTheDocumentWideTrimMargins()
    {
        var document = new PdfDocument();
        document.Settings.TrimMargins.All = Bleed;

        var first = document.AddPage();
        var second = document.AddPage();
        first.TrimMargins.Left = XUnit.FromMillimeter(20);

        // Settings.TrimMargins is one instance handed to every page as it is added. Were the page
        // setter to keep the reference rather than copy from it, moving one page's bleed would
        // move every other page's and the default every future page is given.
        second.TrimMargins.Left.Point.Should().BeApproximately(Bleed.Point, 0.01);
        document.Settings.TrimMargins.Left.Point.Should().BeApproximately(Bleed.Point, 0.01);
        document.AddPage().TrimMargins.Left.Point.Should().BeApproximately(Bleed.Point, 0.01);
    }

    // ----- crop marks ---------------------------------------------------------------------------

    [Fact]
    public void ATrimmedPageIsGivenTheEightStandardCropMarks()
    {
        var page = Trimmed();
        Draw(page, gfx => gfx.DrawRectangle(XBrushes.Black, new XRect(0, 0, 20, 20)));

        var saved = Save(page);

        saved.Marks.Should().HaveCount(8, "two marks meet at each of the four corners");
    }

    [Fact]
    public void EveryMarkLiesInTheRoomOutsideTheBleedAndNoneCrossesIt()
    {
        var page = Trimmed();
        Draw(page, gfx => gfx.DrawRectangle(XBrushes.Black, new XRect(0, 0, 20, 20)));

        var saved = Save(page);
        var bleed = saved.BleedBox;

        foreach (var mark in saved.Marks)
        {
            // A mark that crossed the bleed would be indistinguishable from artwork, and one
            // that crossed the trim would be printed on the page that survives the cut.
            var outside =
                mark.X2 <= bleed.X1 + 0.01 || mark.X1 >= bleed.X2 - 0.01 ||
                mark.Y2 <= bleed.Y1 + 0.01 || mark.Y1 >= bleed.Y2 - 0.01;

            outside.Should().BeTrue("the mark from " + mark + " must stay clear of the bleed");
        }
    }

    [Fact]
    public void TheMarksLineUpWithTheCutsTheyMark()
    {
        var page = Trimmed();
        Draw(page, gfx => gfx.DrawRectangle(XBrushes.Black, new XRect(0, 0, 20, 20)));

        var saved = Save(page);
        var trim = saved.TrimBox;

        // Four horizontal marks lying on the two horizontal cuts, and four vertical ones lying on
        // the two vertical cuts. A mark that does not extend a cut line is telling the trimmer
        // something untrue.
        var horizontal = saved.Marks.Where(mark => Math.Abs(mark.Y1 - mark.Y2) < 0.01).ToList();
        var vertical = saved.Marks.Where(mark => Math.Abs(mark.X1 - mark.X2) < 0.01).ToList();

        horizontal.Should().HaveCount(4);
        vertical.Should().HaveCount(4);

        horizontal.Select(mark => Math.Round(mark.Y1, 2)).Distinct().Should()
            .BeEquivalentTo(new[] { Math.Round(trim.Y1, 2), Math.Round(trim.Y2, 2) });
        vertical.Select(mark => Math.Round(mark.X1, 2)).Distinct().Should()
            .BeEquivalentTo(new[] { Math.Round(trim.X1, 2), Math.Round(trim.X2, 2) });
    }

    [Fact]
    public void ClearingTheMarkAllowanceDrawsNoMarks()
    {
        var page = Trimmed();
        page.MarkMargins.All = 0;
        Draw(page, gfx => gfx.DrawRectangle(XBrushes.Black, new XRect(0, 0, 20, 20)));

        Save(page).Marks.Should().BeEmpty("there is no room on the sheet to put them");
    }

    [Fact]
    public void AnUntrimmedPageIsGivenNoMarks()
    {
        var page = Plain();
        Draw(page, gfx => gfx.DrawRectangle(XBrushes.Black, new XRect(0, 0, 20, 20)));

        Save(page).Marks.Should().BeEmpty();
    }

    [Fact]
    public void TheMarksAreDrawnOnceHoweverManyTimesTheDocumentIsSaved()
    {
        var page = Trimmed();
        Draw(page, gfx => gfx.DrawRectangle(XBrushes.Black, new XRect(0, 0, 20, 20)));

        using (var first = new MemoryStream())
            page.Owner.Save(first, false);

        using var second = new MemoryStream();
        page.Owner.Save(second, false);

        Reread(second).Marks.Should().HaveCount(8);
    }

    [Fact]
    public void AskingForMarksOnAPageWithNoBleedSaysWhyItCannotHaveThem()
    {
        var page = Plain();

        var thrown = Assert.Throws<InvalidOperationException>(() => page.DrawCropMarks());

        thrown.Message.Should().Contain("TrimMargins");
    }

    [Fact]
    public void AskingForMarksWithNoRoomForThemSaysWhyItCannotHaveThem()
    {
        var page = Trimmed();
        page.MarkMargins.All = 0;

        var thrown = Assert.Throws<InvalidOperationException>(() => page.DrawCropMarks());

        thrown.Message.Should().Contain("MarkMargins");
    }

    // ----- what is written into the content stream ----------------------------------------------

    [Fact]
    public void ContentDrawnIntoTheBleedIsWrittenRatherThanClippedAway()
    {
        var page = Trimmed();
        Draw(page, gfx => gfx.DrawRectangle(XBrushes.Black,
            new XRect(-Bleed.Point, -Bleed.Point, A5Width + 2 * Bleed.Point, 60)));

        var saved = Save(page);
        var band = saved.RectangleOnSheet;

        // Reaching both edges of the bleed, with no clipping operator to cut it back to the trim.
        band.Left.Should().BeApproximately(saved.BleedBox.X1, 0.01);
        band.Right.Should().BeApproximately(saved.BleedBox.X2, 0.01);
        saved.Content.Should().NotContain(" W ", "nothing clips the page back to the trimmed area");
    }

    // ----- making pages, and reading back what was saved -----------------------------------------

    static PdfPage Plain()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        page.Size = PageSize.A5;
        return page;
    }

    static PdfPage Trimmed()
    {
        var page = Plain();
        page.TrimMargins.All = Bleed;
        return page;
    }

    static void Draw(PdfPage page, Action<XGraphics> draw)
    {
        using var gfx = XGraphics.FromPdfPage(page);
        draw(gfx);
    }

    static SavedPage Save(PdfPage page)
    {
        using var stream = new MemoryStream();
        page.Owner.Save(stream, false);
        return Reread(stream);
    }

    static SavedPage Reread(MemoryStream stream)
    {
        stream.Position = 0;
        var reopened = PdfPinata.Pdf.IO.PdfReader.Open(stream, PdfDocumentOpenMode.Modify);
        return new SavedPage(reopened.Pages[0]);
    }

    static bool Encloses(PdfRectangle outer, PdfRectangle inner)
    {
        const double slack = 0.001;
        return inner.X1 >= outer.X1 - slack && inner.Y1 >= outer.Y1 - slack &&
               inner.X2 <= outer.X2 + slack && inner.Y2 <= outer.Y2 + slack;
    }

    /// <summary>
    ///   A page as a reader finds it: its boxes, its content, the crop marks drawn on it, and
    ///   where on the sheet the first rectangle of that content actually landed.
    /// </summary>
    sealed class SavedPage
    {
        readonly PdfPage _page;

        internal SavedPage(PdfPage page)
        {
            _page = page;
            Content = Encoding.ASCII.GetString(PageContent.Of(page));
        }

        internal string Content { get; }

        internal string[] Names
        {
            get
            {
                var present = new List<string>();
                foreach (var key in new[] { "/MediaBox", "/CropBox", "/BleedBox", "/TrimBox", "/ArtBox" })
                    if (_page.Elements[key] != null)
                        present.Add(key);
                return present.ToArray();
            }
        }

        internal PdfRectangle MediaBox => Box("/MediaBox");
        internal PdfRectangle CropBox => Box("/CropBox");
        internal PdfRectangle BleedBox => Box("/BleedBox");
        internal PdfRectangle TrimBox => Box("/TrimBox");
        internal PdfRectangle ArtBox => Box("/ArtBox");

        PdfRectangle Box(string key) => _page.Elements.GetRectangle(key);

        /// <summary>
        ///   Every stroked line segment on the page, in the sheet's own coordinates. The marks
        ///   are written in a content stream of their own under no transformation at all, which
        ///   is what makes reading them this directly honest.
        /// </summary>
        internal IReadOnlyList<Segment> Marks =>
            Regex.Matches(Content, @"(-?[\d.]+) (-?[\d.]+) m (-?[\d.]+) (-?[\d.]+) l S")
                .Select(match => new Segment(
                    Number(match.Groups[1].Value), Number(match.Groups[2].Value),
                    Number(match.Groups[3].Value), Number(match.Groups[4].Value)))
                .ToList();

        /// <summary>
        ///   Where the first rectangle in the content stream sits on the sheet, in points from
        ///   the sheet's top-left corner, with the page's own transformation applied.
        /// </summary>
        /// <remarks>
        ///   The renderer writes one <c>cm</c> for the whole page and then draws in the space it
        ///   sets up, so reading the rectangle alone says nothing about where it landed. Only
        ///   translation and the vertical flip are handled, which is all this page's matrix has.
        /// </remarks>
        internal Landed RectangleOnSheet
        {
            get
            {
                var offsetX = 0.0;
                var offsetY = 0.0;
                var cm = Regex.Match(Content, @"1 0 0 1 (-?[\d.]+) (-?[\d.]+) cm");
                if (cm.Success)
                {
                    offsetX = Number(cm.Groups[1].Value);
                    offsetY = Number(cm.Groups[2].Value);
                }

                var re = Regex.Match(Content, @"(-?[\d.]+) (-?[\d.]+) (-?[\d.]+) (-?[\d.]+) re");
                re.Success.Should().BeTrue("the drawing put a rectangle on the page");

                var x = Number(re.Groups[1].Value) + offsetX;
                var y = Number(re.Groups[2].Value) + offsetY;
                var width = Number(re.Groups[3].Value);
                var height = Number(re.Groups[4].Value);

                // Back to the reader's way round: y down from the top of the sheet.
                var sheetTop = MediaBox.Y2;
                return new Landed(x, sheetTop - (y + height), x + width, sheetTop - y);
            }
        }

        static double Number(string text) => double.Parse(text, CultureInfo.InvariantCulture);
    }

    /// <summary>A stroked line, in the sheet's coordinates.</summary>
    readonly struct Segment
    {
        internal Segment(double x1, double y1, double x2, double y2)
        {
            X1 = Math.Min(x1, x2);
            Y1 = Math.Min(y1, y2);
            X2 = Math.Max(x1, x2);
            Y2 = Math.Max(y1, y2);
        }

        internal double X1 { get; }
        internal double Y1 { get; }
        internal double X2 { get; }
        internal double Y2 { get; }

        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture, "({0:0.##}, {1:0.##}) to ({2:0.##}, {3:0.##})", X1, Y1, X2, Y2);
    }

    /// <summary>A rectangle on the sheet, measured in points down and across from its top-left corner.</summary>
    readonly struct Landed
    {
        internal Landed(double left, double top, double right, double bottom)
        {
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
        }

        internal double Left { get; }
        internal double Top { get; }
        internal double Right { get; }
        internal double Bottom { get; }
    }
}
