using System;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel.Fields;
using PinataLayout.DocumentObjectModel.IO;
using PinataLayout.DocumentObjectModel.Shapes;
using PinataLayout.DocumentObjectModel.Visitors;
using Xunit;
using static PinataLayout.DocumentObjectModel.Shapes.ImageSource;

namespace PinataLayout.DocumentObjectModel.Tests;

/// <summary>
///   The three things that hold inline content - <see cref="Paragraph"/>, <see cref="FormattedText"/>
///   and <see cref="Hyperlink"/> - each carry the same forty-odd <c>Add</c> methods, every one a
///   single line handing on to its <see cref="ParagraphElements"/>. What is worth pinning is that
///   each one hands on to the right method: the element added is the one returned, it lands in the
///   container's own elements, and in the order it was added.
/// </summary>
public class ParagraphContentTests
{
    /// <summary>An image nobody draws: <c>AddImage</c> keeps the source and never reads it.</summary>
    private sealed class UndrawnImage : IImageSource
    {
        public int Width => 1;
        public int Height => 1;
        public string Name => "undrawn";
        public bool Transparent => false;
        public void SaveAsJpeg(MemoryStream ms) => throw new NotSupportedException();
        public PixelBuffer GetPixels() => throw new NotSupportedException();
    }

    private static Document RoundTrip(Document document) =>
        DdlReader.DocumentFromString(DdlWriter.WriteToString(document));

    private static SymbolName[] Symbols(ParagraphElements elements) =>
        [..elements.OfType<Character>().Select(c => c.SymbolName)];

    // ----- The Add… methods that build an element ----------------------------------------------

    [Fact]
    public void EveryAddMethodOnAParagraphAddsWhatItReturns()
    {
        var paragraph = new Paragraph();
        var image = new UndrawnImage();

        object[] added =
        [
            paragraph.AddText("text"),
            paragraph.AddChar('a'),
            paragraph.AddChar('b', 3),
            paragraph.AddCharacter(SymbolName.Bullet),
            paragraph.AddCharacter(SymbolName.EmDash, 2),
            paragraph.AddCharacter('©'),
            paragraph.AddCharacter('®', 2),
            paragraph.AddSpace(4),
            paragraph.AddFormattedText(),
            paragraph.AddFormattedText(TextFormat.Bold),
            paragraph.AddFormattedText(new Font("Courier")),
            paragraph.AddFormattedText("plain"),
            paragraph.AddFormattedText("bold", TextFormat.Bold),
            paragraph.AddFormattedText("courier", new Font("Courier")),
            paragraph.AddFormattedText("styled", "Emphasis"),
            paragraph.AddHyperlink("bookmark"),
            paragraph.AddHyperlink("https://example.com", HyperlinkType.Web),
            paragraph.AddBookmark("here"),
            paragraph.AddPageField(),
            paragraph.AddPageRefField("here"),
            paragraph.AddNumPagesField(),
            paragraph.AddSectionField(),
            paragraph.AddSectionPagesField(),
            paragraph.AddDateField(),
            paragraph.AddDateField("yyyy"),
            paragraph.AddInfoField(InfoFieldType.Author),
            paragraph.AddFootnote("note"),
            paragraph.AddFootnote(),
            paragraph.AddImage(image)
        ];

        paragraph.Elements.Cast<object>().Should().Equal(added);
        paragraph.Elements.OfType<Image>().Single().Source.Should().BeSameAs(image);
    }

    [Fact]
    public void EveryAddMethodOnFormattedTextAddsWhatItReturns()
    {
        var formatted = new FormattedText();

        object[] added =
        [
            formatted.AddText("text"),
            formatted.AddChar('a'),
            formatted.AddChar('b', 3),
            formatted.AddCharacter(SymbolName.Bullet),
            formatted.AddCharacter(SymbolName.EmDash, 2),
            formatted.AddCharacter('©'),
            formatted.AddCharacter('®', 2),
            formatted.AddSpace(4),
            formatted.AddFormattedText(),
            formatted.AddFormattedText(TextFormat.Italic),
            formatted.AddFormattedText(new Font("Courier")),
            formatted.AddFormattedText("plain"),
            formatted.AddFormattedText("italic", TextFormat.Italic),
            formatted.AddFormattedText("courier", new Font("Courier")),
            formatted.AddFormattedText("styled", "Emphasis"),
            formatted.AddHyperlink("bookmark"),
            formatted.AddHyperlink("https://example.com", HyperlinkType.Web),
            formatted.AddBookmark("here"),
            formatted.AddPageField(),
            formatted.AddPageRefField("here"),
            formatted.AddNumPagesField(),
            formatted.AddSectionField(),
            formatted.AddSectionPagesField(),
            formatted.AddDateField(),
            formatted.AddDateField("yyyy"),
            formatted.AddInfoField(InfoFieldType.Title),
            formatted.AddFootnote("note"),
            formatted.AddFootnote(),
            formatted.AddImage(new UndrawnImage())
        ];

        formatted.Elements.Cast<object>().Should().Equal(added);
    }

    [Fact]
    public void EveryAddMethodOnAHyperlinkAddsWhatItReturns()
    {
        var hyperlink = new Hyperlink();

        object[] added =
        [
            hyperlink.AddText("text"),
            hyperlink.AddChar('a'),
            hyperlink.AddChar('b', 3),
            hyperlink.AddCharacter(SymbolName.Bullet),
            hyperlink.AddCharacter(SymbolName.EmDash, 2),
            hyperlink.AddCharacter('©'),
            hyperlink.AddCharacter('®', 2),
            hyperlink.AddSpace(4),
            hyperlink.AddFormattedText(),
            hyperlink.AddFormattedText(TextFormat.Underline),
            hyperlink.AddFormattedText(new Font("Courier")),
            hyperlink.AddFormattedText("plain"),
            hyperlink.AddFormattedText("underlined", TextFormat.Underline),
            hyperlink.AddFormattedText("courier", new Font("Courier")),
            hyperlink.AddFormattedText("styled", "Emphasis"),
            hyperlink.AddBookmark("here"),
            hyperlink.AddPageField(),
            hyperlink.AddPageRefField("here"),
            hyperlink.AddNumPagesField(),
            hyperlink.AddSectionField(),
            hyperlink.AddSectionPagesField(),
            hyperlink.AddDateField(),
            hyperlink.AddDateField("yyyy"),
            hyperlink.AddInfoField(InfoFieldType.Subject),
            hyperlink.AddFootnote("note"),
            hyperlink.AddFootnote(),
            hyperlink.AddImage(new UndrawnImage())
        ];

        hyperlink.Elements.Cast<object>().Should().Equal(added);
    }

    [Fact]
    public void TabsAndLineBreaksAreCharactersOfTheirOwn()
    {
        var paragraph = new Paragraph();
        paragraph.AddTab();
        paragraph.AddLineBreak();
        var formatted = new FormattedText();
        formatted.AddTab();
        formatted.AddLineBreak();
        var hyperlink = new Hyperlink();
        hyperlink.AddTab();

        Symbols(paragraph.Elements).Should().Equal(SymbolName.Tab, SymbolName.LineBreak);
        Symbols(formatted.Elements).Should().Equal(SymbolName.Tab, SymbolName.LineBreak);
        Symbols(hyperlink.Elements).Should().Equal(SymbolName.Tab);
    }

    [Fact]
    public void AddingAFormatSetsTheFontOfTheNewText()
    {
        var paragraph = new Paragraph();

        paragraph.AddFormattedText("bold", TextFormat.Bold).Bold.Should().BeTrue();
        paragraph.AddFormattedText("courier", new Font("Courier")).FontName.Should().Be("Courier");
        paragraph.AddFormattedText("styled", "Emphasis").Style.Should().Be("Emphasis");
        paragraph.AddHyperlink("https://example.com", HyperlinkType.Web).Type.Should().Be(HyperlinkType.Web);
        paragraph.AddHyperlink("bookmark").Type.Should().Be(HyperlinkType.Local);
    }

    // ----- The Add overloads that take an element ------------------------------------------------

    // A section or information field has no public constructor, so the one way to have one to
    // hand to Add is to clone one made by the other kind of Add.
    private static DocumentObject[] OneOfEach() =>
    [
        new BookmarkField("here"),
        new PageField(),
        new PageRefField("here"),
        new NumPagesField(),
        new Paragraph().AddSectionField().Clone(),
        new SectionPagesField(),
        new DateField(),
        new Paragraph().AddInfoField(InfoFieldType.Author).Clone(),
        new Footnote(),
        new Text("text"),
        new FormattedText(),
        new Image(),
        new Character { SymbolName = SymbolName.Bullet }
    ];

    [Fact]
    public void EveryAddOverloadOnAParagraphAddsTheElementItIsGiven()
    {
        var paragraph = new Paragraph();
        var elements = OneOfEach();
        var hyperlink = new Hyperlink { Name = "here" };

        paragraph.Add((BookmarkField)elements[0]);
        paragraph.Add((PageField)elements[1]);
        paragraph.Add((PageRefField)elements[2]);
        paragraph.Add((NumPagesField)elements[3]);
        paragraph.Add((SectionField)elements[4]);
        paragraph.Add((SectionPagesField)elements[5]);
        paragraph.Add((DateField)elements[6]);
        paragraph.Add((InfoField)elements[7]);
        paragraph.Add((Footnote)elements[8]);
        paragraph.Add((Text)elements[9]);
        paragraph.Add((FormattedText)elements[10]);
        paragraph.Add((Image)elements[11]);
        paragraph.Add((Character)elements[12]);
        paragraph.Add(hyperlink);

        paragraph.Elements.Cast<DocumentObject>().Should().Equal([.. elements, hyperlink]);
    }

    [Fact]
    public void EveryAddOverloadOnFormattedTextAddsTheElementItIsGiven()
    {
        var formatted = new FormattedText();
        var elements = OneOfEach();
        var hyperlink = new Hyperlink { Name = "here" };

        formatted.Add((BookmarkField)elements[0]);
        formatted.Add((PageField)elements[1]);
        formatted.Add((PageRefField)elements[2]);
        formatted.Add((NumPagesField)elements[3]);
        formatted.Add((SectionField)elements[4]);
        formatted.Add((SectionPagesField)elements[5]);
        formatted.Add((DateField)elements[6]);
        formatted.Add((InfoField)elements[7]);
        formatted.Add((Footnote)elements[8]);
        formatted.Add((Text)elements[9]);
        formatted.Add((FormattedText)elements[10]);
        formatted.Add((Image)elements[11]);
        formatted.Add((Character)elements[12]);
        formatted.Add(hyperlink);

        formatted.Elements.Cast<DocumentObject>().Should().Equal([.. elements, hyperlink]);
    }

    [Fact]
    public void EveryAddOverloadOnAHyperlinkAddsTheElementItIsGiven()
    {
        var hyperlink = new Hyperlink();
        var elements = OneOfEach();

        hyperlink.Add((BookmarkField)elements[0]);
        hyperlink.Add((PageField)elements[1]);
        hyperlink.Add((PageRefField)elements[2]);
        hyperlink.Add((NumPagesField)elements[3]);
        hyperlink.Add((SectionField)elements[4]);
        hyperlink.Add((SectionPagesField)elements[5]);
        hyperlink.Add((DateField)elements[6]);
        hyperlink.Add((InfoField)elements[7]);
        hyperlink.Add((Footnote)elements[8]);
        hyperlink.Add((Text)elements[9]);
        hyperlink.Add((FormattedText)elements[10]);
        hyperlink.Add((Image)elements[11]);
        hyperlink.Add((Character)elements[12]);

        hyperlink.Elements.Cast<DocumentObject>().Should().Equal(elements);
    }

    // ----- Properties ----------------------------------------------------------------------------

    [Fact]
    public void FormattedTextSaysEverythingThroughItsFont()
    {
        var formatted = new FormattedText
        {
            FontName = "Courier",
            Size = 9,
            Bold = true,
            Italic = true,
            Underline = Underline.Dash,
            Color = Colors.Firebrick,
            Superscript = true
        };

        formatted.Font.Name.Should().Be("Courier");
        formatted.Font.Size.Point.Should().Be(9);
        formatted.Font.Bold.Should().BeTrue();
        formatted.Font.Italic.Should().BeTrue();
        formatted.Font.Underline.Should().Be(Underline.Dash);
        formatted.Font.Color.Should().Be(Colors.Firebrick);
        formatted.Font.Superscript.Should().BeTrue();

        formatted.Subscript = true;
        formatted.Subscript.Should().BeTrue();
        (formatted.FontName, formatted.Size.Point, formatted.Bold, formatted.Italic, formatted.Underline,
                formatted.Color)
            .Should().Be(("Courier", 9.0, true, true, Underline.Dash, Colors.Firebrick));
    }

    [Fact]
    public void AnAssignedFontFormatOrElementsIsTheOneKept()
    {
        var formatted = new FormattedText();
        var font = new Font("Courier");
        var elements = new ParagraphElements();
        formatted.Font = font;
        formatted.Elements = elements;

        var hyperlink = new Hyperlink();
        var linkFont = new Font("Courier");
        var linkElements = new ParagraphElements();
        hyperlink.Font = linkFont;
        hyperlink.Elements = linkElements;

        var paragraph = new Paragraph();
        var format = new ParagraphFormat();
        var paragraphElements = new ParagraphElements();
        paragraph.Format = format;
        paragraph.Elements = paragraphElements;
        paragraph.Comment = "a comment";

        formatted.Font.Should().BeSameAs(font);
        formatted.Elements.Should().BeSameAs(elements);
        hyperlink.Font.Should().BeSameAs(linkFont);
        hyperlink.Elements.Should().BeSameAs(linkElements);
        paragraph.Format.Should().BeSameAs(format);
        paragraph.Elements.Should().BeSameAs(paragraphElements);
        paragraph.Comment.Should().Be("a comment");
    }

    [Fact]
    public void AnElementAlreadyInAContainerCannotBeAssignedToAnother()
    {
        var first = new FormattedText();
        var font = first.Font;

        var assign = () => new Hyperlink { Font = font };

        assign.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AnUnsetStyleNameAndCommentReadAsEmpty()
    {
        new FormattedText().Style.Should().BeEmpty();
        new Paragraph().Style.Should().BeEmpty();
        new Paragraph().Comment.Should().BeEmpty();
        new Hyperlink().Name.Should().BeEmpty();
    }

    [Fact]
    public void ACloneIsDeepAndOfTheSameType()
    {
        var paragraph = new Paragraph();
        var formatted = paragraph.AddFormattedText("bold", TextFormat.Bold);
        var hyperlink = paragraph.AddHyperlink("https://example.com", HyperlinkType.Web);
        hyperlink.AddText("example");

        var paragraphClone = paragraph.Clone();
        var formattedClone = formatted.Clone();
        var hyperlinkClone = hyperlink.Clone();

        paragraphClone.Elements.Count.Should().Be(2);
        paragraphClone.Elements[0].Should().NotBeSameAs(formatted);
        formattedClone.Bold.Should().BeTrue();
        formattedClone.Elements.Should().NotBeSameAs(formatted.Elements);
        hyperlinkClone.Name.Should().Be("https://example.com");
        hyperlinkClone.Type.Should().Be(HyperlinkType.Web);
        hyperlinkClone.Elements.Count.Should().Be(1);
    }

    // ----- Writing and reading MDDDL -------------------------------------------------------------

    [Fact]
    public void FormattedTextByStyleAndByFontSurvivesARoundTrip()
    {
        var document = new Document();
        var paragraph = document.AddSection().AddParagraph();
        paragraph.AddFormattedText("styled", "Emphasis");
        paragraph.AddFormattedText("bold", TextFormat.Bold);
        paragraph.AddFormattedText().AddText("unformatted");

        var again = (Paragraph)RoundTrip(document).LastSection.Elements[0];

        var styled = (FormattedText)again.Elements[0];
        styled.Style.Should().Be("Emphasis");
        ((Text)styled.Elements[0]).Content.Should().Be("styled");
        var bold = (FormattedText)again.Elements[1];
        bold.Bold.Should().BeTrue();
        ((Text)bold.Elements[0]).Content.Should().Be("bold");
        again.Elements.OfType<Text>().Concat(
                again.Elements.OfType<FormattedText>().SelectMany(f => f.Elements.OfType<Text>()))
            .Select(t => t.Content).Should().Contain("unformatted");
    }

    [Fact]
    public void AHyperlinkWithAFontSurvivesARoundTrip()
    {
        var document = new Document();
        var paragraph = document.AddSection().AddParagraph();
        var hyperlink = paragraph.AddHyperlink("https://example.com/\"quoted\"", HyperlinkType.Web);
        hyperlink.Font.Underline = Underline.Single;
        hyperlink.AddText("example");

        var again = (Hyperlink)((Paragraph)RoundTrip(document).LastSection.Elements[0]).Elements[0];

        again.Name.Should().Be("https://example.com/\"quoted\"");
        again.Type.Should().Be(HyperlinkType.Web);
        again.Font.Underline.Should().Be(Underline.Single);
        ((Text)again.Elements[0]).Content.Should().Be("example");
    }

    [Fact]
    public void AHyperlinkWithNoTargetCannotBeWritten()
    {
        var document = new Document();
        document.AddSection().AddParagraph().AddHyperlink("").AddText("nowhere");

        var write = () => DdlWriter.WriteToString(document);

        write.Should().Throw<InvalidOperationException>().WithMessage("*Name*");
    }

    [Fact]
    public void AStyledParagraphWithAFormatSurvivesARoundTrip()
    {
        var document = new Document();
        var paragraph = document.AddSection().AddParagraph("body");
        paragraph.Style = StyleNames.Heading1;
        paragraph.Format.SpaceBefore = 7;

        var again = (Paragraph)RoundTrip(document).LastSection.Elements[0];

        again.Style.Should().Be(StyleNames.Heading1);
        again.Format.SpaceBefore.Point.Should().Be(7);
        ((Text)again.Elements[0]).Content.Should().Be("body");
    }

    // ----- Paragraph breaks ----------------------------------------------------------------------

    [Fact]
    public void FlatteningSplitsAParagraphAtEachParagraphBreak()
    {
        var document = new Document();
        var section = document.AddSection();
        section.AddParagraph("before");
        var paragraph = section.AddParagraph();
        paragraph.Style = StyleNames.Heading2;
        paragraph.Format.Alignment = ParagraphAlignment.Center;
        paragraph.AddText("one");
        paragraph.AddCharacter(SymbolName.ParaBreak);
        paragraph.AddText("two");
        paragraph.AddCharacter(SymbolName.ParaBreak);
        paragraph.AddText("three");
        section.AddParagraph("after");

        new PdfFlattenVisitor().Visit(document);

        var paragraphs = section.Elements.Cast<Paragraph>().ToArray();
        paragraphs.Select(p => ((Text)p.Elements[0]).Content)
            .Should().Equal("before", "one", "two", "three", "after");
        paragraphs[1..4].Should().OnlyContain(p =>
            p.Style == StyleNames.Heading2 && p.Format.Alignment == ParagraphAlignment.Center);
        paragraphs[1..4].Should().OnlyContain(p => p.Elements.Count == 1);
    }
}
