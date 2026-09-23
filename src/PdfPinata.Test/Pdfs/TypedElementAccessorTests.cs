using System;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Advanced;
using Xunit;

namespace PdfPinata.Test.Pdfs;

/// <summary>
///   Reading a typed value out of a dictionary or an array. Both collections offer the same set of
///   accessors and each one has the same four cases to deal with: the entry is missing, it is
///   there and of the right type, it is an indirect reference to something of the right type, or
///   it is something else entirely.
///   <para>
///   Their siblings on the same classes are covered; these three were simply missed, and covering
///   them turned up that the two collections disagreed about the third case — see the backlog
///   spec's finding F14.
///   </para>
/// </summary>
public class TypedElementAccessorTests
{
    private static PdfDocument ADocument() => new();

    /// <summary>An indirect reference to a simple value, which is what makes the third case.</summary>
    private static PdfReference IndirectTo(PdfDocument document, PdfObject value)
    {
        document.Internals.AddObject(value);
        return value.Reference;
    }

    // ----- PdfArray.ArrayElements.GetBoolean -------------------------------------------------------

    [Fact]
    public void ABooleanInAnArrayIsReadAsItself()
    {
        var array = new PdfArray(ADocument());
        array.Elements.Add(new PdfBoolean(true));
        array.Elements.Add(new PdfBoolean(false));

        array.Elements.GetBoolean(0).Should().BeTrue();
        array.Elements.GetBoolean(1).Should().BeFalse();
    }

    [Fact]
    public void ANullInAnArrayIsNotTrue()
    {
        var array = new PdfArray(ADocument());
        array.Elements.Add(PdfNull.Value);

        array.Elements.GetBoolean(0).Should().BeFalse("a missing answer is not a yes");
    }

    [Fact]
    public void AnIndirectBooleanInAnArrayIsFollowedToItsValue()
    {
        var document = ADocument();
        var array = new PdfArray(document);
        array.Elements.Add(IndirectTo(document, new PdfBooleanObject(document, true)));

        array.Elements.GetBoolean(0).Should().BeTrue();
    }

    [Fact]
    public void SomethingThatIsNotABooleanIsRefusedRatherThanGuessedAt()
    {
        var array = new PdfArray(ADocument());
        // Fully qualified: this test assembly has a test class named PdfInteger, which
        // shadows the PDF type for anything under PdfPinata.Test.
        array.Elements.Add(new Pdf.PdfInteger(1));

        var read = () => array.Elements.GetBoolean(0);

        read.Should().Throw<InvalidCastException>("1 is not true, whatever C would say");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    public void AnIndexOutsideTheArrayIsRefused(int index)
    {
        var array = new PdfArray(ADocument());
        array.Elements.Add(new PdfBoolean(true));

        var read = () => array.Elements.GetBoolean(index);

        read.Should().Throw<ArgumentOutOfRangeException>();
    }

    // ----- PdfArray.ArrayElements.GetString --------------------------------------------------------

    [Fact]
    public void AStringInAnArrayIsReadAsItself()
    {
        var array = new PdfArray(ADocument());
        array.Elements.Add(new PdfString("the value"));

        array.Elements.GetString(0).Should().Be("the value");
    }

    [Fact]
    public void ANullWhereAStringWasExpectedIsTheEmptyString()
    {
        var array = new PdfArray(ADocument());
        array.Elements.Add(PdfNull.Value);

        array.Elements.GetString(0).Should().BeEmpty();
    }

    [Fact]
    public void AnIndirectStringInAnArrayIsFollowedToItsValue()
    {
        var document = ADocument();
        var array = new PdfArray(document);
        array.Elements.Add(IndirectTo(document, new PdfStringObject(document, "indirect")));

        array.Elements.GetString(0).Should().Be("indirect");
    }

    [Fact]
    public void SomethingThatIsNotAStringIsRefusedRatherThanFormatted()
    {
        var array = new PdfArray(ADocument());
        array.Elements.Add(new Pdf.PdfInteger(7));

        var read = () => array.Elements.GetString(0);

        read.Should().Throw<InvalidCastException>();
    }

    [Fact]
    public void ANameIsNotAString()
    {
        // They are different types in the file and the accessors keep them apart, which is worth
        // saying because both come back as a C# string.
        var array = new PdfArray(ADocument());
        array.Elements.Add(new PdfName("/Name"));

        array.Elements.Invoking(e => e.GetString(0)).Should().Throw<InvalidCastException>();
        array.Elements.GetName(0).Should().Be("/Name");
    }

    /// <summary>
    ///   The whole set, so that the two collections cannot drift apart again on this point: every
    ///   scalar accessor on an array follows an indirect reference, the way the ones on a
    ///   dictionary always did.
    /// </summary>
    [Fact]
    public void EveryScalarAccessorOnAnArrayFollowsAnIndirectReference()
    {
        var document = ADocument();
        var array = new PdfArray(document);
        array.Elements.Add(IndirectTo(document, new PdfBooleanObject(document, true)));
        array.Elements.Add(IndirectTo(document, new PdfIntegerObject(document, 42)));
        array.Elements.Add(IndirectTo(document, new PdfRealObject(document, 1.5)));
        array.Elements.Add(IndirectTo(document, new PdfStringObject(document, "text")));
        array.Elements.Add(IndirectTo(document, new PdfNameObject(document, "/Name")));

        array.Elements.GetBoolean(0).Should().BeTrue();
        array.Elements.GetInteger(1).Should().Be(42);
        array.Elements.GetReal(2).Should().BeApproximately(1.5, 1e-9);
        array.Elements.GetString(3).Should().Be("text");
        array.Elements.GetName(4).Should().Be("/Name");
    }

    // ----- PdfDictionary.DictionaryElements.GetMatrix ----------------------------------------------

    private static PdfArray SixNumbers(PdfDocument document, params double[] values)
    {
        var array = new PdfArray(document);
        foreach (var value in values)
            array.Elements.Add(new PdfReal(value));
        return array;
    }

    [Fact]
    public void AMatrixIsReadFromTheSixNumbersThatMakeIt()
    {
        var document = ADocument();
        var dictionary = new PdfDictionary(document);
        dictionary.Elements["/M"] = SixNumbers(document, 1, 2, 3, 4, 5, 6);

        var matrix = dictionary.Elements.GetMatrix("/M", false);

        matrix.M11.Should().BeApproximately(1, 1e-9);
        matrix.M12.Should().BeApproximately(2, 1e-9);
        matrix.M21.Should().BeApproximately(3, 1e-9);
        matrix.M22.Should().BeApproximately(4, 1e-9);
        matrix.OffsetX.Should().BeApproximately(5, 1e-9);
        matrix.OffsetY.Should().BeApproximately(6, 1e-9);
    }

    [Fact]
    public void AnIndirectMatrixIsFollowedToItsValue()
    {
        var document = ADocument();
        var array = SixNumbers(document, 2, 0, 0, 2, 10, 20);
        document.Internals.AddObject(array);
        var dictionary = new PdfDictionary(document);
        dictionary.Elements["/M"] = array.Reference;

        var matrix = dictionary.Elements.GetMatrix("/M", false);

        matrix.M11.Should().BeApproximately(2, 1e-9);
        matrix.OffsetY.Should().BeApproximately(20, 1e-9);
    }

    [Fact]
    public void AMatrixThatIsNotThereIsTheIdentityAndNothingIsWritten()
    {
        var dictionary = new PdfDictionary(ADocument());

        var matrix = dictionary.Elements.GetMatrix("/M", false);

        matrix.Should().Be(new XMatrix());
        dictionary.Elements.ContainsKey("/M").Should().BeFalse("create was not asked for");
    }

    [Fact]
    public void AskingForAMatrixToBeCreatedWritesTheIdentityIntoTheDictionary()
    {
        // The create overload behaves differently from the plain one, which is the distinction
        // worth pinning: it leaves an entry behind.
        var dictionary = new PdfDictionary(ADocument());

        var matrix = dictionary.Elements.GetMatrix("/M", true);

        matrix.Should().Be(new XMatrix(), "the value returned is still the identity");
        dictionary.Elements.ContainsKey("/M").Should().BeTrue("but now there is one to find");
    }

    [Fact]
    public void AnArrayOfTheWrongLengthIsNotAMatrix()
    {
        var document = ADocument();
        var dictionary = new PdfDictionary(document);
        dictionary.Elements["/M"] = SixNumbers(document, 1, 2, 3);

        var read = () => dictionary.Elements.GetMatrix("/M", false);

        read.Should().Throw<InvalidCastException>("a matrix is six numbers or it is not one");
    }

    [Fact]
    public void SomethingThatIsNotAnArrayIsNotAMatrixEither()
    {
        var dictionary = new PdfDictionary(ADocument());
        dictionary.Elements.SetInteger("/M", 1);

        var read = () => dictionary.Elements.GetMatrix("/M", false);

        read.Should().Throw<InvalidCastException>();
    }

    /// <summary>
    ///   A matrix is written as a literal - by <c>SetMatrix</c>, and by the create overload - so
    ///   the literal is the shape this method most often meets. It used to refuse one with
    ///   NotImplementedException, which meant a matrix the method wrote could not be read back by
    ///   the method that wrote it. See the backlog spec's finding F19.
    /// </summary>
    [Fact]
    public void AMatrixTheCreateOverloadWroteIsReadBackAsTheIdentity()
    {
        var dictionary = new PdfDictionary(ADocument());
        dictionary.Elements.GetMatrix("/M", true);

        dictionary.Elements.GetMatrix("/M", false).Should().Be(new XMatrix());
    }

    [Fact]
    public void AMatrixSetAsALiteralComesBackWithTheNumbersItWasGiven()
    {
        var dictionary = new PdfDictionary(ADocument());
        var written = new XMatrix(2, 0, 0, 3, 17, 29);
        dictionary.Elements.SetMatrix("/M", written);

        var read = dictionary.Elements.GetMatrix("/M", false);

        read.M11.Should().BeApproximately(2, 1e-4);
        read.M22.Should().BeApproximately(3, 1e-4);
        read.OffsetX.Should().BeApproximately(17, 1e-4);
        read.OffsetY.Should().BeApproximately(29, 1e-4);
    }

    [Fact]
    public void ALiteralThatIsNotSixNumbersIsNotAMatrix()
    {
        var dictionary = new PdfDictionary(ADocument());
        dictionary.Elements["/M"] = new PdfLiteral("[1 0 0 1]");

        var read = () => dictionary.Elements.GetMatrix("/M", false);

        read.Should().Throw<InvalidCastException>("four numbers are not a matrix");
    }
}
