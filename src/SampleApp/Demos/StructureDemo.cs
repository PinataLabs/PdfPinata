using System.Collections.Generic;
using PinataLayout.DocumentObjectModel;
using PinataLayout.Rendering;
using PdfPinata.Pdf;
using SampleApp.Infrastructure;

namespace SampleApp.Demos;

/// <summary>
///   The structural half of PinataLayout: headings, a contents table, lists, sections and the
///   bookmarks that come with them.
/// </summary>
internal sealed class StructureDemo : PdfDemo
{
    public StructureDemo() : base() { }

    public override string Name => "Structure";

    public override string Summary => "PinataLayout's TOC, automatic bookmarks, sections, lists and cross-references.";

    public override IReadOnlyList<string> Shows => new[]
    {
        "A table of contents built from PageRefField, BookmarkField and a dotted tab leader",
        "ParagraphFormat.OutlineLevel, which turns headings into PDF bookmarks with no Outlines call",
        "Three sections with different page setups, and a title page with no header of its own",
        "HeadersFooters - Primary, FirstPage and EvenPage, and which one a reader sees where",
        "ListInfo - all six list types, and ContinuePreviousList, which is what keeps numbering",
        "Hyperlink in its bookmark, web and local forms",
        "PageBreak, which is what gives the contents three different page numbers to resolve to",
        "The predefined styles in StyleNames, and how a style inherits from the one it is based on",
    };

    public override int PageCount => 5;

    protected override PdfDocument Build(DemoContext context)
    {
        #region example
        Document report = new Document();
        report.Info.Title = "Structure";

        // docs:begin predefined-styles
        // Every predefined style is already there and already related: Heading1 is based on Normal,
        // Heading2 on Heading1, and so on, so setting the font on Normal reaches all of them.
        // StyleNames holds the names rather than the styles - they are looked up on the document.
        Style normal = report.Styles[StyleNames.Normal];
        normal.Font.Name = "Liberation Serif";
        normal.Font.Size = 10.5;
        normal.ParagraphFormat.SpaceAfter = Unit.FromPoint(6);

        Style heading1 = report.Styles[StyleNames.Heading1];
        heading1.Font.Name = "Liberation Sans";
        heading1.Font.Size = 18;
        heading1.Font.Bold = true;
        heading1.ParagraphFormat.SpaceBefore = Unit.FromPoint(18);
        heading1.ParagraphFormat.SpaceAfter = Unit.FromPoint(8);
        // docs:end predefined-styles

        // docs:begin outline-levels
        // OutlineLevel is the whole of the bookmark story. A paragraph carrying one becomes an
        // entry in the PDF's outline when the document is rendered - DocumentRenderer.AddOutline
        // is called for it - so nothing here ever mentions PdfOutline.
        heading1.ParagraphFormat.OutlineLevel = OutlineLevel.Level1;

        Style heading2 = report.Styles[StyleNames.Heading2];
        heading2.Font.Size = 13;
        heading2.ParagraphFormat.SpaceBefore = Unit.FromPoint(12);
        heading2.ParagraphFormat.OutlineLevel = OutlineLevel.Level2;
        // docs:end outline-levels

        // docs:begin custom-style
        // A style of one's own, based on another. Everything not set here comes from Normal, so a
        // change to Normal's font reaches this too - which is the point of basing rather than
        // copying.
        Style caption = report.Styles.AddStyle("Caption", StyleNames.Normal);
        caption.Font.Size = 8.5;
        caption.Font.Italic = true;
        caption.Font.Color = Colors.DimGray;
        // docs:end custom-style

        // docs:begin entry-style
        // The contents entries are a style too, carrying the tab stop the leader belongs to.
        Style entry = report.Styles.AddStyle("Entry", StyleNames.Normal);
        entry.ParagraphFormat.SpaceAfter = Unit.FromPoint(2);
        entry.ParagraphFormat.TabStops.AddTabStop(Unit.FromCentimeter(15),
            TabAlignment.Right, TabLeader.Dots);
        // docs:end entry-style

        // ----- section one: a title page with a page setup of its own -----

        Section title = report.AddSection();
        title.PageSetup.TopMargin = Unit.FromCentimeter(6);
        title.PageSetup.LeftMargin = Unit.FromCentimeter(3);
        title.PageSetup.RightMargin = Unit.FromCentimeter(3);

        // A section's headers are its own. Leaving this one's empty is how a title page comes out
        // without the running head the rest of the document has.
        Paragraph titleLine = title.AddParagraph("A structured document");
        titleLine.Format.Font.Name = "Liberation Sans";
        titleLine.Format.Font.Size = 30;
        titleLine.Format.Font.Bold = true;
        titleLine.Format.SpaceAfter = Unit.FromPoint(12);

        title.AddParagraph(
            "Built entirely from the document object model. Nothing here positions anything: the "
            + "renderer decides where every paragraph goes, breaks the pages, numbers them and "
            + "resolves the cross-references.").Style = "Caption";

        // ----- section two: the contents -----

        // docs:begin first-and-even-headers
        Section contents = report.AddSection();
        contents.PageSetup.TopMargin = Unit.FromCentimeter(2.5);

        // Primary is what every page of the section gets. FirstPage and EvenPage override it where
        // they apply, and a reader sees whichever is most specific.
        contents.PageSetup.DifferentFirstPageHeaderFooter = true;
        contents.PageSetup.OddAndEvenPagesHeaderFooter = true;

        Paragraph runningHead = contents.Headers.Primary.AddParagraph("A structured document");
        runningHead.Format.Font.Size = 8;
        runningHead.Format.Font.Color = Colors.DimGray;
        runningHead.Format.Alignment = ParagraphAlignment.Right;

        // Filled because DifferentFirstPageHeaderFooter is set above. Leaving it empty does not
        // fall back to Primary - it means the first page of the section has no header at all,
        // which on a section this short is every page a reader would look at for one.
        Paragraph firstHead = contents.Headers.FirstPage.AddParagraph("First page of the section gets this one");
        firstHead.Format.Font.Size = 8;
        firstHead.Format.Font.Color = Colors.DimGray;
        firstHead.Format.Alignment = ParagraphAlignment.Right;

        Paragraph evenHead = contents.Headers.EvenPage.AddParagraph("Even pages get this one");
        evenHead.Format.Font.Size = 8;
        evenHead.Format.Font.Color = Colors.DimGray;
        // docs:end first-and-even-headers

        Paragraph footer = contents.Footers.Primary.AddParagraph();
        footer.Format.Alignment = ParagraphAlignment.Center;
        footer.Format.Font.Size = 8;
        footer.AddText("Page ");
        footer.AddPageField();
        footer.AddText(" of ");
        footer.AddNumPagesField();

        contents.AddParagraph("Contents").Style = StyleNames.Heading1;

        // The classic PinataLayout table of contents. A PageRefField names a bookmark and is resolved
        // to that bookmark's page number when the document is laid out - which is why a TOC cannot
        // be written by hand without being wrong the moment anything moves.
        // docs:begin contents
        (string Bookmark, string Text)[] entries =
        {
            ("chapter-one", "1  Sections and headers"),
            ("lists", "2  Lists"),
            ("references", "3  Cross-references and hyperlinks"),
        };

        foreach ((string Bookmark, string Text) item in entries)
        {
            Paragraph line = contents.AddParagraph();
            line.Style = "Entry";
            line.AddHyperlink(item.Bookmark).AddText(item.Text);
            line.AddTab();
            line.AddPageRefField(item.Bookmark);
        }
        // docs:end contents

        contents.AddParagraph(
            "The dots are a tab leader, set on the tab stop the entry's style carries. The page "
            + "numbers are PageRefFields, resolved during layout. The entries are hyperlinks to "
            + "the same bookmarks the numbers point at, so clicking one goes there.").Style = "Caption";

        // ----- section three: the body -----

        Section bodyText = report.AddSection();
        bodyText.PageSetup.TopMargin = Unit.FromCentimeter(2.5);
        bodyText.PageSetup.DifferentFirstPageHeaderFooter = false;

        Paragraph bodyHead = bodyText.Headers.Primary.AddParagraph("A structured document");
        bodyHead.Format.Font.Size = 8;
        bodyHead.Format.Font.Color = Colors.DimGray;
        bodyHead.Format.Alignment = ParagraphAlignment.Right;

        Paragraph bodyFooter = bodyText.Footers.Primary.AddParagraph();
        bodyFooter.Format.Alignment = ParagraphAlignment.Center;
        bodyFooter.Format.Font.Size = 8;
        bodyFooter.AddText("Page ");
        bodyFooter.AddPageField();

        // A bookmark is invisible and draws nothing. It is a name attached to a point in the flow,
        // and it is what a PageRefField and a hyperlink both resolve against.
        // docs:begin bookmark
        Paragraph chapter = bodyText.AddParagraph();
        chapter.Style = StyleNames.Heading1;
        chapter.AddBookmark("chapter-one");
        chapter.AddText("1  Sections and headers");
        // docs:end bookmark

        bodyText.AddParagraph(
            "A section is where a page setup lives, so a document with a landscape appendix or "
            + "different margins for its front matter is a document with more than one section. "
            + "This one is the third: the title page and the contents each have their own.");

        bodyText.AddParagraph(
            "Headers and footers belong to the section too, and there are three of each. Primary "
            + "is what a page gets; FirstPage replaces it on the section's first page when "
            + "DifferentFirstPageHeaderFooter is set; EvenPage replaces it on even pages when "
            + "OddAndEvenPagesHeaderFooter is. Setting the flags is what makes the other two "
            + "reachable - filling them in without setting the flag does nothing at all.");

        // docs:begin page-break
        // A chapter that starts on a fresh page, which is also what gives the contents table
        // three different page numbers to resolve to rather than three copies of one.
        bodyText.AddPageBreak();
        // docs:end page-break

        Paragraph listsHead = bodyText.AddParagraph();
        listsHead.Style = StyleNames.Heading1;
        listsHead.AddBookmark("lists");
        listsHead.AddText("2  Lists");

        bodyText.AddParagraph(
            "ListInfo is the only list support in the library, and it hangs off a paragraph's "
            + "format rather than being a container of its own. Six types, and a flag that says "
            + "whether this paragraph carries on the list above it or starts a new one.");

        // docs:begin list-types
        foreach (ListType type in new[]
        {
            ListType.BulletList1, ListType.BulletList2, ListType.BulletList3,
            ListType.NumberList1, ListType.NumberList2, ListType.NumberList3,
        })
        {
            for (int item = 1; item <= 3; item++)
            {
                Paragraph line = bodyText.AddParagraph(
                    item == 1 ? $"{type} - first item" : $"{type} - item {item}");
                line.Format.SpaceAfter = Unit.FromPoint(1);
                line.Format.LeftIndent = Unit.FromCentimeter(1);
                line.Format.ListInfo = new ListInfo
                {
                    ListType = type,

                    // False on the first item of a run and true after it. Getting this wrong is
                    // what makes a numbered list restart at one halfway down, or carry on from
                    // the list before it.
                    ContinuePreviousList = item > 1,
                };
            }
        }
        // docs:end list-types

        bodyText.AddPageBreak();

        Paragraph refsHead = bodyText.AddParagraph();
        refsHead.Style = StyleNames.Heading1;
        refsHead.AddBookmark("references");
        refsHead.AddText("3  Cross-references and hyperlinks");

        // docs:begin page-ref
        Paragraph crossRef = bodyText.AddParagraph("The lists above begin on page ");
        crossRef.AddPageRefField("lists");
        crossRef.AddText(", and this sentence knows that without anybody counting: a PageRefField "
            + "is resolved against its bookmark when the document is laid out.");
        // docs:end page-ref

        // docs:begin hyperlinks
        Paragraph links = bodyText.AddParagraph("A hyperlink comes in three shapes. ");
        links.AddHyperlink("chapter-one").AddFormattedText("This one goes to chapter one",
            TextFormat.Underline);
        links.AddText(", inside the document. ");
        links.AddHyperlink("https://github.com/PinataLabs/PdfPinata", HyperlinkType.Web)
            .AddFormattedText("This one leaves it", TextFormat.Underline);
        links.AddText(", to a URI. A third form points at a file on disk.");
        // docs:end hyperlinks

        Paragraph outlineNote = bodyText.AddParagraph();
        outlineNote.Style = "Caption";
        outlineNote.AddText(
            "Every heading on this page is a bookmark in the PDF's outline panel, and nothing in "
            + "this demo calls Outlines.Add. ParagraphFormat.OutlineLevel on the Heading1 and "
            + "Heading2 styles is what does it, and the tree's shape follows the levels.");

        // Section-level fields, which count within the section rather than the document.
        // docs:begin section-fields
        Paragraph sectionNote = bodyText.AddParagraph("This is section ");
        sectionNote.AddSectionField();
        sectionNote.AddText(" of the document, and it has ");
        sectionNote.AddSectionPagesField();
        sectionNote.AddText(" page(s) of its own.");
        sectionNote.Style = "Caption";
        // docs:end section-fields

        // docs:begin render
        PdfDocumentRenderer renderer = new PdfDocumentRenderer(unicode: true) { Document = report };
        renderer.RenderDocument();

        // The outline panel is worth opening on arrival, since the whole point of the page above
        // is that it filled itself in.
        renderer.PdfDocument.PageMode = PdfPageMode.UseOutlines;
        // docs:end render
        #endregion

        return renderer.PdfDocument;
    }
}
