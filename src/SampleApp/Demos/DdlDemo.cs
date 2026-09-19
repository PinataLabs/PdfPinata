using System.Collections.Generic;
using System.IO;
using System.Linq;
using PinataLayout.DocumentObjectModel;
using PinataLayout.DocumentObjectModel.IO;
using PinataLayout.DocumentObjectModel.Tables;
using PinataLayout.Rendering;
using PdfPinata.Pdf;
using SampleApp.Infrastructure;

namespace SampleApp.Demos;

/// <summary>
///   PinataLayout's own serialisation format, written and read back.
/// </summary>
internal sealed class DdlDemo : PdfDemo
{
    public DdlDemo() : base() { }

    public override string Name => "Ddl";

    public override string Summary => "A document written to PinataLayout DDL, read back, and rendered from the copy.";

    public override IReadOnlyList<string> Shows => new[]
    {
        "DdlWriter.WriteToString - a whole document as text, styles and tables and all",
        "DdlReader - the same text parsed back into an object model, errors and all",
        "That what is rendered here is the re-read copy, not the document that was built",
        "DdlReaderErrors, which is where a parse failure is reported rather than thrown",
        "What the format looks like, printed beside what it produces",
    };

    public override int PageCount => 4;

    protected override PdfDocument Build(DemoContext context)
    {
        #region example
        // ----- a document built the ordinary way -----

        Document original = new Document();
        original.Info.Title = "Ddl";
        original.Info.Author = "PdfPinata SampleApp";

        Style normal = original.Styles[StyleNames.Normal];
        normal.Font.Name = "Liberation Serif";
        normal.Font.Size = 10.5;

        Style listing = original.Styles.AddStyle("Listing", StyleNames.Normal);
        listing.Font.Name = "Source Code Pro";
        listing.Font.Size = 7.5;
        listing.ParagraphFormat.SpaceAfter = 0;
        listing.ParagraphFormat.LineSpacingRule = LineSpacingRule.Exactly;
        listing.ParagraphFormat.LineSpacing = Unit.FromPoint(9);

        Section body = original.AddSection();
        body.PageSetup.TopMargin = Unit.FromCentimeter(2.5);

        Paragraph title = body.AddParagraph("Written, serialised, re-read, rendered");
        title.Format.Font.Name = "Liberation Sans";
        title.Format.Font.Size = 18;
        title.Format.Font.Bold = true;
        title.Format.SpaceAfter = Unit.FromPoint(10);

        body.AddParagraph(
            "This page was not rendered from the document that built it. It was built, written out "
            + "as PinataLayout DDL with DdlWriter, parsed back with DdlReader, and the copy that came "
            + "out of the parser is what the renderer was given. Anything the round trip lost "
            + "would be missing from this page.");

        Paragraph styled = body.AddParagraph();
        styled.Format.SpaceBefore = Unit.FromPoint(8);
        styled.AddText("Formatting survives too: ");
        styled.AddFormattedText("bold", TextFormat.Bold);
        styled.AddText(", ");
        styled.AddFormattedText("italic", TextFormat.Italic);
        styled.AddText(", ");
        styled.AddFormattedText("underlined", TextFormat.Underline);
        styled.AddText(", and a colour set on a run rather than on the paragraph.");
        FormattedText coloured = styled.AddFormattedText(" Firebrick.");
        coloured.Color = Colors.Firebrick;

        Table table = body.AddTable();
        table.Borders.Width = 0.5;
        table.Borders.Color = Colors.Gray;
        table.Rows.LeftIndent = 0;
        table.AddColumn(Unit.FromCentimeter(5));
        table.AddColumn(Unit.FromCentimeter(5));
        table.AddColumn(Unit.FromCentimeter(5));

        Row header = table.AddRow();
        header.HeadingFormat = true;
        header.Shading.Color = Colors.WhiteSmoke;
        header.Cells[0].AddParagraph("What");
        header.Cells[1].AddParagraph("Written by");
        header.Cells[2].AddParagraph("Read by");

        (string What, string Written, string Read)[] rows =
        {
            ("A whole document", "DdlWriter.WriteToString", "DdlReader.DocumentFromString"),
            ("One object", "DdlWriter.WriteToString(obj)", "DdlReader.ObjectFromString"),
            ("To a file", "DdlWriter.WriteToFile", "DdlReader.DocumentFromFile"),
        };

        foreach ((string What, string Written, string Read) each in rows)
        {
            Row row = table.AddRow();
            row.Cells[0].AddParagraph(each.What);
            row.Cells[1].AddParagraph(each.Written);
            row.Cells[2].AddParagraph(each.Read);
        }

        // ----- out to text and back again -----

        // docs:begin round-trip
        // The whole document as a string. Styles, sections, paragraphs, runs, the table and its
        // borders - all of it, in PinataLayout's own grammar rather than XML or JSON.
        string ddl = DdlWriter.WriteToString(original);

        // And back. A parse failure is reported through DdlReaderErrors rather than thrown, so a
        // caller who wants to know has to ask - which is why the errors object is passed in.
        //
        // Through the instance rather than DdlReader.DocumentFromString, because the static one
        // has no overload that takes an errors object where ObjectFromString beside it does. The
        // constructors all take one, so the instance is the route to a reader that will tell you
        // what it could not parse.
        DdlReaderErrors errors = new DdlReaderErrors();
        Document reread = new DdlReader(new StringReader(ddl), errors).ReadDocument();
        // docs:end round-trip

        // ----- the listing, added to the re-read copy -----

        // Added after the round trip, so the page that shows the DDL is not itself in the DDL -
        // which would otherwise grow the listing by exactly as much as the listing.
        Section listingSection = reread.AddSection();
        listingSection.PageSetup.TopMargin = Unit.FromCentimeter(2.5);

        Paragraph listingTitle = listingSection.AddParagraph("The DDL it went through");
        listingTitle.Format.Font.Name = "Liberation Sans";
        listingTitle.Format.Font.Size = 18;
        listingTitle.Format.Font.Bold = true;
        listingTitle.Format.SpaceAfter = Unit.FromPoint(8);

        string[] lines = ddl.Replace("\r\n", "\n").Split('\n');

        Paragraph summary = listingSection.AddParagraph(
            $"{lines.Length} lines, {ddl.Length:N0} characters, and "
            + $"{errors.ErrorCount} error(s) reported by the reader. The first eighty lines "
            + "follow. The format is PinataLayout's own: braces nest, an attribute is name colon "
            + "value, and a paragraph's text is written between its braces.");
        summary.Format.SpaceAfter = Unit.FromPoint(10);

        foreach (string line in lines.Take(80))
        {
            // A tab in the source would be a tab stop here, so the indentation is turned into
            // spaces the paragraph can simply carry.
            Paragraph row = listingSection.AddParagraph(line.Replace("\t", "    "));
            row.Style = "Listing";
        }

        if (lines.Length > 80)
        {
            Paragraph more = listingSection.AddParagraph($"... and {lines.Length - 80} more lines.");
            more.Style = "Listing";
            more.Format.Font.Italic = true;
            more.Format.SpaceBefore = Unit.FromPoint(6);
        }

        // ----- what the round trip did and did not keep -----

        Section verdict = reread.AddSection();
        verdict.PageSetup.TopMargin = Unit.FromCentimeter(2.5);

        Paragraph verdictTitle = verdict.AddParagraph("What survived");
        verdictTitle.Format.Font.Name = "Liberation Sans";
        verdictTitle.Format.Font.Size = 18;
        verdictTitle.Format.Font.Bold = true;
        verdictTitle.Format.SpaceAfter = Unit.FromPoint(8);

        // Read off the re-read document rather than asserted, so the page cannot claim something
        // the round trip did not actually do.
        Section firstAgain = (Section)reread.Sections[0];
        Table tableAgain = firstAgain.Elements
            .OfType<Table>()
            .First();

        (string Question, string Answer)[] checks =
        {
            ("Sections", reread.Sections.Count.ToString()),
            ("Elements in the first section", firstAgain.Elements.Count.ToString()),
            ("Styles defined", reread.Styles.Count.ToString()),
            ("Does the Listing style survive", reread.Styles["Listing"] != null ? "yes" : "no"),
            // Read through the same lookup the question above asks, and tolerant of the answer
            // being "no": a page whose job is to report what the round trip lost cannot throw on
            // the way to saying something was lost.
            ("Its font", reread.Styles["Listing"]?.Font.Name ?? "-"),
            ("Table columns", tableAgain.Columns.Count.ToString()),
            ("Table rows", tableAgain.Rows.Count.ToString()),
            ("Is the first row still a heading", tableAgain.Rows[0].HeadingFormat ? "yes" : "no"),
            ("Border width", tableAgain.Borders.Width.ToString()),
            ("Document title", reread.Info.Title),
            ("Reader errors", errors.ErrorCount.ToString()),
        };

        Table results = verdict.AddTable();
        results.Borders.Width = 0;
        results.AddColumn(Unit.FromCentimeter(8));
        results.AddColumn(Unit.FromCentimeter(7));

        foreach ((string Question, string Answer) check in checks)
        {
            Row row = results.AddRow();
            row.Cells[0].AddParagraph(check.Question);
            Paragraph answer = row.Cells[1].AddParagraph(check.Answer);
            answer.Format.Font.Name = "Source Code Pro";
            answer.Format.Font.Size = 9;
        }

        Paragraph closing = verdict.AddParagraph();
        closing.Format.SpaceBefore = Unit.FromPoint(14);
        closing.AddText(
            "Every number above was read off the document the parser produced, not off the one "
            + "that was written - so this page is a round-trip test somebody can look at. DDL is "
            + "worth knowing about for two reasons beyond serialisation: a document dumped to it "
            + "is readable, which makes it the fastest way to see what a document object model "
            + "actually holds, and a report template can be kept as text and filled in at run "
            + "time rather than being written out in C#.");

        // docs:begin render
        PdfDocumentRenderer renderer = new PdfDocumentRenderer(unicode: true) { Document = reread };
        renderer.RenderDocument();
        // docs:end render
        #endregion

        return renderer.PdfDocument;
    }
}
