using System.Linq;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel;
using PinataLayout.DocumentObjectModel.IO;
using Xunit;

namespace PdfPinata.Test.Dom;

/// <summary>
///   Font.Serialize writes a FormattedText's font as <c>\font[Name = ..., Bold = ..., ...]</c>, out
///   of Font's own model. The reader used to resolve those names against the FormattedText, which
///   meant FormattedText had to mirror Font's model in nine delegating [DV] properties - and the
///   mirror was missing Strikethrough, so the name was written and could not be read back.
///
///   Two independent bugs dropped a struck-through font, one in each direction:
///
///     - the writer's CheckWhatIsNotNull never inspected strikethrough, so a font that was bold and
///       struck through looked like "only Bold is set" and was written as the \bold shortcut;
///     - the reader had no FormattedText.Strikethrough to resolve against, so \font[Strikethrough]
///       was reported as an invalid value name and silently discarded.
///
///   The reader now resolves against the Font directly, which is where the names come from.
/// </summary>
public class FormattedTextFontRoundTripTests
{
    private static FormattedText RoundTrip(params System.Action<Font>[] apply)
    {
        var document = new Document();
        var formatted = document.AddSection().AddParagraph().AddFormattedText("text");
        foreach (var set in apply)
            set(formatted.Font);

        var reread = DdlReader.DocumentFromString(DdlWriter.WriteToString(document));

        return ((Paragraph)reread.LastSection.Elements[0])
            .Elements.Cast<DocumentObject>().OfType<FormattedText>().Single();
    }

    [Fact]
    public void StrikethroughSurvivesOnItsOwn()
    {
        RoundTrip(f => f.Strikethrough = Strikethrough.Single)
            .Font.Strikethrough.Should().Be(Strikethrough.Single);
    }

    [Fact]
    public void StrikethroughSurvivesAlongsideAPropertyThatHasAShortcut()
    {
        var result = RoundTrip(
            f => f.Strikethrough = Strikethrough.Single,
            f => f.Bold = true);

        result.Font.Strikethrough.Should().Be(Strikethrough.Single,
            "the \\bold shortcut must not be taken when something else is set too");
        result.Font.Bold.Should().BeTrue();
    }

    [Fact]
    public void EveryFontPropertyTheWriterEmitsCanBeReadBack()
    {
        var result = RoundTrip(
            f => f.Name = "Times New Roman",
            f => f.Size = Unit.FromPoint(14),
            f => f.Bold = true,
            f => f.Italic = true,
            f => f.Underline = Underline.Dash,
            f => f.Strikethrough = Strikethrough.Single,
            f => f.Superscript = true,
            f => f.Color = Colors.Firebrick);

        result.Font.Name.Should().Be("Times New Roman");
        result.Font.Size.Point.Should().BeApproximately(14, 1e-6);
        result.Font.Bold.Should().BeTrue();
        result.Font.Italic.Should().BeTrue();
        result.Font.Underline.Should().Be(Underline.Dash);
        result.Font.Strikethrough.Should().Be(Strikethrough.Single);
        result.Font.Superscript.Should().BeTrue();
        result.Font.Color.Should().Be(Colors.Firebrick);
    }

    /// <summary>
    ///   The shortcuts are what keep ordinary DDL readable, so they have to still be taken when a
    ///   single property really is the only one set.
    /// </summary>
    [Theory]
    [InlineData("bold")]
    [InlineData("italic")]
    public void ASinglePropertyStillUsesItsShortcut(string keyword)
    {
        var document = new Document();
        var formatted = document.AddSection().AddParagraph().AddFormattedText("text");
        if (keyword == "bold")
            formatted.Font.Bold = true;
        else
            formatted.Font.Italic = true;

        var ddl = DdlWriter.WriteToString(document);

        ddl.Should().Contain((char)92 + keyword + "{", "a lone property keeps its keyword form");
    }

    [Fact]
    public void AStyledFormattedTextStillRoundTrips()
    {
        var document = new Document();
        document.Styles.AddStyle("Emphasis", "Normal").Font.Italic = true;
        var formatted = document.AddSection().AddParagraph().AddFormattedText("text", "Emphasis");
        formatted.Font.Bold = true;

        var reread = DdlReader.DocumentFromString(DdlWriter.WriteToString(document));
        var result = ((Paragraph)reread.LastSection.Elements[0])
            .Elements.Cast<DocumentObject>().OfType<FormattedText>().Single();

        result.Style.Should().Be("Emphasis");
        result.Font.Bold.Should().BeTrue();
    }
}
