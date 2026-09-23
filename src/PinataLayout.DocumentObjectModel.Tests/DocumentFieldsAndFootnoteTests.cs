using System;
using System.IO;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel.Fields;
using PinataLayout.DocumentObjectModel.IO;
using PinataLayout.DocumentObjectModel.Shapes;
using PinataLayout.DocumentObjectModel.Tables;
using Xunit;

namespace PinataLayout.DocumentObjectModel.Tests;

/// <summary>
///   The document's own settings, the fields that stand for something only the renderer knows, and
///   the footnote that hangs off a paragraph.
///   <para>
///   A field writes itself as a <c>\field(Kind)[…]</c> run rather than through the attribute
///   machinery every other object uses, which is why each one has a <c>Serialize</c> written by
///   hand - and why two of them refuse to write at all without the one property that gives them
///   their meaning. A bookmark with no name and an info field with no name are both fields that
///   would come out of the renderer as nothing, so they say so here instead.
///   </para>
/// </summary>
public class DocumentFieldsAndFootnoteTests
{
    private static string DdlOf(DocumentObject documentObject) => DdlWriter.WriteToString(documentObject);

    // ----- the document's own settings --------------------------------------------------------

    [Fact]
    public void ADocumentWritesEverySettingItWasGivenAndNoneItWasNot()
    {
        var document = new Document();

        DdlOf(document).Should().NotContain("UseCmykColor");

        document.DefaultTabStop = Unit.FromCentimeter(2);
        document.FootnoteLocation = FootnoteLocation.BeneathText;
        document.FootnoteNumberingRule = FootnoteNumberingRule.RestartPage;
        document.FootnoteNumberStyle = FootnoteNumberStyle.LowercaseLetter;
        document.ImagePath = "images";
        document.UseCmykColor = true;
        document.AddSection().AddParagraph("here");

        document.DefaultTabStop.Centimeter.Should().BeApproximately(2, 1e-9);
        document.ImagePath.Should().Be("images");
        document.UseCmykColor.Should().BeTrue();
        document.DdlFile.Should().BeEmpty();

        var ddl = DdlOf(document);
        ddl.Should().Contain("DefaultTabStop = \"2cm\"")
            .And.Contain("FootnoteLocation = BeneathText")
            .And.Contain("FootnoteNumberingRule = RestartPage")
            .And.Contain("FootnoteNumberStyle = LowercaseLetter")
            .And.Contain("ImagePath = \"images\"")
            .And.Contain("UseCmykColor = true");
    }

    [Fact]
    public void ADocumentTakesASectionOrAStyleThatWasBuiltBeforeIt()
    {
        var document = new Document();

        document.Add(new Section());
        document.Add(new Style("Loud", "Normal"));

        document.Sections.Count.Should().Be(1);
        document.Styles["Loud"].Should().NotBeNull();
    }

    [Fact]
    public void ADocumentHandedItsOwnPartsUsesTheOnesItWasHanded()
    {
        var document = new Document();
        var elsewhere = new Document();
        elsewhere.Info.Title = "from elsewhere";
        elsewhere.AddSection().AddParagraph("content");

        document.Info = elsewhere.Info.Clone();
        document.Styles = elsewhere.Styles.Clone();
        document.Sections = elsewhere.Sections.Clone();

        document.Info.Title.Should().Be("from elsewhere");
        document.Sections.Count.Should().Be(1);
        document.Styles.Count.Should().BeGreaterThan(0);
    }

    /// <summary>
    ///   Rendering rewrites a document in place, so a second renderer would be working on something
    ///   the first had already changed. The document refuses the second rather than producing two
    ///   files that disagree, and says to clone it.
    /// </summary>
    [Fact]
    public void ADocumentBelongsToOneRendererAndRefusesASecond()
    {
        var document = new Document();
        document.BindToRenderer(new object());

        var again = () => document.BindToRenderer(new object());

        again.Should().Throw<InvalidOperationException>()
            .WithMessage("*already bound to another renderer*");
    }

    [Fact]
    public void TheDocumentInformationWritesEveryFieldItWasGiven()
    {
        var document = new Document();

        document.Info.Title = "A title";
        document.Info.Author = "An author";
        document.Info.Subject = "A subject";
        document.Info.Keywords = "one, two";
        document.Info.Comment = "a note";

        document.Info.Subject.Should().Be("A subject");
        document.Info.Keywords.Should().Be("one, two");
        document.Info.Comment.Should().Be("a note");
        document.Info.Clone().Title.Should().Be("A title");

        var ddl = DdlOf(document);
        ddl.Should().Contain("Title = \"A title\"")
            .And.Contain("Author = \"An author\"")
            .And.Contain("Subject = \"A subject\"")
            .And.Contain("Keywords = \"one, two\"")
            .And.Contain("a note");
    }

    // ----- fields -----------------------------------------------------------------------------

    [Fact]
    public void EveryKindOfFieldWritesItselfAsItsOwnRun()
    {
        var paragraph = new Document().AddSection().AddParagraph();

        paragraph.AddBookmark("here");
        paragraph.AddPageField();
        paragraph.AddPageRefField("here");
        paragraph.AddNumPagesField();
        paragraph.AddSectionField();
        paragraph.AddSectionPagesField();
        paragraph.AddDateField();
        paragraph.AddInfoField(InfoFieldType.Title);

        var ddl = DdlOf(paragraph);

        ddl.Should().Contain("\\field(Bookmark)[Name = \"here\"]")
            .And.Contain("\\field(Page)")
            .And.Contain("\\field(PageRef)[Name = \"here\"]")
            .And.Contain("\\field(NumPages)")
            .And.Contain("\\field(Section)")
            .And.Contain("\\field(SectionPages)")
            .And.Contain("\\field(Date)")
            .And.Contain("\\field(Info)[Name = \"Title\"]");
    }

    /// <summary>
    ///   A date field with no format writes an empty bracket pair rather than nothing, because the
    ///   text that follows a field may itself start with a bracket - and a reader with no way to
    ///   tell the two apart reads the text as the field's own attributes.
    /// </summary>
    [Fact]
    public void ADateFieldWithNoFormatStillWritesTheBracketsThatEndIt()
    {
        var paragraph = new Document().AddSection().AddParagraph();
        paragraph.AddDateField();

        DdlOf(paragraph).Should().Contain("\\field(Date)[]");

        var formatted = new Document().AddSection().AddParagraph();
        formatted.AddDateField("yyyy-MM-dd");

        DdlOf(formatted).Should().Contain("\\field(Date)[Format = \"yyyy-MM-dd\"]");
    }

    [Fact]
    public void APageReferenceCarriesItsFormatAlongsideTheBookmarkItPointsAt()
    {
        var paragraph = new Document().AddSection().AddParagraph();
        var reference = paragraph.AddPageRefField("here");
        reference.Format = "ROMAN";

        DdlOf(paragraph).Should().Contain("\\field(PageRef)[Name = \"here\" Format = \"ROMAN\"]");
        new PageRefField("elsewhere").Clone().Name.Should().Be("elsewhere");
    }

    /// <summary>
    ///   Both of these would render as nothing at all, so both refuse to be written rather than
    ///   producing a document with a silent gap where a page number or a title should be.
    /// </summary>
    [Fact]
    public void AFieldWithNothingToStandForRefusesToBeWritten()
    {
        var bookmarked = new Document().AddSection().AddParagraph();
        bookmarked.AddBookmark("");

        var writingABookmark = () => DdlOf(bookmarked);

        writingABookmark.Should().Throw<InvalidOperationException>()
            .WithMessage("*Name*");
    }

    [Fact]
    public void AnInfoFieldRefusesANameThatIsNotOneOfTheDocumentsOwn()
    {
        var field = new Document().AddSection().AddParagraph().AddInfoField(InfoFieldType.Title);

        var naming = () => field.Name = "NoSuchInformation";

        naming.Should().Throw<ArgumentException>();
        field.Clone().Name.Should().Be("Title");
    }

    [Fact]
    public void AFieldIsNeverNullBecauseItsPresenceIsWhatItMeans()
    {
        var paragraph = new Document().AddSection().AddParagraph();

        paragraph.AddDateField().IsNull().Should().BeFalse();
        paragraph.AddInfoField(InfoFieldType.Title).IsNull().Should().BeFalse();
        paragraph.AddBookmark("here").IsNull().Should().BeFalse();
    }

    // ----- footnotes --------------------------------------------------------------------------

    [Fact]
    public void AFootnoteTakesContentBuiltBeforeItAndBuildsItToo()
    {
        var footnote = new Document().AddSection().AddParagraph("text").AddFootnote();

        footnote.Add(new Paragraph());
        footnote.Add(new Table());
        footnote.Add(new Image { Source = new NamedImage("picture.png") });

        footnote.AddParagraph().Should().NotBeNull();
        footnote.AddTable().Should().NotBeNull();
        footnote.AddImage(new NamedImage("picture.png")).Should().NotBeNull();

        footnote.Elements.Count.Should().Be(6);
    }

    [Fact]
    public void AFootnoteMadeFromATextCarriesThatTextAsItsFirstParagraph()
    {
        var paragraph = new Document().AddSection().AddParagraph("text");
        var footnote = paragraph.AddFootnote("the note");

        footnote.Elements.Count.Should().Be(1);
        footnote.Reference = "*";
        footnote.Style = "Normal";
        footnote.Format = new ParagraphFormat { SpaceBefore = Unit.FromPoint(3) };
        footnote.Elements = footnote.Elements.Clone();

        footnote.Format.SpaceBefore.Point.Should().Be(3);
        footnote.Clone().Reference.Should().Be("*");

        DdlOf(paragraph).Should().Contain("\\footnote").And.Contain("the note");
    }

    // ----- finding an image on disk -----------------------------------------------------------

    /// <summary>
    ///   An image is named relative to a search path, and the helper walks that path looking for a
    ///   file that exists. A name with a page number on the end - <c>report.pdf#3</c> - names a page
    ///   of a document rather than a file called that, so the number comes off before the file is
    ///   looked for and is handed back separately.
    /// </summary>
    [Theory]
    [InlineData("report.pdf#3", "report.pdf", 3)]
    [InlineData("report.pdf#12", "report.pdf", 12)]
    [InlineData("picture.png", "picture.png", 0)]
    [InlineData("#123", "#123", 0)]
    [InlineData("", "", 0)]
    public void APageNumberOnTheEndOfANameIsTakenOffBeforeTheFileIsLookedFor(
        string given, string expectedPath, int expectedPage)
    {
        var path = ImageHelper.ExtractPageNumber(given, out var pageNumber);

        path.Should().Be(expectedPath);
        pageNumber.Should().Be(expectedPage);
    }

    [Fact]
    public void ANameWithNoPathAtAllIsRefusedRatherThanSearchedFor()
    {
        var extracting = () => ImageHelper.ExtractPageNumber(null, out _);

        extracting.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AnImageIsFoundInWhicheverSubfolderOfTheSearchPathHoldsIt()
    {
        var root = Path.Combine(Path.GetTempPath(), "PinataLayoutImageHelper" + Guid.NewGuid().ToString("N"));
        var subfolder = Path.Combine(root, "pictures");
        Directory.CreateDirectory(subfolder);
        try
        {
            var file = Path.Combine(subfolder, "picture.png");
            File.WriteAllBytes(file, [0]);

            var found = ImageHelper.GetImageName(root, "picture.png", "nowhere;pictures");

            found.Should().Be(Path.Combine(Path.Combine(root, "pictures"), "picture.png"));
            ImageHelper.InSubfolder(root, "picture.png", "nowhere;pictures", found).Should().BeTrue();
            ImageHelper.InSubfolder(root, "picture.png", "nowhere;pictures", "elsewhere.png")
                .Should().BeFalse();

            ImageHelper.GetImageName(root, "missing.png", "pictures").Should().BeNull();
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    /// <summary>
    ///   An image source that knows nothing but its name; see
    ///   <see cref="SectionAndHeaderFooterTests"/> for why a DOM test needs no more than that.
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
