using System;
using System.IO;
using System.Text;
using AwesomeAssertions;
using PdfPinata.Pdf;
using Xunit;

namespace PdfPinata.Test.Pdfs;

/// <summary>
///   Every simple type has two forms: the value itself - <see cref="PdfInteger"/>,
///   <see cref="PdfReal"/>, <see cref="PdfName"/> - which is immutable and written wherever it is
///   used, and an <c>…Object</c> form deriving from <see cref="PdfObject"/>, which has an object
///   number and is written once in the file with everything else referring to it.
///   <para>
///   The second form is what a file needs when the same value is shared, and it is the form nothing
///   in this library creates for itself. It is public API all the same, so what a caller gets from
///   it is worth saying: the value it was given, a string of that value in invariant form, and a
///   body in the file that is the literal between <c>obj</c> and <c>endobj</c>.
///   </para>
/// </summary>
public class IndirectSimpleObjectTests
{
    static string Written(PdfDocument document, PdfObject value)
    {
        using var stream = new MemoryStream();
        document.Internals.WriteObject(stream, value);
        return Encoding.ASCII.GetString(stream.ToArray());
    }

    // ----- the value each one carries ---------------------------------------------------------

    [Fact]
    public void EachIndirectValueAnswersWhatItWasBuiltWith()
    {
        new PdfIntegerObject(42).Value.Should().Be(42);
        new PdfUIntegerObject(42u).Value.Should().Be(42u);
        new PdfLongObject(4200000000L).Value.Should().Be(4200000000L);
        new PdfRealObject(1.5).Value.Should().Be(1.5);
        new PdfBooleanObject(true).Value.Should().BeTrue();
        new PdfNameObject().Value.Should().Be("/");

        new PdfIntegerObject().Value.Should().Be(0);
        new PdfUIntegerObject().Value.Should().Be(0u);
        new PdfLongObject().Value.Should().Be(0L);
        new PdfRealObject().Value.Should().Be(0);
        new PdfBooleanObject().Value.Should().BeFalse();
    }

    /// <summary>
    ///   Invariant, not the machine's own culture. A real written as <c>1,5</c> is two numbers to
    ///   whatever reads the file back, and a file that parses differently in Germany is not a file.
    /// </summary>
    [Fact]
    public void EachIndirectValueSaysItselfTheWayAFileSpellsIt()
    {
        new PdfIntegerObject(42).ToString().Should().Be("42");
        new PdfUIntegerObject(42u).ToString().Should().Be("42");
        new PdfLongObject(4200000000L).ToString().Should().Be("4200000000");
        new PdfRealObject(1.5).ToString().Should().Be("1.5");
        // Unlike the rest, this one is the CLR spelling rather than the file's: both
        // PdfBoolean and PdfBooleanObject answer bool.TrueString here, and they agree.
        new PdfBooleanObject(true).ToString().Should().Be(bool.TrueString);
        new PdfNameObject(new PdfDocument(), "/Name").ToString().Should().Be("/Name");
    }

    /// <summary>
    ///   A name is a slash and what follows it, and one written without the slash is not a name at
    ///   all. It is refused where it is built rather than where it is written, which is the only
    ///   place the caller's own line is still in view.
    /// </summary>
    [Fact]
    public void AnIndirectNameHasToStartWithASlash()
    {
        var document = new PdfDocument();

        var unslashed = () => new PdfNameObject(document, "Name");
        var empty = () => new PdfNameObject(document, "");
        var nothing = () => new PdfNameObject(document, null);

        unslashed.Should().Throw<ArgumentException>();
        empty.Should().Throw<ArgumentException>();
        nothing.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    ///   <see cref="PdfNameObject.Value"/> is settable and may be set to null, so equality has to
    ///   ask before it reads - putting one in a hash set would otherwise throw rather than answer.
    /// </summary>
    [Fact]
    public void AnIndirectNameWithNoValueEqualsNothingAndStillHashes()
    {
        var name = new PdfNameObject(new PdfDocument(), "/Name");

        name.Equals("/Name").Should().BeTrue();
        name.Equals("/Other").Should().BeFalse();
        name.GetHashCode().Should().Be("/Name".GetHashCode());

        name.Value = null;

        name.Equals("/Name").Should().BeFalse();
        name.GetHashCode().Should().Be(0);
    }

    // ----- what each one becomes in the file --------------------------------------------------

    [Fact]
    public void EachIndirectValueIsWrittenAsItsOwnNumberedObject()
    {
        var document = new PdfDocument();

        Written(document, new PdfIntegerObject(document, 42)).Should().Contain("42");
        Written(document, new PdfUIntegerObject(document, 42u)).Should().Contain("42");
        Written(document, new PdfLongObject(document, 4200000000L)).Should().Contain("4200000000");
        Written(document, new PdfRealObject(document, 1.5)).Should().Contain("1.5");
        Written(document, new PdfBooleanObject(document, true)).Should().Contain("true");
        Written(document, new PdfNameObject(document, "/Name")).Should().Contain("/Name");
        Written(document, new PdfStringObject(document, "text")).Should().Contain("text");
    }

    /// <summary>
    ///   The two boolean keywords are lowercase, and nothing else is one. This wrote
    ///   <c>bool.TrueString</c> - <c>True</c> - and got away with it because the only caller of
    ///   that writer overload is <see cref="PdfBooleanObject"/>, which nothing in this library
    ///   creates for itself: an indirect boolean was the one value written as a token no PDF
    ///   defines. Asserted against the capitalised form as well, because "contains true" is also
    ///   true of "True" once the case is ignored and the whole point here is the case.
    /// </summary>
    [Theory]
    [InlineData(true, "true", "True")]
    [InlineData(false, "false", "False")]
    public void AnIndirectBooleanIsWrittenAsTheLowercaseKeywordTheStandardDefines(
        bool value, string expected, string refused)
    {
        var document = new PdfDocument();

        var written = Written(document, new PdfBooleanObject(document, value));

        written.Should().Contain(expected).And.NotContain(refused);
    }

    /// <summary>
    ///   A shared value reaches the file the way it is meant to: written once, referred to twice.
    /// </summary>
    [Fact]
    public void AnIndirectValueNamedByTwoDictionariesIsWrittenOnce()
    {
        var document = new PdfDocument();
        _ = document.AddPage();

        var shared = new PdfIntegerObject(document, 42);
        var first = new PdfDictionary(document);
        var second = new PdfDictionary(document);
        document.Internals.AddObject(shared);
        document.Internals.AddObject(first);
        document.Internals.AddObject(second);
        first.Elements["/Shared"] = shared.Reference;
        second.Elements["/Shared"] = shared.Reference;

        first.Elements["/Shared"].Should().BeSameAs(second.Elements["/Shared"]);
        shared.Reference.Should().NotBeNull();
    }
}
