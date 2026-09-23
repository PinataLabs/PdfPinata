using System;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel;
using Xunit;

namespace PdfPinata.Test.Dom;

/// <summary>
///   What a style may be based on. The setter is the only thing standing between the styles
///   collection and a chain that never ends, so most of it is refusal: a base style that is not
///   there, a base style that leads back to the style being set, and the two built-in roots that
///   are the end of every chain and cannot themselves be given one.
///   <para>
///   The chain matters because every style reads through it. A cycle here is not a wrong answer
///   later, it is a renderer that walks base styles until the stack runs out, which is why the
///   setter would rather throw than store one.
///   </para>
/// </summary>
public class StyleBaseStyleTests
{
    /// <summary>A document with Derived : Normal, and Grandchild : Derived below it.</summary>
    private static Document WithAChain()
    {
        var document = new Document();
        document.Styles.AddStyle("Derived", Style.DefaultParagraphName);
        document.Styles.AddStyle("Grandchild", "Derived");
        return document;
    }

    // ----- what it accepts ------------------------------------------------------------------------

    [Fact]
    public void AStyleCanBeRebasedOnAnotherStyleThatExists()
    {
        var document = WithAChain();
        document.Styles.AddStyle("Sibling", Style.DefaultParagraphName);

        document.Styles["Grandchild"].BaseStyle = "Sibling";

        document.Styles["Grandchild"].BaseStyle.Should().Be("Sibling");
    }

    [Fact]
    public void ABaseStyleIsFoundWhateverCaseItIsWrittenIn()
    {
        // The whole collection is searched case-insensitively, so the name stored is the one
        // given rather than the one the style was declared under.
        var document = WithAChain();

        document.Styles["Grandchild"].BaseStyle = "nOrMaL";

        document.Styles["Grandchild"].BaseStyle.Should().Be("nOrMaL");
        document.Styles[document.Styles["Grandchild"].BaseStyle].Should().BeSameAs(document.Styles.Normal);
    }

    /// <summary>
    ///   Assigning a style its own base back is allowed and is the one path that skips every
    ///   check - which is what lets the stored spelling change without the collection being
    ///   searched again. The comment in the source dates the carve-out to 2007.
    /// </summary>
    [Fact]
    public void AStyleCanBeGivenTheBaseStyleItAlreadyHasInADifferentCase()
    {
        var document = WithAChain();

        document.Styles["Derived"].BaseStyle = "NORMAL";

        document.Styles["Derived"].BaseStyle.Should().Be("NORMAL");
    }

    // ----- what it refuses ------------------------------------------------------------------------

    [Fact]
    public void AStyleCannotBeBasedOnNothing()
    {
        var document = WithAChain();

        var assign = () => document.Styles["Derived"].BaseStyle = null;

        assign.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AStyleThatHasABaseCannotHaveItTakenAway()
    {
        var document = WithAChain();

        var assign = () => document.Styles["Derived"].BaseStyle = "";

        assign.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AStyleCannotBeBasedOnOneThatIsNotThere()
    {
        var document = WithAChain();

        var assign = () => document.Styles["Derived"].BaseStyle = "NoSuchStyle";

        assign.Should().Throw<ArgumentException>().WithMessage("*NoSuchStyle*",
            "the name that could not be found is the useful half of the complaint");
    }

    [Theory]
    [InlineData(Style.DefaultParagraphName)]
    [InlineData(Style.DefaultParagraphFontName)]
    public void TheTwoRootStylesCannotBeGivenABaseStyle(string rootStyleName)
    {
        // They are where every chain ends. Giving either one a base would make the end of the
        // chain a link in it.
        var document = WithAChain();

        var assign = () => document.Styles[rootStyleName].BaseStyle = "Derived";

        assign.Should().Throw<ArgumentException>().WithMessage("*cannot be altered*");
    }

    [Fact]
    public void AStyleCannotBeBasedOnItself()
    {
        var document = WithAChain();

        var assign = () => document.Styles["Derived"].BaseStyle = "Derived";

        assign.Should().Throw<ArgumentException>().WithMessage("*circular*");
    }

    [Fact]
    public void AStyleCannotBeBasedOnOneThatIsAlreadyBasedOnIt()
    {
        // Derived is Grandchild's base, so basing Derived on Grandchild closes the loop. The
        // setter walks the candidate's own chain looking for the style it is about to change.
        var document = WithAChain();

        var assign = () => document.Styles["Derived"].BaseStyle = "Grandchild";

        assign.Should().Throw<ArgumentException>().WithMessage("*circular*");
    }

    [Fact]
    public void AStyleCannotBeBasedOnOneFurtherDownItsOwnChain()
    {
        // The same loop two links long rather than one, which is the case a check that only
        // compared the immediate base would miss.
        var document = WithAChain();
        document.Styles.AddStyle("GreatGrandchild", "Grandchild");

        var assign = () => document.Styles["Derived"].BaseStyle = "GreatGrandchild";

        assign.Should().Throw<ArgumentException>().WithMessage("*circular*");
    }

    [Fact]
    public void NothingIsStoredWhenTheAssignmentIsRefused()
    {
        // The check that matters: a refusal that had already written the value would leave the
        // cycle in place and only complain about it.
        var document = WithAChain();

        document.Invoking(d => d.Styles["Derived"].BaseStyle = "Grandchild")
            .Should().Throw<ArgumentException>();

        document.Styles["Derived"].BaseStyle.Should().Be(Style.DefaultParagraphName);
    }

    /// <summary>
    ///   A rough edge, pinned rather than argued with. The setter reads its parent collection to
    ///   look the candidate up, so a style that has not been added to a document yet has no
    ///   collection to search and the public setter throws NullReferenceException rather than
    ///   saying what is wrong. Building a style before adding it is otherwise a reasonable thing
    ///   to do - <c>Styles.Add</c> exists for exactly that.
    /// </summary>
    [Fact]
    public void AStyleNotYetInADocumentCannotBeBasedOnAnythingAtAll()
    {
        var loose = new Style("Loose", Style.DefaultParagraphName);

        var assign = () => loose.BaseStyle = "Heading1";

        assign.Should().Throw<NullReferenceException>();
    }
}
