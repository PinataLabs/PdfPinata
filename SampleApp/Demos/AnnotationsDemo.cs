using System;
using System.Collections.Generic;
using System.Text;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Advanced;
using PdfPinata.Pdf.Annotations;
using SampleApp.Infrastructure;

namespace SampleApp.Demos;

/// <summary>
///   Every annotation this library can write, over text that gives each one something to mark.
/// </summary>
/// <remarks>
///   The last page is a parity table against PDFKit's annotation API, because the useful thing to
///   know about an annotation library is as often what it will not do. Two of the eleven methods
///   PDFKit documents have no counterpart here, and saying so on the page is cheaper than letting
///   somebody find out by writing the call.
/// </remarks>
internal sealed class AnnotationsDemo : PdfDemo
{
    public AnnotationsDemo() : base() { }

    public override string Name => "Annotations";

    public override string Summary => "Notes, links, text markup, stamps and attachments.";

    public override IReadOnlyList<string> Shows => new[]
    {
        "The four text markup annotations - highlight, underline, strike out, squiggly",
        "A markup that follows text across a line break, as two quadrilaterals in one annotation",
        "Note annotations: the seven icons, an open popup, colour and opacity",
        "Links: to the web, to a page, and to a named destination - PDFKit's link and goTo",
        "A file attachment carrying its bytes, and a rubber stamp",
        "Which of PDFKit's annotation methods have no counterpart here",
    };

    public override int PageCount => 3;

    protected override PdfDocument Build(DemoContext context)
    {
        #region example
        const string Sans = "Liberation Sans";

        PdfDocument document = new PdfDocument();
        document.Info.Title = "Annotations";

        XFont titleFont = new XFont(Sans, 18, XFontStyle.Bold);
        XFont headingFont = new XFont(Sans, 9, XFontStyle.Bold);
        XFont body = new XFont(Sans, 11);
        XFont noteFont = new XFont(Sans, 7.5);

        // ---- Page one: text markup, and notes ----------------------------------------
        PdfPage page = document.AddPage();
        XGraphics gfx = XGraphics.FromPdfPage(page);

        void Title(XGraphics on, string text)
        {
            on.DrawString(text, titleFont, XBrushes.Black, new XPoint(56, 68));
            on.DrawLine(new XPen(XColors.SteelBlue, 1.5), 56, 78, 539, 78);
        }

        void Heading(XGraphics on, string text, double y)
        {
            on.DrawString(text.ToUpperInvariant(), headingFont, XBrushes.SteelBlue,
                new XPoint(56, y));
            on.DrawLine(XPens.LightGray, 56, y + 5, 539, y + 5);
        }

        void Note(XGraphics on, string text, double y)
        {
            on.DrawString(text, noteFont, XBrushes.DimGray, new XPoint(56, y));
        }

        Title(gfx, "Annotations");
        Note(gfx, "An annotation is not page content. Nothing here is drawn by XGraphics - a reader "
            + "paints it, and can hide it.", 94);

        // A markup annotation covers quadrilaterals given in default page space, so the run of
        // text to be marked has to be measured and then converted out of drawing coordinates.
        // The ratio below is how PdfPinata itself turns a font into a baseline offset.
        double AscentOf(XFont font) => font.GetHeight() * font.CellAscent / font.CellSpace;

        XRect RunOf(string line, string run, XPoint baseline, XFont font)
        {
            int start = line.IndexOf(run, StringComparison.Ordinal);
            double before = gfx.MeasureString(line.Substring(0, start), font).Width;
            double width = gfx.MeasureString(run, font).Width;
            double ascent = AscentOf(font);

            // The box a reader would draw a selection over: from the ascender down past the
            // baseline by what is left of the line.
            return new XRect(baseline.X + before, baseline.Y - ascent, width,
                font.GetHeight());
        }

        // The annotation has to be on the page before a quad is added: AddQuad builds the
        // /QuadPoints array against the document that owns it, and rebuilds the appearance.
        T Mark<T>(T annotation, string line, string run, XPoint baseline)
            where T : PdfTextMarkupAnnotation
        {
            page.Annotations.Add(annotation);
            annotation.AddQuad(gfx.Transformer.WorldToDefaultPage(RunOf(line, run, baseline, body)));
            return annotation;
        }

        Heading(gfx, "Text markup", 124);

        (string Line, string Run, Func<PdfTextMarkupAnnotation> Make, string Caption)[] markups =
        {
            ("Highlight marks a run of text with a wash of colour.", "a wash of colour",
                () => new PdfHighlightAnnotation(), "PdfHighlightAnnotation - PDFKit's highlight()"),
            ("Underline draws a line along the foot of the run.", "along the foot",
                () => new PdfUnderlineAnnotation(), "PdfUnderlineAnnotation - PDFKit's underline()"),
            ("Strike out draws through the middle of it instead.", "through the middle",
                () => new PdfStrikeOutAnnotation(), "PdfStrikeOutAnnotation - PDFKit's strike()"),
            ("Squiggly draws the wavy line a spell checker uses.", "the wavy line",
                () => new PdfSquigglyAnnotation(), "PdfSquigglyAnnotation - PDFKit has no squiggly()"),
        };

        double y = 154;
        foreach ((string Line, string Run, Func<PdfTextMarkupAnnotation> Make, string Caption) each
            in markups)
        {
            XPoint baseline = new XPoint(56, y);
            gfx.DrawString(each.Line, body, XBrushes.Black, baseline);
            Mark(each.Make(), each.Line, each.Run, baseline);
            Note(gfx, each.Caption, y + 11);
            y += 40;
        }

        // Colour and opacity belong to the annotation rather than to the drawing, so the same
        // text can be marked twice over without the page content knowing.
        const string Twice = "One run, marked twice: green underneath and a strike over the top.";
        XPoint twiceAt = new XPoint(56, y);
        gfx.DrawString(Twice, body, XBrushes.Black, twiceAt);
        PdfHighlightAnnotation green = Mark(new PdfHighlightAnnotation(), Twice, "marked twice",
            twiceAt);
        green.Color = XColors.LightGreen;
        green.Contents = "Highlight with a colour of its own";
        Mark(new PdfStrikeOutAnnotation(), Twice, "marked twice", twiceAt).Color = XColors.Crimson;
        Note(gfx, "Color is the annotation's, not the page's. Opacity applies to the whole markup.",
            y + 11);
        y += 40;

        // One annotation, two quadrilaterals. This is how a markup follows a selection that
        // wraps: the quads are the lines, and /Rect becomes the box around both.
        const string First = "A markup that runs past the end of a line is one annotation with";
        const string Second = "two quadrilaterals in it, not two annotations.";
        XPoint firstAt = new XPoint(56, y);
        XPoint secondAt = new XPoint(56, y + 16);
        gfx.DrawString(First, body, XBrushes.Black, firstAt);
        gfx.DrawString(Second, body, XBrushes.Black, secondAt);

        PdfHighlightAnnotation wrapped = new PdfHighlightAnnotation();
        page.Annotations.Add(wrapped);
        wrapped.Color = XColors.Gold;
        wrapped.Opacity = 0.55;
        wrapped.Title = "PdfPinata";
        wrapped.Contents = "Two quads, one annotation.";
        wrapped.AddQuad(gfx.Transformer.WorldToDefaultPage(
            RunOf(First, "past the end of a line is one annotation with", firstAt, body)));
        wrapped.AddQuad(gfx.Transformer.WorldToDefaultPage(
            RunOf(Second, "two quadrilaterals in it", secondAt, body)));
        Note(gfx, "AddQuad twice. /Rect is recomputed as the box around every quad.", y + 27);

        // ---- Notes -------------------------------------------------------------------
        Heading(gfx, "Notes - PDFKit's note()", 424);
        Note(gfx, "A note has no appearance of its own: the reader draws the icon, at whatever "
            + "size it likes.", 444);

        PdfTextAnnotationIcon[] icons =
        {
            PdfTextAnnotationIcon.Comment,
            PdfTextAnnotationIcon.Note,
            PdfTextAnnotationIcon.Help,
            PdfTextAnnotationIcon.Key,
            PdfTextAnnotationIcon.Insert,
            PdfTextAnnotationIcon.NewParagraph,
            PdfTextAnnotationIcon.Paragraph,
        };

        for (int index = 0; index < icons.Length; index++)
        {
            double x = 56 + index * 68;

            PdfTextAnnotation sticky = new PdfTextAnnotation();
            page.Annotations.Add(sticky);
            sticky.Icon = icons[index];
            sticky.Title = "PdfPinata";
            sticky.Subject = icons[index].ToString();
            sticky.Contents = $"The {icons[index]} icon. Every note carries a title, a subject and "
                + "this text, which is what a reader shows in the popup.";
            sticky.CreationDate = new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc);
            sticky.Color = XColors.Goldenrod;
            sticky.Rectangle = new PdfRectangle(
                gfx.Transformer.WorldToDefaultPage(new XRect(x, 456, 20, 20)));

            gfx.DrawString(icons[index].ToString(), noteFont, XBrushes.Black, new XPoint(x, 494));
        }

        PdfTextAnnotation opened = new PdfTextAnnotation();
        page.Annotations.Add(opened);
        opened.Icon = PdfTextAnnotationIcon.Comment;
        opened.Open = true;
        opened.Color = XColors.CornflowerBlue;
        opened.Opacity = 0.85;
        opened.Title = "Open on arrival";
        opened.Contents = "Open = true, so a reader shows the popup without being asked. "
            + "Color tints the note and Opacity applies to the whole annotation.";
        opened.Rectangle = new PdfRectangle(
            gfx.Transformer.WorldToDefaultPage(new XRect(56, 510, 20, 20)));
        Note(gfx, "Open = true on this one - its popup should already be showing.", 544);

        // The place page two links back to. A named destination is a name in the document's
        // name tree, so a link can point at it without knowing a page number.
        gfx.AddNamedDestination("markup", new XPoint(56, 124));

        // ---- Page two: links, attachments, stamps ------------------------------------
        PdfPage second = document.AddPage();
        XGraphics secondGfx = XGraphics.FromPdfPage(second);

        Title(secondGfx, "Links, attachments and stamps");

        Heading(secondGfx, "Links - PDFKit's link() and goTo()", 116);

        double linkY = 146;
        void Link(string label, string caption, Action<XRect> add)
        {
            XSize size = secondGfx.MeasureString(label, body);
            secondGfx.DrawString(label, body, XBrushes.MediumBlue, new XPoint(56, linkY));

            // The underline is drawn by hand: a link annotation is a hot area, not a decoration.
            secondGfx.DrawLine(new XPen(XColors.MediumBlue, 0.6),
                56, linkY + 2, 56 + size.Width, linkY + 2);

            double ascent = AscentOf(body);
            add(new XRect(56, linkY - ascent, size.Width, body.GetHeight()));

            Note(secondGfx, caption, linkY + 13);
            linkY += 44;
        }

        Link("The PdfPinata repository",
            "gfx.AddWebLink(rect, url) - a URI action. PDFKit calls this link().",
            rect => secondGfx.AddWebLink(rect, "https://github.com/PinataLabs/PdfPinata"));

        Link("Jump to the parity table on page three",
            "gfx.AddDocumentLink(rect, 3) - a GoTo by one-based page number.",
            rect => secondGfx.AddDocumentLink(rect, 3));

        Link("Back to the text markup on page one",
            "gfx.AddNamedLink(rect, \"markup\") against gfx.AddNamedDestination on page one - "
            + "PDFKit's goTo().",
            rect => secondGfx.AddNamedLink(rect, "markup"));

        // A link annotation is a PdfLinkAnnotation like any other, so the returned object can
        // still be given the fields every annotation has.
        XSize titledSize = secondGfx.MeasureString("A link with a tooltip", body);
        secondGfx.DrawString("A link with a tooltip", body, XBrushes.MediumBlue,
            new XPoint(56, linkY));
        secondGfx.DrawLine(new XPen(XColors.MediumBlue, 0.6),
            56, linkY + 2, 56 + titledSize.Width, linkY + 2);
        PdfLinkAnnotation described = secondGfx.AddWebLink(
            new XRect(56, linkY - AscentOf(body), titledSize.Width, body.GetHeight()),
            "https://www.pdfa.org/");
        described.Contents = "Shown as a tooltip while the pointer is over the link.";
        Note(secondGfx, "AddWebLink returns the annotation, so /Contents can be set on it "
            + "afterwards.", linkY + 13);

        // ---- An attachment -----------------------------------------------------------
        Heading(secondGfx, "File attachment - PDFKit's fileAnnotation()", 330);

        byte[] payload = Encoding.UTF8.GetBytes(
            "This file is carried inside the PDF, as the /EF stream of a file specification.\r\n"
            + "Open the paperclip on the page to save it out again.\r\n");

        PdfEmbeddedFile embedded = new PdfEmbeddedFile(document, payload);
        embedded.MimeType = "text/plain";

        PdfFileSpecification specification =
            new PdfFileSpecification(document, "readme.txt", embedded);

        PdfFileAttachmentAnnotation attachment = new PdfFileAttachmentAnnotation(document);
        attachment.File = specification;
        attachment.Icon = PdfFileAttachmentAnnotation.IconType.Paperclip;
        attachment.Title = "PdfPinata";
        attachment.Contents = "readme.txt, carried inside this document.";
        attachment.Rectangle = new PdfRectangle(
            secondGfx.Transformer.WorldToDefaultPage(new XRect(56, 350, 18, 18)));
        second.Annotations.Add(attachment);

        Note(secondGfx, "PdfEmbeddedFile holds the bytes, PdfFileSpecification names them, and the "
            + "annotation points at it.", 384);
        Note(secondGfx, "The constructor sets PdfAnnotationFlags.Locked, so a reader will not let "
            + "it be dragged off the page.", 396);
        Note(secondGfx, "Like a note's, the paperclip is the reader's own drawing - so a renderer "
            + "that paints only appearance streams shows nothing above.", 408);

        // ---- A rubber stamp ----------------------------------------------------------
        Heading(secondGfx, "Rubber stamp - PDFKit has no equivalent", 430);

        PdfRubberStampAnnotationIcon[] stamps =
        {
            PdfRubberStampAnnotationIcon.Draft,
            PdfRubberStampAnnotationIcon.Confidential,
            PdfRubberStampAnnotationIcon.ForComment,
            PdfRubberStampAnnotationIcon.Final,
        };

        for (int index = 0; index < stamps.Length; index++)
        {
            double x = 56 + index * 120;

            PdfRubberStampAnnotation stamp = new PdfRubberStampAnnotation(document);
            stamp.Icon = stamps[index];
            stamp.Title = "PdfPinata";
            stamp.Contents = stamps[index] + " stamp";
            stamp.Rectangle = new PdfRectangle(
                secondGfx.Transformer.WorldToDefaultPage(new XRect(x, 450, 104, 34)));
            second.Annotations.Add(stamp);

            secondGfx.DrawString(stamps[index].ToString(), noteFont, XBrushes.Black,
                new XPoint(x, 498));
        }

        Note(secondGfx, "Fifteen standard names, drawn by the reader. A stamp with artwork of its "
            + "own would need an appearance stream.", 520);

        // ---- Page three: what PDFKit has that this does not ---------------------------
        PdfPage third = document.AddPage();
        XGraphics thirdGfx = XGraphics.FromPdfPage(third);

        Title(thirdGfx, "Parity with PDFKit's annotations");
        Note(thirdGfx, "pdfkit.org/docs/annotations.html documents eleven methods. Nine of them "
            + "have something here; two do not.", 94);

        XFont mono = new XFont("Source Code Pro", 8);

        (string PdfKit, string Here)[] parity =
        {
            ("note(x, y, w, h, contents)", "PdfTextAnnotation"),
            ("link(x, y, w, h, url)", "gfx.AddWebLink / PdfLinkAnnotation.CreateWebLink"),
            ("goTo(x, y, w, h, name)", "gfx.AddNamedLink / gfx.AddDocumentLink"),
            ("highlight(x, y, w, h)", "PdfHighlightAnnotation"),
            ("underline(x, y, w, h)", "PdfUnderlineAnnotation"),
            ("strike(x, y, w, h)", "PdfStrikeOutAnnotation"),
            ("fileAnnotation(x, y, w, h, file)", "PdfFileAttachmentAnnotation"),
            ("lineAnnotation(x1, y1, x2, y2)", "MISSING - no /Line annotation"),
            ("rectAnnotation(x, y, w, h)", "PdfSquareAnnotation"),
            ("ellipseAnnotation(x, y, w, h)", "PdfCircleAnnotation"),
            ("textAnnotation(x, y, w, h, text)", "MISSING - no /FreeText annotation"),
        };

        double rowY = 132;
        thirdGfx.DrawString("PDFKit", headingFont, XBrushes.SteelBlue, new XPoint(56, rowY));
        thirdGfx.DrawString("PdfPinata", headingFont, XBrushes.SteelBlue, new XPoint(260, rowY));
        rowY += 6;
        thirdGfx.DrawLine(XPens.LightGray, 56, rowY, 539, rowY);
        rowY += 18;

        foreach ((string PdfKit, string Here) row in parity)
        {
            bool missing = row.Here.StartsWith("MISSING", StringComparison.Ordinal);
            XBrush brush = missing ? XBrushes.Crimson : XBrushes.Black;

            thirdGfx.DrawString(row.PdfKit, mono, XBrushes.Black, new XPoint(56, rowY));
            thirdGfx.DrawString(row.Here, mono, brush, new XPoint(260, rowY));
            rowY += 17;
        }

        rowY += 20;
        Heading(thirdGfx, "And what this has that PDFKit does not", rowY);
        rowY += 26;

        (string What, string Why)[] extras =
        {
            ("PdfSquigglyAnnotation", "the fourth text markup subtype"),
            ("PdfRubberStampAnnotation", "fifteen standard stamp names"),
            ("PdfAnnotation.Opacity", "/CA, applied to the whole annotation"),
            ("PdfAnnotation.Flags", "Hidden, Print, Locked, NoZoom and the rest"),
            ("PdfTextMarkupAnnotation.AddQuad", "many quads under one annotation"),
        };

        foreach ((string What, string Why) row in extras)
        {
            thirdGfx.DrawString(row.What, mono, XBrushes.Black, new XPoint(56, rowY));
            thirdGfx.DrawString(row.Why, noteFont, XBrushes.DimGray, new XPoint(260, rowY));
            rowY += 17;
        }

        rowY += 22;
        thirdGfx.DrawString(
            "The two missing subtypes are appearance-bearing: a viewer will not draw a /Line from",
            noteFont, XBrushes.DimGray, new XPoint(56, rowY));
        rowY += 12;
        thirdGfx.DrawString(
            "its endpoints alone, so adding them means writing appearance streams the way",
            noteFont, XBrushes.DimGray, new XPoint(56, rowY));
        rowY += 12;
        thirdGfx.DrawString(
            "PdfTextMarkupAnnotation already does. Until then, draw the shape with XGraphics.",
            noteFont, XBrushes.DimGray, new XPoint(56, rowY));
        rowY += 18;
        thirdGfx.DrawString(
            "Nor can a caller supply one: PdfAnnotation is abstract with no public way to set",
            noteFont, XBrushes.DimGray, new XPoint(56, rowY));
        rowY += 12;
        thirdGfx.DrawString(
            "/Subtype, PdfGenericAnnotation is internal, and PdfAnnotations.Add takes neither -",
            noteFont, XBrushes.DimGray, new XPoint(56, rowY));
        rowY += 12;
        thirdGfx.DrawString(
            "so a subtype this library has no class for cannot be added through the typed API.",
            noteFont, XBrushes.DimGray, new XPoint(56, rowY));
        #endregion

        return document;
    }
}
