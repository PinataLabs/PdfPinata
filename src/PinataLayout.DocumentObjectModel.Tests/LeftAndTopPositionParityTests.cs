using System;
using System.Globalization;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel.Shapes;
using TUnit.Core;

namespace PinataLayout.DocumentObjectModel.Tests;

/// <summary>
///   <c>LeftPosition.Parse</c> and <c>TopPosition.Parse</c> are the same eleven-branch parse
///   written twice, byte for byte identical apart from the type name: trim, look at the first
///   character, and hand the string either to <see cref="Unit.Parse"/> or to
///   <c>Enum.Parse(typeof(ShapePosition), …)</c>. Neither had ever been executed.
///   <para>
///   This is the pattern <c>CLAUDE.md</c> warns about for the two lexers and the category axis
///   renderers, and the third time it has come up in this backlog - F6, F12 and F18 were each one
///   half of a near-copy pair carrying a guard its twin lacked. So everything the two must agree
///   about is asserted from one table, against both, and a divergence fails rather than passing
///   quietly in one assembly.
///   </para>
///   <para>
///   They agree, and they were wrong together, exactly as the <c>ExtractPageNumber</c> pair was:
///   the emptiness guard ran <em>before</em> the trim, so a string of nothing but whitespace passed
///   it, trimmed to nothing, and then read <c>value[0]</c> off the end. See the backlog spec's
///   finding F21.
///   </para>
///   <para>
///   The two are not interchangeable, and that is deliberate rather than a divergence: they share
///   one <see cref="ShapePosition"/> enum but accept different members of it, so each must refuse
///   the names the other takes. That asymmetry is pinned separately, below.
///   </para>
/// </summary>
public class LeftAndTopPositionParityTests
{
    /// <summary>
    ///   What a parse produced, as one string, so that a single assertion covers the whole answer -
    ///   which of the two representations came back, and what was in it.
    /// </summary>
    private static string Describe(ShapePosition shape, Unit position)
    {
        if (shape != ShapePosition.Undefined)
            return "shape:" + shape;
        if (position.IsEmpty)
            return "unit:empty";
        return "unit:" + position.Value.ToString("0.###", CultureInfo.InvariantCulture) + ":" + position.Type;
    }

    /// <summary>
    ///   Naming the exception rather than letting it escape is the assertion here, not a tolerance:
    ///   which exception each input produces is precisely what these tests claim. Contrast
    ///   <c>ReaderDiagnostics.ComplaintsAbout</c>, which catches so that a test can collect
    ///   complaints and is therefore the wrong route for a test claiming nothing is thrown.
    /// </summary>
    private static string Outcome(Func<string> parse)
    {
        try
        {
            return parse();
        }
        catch (Exception ex)
        {
            return "throws:" + ex.GetType().Name;
        }
    }

    private static string ByLeft(string value)
    {
        return Outcome(() =>
        {
            var parsed = LeftPosition.Parse(value);
            return Describe(parsed.ShapePosition, parsed.Position);
        });
    }

    private static string ByTop(string value)
    {
        return Outcome(() =>
        {
            var parsed = TopPosition.Parse(value);
            return Describe(parsed.ShapePosition, parsed.Position);
        });
    }

    // ---------------------------------------------------------------------------------------
    // Everything the two must answer identically, asserted against both from one table.
    // ---------------------------------------------------------------------------------------

    [Test]
    // A bare number is a Unit, and the sign characters are why the first character is looked at.
    [Arguments("5")]
    [Arguments("+5")]
    [Arguments("-5")]
    [Arguments("0")]
    [Arguments("2.5")]
    // A number with a unit on it takes the same path.
    [Arguments("2.5cm")]
    [Arguments("10pt")]
    [Arguments("1in")]
    [Arguments("3mm")]
    // Surrounding whitespace is trimmed before the first character is read.
    [Arguments(" 5 ")]
    [Arguments("\t5")]
    // Center is the one member both accept, so it is a shared case rather than an asymmetric one.
    [Arguments("Center")]
    [Arguments("center")]
    [Arguments("CENTER")]
    [Arguments(" Center ")]
    // Undefined is a member of the enum like any other, and the private constructors admit it
    // explicitly, so it parses rather than throwing - and yields a position that is null.
    [Arguments("Undefined")]
    // A name that is in no enum at all.
    [Arguments("Sideways")]
    [Arguments("Middle")]
    // The guard cases. Null and empty are refused; whitespace alone is F21.
    [Arguments(null)]
    [Arguments("")]
    [Arguments("   ")]
    [Arguments("\t")]
    // A number with trailing junk, which only Unit.Parse can rule on.
    [Arguments("5x")]
    public void TheTwoPositionsAnswerTheSameWayForEverythingTheyShare(string value)
    {
        ByLeft(value).Should().Be(ByTop(value),
            "LeftPosition.Parse and TopPosition.Parse are copies of one another and must not drift");
    }

    // ---------------------------------------------------------------------------------------
    // The deliberate asymmetry: one enum, two different subsets of it.
    // ---------------------------------------------------------------------------------------

    [Test]
    [Arguments("Left", ShapePosition.Left)]
    [Arguments("Right", ShapePosition.Right)]
    [Arguments("Center", ShapePosition.Center)]
    [Arguments("Inside", ShapePosition.Inside)]
    [Arguments("Outside", ShapePosition.Outside)]
    [Arguments("left", ShapePosition.Left)]
    [Arguments("OUTSIDE", ShapePosition.Outside)]
    public void ALeftPositionTakesTheFiveNamesThatMeanSomethingHorizontal(string value, ShapePosition expected)
    {
        LeftPosition.Parse(value).ShapePosition.Should().Be(expected);
    }

    [Test]
    [Arguments("Top", ShapePosition.Top)]
    [Arguments("Bottom", ShapePosition.Bottom)]
    [Arguments("Center", ShapePosition.Center)]
    [Arguments("top", ShapePosition.Top)]
    [Arguments("BOTTOM", ShapePosition.Bottom)]
    public void ATopPositionTakesTheThreeNamesThatMeanSomethingVertical(string value, ShapePosition expected)
    {
        TopPosition.Parse(value).ShapePosition.Should().Be(expected);
    }

    [Test]
    [Arguments("Top")]
    [Arguments("Bottom")]
    public void ALeftPositionRefusesAVerticalName(string value)
    {
        FluentActions.Invoking(() => LeftPosition.Parse(value)).Should().ThrowExactly<ArgumentException>();
    }

    [Test]
    [Arguments("Left")]
    [Arguments("Right")]
    [Arguments("Inside")]
    [Arguments("Outside")]
    public void ATopPositionRefusesAHorizontalName(string value)
    {
        FluentActions.Invoking(() => TopPosition.Parse(value)).Should().ThrowExactly<ArgumentException>();
    }

    // ---------------------------------------------------------------------------------------
    // What a parsed value actually holds.
    // ---------------------------------------------------------------------------------------

    [Test]
    [Arguments("5", 5)]
    [Arguments("+5", 5)]
    [Arguments("-5", -5)]
    [Arguments("0", 0)]
    [Arguments("10pt", 10)]
    [Arguments(" 12 ", 12)]
    public void ANumberIsReadAsAPositionInPoints(string value, double expectedPoints)
    {
        LeftPosition.Parse(value).Position.Point.Should().BeApproximately(expectedPoints, 0.001);
        TopPosition.Parse(value).Position.Point.Should().BeApproximately(expectedPoints, 0.001);
    }

    [Test]
    public void ACentimetreIsConvertedToPointsRatherThanKeptAsANumber()
    {
        // 2.54 cm to the inch, 72 points to the inch.
        LeftPosition.Parse("2.54cm").Position.Point.Should().BeApproximately(72, 0.001);
        TopPosition.Parse("2.54cm").Position.Point.Should().BeApproximately(72, 0.001);
    }

    [Test]
    public void ANamedPositionCarriesNoUnitAndAUnitCarriesNoName()
    {
        var named = LeftPosition.Parse("Right");
        named.ShapePosition.Should().Be(ShapePosition.Right);
        named.Position.IsEmpty.Should().BeTrue();

        var measured = LeftPosition.Parse("4cm");
        measured.ShapePosition.Should().Be(ShapePosition.Undefined);
        measured.Position.IsEmpty.Should().BeFalse();
    }

    [Test]
    public void UndefinedParsesToAPositionThatStatesNothing()
    {
        // Worth pinning rather than assuming: "Undefined" is a member of the enum, and both private
        // constructors admit it by name, so it is the one name that parses to an empty position
        // instead of throwing.
        LeftPosition.Parse("Undefined").ShapePosition.Should().Be(ShapePosition.Undefined);
        TopPosition.Parse("Undefined").ShapePosition.Should().Be(ShapePosition.Undefined);
    }

    [Test]
    [Arguments("Sideways")]
    [Arguments("Middle")]
    [Arguments("Lefty")]
    public void ANameInNoEnumAtAllIsRefused(string value)
    {
        FluentActions.Invoking(() => LeftPosition.Parse(value)).Should().ThrowExactly<ArgumentException>();
        FluentActions.Invoking(() => TopPosition.Parse(value)).Should().ThrowExactly<ArgumentException>();
    }

    // ---------------------------------------------------------------------------------------
    // The guard, and finding F21.
    // ---------------------------------------------------------------------------------------

    [Test]
    public void NothingAtAllIsRefusedRatherThanParsed()
    {
        FluentActions.Invoking(() => LeftPosition.Parse(null)).Should().ThrowExactly<ArgumentNullException>();
        FluentActions.Invoking(() => TopPosition.Parse(null)).Should().ThrowExactly<ArgumentNullException>();
        FluentActions.Invoking(() => LeftPosition.Parse("")).Should().ThrowExactly<ArgumentNullException>();
        FluentActions.Invoking(() => TopPosition.Parse("")).Should().ThrowExactly<ArgumentNullException>();
    }

    [Test]
    [Arguments("   ")]
    [Arguments("\t")]
    [Arguments(" \t ")]
    [Arguments("\r\n")]
    public void WhitespaceAloneIsRefusedTheSameWayAsNothingAtAll(string value)
    {
        // F21. The guard tested the untrimmed string, so whitespace alone passed it, trimmed away
        // to nothing, and then read value[0] off the end of an empty string - IndexOutOfRangeException
        // out of a public API, identically in both copies. Refused as empty now, which is what it is.
        FluentActions.Invoking(() => LeftPosition.Parse(value)).Should().ThrowExactly<ArgumentNullException>();
        FluentActions.Invoking(() => TopPosition.Parse(value)).Should().ThrowExactly<ArgumentNullException>();
    }
}
