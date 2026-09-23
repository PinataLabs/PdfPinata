using System;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel;
using Xunit;

namespace PdfPinata.Test.Dom;

/// <summary>
///   Merging one font over another. Every property of a <see cref="Font"/> is nullable underneath
///   and null means "not stated", so applying a font copies across the properties it states and
///   leaves the rest of the target alone. It is how <c>ParagraphElements.AddFormattedText</c>
///   builds a run's font from the one it is given.
///   <para>
///   The property to watch is the pair: subscript and superscript are mutually exclusive, so the
///   second is applied only when the first was not stated. A font stating both is a font that
///   cannot be honoured, and what happens then is worth pinning rather than assuming.
///   </para>
/// </summary>
public class FontApplyFontTests
{
    /// <summary>A font with every property stated, so that any of them failing to copy shows.</summary>
    private static Font FullyStated() => new("Courier New")
    {
        Size = 14,
        Bold = true,
        Italic = true,
        Underline = Underline.Dash,
        Strikethrough = Strikethrough.Single,
        Color = Colors.Red,
        Subscript = true
    };

    [Fact]
    public void EveryPropertyTheGivenFontStatesIsCopiedOver()
    {
        var target = new Font();

        target.ApplyFont(FullyStated());

        target.Name.Should().Be("Courier New");
        target.Size.Point.Should().BeApproximately(14, 1e-4);
        target.Bold.Should().BeTrue();
        target.Italic.Should().BeTrue();
        target.Underline.Should().Be(Underline.Dash);
        target.Strikethrough.Should().Be(Strikethrough.Single);
        target.Color.Should().Be(Colors.Red);
        target.Subscript.Should().BeTrue();
    }

    [Fact]
    public void APropertyTheGivenFontDoesNotStateLeavesTheTargetAsItWas()
    {
        // The point of the whole method: an empty font applied over a stated one changes nothing.
        var target = FullyStated();

        target.ApplyFont(new Font());

        target.Name.Should().Be("Courier New");
        target.Size.Point.Should().BeApproximately(14, 1e-4);
        target.Bold.Should().BeTrue();
        target.Italic.Should().BeTrue();
        target.Underline.Should().Be(Underline.Dash);
        target.Strikethrough.Should().Be(Strikethrough.Single);
        target.Color.Should().Be(Colors.Red);
        target.Subscript.Should().BeTrue();
    }

    [Fact]
    public void AStatedPropertyOverwritesAStatedOne()
    {
        var target = new Font("Courier New") { Bold = true, Size = 14 };

        target.ApplyFont(new Font("Times New Roman") { Bold = false, Size = 9 });

        target.Name.Should().Be("Times New Roman");
        target.Bold.Should().BeFalse("false is stated, and stated is not the same as absent");
        target.Size.Point.Should().BeApproximately(9, 1e-4);
    }

    [Fact]
    public void OnlyTheStatedPropertiesOfAPartlyStatedFontAreCopied()
    {
        var target = FullyStated();

        target.ApplyFont(new Font { Italic = false });

        target.Italic.Should().BeFalse("it was stated");
        target.Bold.Should().BeTrue("it was not");
        target.Name.Should().Be("Courier New");
    }

    [Fact]
    public void AnEmptyNameIsNotAName()
    {
        // The name is the one property held as a string rather than a nullable, so "not stated"
        // and "stated as empty" are the same thing to it.
        var target = new Font("Courier New");

        target.ApplyFont(new Font(""));

        target.Name.Should().Be("Courier New");
    }

    // ----- the pair -----------------------------------------------------------------------------

    [Fact]
    public void SuperscriptIsCopiedWhenSubscriptIsNotStated()
    {
        var target = new Font();

        target.ApplyFont(new Font { Superscript = true });

        target.Superscript.Should().BeTrue();
    }

    /// <summary>
    ///   The two cannot both be stated, so the <c>else</c> in the merge never has to choose
    ///   between them: each setter unstates the other, and a font carries whichever was written
    ///   last. Worth pinning because the merge reads the fields rather than the properties, so it
    ///   would be perfectly capable of applying both if a font could hold both.
    /// </summary>
    [Fact]
    public void AFontStatesOneOfSubscriptAndSuperscriptRatherThanBoth()
    {
        var source = new Font { Subscript = true };
        source.Superscript = true;

        var target = new Font();
        target.ApplyFont(source);

        target.Superscript.Should().BeTrue("it was written last");
        target.Subscript.Should().BeFalse("writing superscript unstated it");
    }

    [Fact]
    public void ApplyingASubscriptFontOverASuperscriptOneTurnsTheTargetRound()
    {
        var target = new Font { Superscript = true };

        target.ApplyFont(new Font { Subscript = true });

        target.Subscript.Should().BeTrue();
        target.Superscript.Should().BeFalse("the target cannot be both either");
    }

    // ----- refusals -----------------------------------------------------------------------------

    [Fact]
    public void ThereIsNoFontToApply()
    {
        var apply = () => new Font().ApplyFont(null);

        apply.Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("font");
    }
}
