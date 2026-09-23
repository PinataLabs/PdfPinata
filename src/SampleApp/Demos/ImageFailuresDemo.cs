using System;
using System.Collections.Generic;
using System.IO;
using PinataLayout.DocumentObjectModel;
using PinataLayout.DocumentObjectModel.Shapes;
using PinataLayout.Rendering;
using PdfPinata.Pdf;
using SampleApp.Infrastructure;
using static PinataLayout.DocumentObjectModel.Shapes.ImageSource;

namespace SampleApp.Demos;

/// <summary>
///   What PinataLayout does with an image it cannot read, and how a caller finds out.
/// </summary>
internal sealed class ImageFailuresDemo : PdfDemo
{
    public ImageFailuresDemo() : base() { }

    public override string Name => "ImageFailures";

    public override string Summary => "An image that cannot be read, and the event that says why.";

    public override IReadOnlyList<string> Shows => new[]
    {
        "IImageSource implemented by hand - six members, and nothing else needed to supply an image",
        "DocumentRenderer.ImageFailed, which carries the Image, the ImageFailure and the Exception",
        "Each ImageFailure kind provoked deliberately, by failing at a different point",
        "That the document still renders - a placeholder is drawn and the report goes on",
        "Where in the pipeline each failure is detected: measuring the image, or drawing it",
        "Why this is an event rather than a throw"
    };

    public override int PageCount => 2;

    #region example
    /// <summary>
    ///   An image source that fails on purpose, at a point of the caller's choosing.
    /// </summary>
    /// <remarks>
    ///   IImageSource is six members, and implementing it by hand is how an application supplies
    ///   images from somewhere the library has never heard of - a database, an HTTP response, a
    ///   generated bitmap. Here it is the opposite: an image that is never going to work, so that
    ///   the failure path can be shown rather than described.
    /// </remarks>
    private sealed class FailingImage : IImageSource
    {
        private readonly Func<int> _size;
        private readonly Func<bool> _transparent;
        private readonly Action _write;

        private FailingImage(string name, Func<int> size, Func<bool> transparent, Action write)
        {
            Name = name;
            _size = size;
            _transparent = transparent;
            _write = write;
        }

        // docs:begin image-source-members
        public string Name { get; }

        // Every one of these is read at a different point of the render, which is what lets one
        // class provoke every failure kind.
        public int Width => _size();
        public int Height => _size();
        public bool Transparent => _transparent();
        public void SaveAsJpeg(MemoryStream ms) => _write();

        public PixelBuffer GetPixels()
        {
            _write();
            return default;
        }
        // docs:end image-source-members

        /// <summary>
        ///   Throws while XImage is being built, before anything is measured. XImage's constructor
        ///   reads Transparent to decide the format, and ImageRenderer catches an
        ///   InvalidOperationException from there specifically.
        /// </summary>
        public static FailingImage OfAnUnsupportedType() => new(
            "unsupported.xyz",
            () => 64,
            () => throw new InvalidOperationException("xyz is not an image format anyone knows."),
            () => { });

        /// <summary>
        ///   Reports a size of nothing. Nothing throws; the image is simply of zero extent, which
        ///   is caught after the crop and resolution arithmetic has run.
        /// </summary>
        public static FailingImage OfNoSize() => new(
            "empty.png", () => 0, () => false, () => { });

        /// <summary>
        ///   Throws while being measured. XImage.PixelWidth reads straight through to Width.
        /// </summary>
        public static FailingImage ThatCannotBeMeasured() => new(
            "truncated.png",
            () => throw new InvalidDataException("The file ends in the middle of the header."),
            () => false,
            () => { });

        /// <summary>
        ///   Measures perfectly and then throws on the way out. This one is worth its own case: the
        ///   failure is not detected until the render pass, by which time the layout has already
        ///   been decided around an image that is never going to arrive.
        /// </summary>
        public static FailingImage ThatCannotBeWritten() => new(
            "unreadable.png",
            () => 64,
            () => false,
            () => throw new IOException("The stream was closed by the other end."));
    }

    /// <summary>The four ways to fail, and where in the render each of them lands.</summary>
    private static (string What, Func<IImageSource> Source, string When)[] Cases() => new[]
    {
        ("A type nothing can decode", (Func<IImageSource>)FailingImage.OfAnUnsupportedType,
            "throws while XImage is built, before any measuring"),
        ("An image of no extent", FailingImage.OfNoSize,
            "no exception at all - it measures to nothing"),
        ("A file that ends too soon", FailingImage.ThatCannotBeMeasured,
            "throws while being measured"),
        ("A stream that dies on the way out", FailingImage.ThatCannotBeWritten,
            "measures fine, throws while being drawn")
    };

    protected override PdfDocument Build(DemoContext context)
    {
        // ----- the probe: the same four images, rendered once to collect the events -----

        // A document of its own, because a PinataLayout Document binds to the first renderer it is
        // given and refuses a second - so the report below cannot be rendered once to find out
        // what happens and again to say so. The probe is thrown away; only its findings are kept.
        List<(string Name, string Failure, string Exception, string Message)> failures = new();

        // docs:begin report-failures
        var probe = new Document();
        var probePage = probe.AddSection();
        foreach (var each in Cases())
        {
            var probeImage = probePage.AddImage(each.Source());
            probeImage.Width = Unit.FromCentimeter(4);
            probeImage.Height = Unit.FromCentimeter(2.5);
        }

        var probeRenderer =
            new PdfDocumentRenderer(unicode: true) { Document = probe };

        // The event lives on DocumentRenderer, which PdfDocumentRenderer builds lazily - so reading
        // the property here is what creates it, and attaching before RenderDocument is what catches
        // everything. Attaching afterwards would attach to a renderer that had already finished.
        probeRenderer.DocumentRenderer.ImageFailed += (_, e) =>
        {
            failures.Add((
                // The DOM Image carries the IImageSource itself rather than a path, so the name is
                // whatever the source calls itself - here, the name the failing source was given.
                e.Image.Source?.Name ?? "unnamed",
                e.Failure.ToString(),
                e.Exception?.GetType().Name ?? "none",
                e.Exception?.Message ?? "no exception was thrown"));
        };

        probeRenderer.RenderDocument();
        // docs:end report-failures

        // ----- the document the demo hands back -----

        var report = new Document
        {
            Info =
            {
                Title = "ImageFailures"
            }
        };

        var normal = report.Styles[StyleNames.Normal];
        normal.Font.Name = "Liberation Serif";
        normal.Font.Size = 10.5;

        var heading = report.Styles[StyleNames.Heading1];
        heading.Font.Name = "Liberation Sans";
        heading.Font.Size = 18;
        heading.Font.Bold = true;
        heading.ParagraphFormat.SpaceAfter = Unit.FromPoint(8);

        var caption = report.Styles.AddStyle("Caption", StyleNames.Normal);
        caption.Font.Size = 8.5;
        caption.Font.Italic = true;
        caption.Font.Color = Colors.DimGray;

        var page = report.AddSection();
        page.PageSetup.TopMargin = Unit.FromCentimeter(2.5);

        page.AddParagraph("Four images that will not load").Style = StyleNames.Heading1;

        page.AddParagraph(
            "Each of the four below is an IImageSource written to fail, and each fails at a "
            + "different point of the render. PinataLayout draws a placeholder where the picture would "
            + "have gone and carries on - which is the contract, because one unreadable image "
            + "should not cost a five hundred page report - and raises an event saying what "
            + "happened. The next page is that event, collected.");

        foreach (var each in Cases())
        {
            var label = page.AddParagraph(each.What);
            label.Format.Font.Bold = true;
            label.Format.SpaceBefore = Unit.FromPoint(10);
            label.Format.SpaceAfter = Unit.FromPoint(2);

            page.AddParagraph(each.When).Style = "Caption";

            var image = page.AddImage(each.Source());
            image.Width = Unit.FromCentimeter(4);
            image.Height = Unit.FromCentimeter(2.5);
        }

        // ----- what the handler saw -----

        var verdict = report.AddSection();
        verdict.PageSetup.TopMargin = Unit.FromCentimeter(2.5);

        verdict.AddParagraph("What the handler was told").Style = StyleNames.Heading1;

        verdict.AddParagraph(
            $"{failures.Count} failures, each reported once, at the moment its placeholder was "
            + "drawn. The exception is the instance that was thrown - not a message, not a copy - "
            + "so a handler can log it, rethrow it, or match on its type.");

        var table = verdict.AddTable();
        table.Borders.Width = 0.5;
        table.Borders.Color = Colors.Gainsboro;
        table.Rows.LeftIndent = 0;
        table.Format.Font.Size = 9;
        table.Format.SpaceAfter = 0;
        table.AddColumn(Unit.FromCentimeter(3.2));
        table.AddColumn(Unit.FromCentimeter(2.4));
        table.AddColumn(Unit.FromCentimeter(4.4));
        table.AddColumn(Unit.FromCentimeter(6.0));

        var header = table.AddRow();
        header.HeadingFormat = true;
        header.Shading.Color = Colors.WhiteSmoke;
        header.Cells[0].AddParagraph("Image.Name");
        header.Cells[1].AddParagraph("Failure");
        header.Cells[2].AddParagraph("Exception");
        header.Cells[3].AddParagraph("Message");

        foreach (var failure in failures)
        {
            var row = table.AddRow();
            row.Cells[0].AddParagraph(failure.Name);
            row.Cells[1].AddParagraph(failure.Failure);
            row.Cells[2].AddParagraph(failure.Exception);
            row.Cells[3].AddParagraph(failure.Message);
        }

        var why = verdict.AddParagraph();
        why.Format.SpaceBefore = Unit.FromPoint(14);
        why.AddFormattedText("Why an event and not a throw. ", TextFormat.Bold);
        why.AddText(
            "PinataLayout's contract is that a document with a bad image still renders, and callers "
            + "depend on it. Throwing would be the simpler change and the wrong one. The event "
            + "leaves the contract alone while making the reason reachable, which is what issue "
            + "366 asked for - the exception used to go to Debug.WriteLine and nowhere else, which "
            + "a release build compiles away entirely.");

        var kinds = verdict.AddParagraph();
        kinds.Format.SpaceBefore = Unit.FromPoint(10);
        kinds.AddFormattedText("The fifth kind. ", TextFormat.Bold);
        kinds.AddText(
            "ImageFailure has five values and only four appear above. FileNotFound has a "
            + "placeholder string of its own but nothing in this fork ever assigns it: images "
            + "arrive through IImageSource rather than by path, so there is no file for the "
            + "renderer to fail to find. It is left in the enum because removing a public value "
            + "would break callers switching on it.");

        var where = verdict.AddParagraph();
        where.Format.SpaceBefore = Unit.FromPoint(10);
        where.AddFormattedText("Measured or drawn. ", TextFormat.Bold);
        where.AddText(
            "The last of the four is the one worth remembering. It measures perfectly and fails "
            + "only when its bytes are asked for, by which time the layout has been decided around "
            + "an image that is never going to arrive - so the placeholder is exactly the size the "
            + "picture would have been, and the page does not reflow. The other three are caught "
            + "while measuring, and their placeholder is sized by SetFallbackDimensions instead.");

        verdict.AddParagraph(
            "See docs/specs/image-failure-reporting.md for what was wrong before this and why each "
            + "part of it is the way it is.").Style = "Caption";

        // The four images on page one fail all over again here, into a DocumentRenderer nothing is
        // listening to. That is the point: the placeholders on the page and the rows in the table
        // are two views of the same four failures, taken by two different runs, and they agree.
        var renderer = new PdfDocumentRenderer(unicode: true) { Document = report };
        renderer.RenderDocument();
        #endregion

        return renderer.PdfDocument;
    }
}
