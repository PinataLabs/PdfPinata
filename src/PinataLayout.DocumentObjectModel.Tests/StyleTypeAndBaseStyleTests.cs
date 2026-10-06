using System;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel.IO;
using PinataLayout.DocumentObjectModel.Internals;
using TUnit.Core;

namespace PinataLayout.DocumentObjectModel.Tests;

/// <summary>
///   What a <see cref="Style"/> answers about itself before it is used: its name, its base style
///   and — the one that is worked out rather than stored — its <see cref="Style.Type"/>.
///   <para>
///   A style's type is not written down. It is inherited from whatever the style is based on, all
///   the way up to the two root styles, and the walk up is where it can go wrong: a style whose
///   base style names nothing that exists has no type to inherit, and says so rather than
///   answering a default.
///   </para>
/// </summary>
public class StyleTypeAndBaseStyleTests
{
    [Test]
    public void AStyleMustBeGivenAName()
    {
        ((Action)(() => _ = new Style(null!, "Normal"))).Should().Throw<ArgumentNullException>();
        ((Action)(() => _ = new Style("", "Normal"))).Should().Throw<ArgumentException>();
    }

    [Test]
    public void AStyleKeepsTheNameAndBaseStyleItWasGiven()
    {
        var style = new Style("Mine", "Normal");

        style.Name.Should().Be("Mine");
        style.BaseStyle.Should().Be("Normal");
        style.BuildIn.Should().BeFalse();
    }

    [Test]
    public void AStyleBasedOnTheDefaultParagraphFontIsACharacterStyle()
    {
        var style = new Style("Mine", Style.DefaultParagraphFontName);

        style.Type.Should().Be(StyleType.Character);
    }

    [Test]
    public void AStyleTakesItsTypeFromTheStyleItIsBasedOn()
    {
        var document = new Document();
        var style = document.AddStyle("Mine", "Normal");

        style.Type.Should().Be(StyleType.Paragraph);
    }

    [Test]
    public void AStyleWithNoOwningCollectionCannotLookItsBaseStyleUp()
    {
        var style = new Style("Mine", "Normal");

        var asking = () => style.GetBaseStyle();

        asking.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void ARootStyleIsBasedOnNothingAtAll()
    {
        var document = new Document();

        document.Styles[Style.DefaultParagraphName].GetBaseStyle().Should().BeNull();
        document.Styles[Style.DefaultParagraphFontName].GetBaseStyle().Should().BeNull();
    }

    [Test]
    public void TheDefaultParagraphFontIsReadOnlyAndNormalIsNot()
    {
        var document = new Document();

        document.Styles[Style.DefaultParagraphFontName].IsReadOnly.Should().BeTrue();
        document.Styles[Style.DefaultParagraphName].IsReadOnly.Should().BeFalse();
    }

    [Test]
    public void AStyleMustBeAskedForSomethingByName()
    {
        var style = new Document().Styles[Style.DefaultParagraphName];

        ((Action)(() => style.GetValue(null!, GV.ReadWrite))).Should().Throw<ArgumentNullException>();
        ((Action)(() => style.GetValue("", GV.ReadWrite))).Should().Throw<ArgumentException>();
    }

    [Test]
    public void AFontPropertyAskedForByNameIsAnsweredByTheParagraphFormat()
    {
        var document = new Document();
        var style = document.AddStyle("Mine", "Normal");
        style.Font.Name = "Arial";

        style.GetValue("Font.Name", GV.ReadWrite).Should().Be("Arial");
    }

    [Test]
    public void AStyleCarriesACommentThatIsWrittenOutAboveIt()
    {
        var document = new Document();
        var style = document.AddStyle("Mine", "Normal");

        style.Comment.Should().BeEmpty();

        style.Comment = "the one for headings";

        var ddl = DdlWriter.WriteToString(document);

        ddl.Should().Contain("the one for headings");
    }

    [Test]
    public void AFontAssignedToAStyleGoesToItsParagraphFormat()
    {
        var document = new Document();
        var style = document.AddStyle("Mine", "Normal");
        var font = new Font { Name = "Arial", Size = 11 };

        style.Font = font;

        style.ParagraphFormat.Font.Name.Should().Be("Arial");
    }

    [Test]
    public void AParagraphFormatAssignedToAStyleReplacesTheOneItHad()
    {
        var document = new Document();
        var style = document.AddStyle("Mine", "Normal");
        var format = document.AddSection().AddParagraph("x").Format;
        format.SpaceBefore = "3cm";

        style.ParagraphFormat = format.Clone();

        style.ParagraphFormat.SpaceBefore.Centimeter.Should().BeApproximately(3, 1e-4);
    }

    [Test]
    public void AStyleClonesItselfWithItsOwnFormat()
    {
        var document = new Document();
        var style = document.AddStyle("Mine", "Normal");
        style.Font.Name = "Arial";

        var clone = style.Clone();
        clone.Font.Name = "Times";

        style.Font.Name.Should().Be("Arial");
        clone.Font.Name.Should().Be("Times");
    }

    /// <summary>
    ///   A built-in style whose base style is changed is written out with the new base named after
    ///   it, where one left alone is written with its name only. Both arms of that decision are
    ///   reached only through serialization, which is why this asserts on the DDL.
    /// </summary>
    [Test]
    public void ABuiltInStyleGivenANewBaseStyleIsWrittenOutWithIt()
    {
        var document = new Document();
        document.AddStyle("Mine", "Normal");
        var heading = document.Styles["Heading1"];
        heading.BaseStyle = "Mine";
        heading.Font.Size = 20;

        var ddl = DdlWriter.WriteToString(document);

        ddl.Should().Contain("Heading1 : Mine");
    }

    [Test]
    public void ABuiltInStyleLeftOnItsOwnBaseIsWrittenOutByNameAlone()
    {
        var document = new Document();
        document.Styles["Heading1"].Font.Size = 20;

        var ddl = DdlWriter.WriteToString(document);

        ddl.Should().Contain("Heading1").And.NotContain("Heading1 :");
    }
}
