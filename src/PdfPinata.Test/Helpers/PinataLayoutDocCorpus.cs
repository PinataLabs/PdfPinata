using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using PinataLayout.DocumentObjectModel;
using PinataLayout.DocumentObjectModel.Shapes;
using PinataLayout.Rendering;
using PdfPinata.Pdf.IO;

namespace PdfPinata.Test.Helpers;

/// <summary>
///   A corpus of PinataLayout documents covering the shapes of layout that a change to
///   <c>Area.GetFittingRect</c> could disturb, and the content streams they render to.
/// </summary>
/// <remarks>
///   Text flowing beside a shape means the area a line is laid out in stops being a rectangle. That
///   is a change in the middle of the layout engine, and layout regressions are silent: every word
///   is still on the page, a little way from where it was. So the documents that ask for no such
///   thing are pinned first, and re-checked after each step.
///   <para>
///   The content stream rather than the file, because the file carries a creation date and an
///   identifier that have nothing to do with layout.
///   </para>
/// </remarks>
internal static class PinataLayoutDocCorpus
{
    private const string Prose =
        "The quick brown fox jumps over the lazy dog, and having jumped it lands and looks about " +
        "for somewhere else to be, which takes rather longer than the jump did and is far less " +
        "impressive to watch from any distance at all.";

    /// <summary>What every document in the corpus is called, in order.</summary>
    internal static IReadOnlyList<string> Names
    {
        get
        {
            var names = new List<string>();
            foreach (var (name, _) in Documents())
                names.Add(name);
            return names;
        }
    }

    /// <summary>
    ///   Every document in the corpus, named so a failure says which one moved.
    /// </summary>
    private static IEnumerable<(string Name, Action<Document> Build)> Documents()
    {
        yield return ("flowed prose", FlowedProse);
        yield return ("justified with indents", JustifiedWithIndents);
        yield return ("paragraph across a page break", ParagraphAcrossAPageBreak);
        yield return ("table across a page break", TableAcrossAPageBreak);
        yield return ("text frame beside prose", TextFrameBesideProse);
        yield return ("frame the text ignores", FrameTheTextIgnores);
        yield return ("image between paragraphs", ImageBetweenParagraphs);
        yield return ("headers, footers and page fields", HeadersFootersAndPageFields);
        yield return ("lists", Lists);
        yield return ("two sections", TwoSections);
    }

    private static void FlowedProse(Document document)
    {
        var section = document.AddSection();
        for (var idx = 0; idx < 6; idx++)
            section.AddParagraph(Prose);
    }

    private static void JustifiedWithIndents(Document document)
    {
        var section = document.AddSection();
        for (var idx = 0; idx < 6; idx++)
        {
            var paragraph = section.AddParagraph(Prose);
            paragraph.Format.Alignment = ParagraphAlignment.Justify;
            paragraph.Format.FirstLineIndent = "1cm";
            paragraph.Format.LeftIndent = "0.5cm";
            paragraph.Format.RightIndent = "0.5cm";
        }
    }

    private static void ParagraphAcrossAPageBreak(Document document)
    {
        var section = document.AddSection();
        for (var idx = 0; idx < 40; idx++)
            section.AddParagraph(Prose);
    }

    private static void TableAcrossAPageBreak(Document document)
    {
        var section = document.AddSection();
        var table = section.AddTable();
        table.Borders.Width = 0.5;
        table.AddColumn("6cm");
        table.AddColumn("6cm");

        var heading = table.AddRow();
        heading.HeadingFormat = true;
        heading.Cells[0].AddParagraph("Column one");
        heading.Cells[1].AddParagraph("Column two");

        for (var idx = 0; idx < 45; idx++)
        {
            var row = table.AddRow();
            row.Cells[0].AddParagraph("Row " + idx);
            row.Cells[1].AddParagraph(Prose[..40]);
        }
    }

    private static void TextFrameBesideProse(Document document)
    {
        var section = document.AddSection();
        var frame = section.AddTextFrame();
        frame.Width = "4cm";
        frame.Height = "3cm";
        frame.RelativeVertical = RelativeVertical.Paragraph;
        frame.RelativeHorizontal = RelativeHorizontal.Margin;
        frame.Left = ShapePosition.Right;
        frame.WrapFormat.Style = WrapStyle.TopBottom;
        frame.AddParagraph("A frame with words in it.");

        for (var idx = 0; idx < 8; idx++)
            section.AddParagraph(Prose);
    }

    private static void FrameTheTextIgnores(Document document)
    {
        var section = document.AddSection();
        var frame = section.AddTextFrame();
        frame.Width = "4cm";
        frame.Height = "3cm";
        frame.RelativeVertical = RelativeVertical.Paragraph;
        frame.WrapFormat.Style = WrapStyle.Through;
        frame.AddParagraph("Overlapping on purpose.");

        for (var idx = 0; idx < 8; idx++)
            section.AddParagraph(Prose);
    }

    private static void ImageBetweenParagraphs(Document document)
    {
        var section = document.AddSection();
        section.AddParagraph(Prose);

        var image = section.AddImage(ImageSource.FromFile(
            PathHelper.GetInstance().GetAssetPath("frog-and-toad.jpg")));
        image.Width = "5cm";
        image.WrapFormat.Style = WrapStyle.TopBottom;

        for (var idx = 0; idx < 6; idx++)
            section.AddParagraph(Prose);
    }

    private static void HeadersFootersAndPageFields(Document document)
    {
        var section = document.AddSection();
        section.PageSetup.StartingNumber = 1;
        section.Headers.Primary.AddParagraph("A header");
        var footer = section.Footers.Primary.AddParagraph("Page ");
        footer.AddPageField();
        footer.AddText(" of ");
        footer.AddNumPagesField();

        for (var idx = 0; idx < 30; idx++)
            section.AddParagraph(Prose);
    }

    private static void Lists(Document document)
    {
        var section = document.AddSection();
        for (var idx = 0; idx < 8; idx++)
        {
            var item = section.AddParagraph("Item " + idx + ": " + Prose);
            item.Format.ListInfo = new ListInfo { ListType = ListType.BulletList1 };
        }
    }

    private static void TwoSections(Document document)
    {
        var first = document.AddSection();
        for (var idx = 0; idx < 5; idx++)
            first.AddParagraph(Prose);

        var second = document.AddSection();
        second.PageSetup.Orientation = Orientation.Landscape;
        for (var idx = 0; idx < 5; idx++)
            second.AddParagraph(Prose);
    }

    /// <summary>
    ///   Every document in the corpus, rendered, with the content of each page of each.
    /// </summary>
    /// <param name="tagged">
    ///   Whether the renderer describes what it draws as well as drawing it. Both are wanted: the
    ///   pinned baseline is what these documents drew before there was any such thing as tagging, so
    ///   the way to show that tagging moved nothing is to render with it and find the same drawing
    ///   underneath the marks.
    /// </param>
    internal static string OfEveryDocument(bool tagged = true)
    {
        var report = new StringBuilder();

        foreach (var (name, build) in Documents())
        {
            report.Append("=== ").Append(name).Append(" ===\n");

            var document = new Document();
            build(document);

            var renderer = new PdfDocumentRenderer(true) { Document = document, TagContent = tagged };
            renderer.RenderDocument();

            using var stream = new MemoryStream();
            renderer.PdfDocument.Save(stream, false);
            stream.Position = 0;

            var saved = PdfPinata.Pdf.IO.PdfReader.Open(stream, PdfDocumentOpenMode.Modify);
            for (var page = 0; page < saved.PageCount; page++)
            {
                report.Append("--- page ").Append(page + 1).Append(" ---\n");
                report.Append(Encoding.ASCII.GetString(PageContent.Of(saved.Pages[page])).Replace("\r\n", "\n"));
                report.Append('\n');
            }
        }

        return report.ToString();
    }
}
