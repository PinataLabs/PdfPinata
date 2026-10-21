using System.Collections.Generic;
using AwesomeAssertions;
using PdfPinata.Pdf;
using TUnit.Core;

namespace PdfPinata.Test.Pdfs;

/// <summary>
///   A name is its value. <see cref="PdfName.Equals(object)"/> and
///   <see cref="PdfNameObject.Equals(object)"/> compared their string with the object passed in,
///   so two equal names were unequal although their hash codes agreed, and only a bare string
///   compared equal. Issue #337.
/// </summary>
public class PdfNameEqualityTests
{
    [Test]
    public void TwoNamesWithTheSameValueAreEqual()
    {
        var name = new PdfName("/A");

        name.Equals(new PdfName("/A")).Should().BeTrue();
        name.Equals(new PdfNameObject(new PdfDocument(), "/A")).Should().BeTrue();
        name.Equals("/A").Should().BeTrue();
        name.GetHashCode().Should().Be(new PdfName("/A").GetHashCode());
    }

    [Test]
    public void TwoNameObjectsWithTheSameValueAreEqual()
    {
        var name = new PdfNameObject(new PdfDocument(), "/A");

        name.Equals(new PdfNameObject(new PdfDocument(), "/A")).Should().BeTrue();
        name.Equals(new PdfName("/A")).Should().BeTrue();
        name.Equals("/A").Should().BeTrue();
        name.GetHashCode().Should().Be(new PdfName("/A").GetHashCode());
    }

    [Test]
    public void NamesWithDifferentValuesAreNotEqual()
    {
        new PdfName("/A").Equals(new PdfName("/B")).Should().BeFalse();
        new PdfName("/A").Equals(new PdfNameObject(new PdfDocument(), "/B")).Should().BeFalse();
        new PdfNameObject(new PdfDocument(), "/A").Equals(new PdfName("/B")).Should().BeFalse();
        new PdfName("/A").Equals(null).Should().BeFalse();
        new PdfName("/A").Equals(new PdfString("/A")).Should().BeFalse();
    }

    [Test]
    public void ANameObjectEqualsNothingButANameOrAString()
    {
        var name = new PdfNameObject(new PdfDocument(), "/A");

        name.Equals(null).Should().BeFalse();
        name.Equals(new PdfString("/A")).Should().BeFalse("a string object is not a name, whatever it says");
        name.Equals(42).Should().BeFalse();
        name.Equals("/B").Should().BeFalse();
    }

    [Test]
    public void EqualNamesFindEachOtherInAHashSet()
    {
        var names = new HashSet<PdfName> { new("/Font") };

        names.Contains(new PdfName("/Font")).Should().BeTrue();
    }

    [Test]
    public void TheComparerPutsNullBeforeEveryName()
    {
        var names = new List<PdfName> { new("/B"), null, new("/A") };

        names.Sort(PdfName.Comparer);

        names.Should().Equal(null, new PdfName("/A"), new PdfName("/B"));
    }

    [Test]
    public void TheComparerOrdersNullAgainstANameBothWaysRound()
    {
        var comparer = PdfName.Comparer;

        comparer.Compare(null, new PdfName("/A")).Should().BeNegative();
        comparer.Compare(new PdfName("/A"), null).Should().BePositive();
        comparer.Compare(null, null).Should().Be(0);
    }
}
