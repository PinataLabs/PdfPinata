using System.Collections.Generic;
using PinataLayout.DocumentObjectModel;
using PinataLayout.Rendering;
using PdfPinata.Pdf;
using SampleApp.Infrastructure;

namespace SampleApp.Demos;

/// <summary>
///   PinataLayout's footnotes: the mark, the block at the foot of the page, and the four settings that
///   decide how they are numbered and where they go.
/// </summary>
internal sealed class FootnotesDemo : PdfDemo
{
    public FootnotesDemo() : base() { }

    public override string Name => "Footnotes";

    public override string Summary => "Notes at the foot of the page, and the four settings that shape them.";

    public override IReadOnlyList<string> Shows => new[]
    {
        "Paragraph.AddFootnote, and that the note's content is block content rather than a string",
        "That the room a note needs comes off the page before the text carrying its mark is laid out",
        "Document.FootnoteNumberStyle - all five, on the same three notes",
        "Document.FootnoteNumberingRule - and that its default restarts on every page",
        "Document.FootnoteLocation - what BeneathText would change, on a page with room to spare",
        "Footnote.Reference, a mark of the caller's own, which does not advance the numbering",
        "StyleNames.Footnote, the predefined style the notes are set in",
    };

    public override int PageCount => 5;

    #region example
    protected override PdfDocument Build(DemoContext context)
    {
        Document document = new Document();
        document.Info.Title = "Footnotes";

        Style normal = document.Styles[StyleNames.Normal];
        normal.Font.Name = "Liberation Serif";
        normal.Font.Size = 10.5;
        normal.ParagraphFormat.SpaceAfter = Unit.FromPoint(6);

        Style heading = document.Styles[StyleNames.Heading1];
        heading.Font.Name = "Liberation Sans";
        heading.Font.Size = 17;
        heading.Font.Bold = true;
        heading.ParagraphFormat.SpaceAfter = Unit.FromPoint(8);

        // The notes are set in this predefined style. It is based on Normal and exists whether or
        // not anybody touches it, so a document that says nothing about footnotes still gets a
        // sensible one - and a document that wants them smaller says so here rather than on each.
        Style footnote = document.Styles[StyleNames.Footnote];
        footnote.Font.Size = 8;
        footnote.Font.Color = Colors.Black;

        Style caption = document.Styles.AddStyle("Caption", StyleNames.Normal);
        caption.Font.Size = 8.5;
        caption.Font.Italic = true;
        caption.Font.Color = Colors.DimGray;

        // Continuous numbering, so the marks on this page carry on from each other rather than
        // starting again. The default is RestartPage, which is worth knowing and is why this line
        // is here at all - see the last page.
        document.FootnoteNumberingRule = FootnoteNumberingRule.RestartContinuous;

        // ----- page one: what a footnote is -----

        Section first = document.AddSection();
        first.PageSetup.TopMargin = Unit.FromCentimeter(2.5);

        first.AddParagraph("A note at the foot of the page").Style = StyleNames.Heading1;

        Paragraph opening = first.AddParagraph(
            "A footnote is a paragraph element like any other run of text");
        opening.AddFootnote(
            "This is the note. It is attached to the word before the mark, and it appears at the "
            + "foot of whichever page that word ends up on.");
        opening.AddText(
            ", so it goes wherever a run can go: in a plain paragraph, inside formatted text, or "
            + "inside a hyperlink");
        opening.AddFormattedText(" - here inside a bold run", TextFormat.Bold)
            .AddFootnote("Attached from inside a FormattedText, and numbered in reading order.");
        opening.AddText(".");

        first.AddParagraph(
            "The mark in the running text is drawn as a superscript, at the reduced size the font "
            + "asks for. The note itself is not part of this paragraph and takes no room in it: it "
            + "is laid out separately and drawn in a band at the foot of the page.");

        Paragraph blockContent = first.AddParagraph(
            "A note is not a string. Footnote.Elements is block content, so a note can hold more "
            + "than one paragraph");
        Footnote longNote = blockContent.AddFootnote();
        longNote.AddParagraph(
            "The first paragraph of a note that has two. Everything PinataLayout can put in a section "
            + "can go in here.");
        longNote.AddParagraph(
            "The second. Both are laid out into a column the width of the text above, indented by "
            + "the width of the mark, so the mark stands in the margin and the lines all line up.");
        blockContent.AddText(", and a table or an image as readily as a paragraph.");

        first.AddParagraph(
            "Room for the note comes off the page before the paragraph carrying its mark is laid "
            + "out, so the page breaks where it should and the body text never runs into the "
            + "block. That is the whole of the layout problem, and it is why a footnote is a "
            + "feature rather than a line of drawing code.").Style = "Caption";

        Paragraph ownMark = first.AddParagraph(
            "A note can carry a mark of the caller's own instead of a number");
        ownMark.AddFootnote(
            "Marked with an asterisk rather than a number, by setting Reference. A note marked "
            + "this way is left out of the counting, so the numbers around it do not skip.")
            .Reference = "*";
        ownMark.AddText(", which is what Footnote.Reference is for.");

        Paragraph after = first.AddParagraph("The numbering carries on regardless");
        after.AddFootnote("The fourth note, and the third number - the starred one did not count.");
        after.AddText(".");

        // ----- page two: the five number styles -----

        Section styles = document.AddSection();
        styles.PageSetup.TopMargin = Unit.FromCentimeter(2.5);

        styles.AddParagraph("Five ways to mark a note").Style = StyleNames.Heading1;

        styles.AddParagraph(
            "FootnoteNumberStyle decides what the generated mark looks like. It is a document-wide "
            + "setting, so the three notes below are all in the style named at the end of this "
            + "sentence: " + document.FootnoteNumberStyle + ".");

        Paragraph marks = styles.AddParagraph("Three notes in a row");
        marks.AddFootnote("The first.");
        marks.AddText(", one after another");
        marks.AddFootnote("The second.");
        marks.AddText(", so the sequence is visible");
        marks.AddFootnote("The third.");
        marks.AddText(".");

        styles.AddParagraph(
            "The five values are Arabic (1, 2, 3), LowercaseLetter (a, b, c), UppercaseLetter "
            + "(A, B, C), LowercaseRoman (i, ii, iii) and UppercaseRoman (I, II, III). "
            + "FootnoteStartingNumber moves where the sequence begins; left alone it starts at "
            + "one, because the property's own default is zero and a first note marked \"0\" would "
            + "be a strange thing to print.").Style = "Caption";

        // ----- page three: where the block goes -----

        Section beneath = document.AddSection();
        beneath.PageSetup.TopMargin = Unit.FromCentimeter(2.5);

        beneath.AddParagraph("Where the block sits on the page").Style = StyleNames.Heading1;

        beneath.AddParagraph(
            "FootnoteLocation has two values. BottomOfPage - the default, and what every page of "
            + "this document uses - pins the block to the foot of the text area however little the "
            + "page holds. BeneathText puts it directly under the last thing laid out, which on a "
            + "page like this one is a long way higher up.");

        Paragraph shortPage = beneath.AddParagraph("This page stops here");
        shortPage.AddFootnote(
            "Pinned to the foot of the sheet, a long way below the line it belongs to, because "
            + "this document uses BottomOfPage.");
        shortPage.AddText(".");

        beneath.AddParagraph(
            "The note above is at the foot of the sheet, a long way below the line it belongs to, "
            + "because this document leaves FootnoteLocation alone. Setting it to BeneathText would "
            + "draw the same note immediately under that line instead, and would change nothing "
            + "else about this page. The setting belongs to the document rather than to the page, "
            + "so one document cannot show both - set it and run the demo again to see the other.");

        beneath.AddParagraph(
            "Both reserve the same room while the page is being formatted, so neither can collide "
            + "with the body text. They differ only in where the room that was kept clear is "
            + "actually used. On a full page the two are the same place.").Style = "Caption";

        // ----- pages four and five: the numbering rule -----

        Section rule = document.AddSection();
        rule.PageSetup.TopMargin = Unit.FromCentimeter(2.5);

        rule.AddParagraph("Where the numbering starts again").Style = StyleNames.Heading1;

        rule.AddParagraph(
            "FootnoteNumberingRule decides what the sequence counts within. RestartContinuous "
            + "numbers the whole document, which is what this one does and what most reports want. "
            + "RestartSection begins again at each section. RestartPage begins again on each page.");

        Paragraph acrossOne = rule.AddParagraph("A note on this page");
        acrossOne.AddFootnote("Numbered on from every note before it in the document.");
        acrossOne.AddText(", and another on the next.");

        rule.AddParagraph(
            "RestartPage is the enum's first value and therefore its default, so a document that "
            + "says nothing gets notes numbered from one on every page. That is rarely what "
            + "anybody means, and it is the reason this demo sets the rule explicitly at the top.")
            .Style = "Caption";

        rule.AddPageBreak();

        Paragraph acrossTwo = rule.AddParagraph("The note on the page after");
        acrossTwo.AddFootnote(
            "Under RestartContinuous this carries on from the page before. Under the default it "
            + "would be number one again.");
        acrossTwo.AddText(".");

        PdfDocumentRenderer renderer = new PdfDocumentRenderer(unicode: true) { Document = document };
        renderer.RenderDocument();
        #endregion

        return renderer.PdfDocument;
    }
}
