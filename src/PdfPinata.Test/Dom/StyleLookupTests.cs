using System;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel;
using PinataLayout.DocumentObjectModel.IO;
using Xunit;

namespace PdfPinata.Test.Dom;

/// <summary>
///   Styles[string] used to start its search at index 1, skipping DefaultParagraphFont, with the
///   comment "DefaultParagraphFont cannot be modified". That is protection by being unreachable,
///   and both GetIndex and the integer indexer saw through it - they start at 0.
///
///   Style.Serialize looked the base style up both ways, by index into an unused local and by name
///   into the one it used, so for a style based on DefaultParagraphFont the second returned null.
///   refFormat was null-guarded; the line after it was not, and it read a field of a variable that
///   nothing ever used. Serializing such a document threw NullReferenceException.
///
///   The lookup now starts at 0, and the two unread locals are gone. Being unmodifiable is enforced
///   where it belongs - every setter on the style's ParagraphFormat and Font throws. See
///   ReadOnlyStyleTests.
/// </summary>
public class StyleLookupTests
{
    static Document WithDerivedStyle(string baseStyleName)
    {
        var document = new Document();
        Style derived = document.Styles.AddStyle("Derived", baseStyleName);
        derived.Font.Italic = true;
        document.AddSection().AddParagraph("text").Style = "Derived";
        return document;
    }

    [Fact]
    public void AStyleBasedOnDefaultParagraphFontSerializes()
    {
        Document document = WithDerivedStyle(Style.DefaultParagraphFontName);

        document.Invoking(d => DdlWriter.WriteToString(d))
            .Should().NotThrow<NullReferenceException>(
                "the base style is found by name now, so there is a reference format to compare against");
    }

    [Fact]
    public void AStyleBasedOnDefaultParagraphFontRoundTrips()
    {
        Document document = WithDerivedStyle(Style.DefaultParagraphFontName);

        Document reread = DdlReader.DocumentFromString(DdlWriter.WriteToString(document));

        Style derived = reread.Styles["Derived"];
        derived.Should().NotBeNull();
        derived.BaseStyle.Should().Be(Style.DefaultParagraphFontName);
        derived.Font.Italic.Should().BeTrue();
    }

    [Fact]
    public void AStyleBasedOnNormalStillSerializes()
    {
        WithDerivedStyle(Style.DefaultParagraphName)
            .Invoking(d => DdlWriter.WriteToString(d))
            .Should().NotThrow();
    }

    /// <summary>
    ///   The lookup and GetIndex disagreed for exactly one style. They must agree for all of them.
    /// </summary>
    [Fact]
    public void LookupByNameAgreesWithGetIndex()
    {
        Styles styles = new Document().Styles;

        for (int index = 0; index < styles.Count; index++)
        {
            string name = ((Style)styles[index]).Name;

            styles.GetIndex(name).Should().Be(index, $"GetIndex should find '{name}' where it is");
            styles[name].Should().NotBeNull($"'{name}' is in the collection and must be findable by name");
            styles[name].Name.Should().Be(name);
        }
    }

    [Fact]
    public void TheBuiltInCharacterStyleIsNowFindableByName()
    {
        Styles styles = new Document().Styles;

        styles[Style.DefaultParagraphFontName].Should().NotBeNull();
        styles[Style.DefaultParagraphFontName].Should().BeSameAs(styles[0]);
    }

    [Fact]
    public void AnUnknownNameStillReturnsNull()
    {
        new Document().Styles["NoSuchStyle"].Should().BeNull();
    }

    /// <summary>
    ///   Being findable is not being writable. The two were conflated, which is how the skip came
    ///   to exist in the first place.
    /// </summary>
    [Fact]
    public void FindingItByNameDoesNotMakeItWritable()
    {
        Styles styles = new Document().Styles;
        Style builtIn = styles[Style.DefaultParagraphFontName];

        builtIn.Invoking(s => s.Font.Bold = true).Should().Throw<InvalidOperationException>();
    }
}
