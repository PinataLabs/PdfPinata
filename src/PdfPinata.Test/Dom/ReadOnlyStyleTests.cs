using System;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel;
using Xunit;

namespace PdfPinata.Test.Dom;

/// <summary>
///   DefaultParagraphFont is the one built-in style marked read-only, and it used to enforce that by
///   handing back a clone of its ParagraphFormat on every read. That is not enforcement. Clone()
///   nulls the parent, so an assignment landed on an object with no way of knowing the write was
///   pointless, the clone was discarded when the expression ended, and the caller got no exception,
///   no diagnostic and no effect:
///
///     document.Styles[0].Font.Bold = true;
///     document.Styles[0].Font.Bold;          // false
///
///   The clone is still handed out, because reading a built-in style is legitimate. It now carries
///   its Style as its parent, so a write can find it and refuse.
/// </summary>
public class ReadOnlyStyleTests
{
    // By index, not by name: Styles[Style.DefaultParagraphFontName] returns null for this one,
    // which is its own oddity and not what these tests are about.
    static Style DefaultParagraphFont() => (Style)new Document().Styles[0];

    [Fact]
    public void TheBuiltInCharacterStyleIsReadOnly()
    {
        DefaultParagraphFont().IsReadOnly.Should().BeTrue();
    }

    [Fact]
    public void WritingToAReadOnlyStylesFontThrows()
    {
        var style = DefaultParagraphFont();

        var write = () => style.Font.Bold = true;

        write.Should().Throw<InvalidOperationException>()
            .WithMessage("*read-only*")
            .WithMessage("*AddStyle*", "the message says what to do instead");
    }

    [Fact]
    public void WritingToAReadOnlyStylesParagraphFormatThrows()
    {
        var style = DefaultParagraphFont();

        var write = () => style.ParagraphFormat.Alignment = ParagraphAlignment.Center;

        write.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AWriteThatUsedToVanishNoLongerDoes()
    {
        var style = DefaultParagraphFont();

        try { style.Font.Bold = true; } catch (InvalidOperationException) { }

        style.Font.Bold.Should().BeFalse(
            "it was never going to be applied - the point is that the caller is now told");
    }

    [Fact]
    public void ReadingAReadOnlyStyleStillWorks()
    {
        var style = DefaultParagraphFont();

        style.Invoking(s => { _ = s.Font.Name; _ = s.Font.Bold; _ = s.ParagraphFormat.Alignment; })
            .Should().NotThrow("inspecting a built-in style is legitimate");
    }

    [Fact]
    public void AUserDefinedStyleIsUnaffected()
    {
        var document = new Document();
        var style = document.Styles.AddStyle("Mine", Style.DefaultParagraphName);

        style.IsReadOnly.Should().BeFalse();
        style.Font.Bold = true;
        style.ParagraphFormat.Alignment = ParagraphAlignment.Center;

        style.Font.Bold.Should().BeTrue("a style of your own still takes writes");
        style.ParagraphFormat.Alignment.Should().Be(ParagraphAlignment.Center);
    }

    /// <summary>
    ///   A style based on the read-only one is not itself read-only. The guard walks the parent
    ///   chain of the object being written, not the style inheritance chain.
    /// </summary>
    [Fact]
    public void AStyleBasedOnAReadOnlyStyleIsWritable()
    {
        var document = new Document();
        var style = document.Styles.AddStyle("Derived", Style.DefaultParagraphFontName);

        style.Font.Italic = true;

        style.Font.Italic.Should().BeTrue();
    }

    [Fact]
    public void TheDocumentStillBuildsAndSerializes()
    {
        var document = new Document();
        document.AddSection().AddParagraph("text").Format.Font.Bold = true;

        document.Invoking(d => PinataLayout.DocumentObjectModel.IO.DdlWriter.WriteToString(d))
            .Should().NotThrow("the built-in styles are read during serialization");
    }
}
