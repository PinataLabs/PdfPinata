using System;
using System.Collections.Generic;
using AwesomeAssertions;
using PdfPinata.Pdf;
using Xunit;

namespace PdfPinata.Test.Pdfs;

/// <summary>
///   <see cref="PdfNameObject"/> overloads <c>==</c> and <c>!=</c> against <see cref="string"/>,
///   and an untyped null binds to that overload rather than to reference equality. So the ordinary
///   looking <c>nameObject != null</c> does not test for null at all: it asks the operator, which
///   dereferenced the very thing being tested.
///   <para>
///   Five places in the library are written that way, four of them the type lookups inside
///   <see cref="PdfDictionary"/>. The result was that <c>GetString</c> and <c>GetName</c> answered
///   a <see cref="NullReferenceException"/> instead of the <see cref="InvalidCastException"/> they
///   are written to throw, for every value that is neither a string nor a name. Reading a choice
///   field whose <c>/V</c> is an array - which is how a list box with several options selected is
///   written - was one way in.
///   </para>
///   <para>
///   <see cref="PdfName"/>, which this is the indirect twin of, has always had the guard.
///   </para>
/// </summary>
public class PdfNameObjectComparisonTests
{
    [Fact]
    public void ANullNameIsNotEqualToAString()
    {
        PdfNameObject name = null;

        // ReSharper disable once ConditionIsAlwaysTrueOrFalse
        (name != null).Should().BeFalse("a null name is null, and asking must not throw");
        // ReSharper disable once ConditionIsAlwaysTrueOrFalse
        (name == null).Should().BeTrue();
    }

    [Fact]
    public void ANullNameComparesUnequalToAnActualString()
    {
        PdfNameObject name = null;

        // ReSharper disable once ConditionIsAlwaysTrueOrFalse
        (name == "/Kent").Should().BeFalse();
        // ReSharper disable once ConditionIsAlwaysTrueOrFalse
        (name != "/Kent").Should().BeTrue();
    }

    [Fact]
    public void ANameComparesByItsValue()
    {
        var name = new PdfNameObject(new PdfDocument(), "/Kent");

        (name == "/Kent").Should().BeTrue();
        (name != "/Kent").Should().BeFalse();
        (name == "/Sussex").Should().BeFalse();
        // ReSharper disable once ConditionIsAlwaysTrueOrFalse
        (name != null).Should().BeTrue();
    }

    /// <summary>
    ///   <see cref="PdfNameObject.Value"/> is settable and may be set to null. That must not make
    ///   the object holding it look like a null object: only the reference answers that.
    /// </summary>
    [Fact]
    public void ANameWhoseValueIsNullIsStillNotANullName()
    {
        var name = new PdfNameObject(new PdfDocument(), "/Kent");
        name.Value = null;

        // ReSharper disable once ConditionIsAlwaysTrueOrFalse
        (name == null).Should().BeFalse("the name is there, whatever it holds");
        // ReSharper disable once ConditionIsAlwaysTrueOrFalse
        (name != null).Should().BeTrue();
        (name == "/Kent").Should().BeFalse();
    }

    /// <summary>
    ///   <c>Equals</c> and <c>GetHashCode</c> read the same settable value the operators do, and so
    ///   need the same guard: without it an ordinary equality test against a name whose value has
    ///   been cleared throws <see cref="NullReferenceException"/> rather than answering, and putting
    ///   that name in any hash-based collection throws on the way in.
    /// </summary>
    [Fact]
    public void ANameWhoseValueIsNullStillAnswersEqualityRatherThanThrowing()
    {
        var name = new PdfNameObject(new PdfDocument(), "/Kent");
        name.Value = null;

        // ReSharper disable once SuspiciousTypeConversion.Global
        name.Equals("/Kent").Should().BeFalse();
        name.Equals(null).Should().BeFalse("a name that is there equals nothing when it holds nothing");
        name.GetHashCode().Should().Be(0);
    }

    [Fact]
    public void ANameWhoseValueIsNullCanBeHeldInAHashSet()
    {
        var name = new PdfNameObject(new PdfDocument(), "/Kent");
        name.Value = null;

        var act = () => new HashSet<PdfNameObject> { name }.Contains(name);

        act.Should().NotThrow();
    }

    [Fact]
    public void ANameEqualsTheStringItHolds()
    {
        var name = new PdfNameObject(new PdfDocument(), "/Kent");

        // ReSharper disable once SuspiciousTypeConversion.Global
        name.Equals("/Kent").Should().BeTrue();
        // ReSharper disable once SuspiciousTypeConversion.Global
        name.Equals("/Sussex").Should().BeFalse();
        name.GetHashCode().Should().Be("/Kent".GetHashCode(), "the name hashes as the string it is");
    }

    /// <summary>
    ///   The behaviour the guard restores at the call sites: a dictionary entry that is neither a
    ///   string nor a name reports itself as one, rather than falling over on the way to saying so.
    /// </summary>
    [Fact]
    public void ReadingANonStringEntryAsAStringSaysSoRatherThanThrowingNullReference()
    {
        var document = new PdfDocument();
        var dictionary = new PdfDictionary(document);
        dictionary.Elements["/V"] = new PdfArray(document, new PdfString("Kent"));

        var act = () => dictionary.Elements.GetString("/V");

        act.Should().Throw<InvalidCastException>("that is what the method is written to throw");
    }

    [Fact]
    public void ReadingANonNameEntryAsANameSaysSoRatherThanThrowingNullReference()
    {
        var document = new PdfDocument();
        var dictionary = new PdfDictionary(document);
        dictionary.Elements["/V"] = new PdfArray(document, new PdfString("Kent"));

        var act = () => dictionary.Elements.GetName("/V");

        act.Should().Throw<InvalidCastException>();
    }
}
