using System;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel;
using Xunit;

namespace PdfPinata.Test.Dom;

/// <summary>
///   Adding a style to a document. <c>Document.AddStyle</c> checks its two arguments and hands
///   them to <c>Styles.AddStyle</c>, which is where the base style is resolved and the style is
///   placed - so the interesting behaviour is what the collection does with a name it has already
///   seen, not what the wrapper does with a null.
/// </summary>
public class DocumentAddStyleTests
{
    [Fact]
    public void ANewStyleIsAddedAndFindableByName()
    {
        var document = new Document();

        var added = document.AddStyle("Quiet", Style.DefaultParagraphName);

        added.Name.Should().Be("Quiet");
        added.BaseStyle.Should().Be(Style.DefaultParagraphName);
        document.Styles["Quiet"].Should().BeSameAs(added);
    }

    [Fact]
    public void ANewStyleTakesTheTypeOfTheStyleItIsBasedOn()
    {
        var document = new Document();

        document.AddStyle("Loud", Style.DefaultParagraphFontName)
            .Type.Should().Be(StyleType.Character);
        document.AddStyle("Quiet", Style.DefaultParagraphName)
            .Type.Should().Be(StyleType.Paragraph);
    }

    [Fact]
    public void AStyleCanBeBuiltOnOneAddedAMomentAgo()
    {
        var document = new Document();

        document.AddStyle("Quiet", Style.DefaultParagraphName);
        var quieter = document.AddStyle("Quieter", "Quiet");

        quieter.BaseStyle.Should().Be("Quiet");
    }

    [Theory]
    [InlineData(null, Style.DefaultParagraphName, "name")]
    [InlineData("Quiet", null, "baseStyle")]
    public void NeitherNameCanBeNull(string name, string baseStyle, string offendingArgument)
    {
        var add = () => new Document().AddStyle(name, baseStyle);

        add.Should().Throw<ArgumentNullException>()
            .And.ParamName.Should().Be(offendingArgument);
    }

    [Theory]
    [InlineData("", Style.DefaultParagraphName)]
    [InlineData("Quiet", "")]
    public void NeitherNameCanBeEmpty(string name, string baseStyle)
    {
        var add = () => new Document().AddStyle(name, baseStyle);

        add.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AStyleCannotBeBuiltOnOneThatIsNotThere()
    {
        var add = () => new Document().AddStyle("Quiet", "NoSuchStyle");

        add.Should().Throw<ArgumentException>().WithMessage("*NoSuchStyle*");
    }

    [Fact]
    public void AddingANameThatIsAlreadyThereReplacesItRatherThanAddingASecond()
    {
        var document = new Document();
        var before = document.Styles.Count;

        document.AddStyle("Quiet", Style.DefaultParagraphName);
        document.AddStyle("Quiet", Style.DefaultParagraphName);

        document.Styles.Count.Should().Be(before + 1);
    }

    [Fact]
    public void TheStyleHandedBackIsTheOneTheDocumentIsHolding()
    {
        var document = new Document();
        document.AddStyle("Quiet", Style.DefaultParagraphName);

        var redefined = document.AddStyle("Quiet", Style.DefaultParagraphName);
        redefined.Font.Bold = true;

        document.Styles["Quiet"].Font.Bold.Should()
            .BeTrue("writing to the style that was handed back must reach the document");
    }
}
