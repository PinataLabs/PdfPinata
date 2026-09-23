using System;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel.Fields;
using Xunit;

namespace PinataLayout.DocumentObjectModel.Tests;

/// <summary>
///   What a field reads as, asked directly. The answer used to live inside
///   <c>ParagraphRenderer</c>, so pinning that a section field formats one as "A" cost a rendered
///   page, a saved document and a read of its content stream; every case here is a function call
///   against a hand-built context instead.
/// </summary>
public class FieldEvaluatorTests
{
    /// <summary>
    ///   A document laid out and finished: three pages, the second section, both counts known and
    ///   one bookmark placed. Individual tests vary what they are about and leave the rest alone.
    /// </summary>
    private static FieldEvaluationContext Paginated() => new()
    {
        DisplayPageNumber = 2,
        SectionNumber = 2,
        NumberOfPages = 3,
        PagesInSection = 2,
        PrintDate = new DateTime(2026, 8, 23, 14, 30, 0),
        ResolveBookmarkPage = name => name == "Chapter" ? 3 : null
    };

    private static Paragraph AParagraph() => new Document().AddSection().AddParagraph();

    [Fact]
    public void APageFieldReadsAsThePageItIsOn()
    {
        var field = AParagraph().AddPageField();

        FieldEvaluator.Evaluate(field, Paginated()).Should().Be("2");
    }

    [Fact]
    public void ASectionFieldReadsAsTheSectionItIsIn()
    {
        var field = AParagraph().AddSectionField();

        FieldEvaluator.Evaluate(field, Paginated()).Should().Be("2");
    }

    [Fact]
    public void ANumPagesFieldReadsAsTheLengthOfTheDocument()
    {
        var field = AParagraph().AddNumPagesField();

        FieldEvaluator.Evaluate(field, Paginated()).Should().Be("3");
    }

    [Fact]
    public void ASectionPagesFieldReadsAsTheLengthOfItsSection()
    {
        var field = AParagraph().AddSectionPagesField();

        FieldEvaluator.Evaluate(field, Paginated()).Should().Be("2");
    }

    [Fact]
    public void APageRefFieldReadsAsThePageItsBookmarkIsOn()
    {
        var field = AParagraph().AddPageRefField("Chapter");

        FieldEvaluator.Evaluate(field, Paginated()).Should().Be("3");
    }

    [Fact]
    public void ADateFieldReadsAsThePrintDateInTheFormatItNames()
    {
        var field = AParagraph().AddDateField("yyyy-MM-dd");

        FieldEvaluator.Evaluate(field, Paginated()).Should().Be("2026-08-23");
    }

    /// <summary>
    ///   Nothing in the context says anything about a document's title; the field walks up to the
    ///   document it belongs to and reads it there.
    /// </summary>
    [Fact]
    public void AnInfoFieldReadsWhatTheDocumentRecordsUnderThatName()
    {
        var document = new Document { Info = { Title = "The Annual Report" } };
        var field = document.AddSection().AddParagraph().AddInfoField(InfoFieldType.Title);

        FieldEvaluator.Evaluate(field, Paginated()).Should().Be("The Annual Report");
    }

    [Fact]
    public void AnInfoFieldNamingSomethingTheDocumentDoesNotRecordReadsAsNothing()
    {
        var document = new Document();
        var field = document.AddSection().AddParagraph().AddInfoField(InfoFieldType.Subject);

        FieldEvaluator.Evaluate(field, Paginated()).Should().BeEmpty();
    }

    /// <summary>
    ///   Not the same as a document that records nothing under the name: a field belonging to no
    ///   document has nowhere to look, and answering "" would hide the caller's mistake behind a
    ///   blank in the output.
    /// </summary>
    [Fact]
    public void AnInfoFieldBelongingToNoDocumentIsRefusedRatherThanReadAsBlank()
    {
        // A clone is the copy of a field with its parent dropped, which is exactly a field that
        // belongs to nothing.
        var field = AParagraph().AddInfoField(InfoFieldType.Title).Clone();
        field.Document.Should().BeNull("the case is about a field with nowhere to look");

        var evaluate = () => FieldEvaluator.Evaluate(field, Paginated());

        evaluate.Should().Throw<ArgumentException>().WithMessage("*no document*");
    }

    [Theory]
    [InlineData("", "27")]
    [InlineData("ROMAN", "XXVII")]
    [InlineData("roman", "xxvii")]
    [InlineData("ALPHABETIC", "AA")]
    [InlineData("alphabetic", "aa")]
    public void ANumericFieldIsWrittenInTheFormatItNames(string format, string expected)
    {
        var field = AParagraph().AddPageField();
        field.Format = format;
        var context = Paginated();
        context.DisplayPageNumber = 27;

        FieldEvaluator.Evaluate(field, context).Should().Be(expected);
    }

    /// <summary>
    ///   The three facts a document that has not finished being laid out genuinely does not have.
    ///   Each answers null - never a placeholder, which is a rendering pipeline's decision about
    ///   what to draw in the meantime rather than anything true about the field.
    /// </summary>
    [Fact]
    public void ANumPagesFieldAskedBeforeTheDocumentIsFinishedAnswersThatItCannotSayYet()
    {
        var field = AParagraph().AddNumPagesField();
        var context = Paginated();
        context.NumberOfPages = null;

        FieldEvaluator.Evaluate(field, context).Should().BeNull();
    }

    [Fact]
    public void ASectionPagesFieldAskedBeforeItsSectionIsFinishedAnswersThatItCannotSayYet()
    {
        var field = AParagraph().AddSectionPagesField();
        var context = Paginated();
        context.PagesInSection = null;

        FieldEvaluator.Evaluate(field, context).Should().BeNull();
    }

    [Fact]
    public void APageRefFieldToABookmarkNotPlacedYetAnswersThatItCannotSayYet()
    {
        var field = AParagraph().AddPageRefField("NotPlacedAnywhere");

        FieldEvaluator.Evaluate(field, Paginated()).Should().BeNull();
    }

    /// <summary>
    ///   A context that answers no bookmark at all is the same case as one that does not know this
    ///   name, and must not be a null reference.
    /// </summary>
    [Fact]
    public void APageRefFieldWithNoWayToResolveBookmarksAnswersThatItCannotSayYet()
    {
        var field = AParagraph().AddPageRefField("Chapter");
        var context = Paginated();
        context.ResolveBookmarkPage = null;

        FieldEvaluator.Evaluate(field, context).Should().BeNull();
    }

    /// <summary>
    ///   One case per type in PinataLayout.DocumentObjectModel.Fields, because the predicate this
    ///   replaces tested for <c>DocumentInfo</c> - the document's own info object, which is never a
    ///   paragraph's leaf - and so never recognised the <see cref="InfoField"/> that is. A heading
    ///   built with one lost that text from its outline entry, and nothing said so.
    /// </summary>
    [Theory]
    [InlineData(typeof(PageField), true)]
    [InlineData(typeof(PageRefField), true)]
    [InlineData(typeof(NumPagesField), true)]
    [InlineData(typeof(SectionField), true)]
    [InlineData(typeof(SectionPagesField), true)]
    [InlineData(typeof(DateField), true)]
    [InlineData(typeof(InfoField), true)]
    [InlineData(typeof(BookmarkField), false)]
    [InlineData(typeof(Text), false)]
    public void AFieldWithAValueIsToldApartFromOneWithout(Type type, bool isField)
    {
        // Every one of them has a parameterless constructor, public or internal; what a field is
        // asked here is only its type, so none of them needs a paragraph to sit in.
        var leaf = (DocumentObject)Activator.CreateInstance(type, nonPublic: true);

        FieldEvaluator.IsField(leaf).Should().Be(isField);
    }

    [Fact]
    public void SomethingThatIsNotAFieldIsRefusedRatherThanReadAsBlank()
    {
        var text = AParagraph().AddText("plain");

        var evaluate = () => FieldEvaluator.Evaluate(text, Paginated());

        evaluate.Should().Throw<ArgumentException>().WithMessage("*not a field*");
    }
}
