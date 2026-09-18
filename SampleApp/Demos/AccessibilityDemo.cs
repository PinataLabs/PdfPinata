using System;
using System.Collections.Generic;
using System.IO;
using PinataLayout.DocumentObjectModel;
using PinataLayout.DocumentObjectModel.Shapes;
using PinataLayout.DocumentObjectModel.Tables;
using PinataLayout.Rendering;
using PdfPinata.Pdf;
using SampleApp.Infrastructure;

namespace SampleApp.Demos;

/// <summary>
///   A document that says what it is rather than only how it looks: a structure tree MigraDoc
///   builds for itself, and the PDF/UA-1 claim the writer refuses to make unless it is true.
/// </summary>
internal sealed class AccessibilityDemo : PdfDemo
{
    public AccessibilityDemo() : base() { }

    public override string Name => "Accessibility";

    public override string Summary => "Tagged output, and a PDF/UA-1 claim that is enforced rather than stamped on.";

    public override IReadOnlyList<string> Shows => new[]
    {
        "PdfDocumentRenderer.TagContent - true by default, so this document is tagged already",
        "Headings becoming /H1../H6 from the same OutlineLevel that makes them bookmarks",
        "A table whose heading row is /TH with /Scope /Column, and Table.Summary as /Summary",
        "Image.AlternativeText, which decides between a described /Figure and an artifact",
        "PdfUAConformance.PdfUA1 - the claim, and the six rules the writer holds it to",
        "The refusal messages themselves, caught from documents deliberately built wrong",
    };

    public override int PageCount => 2;

    protected override PdfDocument Build(DemoContext context)
    {
        #region example
        Document report = new Document();

        Style normal = report.Styles[StyleNames.Normal];
        normal.Font.Name = "Liberation Serif";
        normal.Font.Size = 10.5;
        normal.ParagraphFormat.SpaceAfter = Unit.FromPoint(6);

        Style heading1 = report.Styles[StyleNames.Heading1];
        heading1.Font.Name = "Liberation Sans";
        heading1.Font.Size = 17;
        heading1.Font.Bold = true;
        heading1.ParagraphFormat.SpaceBefore = Unit.FromPoint(16);
        heading1.ParagraphFormat.SpaceAfter = Unit.FromPoint(7);

        // The same property that puts a heading in the bookmark panel is what gives it its
        // structure type: /H1 from Level1, and so on down to /H6. Nothing else has to be said.
        heading1.ParagraphFormat.OutlineLevel = OutlineLevel.Level1;

        Style heading2 = report.Styles[StyleNames.Heading2];
        heading2.Font.Name = "Liberation Sans";
        heading2.Font.Size = 12.5;
        heading2.Font.Bold = true;
        heading2.ParagraphFormat.SpaceBefore = Unit.FromPoint(12);
        heading2.ParagraphFormat.OutlineLevel = OutlineLevel.Level2;

        Style caption = report.Styles.AddStyle("Caption", StyleNames.Normal);
        caption.Font.Size = 8.5;
        caption.Font.Italic = true;
        caption.Font.Color = Colors.DimGray;

        Section section = report.AddSection();
        section.PageSetup.TopMargin = Unit.FromCentimeter(2.2);
        section.PageSetup.BottomMargin = Unit.FromCentimeter(2);

        // A running head is decoration, not content. The renderer draws it inside an artifact scope
        // and nothing inside one is tagged at all - so this line does not appear in the tree, and a
        // reader announcing the document does not read it out once per page.
        Paragraph runningHead = section.Headers.Primary.AddParagraph("Accessible output");
        runningHead.Format.Font.Size = 8;
        runningHead.Format.Font.Color = Colors.DimGray;
        runningHead.Format.Alignment = ParagraphAlignment.Right;

        // ----- what the tree is for -----

        section.AddParagraph("A document that says what it is").Style = StyleNames.Heading1;

        section.AddParagraph(
            "Everything on this page is in a structure tree, and nothing here asked for one. "
            + "PdfDocumentRenderer.TagContent defaults to true, so MigraDoc records what it is "
            + "drawing as it draws it: this paragraph is a /P, the line above it is an /H1, and "
            + "the table further down is a /Table whose first row is headers.");

        section.AddParagraph(
            "The tree is what a screen reader, a reflowing viewer and a text extractor all read "
            + "instead of guessing from coordinates. Without one, a page is a bag of glyphs at "
            + "positions, and the order they were painted in is the only order there is - which "
            + "is drawing order, not reading order, and on a two-column page those are different.");

        section.AddParagraph("What the renderer maps").Style = StyleNames.Heading2;

        section.AddParagraph(
            "A section becomes a /Sect and a paragraph a /P, with /H1 to /H6 taken from "
            + "Format.OutlineLevel. A run of list paragraphs becomes one /L of /LI, each with its "
            + "bullet as the /Lbl. A hyperlink becomes a /Link that also carries a description on "
            + "the annotation. Headers, footers, borders and shading become artifacts, which is "
            + "the tree's way of saying \"this is decoration, skip it\".");

        // ----- the table -----

        section.AddParagraph("A table that can be navigated").Style = StyleNames.Heading1;

        section.AddParagraph(
            "A sighted reader finds the meaning of a cell by looking up its column. A reader "
            + "hearing the document has to be told, which is what /TH and its /Scope are for: a "
            + "header cell announced before the value under it turns a grid of numbers back into "
            + "sentences.");

        Table table = section.AddTable();
        table.Borders.Width = 0.5;
        table.Borders.Color = Colors.Gray;
        table.LeftPadding = Unit.FromPoint(4);
        table.RightPadding = Unit.FromPoint(4);
        table.TopPadding = Unit.FromPoint(3);
        table.BottomPadding = Unit.FromPoint(3);

        // Written to /Summary on the /Table element. A caption describes a table to somebody who
        // can see its shape; a summary describes the shape itself, which is what somebody who
        // cannot see it is missing.
        table.Summary =
            "Quarterly revenue and headcount for three regions. Columns: region, revenue in "
            + "thousands of pounds, and headcount.";

        table.AddColumn(Unit.FromCentimeter(5));
        table.AddColumn(Unit.FromCentimeter(4));
        table.AddColumn(Unit.FromCentimeter(3));

        Row header = table.AddRow();

        // The one flag that makes the difference. A heading row's cells are tagged /TH with
        // /Scope /Column rather than /TD, and it is also what repeats the row over a page break.
        header.HeadingFormat = true;
        header.Format.Font.Bold = true;
        header.Shading.Color = Colors.WhiteSmoke;
        header.Cells[0].AddParagraph("Region");
        header.Cells[1].AddParagraph("Revenue (GBP thousand)");
        header.Cells[2].AddParagraph("Headcount");

        (string Region, string Revenue, string People)[] figures =
        {
            ("North", "1,240", "38"),
            ("Midlands", "980", "31"),
            ("South West", "1,505", "44"),
        };

        foreach ((string Region, string Revenue, string People) each in figures)
        {
            Row row = table.AddRow();
            row.Cells[0].AddParagraph(each.Region);
            row.Cells[1].AddParagraph(each.Revenue);
            row.Cells[2].AddParagraph(each.People);
        }

        section.AddParagraph(
            "Row.HeadingFormat is the whole of it. It was already there to repeat the row over a "
            + "page break, and it turns out to be exactly the header-versus-data distinction the "
            + "tree needs - which is lucky, because that association is otherwise the hardest part "
            + "of tagging a table.").Style = "Caption";

        // ----- figures -----

        section.AddParagraph("A picture, described or dismissed").Style = StyleNames.Heading1;

        section.AddParagraph(
            "There are two honest things to do with an image and no third. Describe it, and it is "
            + "a /Figure with /Alt. Say it is decoration, and it is an artifact a reader skips. "
            + "What conforms to nothing is a /Figure with nothing to say, and that is the one thing "
            + "a document produces by accident.");

        Paragraph described = section.AddParagraph();
        described.Format.Alignment = ParagraphAlignment.Center;
        Image photograph = described.AddImage(ImageSource.FromStream(
            "described.jpg", () => Assets.Open(Assets.ImagePrefix + "frog-and-toad.jpg")));
        photograph.Width = Unit.FromCentimeter(6);
        photograph.LockAspectRatio = true;

        // Set, so this one is a /Figure with an /Alt. Left unset, MigraDoc draws the image as an
        // artifact instead - it will not produce a figure with nothing to say, which is why the
        // refusal on the last page had to be provoked by reaching past the renderer.
        photograph.AlternativeText =
            "A photograph of a frog and a toad sitting side by side on a mossy log.";

        section.AddParagraph(
            "The image above carries AlternativeText and is therefore a described /Figure. Set it "
            + "to nothing and the same call draws the same picture as an artifact - still visible, "
            + "and honestly marked as saying nothing.").Style = "Caption";

        // ----- the claim -----

        section.AddParagraph("Claiming PDF/UA-1").Style = StyleNames.Heading1;

        section.AddParagraph(
            "Tagging a document and conforming to PDF/UA are not the same thing, and the gap is "
            + "mostly rules that are cheap to check and easy to break. Setting "
            + "Options.UAConformance to PdfUA1 makes the claim, and the writer then walks the "
            + "document before writing a byte and throws on the first rule it breaks.");

        Paragraph link = section.AddParagraph("The rules are listed on PdfUaValidator, and ");
        link.AddHyperlink("https://github.com/PinataLabs/PdfPinata", HyperlinkType.Web)
            .AddFormattedText("this link", TextFormat.Underline);
        link.AddText(
            " is itself one of them: a /Link with no description leaves a reader able to say only "
            + "\"link\", so the annotation gets /Contents from the hyperlink's own text and is "
            + "joined to the tree with a /StructParent.");

        section.AddParagraph(
            "Two things the writer settles rather than demands, because there is exactly one right "
            + "answer and refusing over it would teach nobody anything: DisplayDocTitle is set, so "
            + "a reader announces the title rather than the file name, and /Tabs is set to /S on "
            + "every page, so the tab key walks the structure rather than the order the annotations "
            + "happen to sit in.");

        section.AddParagraph("What a successful save is not").Style = StyleNames.Heading2;

        section.AddParagraph(
            "It is not a validator's verdict. What can be settled by looking at the document is "
            + "checked; what cannot is not. That no content sits outside the tree, that the reading "
            + "order makes sense, that headings do not skip a level - none of those are here, and "
            + "a page imported from an untagged document passes every rule and conforms to nothing. "
            + "veraPDF has the last word.");

        // ----- the refusals, provoked on purpose -----

        section.AddParagraph("The refusals, in their own words").Style = StyleNames.Heading1;

        section.AddParagraph(
            "Each line below is a real exception message, caught from a document built to break "
            + "one rule and then asked to save. Nothing here is quoted from documentation - the "
            + "text is whatever the library said when this demo was run.");

        foreach ((string Broken, string Message) refusal in Refusals())
        {
            Paragraph what = section.AddParagraph(refusal.Broken);
            what.Format.Font.Bold = true;
            what.Format.SpaceBefore = Unit.FromPoint(8);
            what.Format.SpaceAfter = Unit.FromPoint(1);

            Paragraph said = section.AddParagraph(refusal.Message);
            said.Format.Font.Name = "Source Code Pro";
            said.Format.Font.Size = 7.5;
            said.Format.LeftIndent = Unit.FromCentimeter(0.5);
        }

        PdfDocumentRenderer renderer = new PdfDocumentRenderer(unicode: true)
        {
            Document = report,

            // The default, written out because this demo is about it. Set it to false and every
            // structure element above disappears - and then the claim below is refused.
            TagContent = true,

            // An RFC 3066 tag, and a rule of its own: a reader that does not know the language
            // cannot choose a voice to read the document in.
            Language = "en-GB",
        };

        renderer.RenderDocument();

        PdfDocument document = renderer.PdfDocument;

        // A rule rather than a nicety. The title is what a reader announces the document as, and
        // the file name standing in for it is the failure the rule exists to stop.
        document.Info.Title = "Accessible output";
        document.Info.Author = "PdfPinata sample app";
        document.Info.Subject = "A tagged document claiming PDF/UA-1";

        // The claim itself. Everything above had to be true before this line could be written.
        document.Options.UAConformance = PdfUAConformance.PdfUA1;
        #endregion

        return document;
    }

    /// <summary>
    ///   Builds a document to break each rule in turn, asks it to save, and collects what it said.
    /// </summary>
    /// <remarks>
    ///   Provoked rather than transcribed. A demo that printed the messages from a string array
    ///   would go quietly stale the day one of them was reworded, and the whole claim of this page
    ///   is that the library says something useful at the moment the mistake is made.
    /// </remarks>
    static IEnumerable<(string Broken, string Message)> Refusals()
    {
        yield return Refusal("Not tagged at all",
            renderer => renderer.TagContent = false, _ => { });

        yield return Refusal("No title",
            _ => { }, document => document.Info.Title = "");

        yield return Refusal("No declared language",
            renderer => renderer.Language = null, _ => { });

        yield return Refusal("A figure with nothing to say",
            _ => { },
            document =>
            {
                // Reaching past the renderer, because MigraDoc will not produce one: an image with
                // no alternative text is drawn as an artifact rather than as an undescribed figure.
                // This is the check standing behind a document tagged by hand.
                document.Structure.CreateElement(PdfPinata.Pdf.Structure.PdfTag.Figure);
            });
    }

    static (string Broken, string Message) Refusal(string broken,
        Action<PdfDocumentRenderer> arrangeRenderer, Action<PdfDocument> arrangeDocument)
    {
        Document probe = new Document();
        Section section = probe.AddSection();
        section.AddParagraph("A heading", StyleNames.Heading1);
        section.AddParagraph("And a paragraph under it.");

        PdfDocumentRenderer renderer = new PdfDocumentRenderer(unicode: true)
        {
            Document = probe,
            TagContent = true,
            Language = "en-GB",
        };

        arrangeRenderer(renderer);
        renderer.RenderDocument();

        using PdfDocument document = renderer.PdfDocument;
        document.Info.Title = "A probe";
        document.Options.UAConformance = PdfUAConformance.PdfUA1;
        arrangeDocument(document);

        try
        {
            using MemoryStream buffer = new MemoryStream();
            document.Save(buffer, false);
        }
        catch (InvalidOperationException refused)
        {
            return (broken, refused.Message);
        }

        // Not an assertion dressed up as a demo: if the writer stops refusing one of these, the
        // page says so in place of the message rather than printing a stale quotation.
        return (broken, "This document saved. The rule is no longer enforced.");
    }
}
