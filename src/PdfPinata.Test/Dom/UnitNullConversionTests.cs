using System;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel;
using Xunit;

namespace PdfPinata.Test.Dom;

/// <summary>
///   Unit declares an implicit conversion from string, which is what makes <c>someUnit == null</c>
///   compile: null converts to string, string converts to Unit, and the comparison binds to
///   operator ==(Unit, Unit) rather than lifting to Unit?. Because nothing about it is constant,
///   CS8073 - which this repo now treats as an error - correctly says nothing about it.
///
///   So the guard against a mechanical IsNull -> == null rewrite does not reach Unit, and Unit is
///   the most-used struct in the DOM: 52 of its 323 [DV] members. The conversion opened with
///   value.Trim(), so the expression threw NullReferenceException with nothing to say for itself.
///   It now throws ArgumentNullException naming the likely cause.
/// </summary>
public class UnitNullConversionTests
{
    [Fact]
    public void ConvertingANullStringThrowsSomethingThatExplainsItself()
    {
        var convert = () => { Unit _ = null; };

        convert.Should().Throw<ArgumentNullException>()
            .WithMessage("*unit == null*", "the message names the mistake that usually causes this")
            .WithMessage("*IsEmpty*", "and what to write instead");
    }

    /// <summary>
    ///   The expression this is really about. It compiles, no compiler warning can catch it, and
    ///   before the guard it threw a bare NullReferenceException.
    /// </summary>
    [Fact]
    public void ComparingAUnitToNullThrowsSomethingThatExplainsItself()
    {
        var unit = Unit.FromPoint(3);

        var compare = () => unit == null;

        compare.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ConvertingARealStringStillWorks()
    {
        Unit fromPoints = "12pt";
        Unit fromCentimetres = "2cm";
        Unit bare = "5";

        fromPoints.Point.Should().BeApproximately(12, 1e-6);
        fromCentimetres.Centimeter.Should().BeApproximately(2, 1e-6);
        bare.Point.Should().BeApproximately(5, 1e-6, "point is assumed when there is no suffix");
    }

    [Fact]
    public void EmptinessIsTestedWithIsEmpty()
    {
        var unset = new Unit();
        var set = Unit.FromPoint(1);

        unset.IsEmpty.Should().BeTrue();
        set.IsEmpty.Should().BeFalse();
    }
}
