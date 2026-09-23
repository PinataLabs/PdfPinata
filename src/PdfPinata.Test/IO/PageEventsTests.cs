using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using ImageMagick;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using PdfPinata.Test.Helpers;
using PinataLayout.DocumentObjectModel;
using PinataLayout.Rendering;
using Xunit;

namespace PdfPinata.Test.IO;

/// <summary>
///   <see cref="PdfDocument.PageAdded"/>, <see cref="PdfDocument.PageRemoved"/> and
///   <see cref="PdfDocument.PageGraphicsCreated"/>.
/// </summary>
[Collection(RasterizingCollection.Name)]
public sealed class PageEventsTests : IDisposable
{
    private const string OutDir = "Out/PageEvents";

    private readonly List<MagickImageCollection> _rasterized = new();

    static PageEventsTests()
    {
        GhostscriptSetup.Configure();
    }

    public void Dispose()
    {
        foreach (var collection in _rasterized)
            collection.Dispose();

        _rasterized.Clear();
    }

    // ----- pages added and removed ----------------------------------------------------------------------

    [Fact]
    public void AddingAndInsertingAPageSaysWhichAndWhere()
    {
        var document = new PdfDocument();
        var seen = Record(document);

        var first = document.AddPage();
        var second = document.AddPage();
        var inserted = document.InsertPage(1);

        seen.Should().Equal(("added", first, 0), ("added", second, 1), ("added", inserted, 1));
    }

    [Fact]
    public void ThePageIsInTheTreeWhenTheHandlerRuns()
    {
        var document = new PdfDocument();
        var counts = new List<int>();
        document.PageAdded += (sender, _) => counts.Add(((PdfDocument)sender)!.PageCount);

        _ = document.AddPage();
        _ = document.AddPage();

        counts.Should().Equal(1, 2);
    }

    [Fact]
    public void AnImportedPageIsReportedAsTheCopyThisDocumentHolds()
    {
        var source = Reopen(TwoPages(), PdfDocumentOpenMode.Import);
        var document = new PdfDocument();
        var seen = Record(document);

        var imported = document.AddPage(source.Pages[0]);

        seen.Should().ContainSingle().Which.Should().Be(("added", imported, 0));
        imported.Should().NotBeSameAs(source.Pages[0]);
    }

    [Fact]
    public void ARangeIsReportedPageByPageOnceItIsAllIn()
    {
        var source = Reopen(TwoPages(), PdfDocumentOpenMode.Import);
        var document = new PdfDocument();
        _ = document.AddPage();
        var counts = new List<int>();
        var indexes = new List<int>();
        document.PageAdded += (_, e) =>
        {
            counts.Add(document.PageCount);
            indexes.Add(e.Index);
        };

        document.Pages.InsertRange(0, source);

        indexes.Should().Equal(0, 1);
        counts.Should().Equal(3, 3);
    }

    [Fact]
    public void ARangeReportsTheInsertedPagesEvenWhenAHandlerAddsPagesOfItsOwn()
    {
        var source = Reopen(TwoPages(), PdfDocumentOpenMode.Import);
        var document = new PdfDocument();
        var original = document.AddPage();
        var reported = new List<PdfPage>();
        var separators = new List<PdfPage>();
        var inHandler = false;
        document.PageAdded += (_, e) =>
        {
            // The handler's own insertion raises the event again, inside itself; this is how a
            // handler that adds pages keeps from adding them for ever.
            if (inHandler)
                return;

            reported.Add(e.Page);
            inHandler = true;
            separators.Add(document.InsertPage(0));
            inHandler = false;
        };

        document.Pages.InsertRange(1, source);

        reported.Where(page => !separators.Contains(page)).Should().HaveCount(2)
            .And.OnlyHaveUniqueItems()
            .And.NotContain(original);
    }

    [Fact]
    public void DuplicatingAPageReportsTheDuplicate()
    {
        var document = new PdfDocument();
        _ = document.AddPage();
        var seen = Record(document);

        var duplicate = document.Pages.Duplicate(0, 1);

        seen.Should().ContainSingle().Which.Should().Be(("added", duplicate, 1));
    }

    [Fact]
    public void RemovingAPageSaysWhichAndWhereItWas()
    {
        var document = new PdfDocument();
        var first = document.AddPage();
        var second = document.AddPage();
        var third = document.AddPage();
        var seen = Record(document);

        document.Pages.RemoveAt(1);
        document.Pages.Remove(first);

        seen.Should().Equal(("removed", second, 1), ("removed", first, 0));
        document.Pages[0].Should().BeSameAs(third);
    }

    [Fact]
    public void RemovingAPageTheDocumentDoesNotHoldRaisesNothing()
    {
        var document = new PdfDocument();
        _ = document.AddPage();
        var stranger = new PdfDocument().AddPage();
        var seen = Record(document);

        document.Pages.Remove(stranger);

        seen.Should().BeEmpty();
        document.PageCount.Should().Be(1);
    }

    [Fact]
    public void AnUnsubscribedHandlerHearsNothing()
    {
        var document = new PdfDocument();
        var heard = 0;
        EventHandler<PdfPageEventArgs> handler = (_, _) => heard++;
        document.PageAdded += handler;
        _ = document.AddPage();
        document.PageAdded -= handler;

        _ = document.AddPage();

        heard.Should().Be(1);
    }

    // ----- a surface made for a page --------------------------------------------------------------------

    [Fact]
    public void EverySurfaceMadeForAPageIsReported()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        var seen = new List<(PdfPage, XGraphics)>();
        document.PageGraphicsCreated += (_, e) => seen.Add((e.Page, e.Graphics));

        XGraphics first, second;
        using (first = XGraphics.FromPdfPage(page)) { }
        using (second = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Prepend, XGraphicsUnit.Millimeter)) { }

        seen.Should().Equal((page, first), (page, second));
    }

    /// <summary>
    ///   What the handler draws lies under what the caller draws, and a transform the handler
    ///   leaves set does not move the caller's drawing - here, off the page altogether.
    /// </summary>
    [Fact]
    public void TheHandlerDrawsUnderneathAndItsStateDoesNotLeak()
    {
        var document = new PdfDocument();
        document.PageGraphicsCreated += (_, e) =>
        {
            e.Graphics.DrawRectangle(XBrushes.Gray, 0, 0, e.Page.Width.Point, e.Page.Height.Point);
            e.Graphics.TranslateTransform(5000, 5000);
        };

        var page = document.AddPage();
        using (var gfx = XGraphics.FromPdfPage(page))
            gfx.DrawRectangle(XBrushes.Blue, 100, 100, 200, 200);

        var images = PdfHelper.Rasterize(document).ImageCollection;
        _rasterized.Add(images);
        PdfHelper.WriteImageCollection(images, OutDir, "underneath");

        Count(images[0], IsBlue).Should().BeGreaterThan(10000);
        Count(images[0], IsGrey).Should().BeGreaterThan(10000);
    }

    [Fact]
    public void AHandlerThatThrowsLeavesThePageDrawable()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        var fail = true;
        document.PageGraphicsCreated += (_, _) =>
        {
            if (fail)
                throw new InvalidOperationException("handler failed");
        };

        Action draw = () => XGraphics.FromPdfPage(page);
        draw.Should().Throw<InvalidOperationException>().WithMessage("handler failed");

        fail = false;
        Action again = () => XGraphics.FromPdfPage(page).Dispose();
        again.Should().NotThrow();
    }

    [Fact]
    public void AHandlerCannotAskForASecondSurfaceForTheSamePage()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        document.PageGraphicsCreated += (_, e) => XGraphics.FromPdfPage(e.Page);

        Action draw = () => XGraphics.FromPdfPage(page);

        draw.Should().Throw<InvalidOperationException>();
    }

    /// <summary>
    ///   PinataLayout draws every page through <see cref="XGraphics.FromPdfPage(PdfPage)"/>, so a
    ///   handler sees each of them - with its real size, which <see cref="PdfDocument.PageAdded"/>
    ///   cannot promise - and can draw in a tagged document inside an artifact.
    /// </summary>
    [Fact]
    public void ALaidOutDocumentRaisesOneSurfacePerPage()
    {
        var content = new Document();
        var section = content.AddSection();
        section.PageSetup.PageFormat = PageFormat.A5;
        section.AddParagraph("First page");
        section.AddPageBreak();
        section.AddParagraph("Second page");

        var target = new PdfDocument();
        var widths = new List<double>();
        target.PageGraphicsCreated += (_, e) =>
        {
            widths.Add(e.Page.Width.Point);
            using (e.Graphics.BeginArtifact())
                e.Graphics.DrawRectangle(XBrushes.LightGray, 0, 0, 20, 20);
        };

        var renderer = new PdfDocumentRenderer(true) { Document = content, PdfDocument = target };
        renderer.RenderDocument();

        widths.Should().HaveCount(2);
        widths.Should().AllSatisfy(width => width.Should().BeApproximately(419.5, 1));

        using var stream = new MemoryStream();
        target.Save(stream, false);
        var saved = Reopen(stream.ToArray(), PdfDocumentOpenMode.Modify);
        saved.Internals.Catalog.Elements.ContainsKey("/StructTreeRoot").Should().BeTrue("the layout is tagged by default");
    }

    // ----- helpers ------------------------------------------------------------------------------------

    private static List<(string, PdfPage, int)> Record(PdfDocument document)
    {
        var seen = new List<(string, PdfPage, int)>();
        document.PageAdded += (_, e) => seen.Add(("added", e.Page, e.Index));
        document.PageRemoved += (_, e) => seen.Add(("removed", e.Page, e.Index));
        return seen;
    }

    private static byte[] TwoPages()
    {
        var document = new PdfDocument();
        _ = document.AddPage();
        _ = document.AddPage();
        using var output = new MemoryStream();
        document.Save(output, false);
        return output.ToArray();
    }

    private static PdfDocument Reopen(byte[] bytes, PdfDocumentOpenMode mode) =>
        PdfPinata.Pdf.IO.PdfReader.Open(new MemoryStream(bytes), mode);

    private static bool IsBlue(IMagickColor<byte> c) => c.B > 150 && c.R < 120 && c.G < 150;

    private static bool IsGrey(IMagickColor<byte> c) =>
        Math.Abs(c.R - c.G) < 10 && Math.Abs(c.G - c.B) < 10 && c.R is > 90 and < 200;

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
