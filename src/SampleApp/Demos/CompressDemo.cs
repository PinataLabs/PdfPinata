using System;
using System.Collections.Generic;
using System.IO;
using PdfPinata.Drawing;
using PdfPinata.Drawing.Layout;
using PdfPinata.Pdf;
using SampleApp.Infrastructure;

namespace SampleApp.Demos;

/// <summary>
///   <see cref="PdfDocumentOptions"/>, whose every setting is invisible on the page and visible
///   only in the byte count.
/// </summary>
internal sealed class CompressDemo : PdfDemo
{
    public CompressDemo() : base() { }

    public override string Name => "Compress";

    public override string Summary => "Every PdfDocumentOptions setting, measured in bytes.";

    public override IReadOnlyList<string> Shows => new[]
    {
        "CompressContentStreams and NoCompression, against the same page of content",
        "That every measurement sets its options explicitly rather than measuring a default",
        "FlateEncodeMode - the trade between how long a save takes and how small it comes out",
        "UseFlateDecoderForJpegImages, Always against Automatic, measured on a real photograph",
        "ColorMode RGB against CMYK, and what changes in the file when it is switched",
        "CrossReferenceFormat - Classic against Stream, measured where it helps and where it does not",
        "A page whose whole subject is the byte counts, because nothing else here is visible"
    };

    public override int PageCount => 3;

    protected override PdfDocument Build(DemoContext context)
    {
        #region example
        var heading = new XFont("Liberation Sans", 16, XFontStyle.Bold);
        var label = new XFont("Liberation Sans", 9, XFontStyle.Bold);
        var body = new XFont("Liberation Sans", 9);
        var mono = new XFont("Source Code Pro", 8.5);

        // One page of representative content - text, a long path, a photograph - built the same
        // way every time so that the only thing that varies between the measurements below is the
        // options the document was saved under.
        void Representative(PdfDocument target)
        {
            var page = target.AddPage();
            using var gfx = XGraphics.FromPdfPage(page);

            gfx.DrawString("Representative content", heading, XBrushes.Black, new XPoint(50, 60));

            var prose = new XTextFormatter(gfx);
            for (var block = 0; block < 4; block++)
            {
                prose.DrawString(
                    "Compression acts on the content stream, which is the list of drawing "
                    + "operators a page is made of. Text is cheap, paths are dear, and an image is "
                    + "usually already compressed by the time it arrives - which is why the "
                    + "settings below move the total by wildly different amounts.",
                    body, XBrushes.Black, new XRect(50, 90 + block * 60, 495, 55));
            }

            // A path with a great many segments. This is what compression has something to work
            // on: a few hundred coordinates written out as text.
            var path = new XGraphicsPath();
            for (var step = 0; step < 400; step++)
            {
                var t = step / 400.0 * Math.PI * 8;
                var point = new XPoint(
                    50 + step * 495.0 / 400,
                    500 + Math.Sin(t) * 60 * (1 - step / 400.0));

                if (step == 0)
                    path.AddLine(point, point);
                else
                    path.AddLine(new XPoint(50 + (step - 1) * 495.0 / 400,
                        500 + Math.Sin((step - 1) / 400.0 * Math.PI * 8) * 60 * (1 - (step - 1) / 400.0)), point);
            }

            gfx.DrawPath(new XPen(XColors.MidnightBlue, 0.8), path);

            using var photograph = XImage.FromStream(
                () => Assets.Open(Assets.ImagePrefix + "pdf-pinata.jpg"));
            gfx.DrawImage(photograph, 50, 580, 240, 180);
        }

        // docs:begin measure
        // Saved under one arrangement of the options and measured. Nothing is written to disk.
        long Measure(Action<PdfDocumentOptions> configure)
        {
            using var probe = new PdfDocument();
            configure(probe.Options);
            Representative(probe);

            using var buffer = new MemoryStream();
            probe.Save(buffer, false);
            return buffer.Length;
        }
        // docs:end measure

        // Every row sets CompressContentStreams explicitly rather than leaving it alone. A table of
        // "the defaults" would go quietly wrong the day a default changed, and this one did change:
        // it used to be declared false under #if DEBUG and true otherwise, so the same code wrote a
        // materially larger file from a debug build than from a release one.
        var defaultCompression = new PdfDocument().Options.CompressContentStreams;

        // docs:begin compress-content
        var compressed = Measure(options => options.CompressContentStreams = true);
        var uncompressed = Measure(options => options.CompressContentStreams = false);
        var noCompression = Measure(options =>
        {
            options.CompressContentStreams = true;
            options.NoCompression = true;
        });
        // docs:end compress-content
        // docs:begin flate-mode
        var flateBest = Measure(options =>
        {
            options.CompressContentStreams = true;
            options.FlateEncodeMode = PdfFlateEncodeMode.BestCompression;
        });
        var flateFast = Measure(options =>
        {
            options.CompressContentStreams = true;
            options.FlateEncodeMode = PdfFlateEncodeMode.BestSpeed;
        });
        // docs:end flate-mode
        // docs:begin jpeg-flate
        var jpegFlate = Measure(options =>
        {
            options.CompressContentStreams = true;
            options.UseFlateDecoderForJpegImages = PdfUseFlateDecoderForJpegImages.Always;
        });
        var jpegAuto = Measure(options =>
        {
            options.CompressContentStreams = true;
            options.UseFlateDecoderForJpegImages = PdfUseFlateDecoderForJpegImages.Automatic;
        });
        // docs:end jpeg-flate
        // docs:begin cmyk
        var cmyk = Measure(options =>
        {
            options.CompressContentStreams = true;
            options.ColorMode = PdfColorMode.Cmyk;
        });
        // docs:end cmyk
        // docs:begin xref-stream
        var xrefStream = Measure(options =>
        {
            options.CompressContentStreams = true;
            options.CrossReferenceFormat = PdfCrossReferenceFormat.Stream;
        });
        // docs:end xref-stream

        // A page of drawing is mostly one large content stream and a handful of objects, which is
        // the shape a cross-reference stream has least to offer. Measured again over a document that
        // is mostly objects - a hundred nearly empty pages - because that is where the setting is
        // worth reaching for, and a table showing only the first number would teach the opposite of
        // what is true.
        // docs:begin many-objects
        long ManyObjects(PdfCrossReferenceFormat format)
        {
            using var probe = new PdfDocument();
            probe.Options.CompressContentStreams = true;
            probe.Options.CrossReferenceFormat = format;

            for (var number = 1; number <= 100; number++)
            {
                var page = probe.AddPage();
                using var gfx = XGraphics.FromPdfPage(page);
                gfx.DrawString($"Page {number}", body, XBrushes.Black, new XPoint(50, 60));
            }

            using var buffer = new MemoryStream();
            probe.Save(buffer, false);
            return buffer.Length;
        }

        var manyClassic = ManyObjects(PdfCrossReferenceFormat.Classic);
        var manyStream = ManyObjects(PdfCrossReferenceFormat.Stream);
        // docs:end many-objects

        // ----- the document the demo hands back -----

        var document = new PdfDocument();
        document.Info.Title = "Compress";

        // Page one is the content itself, so the reader can see that every measurement above was
        // taken over this and that none of the settings changed how it looks.
        Representative(document);

        var report = document.AddPage();
        using (var gfx = XGraphics.FromPdfPage(report))
        {
            var prose = new XTextFormatter(gfx);

            gfx.DrawString("What each setting costs", heading, XBrushes.Black, new XPoint(50, 60));

            prose.DrawString(
                "The page before this one, saved eight times under different options. Every one of "
                + "those files renders identically; the only difference between them is how many "
                + "bytes it takes to say the same thing. That is why this demo is a table - there "
                + "is nothing else to look at.",
                body, XBrushes.Black, new XRect(50, 80, 495, 50));

            (string Setting, long Bytes, string Note)[] rows =
            {
                ("CompressContentStreams = true", compressed, "The baseline every other row is measured against"),
                ("CompressContentStreams = false", uncompressed, "Operators written as readable text"),
                ("NoCompression = true", noCompression, "Nothing in the file is compressed at all"),
                ("FlateEncodeMode.BestCompression", flateBest, "Slower to save, smaller to keep"),
                ("FlateEncodeMode.BestSpeed", flateFast, "Faster to save, larger to keep"),
                ("UseFlateDecoderForJpegImages.Always", jpegFlate, "Flate over the already-compressed JPEG, whatever it costs"),
                ("UseFlateDecoderForJpegImages.Automatic", jpegAuto, "The same, kept only if it turned out smaller"),
                ("ColorMode.Cmyk", cmyk, "Four components per colour instead of three"),
                ("CrossReferenceFormat.Stream", xrefStream, "Barely moves a page that is mostly one content stream")
            };

            double y = 145;
            gfx.DrawString("setting", label, XBrushes.Black, new XPoint(50, y));
            gfx.DrawString("bytes", label, XBrushes.Black, new XPoint(280, y));
            gfx.DrawString("against the first row", label, XBrushes.Black, new XPoint(350, y));
            y += 18;

            foreach (var row in rows)
            {
                var delta = row.Bytes - compressed;
                gfx.DrawString(row.Setting, mono, XBrushes.Black, new XPoint(50, y));
                gfx.DrawString($"{row.Bytes:N0}", body, XBrushes.Black, new XPoint(280, y));
                gfx.DrawString(
                    delta == 0 ? "-" : $"{(delta > 0 ? "+" : "")}{delta:N0}",
                    body, delta > 0 ? XBrushes.Firebrick : XBrushes.SeaGreen, new XPoint(350, y));
                gfx.DrawString(row.Note, body, XBrushes.DimGray, new XPoint(50, y + 11));
                y += 28;
            }

            gfx.DrawString("The default used not to be a constant", label, XBrushes.Firebrick,
                new XPoint(50, y + 15));

            prose.DrawString(
                $"CompressContentStreams defaults to {defaultCompression.ToString().ToLowerInvariant()}, "
                + "and now does so in every build. It used to be declared false under #if DEBUG and "
                + "true otherwise, which made the size of the output a property of how the library "
                + "had been compiled rather than of the code calling it: the same program wrote a "
                + "materially larger PDF from a debug build, and two files could not be compared "
                + "without knowing which configuration each came from. Set it to false to get the "
                + "readable content stream back - that was the only thing the conditional bought.",
                body, XBrushes.Black, new XRect(50, y + 28, 495, 80));

            gfx.DrawString("What to make of the rest", label, XBrushes.Black, new XPoint(50, y + 105));

            prose.DrawString(
                "Turning compression off has a large and predictable cost, and it is worth doing "
                + "exactly once: an uncompressed PDF can be opened in a text editor and read, "
                + "which is the fastest way to find out what the library actually wrote. Ship the "
                + "compressed one.",
                body, XBrushes.Black, new XRect(50, y + 118, 495, 45));

            prose.DrawString(
                "UseFlateDecoderForJpegImages runs flate over a stream that is already compressed, "
                + "and whether that wins depends entirely on the picture - the number above is "
                + "this photograph's answer and not a general one. Always takes the flated form "
                + "whatever it costs, because some readers will not decode a bare DCT stream; "
                + "Automatic keeps it only when it came out smaller, which is the setting to reach "
                + "for if size is the reason you are here.",
                body, XBrushes.Black, new XRect(50, y + 168, 495, 60));

            prose.DrawString(
                "ColorMode decides what is written for every colour in the file - three components "
                + "or four. Switching to CMYK is what a press wants and what a screen does not; "
                + "the colours are converted on the way out, so a document built in RGB and saved "
                + "as CMYK is not the same colours, only the nearest ones.",
                body, XBrushes.Black, new XRect(50, y + 235, 495, 45));

            gfx.DrawString("One of those rows is measured unfairly", label, XBrushes.Black,
                new XPoint(50, y + 290));

            prose.DrawString(
                "CrossReferenceFormat, which barely moves the table above and moves a great deal on "
                + "the right document. That comparison is the next page, because it needs a second "
                + "document to make it against.",
                body, XBrushes.Black, new XRect(50, y + 304, 495, 34));
        }

        // A page of its own rather than the foot of the one before it. The table above ends near the
        // bottom margin already, and a block appended after it was drawn off the media box entirely -
        // painted, and invisible, which is the one kind of layout mistake nothing complains about.
        var objects = document.AddPage();
        using (var gfx = XGraphics.FromPdfPage(objects))
        {
            var prose = new XTextFormatter(gfx);

            gfx.DrawString("Where a cross-reference stream pays", heading, XBrushes.Black,
                new XPoint(50, 60));

            prose.DrawString(
                "CrossReferenceFormat.Stream gathers the objects that may be gathered into object "
                + "streams and compresses them together, and it has almost nothing to work on in the "
                + "measurement on the page before: one page of drawing is a single large content "
                + "stream and a dozen objects, and a content stream is not something an object stream "
                + "may hold. Measured over a document that is mostly objects instead - a hundred "
                + "nearly empty pages - the same setting reads differently.",
                body, XBrushes.Black, new XRect(50, 80, 495, 72));

            gfx.DrawString("format", label, XBrushes.Black, new XPoint(50, 170));
            gfx.DrawString("bytes", label, XBrushes.Black, new XPoint(280, 170));
            gfx.DrawString("against classic", label, XBrushes.Black, new XPoint(350, 170));

            gfx.DrawString("Classic, 100 pages", mono, XBrushes.Black, new XPoint(50, 190));
            gfx.DrawString($"{manyClassic:N0}", body, XBrushes.Black, new XPoint(280, 190));
            gfx.DrawString("-", body, XBrushes.DimGray, new XPoint(350, 190));

            gfx.DrawString("Stream, 100 pages", mono, XBrushes.Black, new XPoint(50, 208));
            gfx.DrawString($"{manyStream:N0}", body, XBrushes.Black, new XPoint(280, 208));
            gfx.DrawString(
                $"{(manyStream > manyClassic ? "+" : "")}{manyStream - manyClassic:N0}",
                body, manyStream > manyClassic ? XBrushes.Firebrick : XBrushes.SeaGreen,
                new XPoint(350, 208));

            gfx.DrawString("The same page, measured twice", mono, XBrushes.Black, new XPoint(50, 226));
            gfx.DrawString($"{compressed:N0} / {xrefStream:N0}", body, XBrushes.Black, new XPoint(280, 226));
            gfx.DrawString($"{xrefStream - compressed:N0}", body,
                xrefStream > compressed ? XBrushes.Firebrick : XBrushes.SeaGreen, new XPoint(350, 226));

            gfx.DrawString("What it costs", label, XBrushes.Black, new XPoint(50, 265));

            prose.DrawString(
                "A reader that understands PDF 1.5, which by now is all of them - and it is the one "
                + "option here PDF/A-1 refuses outright, because a cross-reference stream is a PDF "
                + "1.5 construction and PDF/A-1 is defined against 1.4. The Archive demo shows that "
                + "refusal in the writer's own words.",
                body, XBrushes.Black, new XRect(50, 280, 495, 48));

            gfx.DrawString("MaxObjectsPerObjectStream trades the other way", label, XBrushes.Black,
                new XPoint(50, 345));

            prose.DrawString(
                "A reader has to decompress a whole object stream to reach any one object in it, so a "
                + "larger number is a smaller file and a slower open. Acrobat uses 200 and so does "
                + "this. It means nothing at all unless CrossReferenceFormat is Stream, which is the "
                + "sort of option that is easy to set and hard to notice has done nothing.",
                body, XBrushes.Black, new XRect(50, 360, 495, 58));
        }
        #endregion

        return document;
    }
}
