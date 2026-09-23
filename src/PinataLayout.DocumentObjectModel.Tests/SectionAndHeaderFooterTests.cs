using System;
using System.IO;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel.IO;
using PinataLayout.DocumentObjectModel.Shapes;
using PinataLayout.DocumentObjectModel.Shapes.Charts;
using PinataLayout.DocumentObjectModel.Tables;
using Xunit;

namespace PinataLayout.DocumentObjectModel.Tests;

/// <summary>
///   A section and the six header and footer areas around it. Both are containers with the same
///   shape: an <c>Add…</c> method per kind of content that builds the object, and an <c>Add</c>
///   overload per kind that takes one built elsewhere.
///   <para>
///   The building half is what every document uses and so what everything covers. The taking half
///   is the one a caller reaches for when the content was made before the page it goes on - and it
///   is also where a container could quietly drop what it was handed, because nothing it returns
///   would say so. A header additionally has to know <em>which</em> of the six it is, by reference
///   against its collection, and that is what decides the keyword it writes itself under.
///   </para>
/// </summary>
public class SectionAndHeaderFooterTests
{
    private static string DdlOf(DocumentObject documentObject) => DdlWriter.WriteToString(documentObject);

    // ----- content handed to a section --------------------------------------------------------

    [Fact]
    public void ASectionTakesContentThatWasBuiltBeforeIt()
    {
        var section = new Document().AddSection();

        section.Add(new Paragraph());
        section.Add(new Chart(ChartType.Line));
        section.Add(new Table());
        section.Add(new Image { Source = new NamedImage("picture.png") });
        section.Add(new TextFrame());

        section.Elements.Count.Should().Be(5);
        section.Elements[0].Should().BeOfType<Paragraph>();
        section.Elements[1].Should().BeOfType<Chart>();
        section.Elements[2].Should().BeOfType<Table>();
        section.Elements[3].Should().BeOfType<Image>();
        section.Elements[4].Should().BeOfType<TextFrame>();
    }

    [Fact]
    public void ASectionBuildsEveryKindOfContentItselfToo()
    {
        var section = new Document().AddSection();

        section.AddChart().Type.Should().Be(ChartType.Line);
        section.AddChart(ChartType.Bar2D).Type.Should().Be(ChartType.Bar2D);
        section.AddImage(new NamedImage("picture.png")).Source.Name.Should().Be("picture.png");
        section.AddTextFrame().Should().NotBeNull();
        section.AddPageBreak();

        section.Elements.Count.Should().Be(5);
    }

    /// <summary>
    ///   Both answer the last one of their kind, searching backwards, and null when there is none -
    ///   which is how a caller appends to what it just wrote without holding on to it.
    ///   <para>
    ///   The empty section is the case worth spelling out. Both used to read the backing field
    ///   rather than the property, and a section nobody had added anything to has not built its
    ///   element collection yet - so asking either of them threw a
    ///   <see cref="NullReferenceException"/> exactly where the summary promises a null.
    ///   </para>
    /// </summary>
    [Fact]
    public void ASectionAnswersTheLastParagraphAndTheLastTableItHolds()
    {
        var section = new Document().AddSection();

        section.LastParagraph.Should().BeNull();
        section.LastTable.Should().BeNull();

        section.AddTextFrame();
        section.LastParagraph.Should().BeNull();
        section.LastTable.Should().BeNull();

        section.AddParagraph("first");
        var table = section.AddTable();
        var last = section.AddParagraph("last");

        section.LastParagraph.Should().BeSameAs(last);
        section.LastTable.Should().BeSameAs(table);
    }

    [Fact]
    public void ASectionKnowsTheOneBeforeItAndTheFirstKnowsThereIsNone()
    {
        var document = new Document();
        var first = document.AddSection();
        var second = document.AddSection();

        first.PreviousSection().Should().BeNull();
        second.PreviousSection().Should().BeSameAs(first);
    }

    [Fact]
    public void ASectionCopiesItselfAndCarriesItsCommentAcross()
    {
        var section = new Document().AddSection();
        section.Comment = "the only one";
        section.AddParagraph("here");

        var copy = section.Clone();

        copy.Comment.Should().Be("the only one");
        copy.Elements.Count.Should().Be(1);
        copy.Should().NotBeSameAs(section);
        DdlOf(section).Should().Contain("the only one");
    }

    [Fact]
    public void ASectionHandedItsOwnPartsUsesTheOnesItWasHanded()
    {
        var section = new Document().AddSection();
        var elsewhere = new Document().AddSection();
        elsewhere.PageSetup.LeftMargin = Unit.FromCentimeter(4);
        elsewhere.Headers.Primary.AddParagraph("from elsewhere");
        elsewhere.AddParagraph("content");

        section.PageSetup = elsewhere.PageSetup.Clone();
        section.Headers = elsewhere.Headers.Clone();
        section.Footers = elsewhere.Footers.Clone();
        section.Elements = elsewhere.Elements.Clone();

        section.PageSetup.LeftMargin.Centimeter.Should().Be(4);
        section.Headers.Primary.Elements.Count.Should().Be(1);
        section.Elements.Count.Should().Be(1);
    }

    // ----- content handed to a header ---------------------------------------------------------

    [Fact]
    public void AHeaderTakesContentThatWasBuiltBeforeItAndBuildsItToo()
    {
        var header = new Document().AddSection().Headers.Primary;

        header.Add(new Paragraph());
        header.Add(new Chart(ChartType.Line));
        header.Add(new Table());
        header.Add(new Image { Source = new NamedImage("picture.png") });
        header.Add(new TextFrame());

        header.AddChart().Should().NotBeNull();
        header.AddChart(ChartType.Bar2D).Type.Should().Be(ChartType.Bar2D);
        header.AddTable().Should().NotBeNull();
        header.AddImage(new NamedImage("picture.png")).Should().NotBeNull();
        header.AddTextFrame().Should().NotBeNull();

        header.Elements.Count.Should().Be(10);
    }

    /// <summary>
    ///   Each of the six areas is told apart by reference against the collection holding it, not by
    ///   a flag it carries. That is what lets one <see cref="HeaderFooter"/> class serve all six,
    ///   and it is what decides the keyword each writes itself under.
    /// </summary>
    [Fact]
    public void EachOfTheSixAreasKnowsWhichOfTheSixItIs()
    {
        var section = new Document().AddSection();

        section.Headers.Primary.IsHeader.Should().BeTrue();
        section.Headers.Primary.IsFooter.Should().BeFalse();
        section.Headers.Primary.IsPrimary.Should().BeTrue();
        section.Headers.Primary.IsFirstPage.Should().BeFalse();
        section.Headers.Primary.IsEvenPage.Should().BeFalse();

        section.Headers.FirstPage.IsFirstPage.Should().BeTrue();
        section.Headers.EvenPage.IsEvenPage.Should().BeTrue();

        section.Footers.Primary.IsHeader.Should().BeFalse();
        section.Footers.Primary.IsFooter.Should().BeTrue();
        section.Footers.FirstPage.IsFirstPage.Should().BeTrue();
        section.Footers.EvenPage.IsEvenPage.Should().BeTrue();
    }

    [Fact]
    public void EachOfTheSixAreasWritesItselfUnderItsOwnKeyword()
    {
        var section = new Document().AddSection();

        section.Headers.Primary.AddParagraph("primary header");
        section.Headers.FirstPage.AddParagraph("first page header");
        section.Headers.EvenPage.AddParagraph("even page header");
        section.Footers.Primary.AddParagraph("primary footer");
        section.Footers.FirstPage.AddParagraph("first page footer");
        section.Footers.EvenPage.AddParagraph("even page footer");

        var ddl = DdlOf(section);

        ddl.Should().Contain("\\primaryheader")
            .And.Contain("\\firstpageheader")
            .And.Contain("\\evenpageheader")
            .And.Contain("\\primaryfooter")
            .And.Contain("\\firstpagefooter")
            .And.Contain("\\evenpagefooter");
    }

    [Fact]
    public void AHeaderWritesTheStyleTheFormatAndTheCommentItWasGiven()
    {
        var document = new Document();
        document.Styles.AddStyle("Loud", "Normal");
        var header = document.AddSection().Headers.Primary;

        header.AddParagraph("here");
        header.Style = "Loud";
        header.Format = new ParagraphFormat { SpaceBefore = Unit.FromPoint(6) };
        header.Comment = "at the top";
        header.Elements = header.Elements.Clone();

        header.Style.Should().Be("Loud");
        header.Comment.Should().Be("at the top");
        header.Format.SpaceBefore.Point.Should().Be(6);
        header.IsNull().Should().BeFalse();

        DdlOf(document).Should().Contain("Style = \"Loud\"")
            .And.Contain("SpaceBefore = 6")
            .And.Contain("at the top");
    }

    /// <summary>
    ///   A style name is checked against the document's styles when it is set, because the only
    ///   other moment it could be checked is the render that silently formats the text as Normal.
    /// </summary>
    [Fact]
    public void AHeaderRefusesAStyleTheDocumentHasNeverHeardOf()
    {
        var header = new Document().AddSection().Headers.Primary;

        var naming = () => header.Style = "NoSuchStyle";

        naming.Should().Throw<ArgumentException>().WithMessage("*Invalid style name*");
    }

    [Fact]
    public void AHeaderCopiesItselfWithTheContentInIt()
    {
        var header = new Document().AddSection().Headers.Primary;
        header.AddParagraph("here");
        header.Comment = "at the top";

        var copy = header.Clone();

        copy.Should().NotBeSameAs(header);
        copy.Comment.Should().Be("at the top");
        copy.Elements.Count.Should().Be(1);
    }

    /// <summary>
    ///   An image source that knows nothing but its name. Every DOM test of an image is about the
    ///   description of one rather than about its pixels, and decoding is a backend's job - which
    ///   this project deliberately has none of.
    /// </summary>
    private sealed class NamedImage : ImageSource.IImageSource
    {
        internal NamedImage(string name) => Name = name;

        public int Width => 1;

        public int Height => 1;

        public string Name { get; }

        public bool Transparent => false;

        public void SaveAsJpeg(MemoryStream ms) => throw new NotSupportedException();

        public PixelBuffer GetPixels() => throw new NotSupportedException();
    }
}
