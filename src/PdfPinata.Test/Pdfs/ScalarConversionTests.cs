using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Advanced;
using PdfPinata.Test.IO;
using Xunit;

namespace PdfPinata.Test.Pdfs;

/// <summary>
///   The five scalar accessors of a dictionary and of an array, asked of every shape a value can
///   come in, side by side. The two collections used to convert an item each in their own way,
///   and so did three helpers elsewhere, and the copies disagreed; this is the table that says
///   what each answers, so that they cannot drift apart again.
///   <para>
///   Each outcome is written as five words, one per accessor in the order boolean, integer,
///   real, string, name. A string is quoted, and <c>!</c> is an
///   <see cref="InvalidCastException"/>.
///   </para>
/// </summary>
public class ScalarConversionTests
{
    /// <summary>What every accessor answers for an entry that is not there.</summary>
    private const string Absent = "false 0 0 '' ''";

    [Theory]
    [InlineData("a boolean", "true ! ! ! !", "true ! ! ! !")]
    [InlineData("an indirect boolean", "true ! ! ! !", "true ! ! ! !")]
    [InlineData("an integer", "! 3 3 ! !", "! 3 3 ! !")]
    [InlineData("an indirect integer", "! 3 3 ! !", "! 3 3 ! !")]
    [InlineData("a real", "! ! 2.5 ! !", "! ! 2.5 ! !")]
    [InlineData("an indirect real", "! ! 2.5 ! !", "! ! 2.5 ! !")]
    [InlineData("a string", "! ! ! 's' !", "! ! ! 's' !")]
    [InlineData("an indirect string", "! ! ! 's' !", "! ! ! 's' !")]
    [InlineData("a name", "! ! ! '/N' '/N'", "! ! ! ! '/N'")]
    [InlineData("an indirect name", "! ! ! '/N' '/N'", "! ! ! ! '/N'")]
    [InlineData("the null object", Absent, Absent)]
    [InlineData("a null object written out", Absent, Absent)]
    [InlineData("an indirect null object", Absent, Absent)]
    [InlineData("a reference with nothing behind it", Absent, Absent)]
    [InlineData("an unsigned integer", "! 7 ! ! !", "! ! ! ! !")]
    [InlineData("an indirect unsigned integer", "! ! ! ! !", "! ! ! ! !")]
    [InlineData("an unsigned integer too large for an int", "! -1294967296 ! ! !", "! ! ! ! !")]
    [InlineData("a long", "! ! ! ! !", "! ! ! ! !")]
    [InlineData("an indirect long", "! ! ! ! !", "! ! ! ! !")]
    public void EveryAccessorAnswersForEveryShapeOfValue(string shape, string fromADictionary,
        string fromAnArray)
    {
        var document = new PdfDocument();

        var dictionary = new PdfDictionary(document);
        dictionary.Elements["/Key"] = Make(document, shape);
        OutcomesOf(dictionary).Should().Be(fromADictionary, "a dictionary holding {0}", shape);

        var array = new PdfArray(document);
        array.Elements.Add(Make(document, shape));
        OutcomesOf(array).Should().Be(fromAnArray, "an array holding {0}", shape);
    }

    [Fact]
    public void AnEntryThatIsNotThereIsTheDefaultOfEveryAccessor()
    {
        OutcomesOf(new PdfDictionary(new PdfDocument())).Should().Be(Absent);
    }

    [Theory]
    [InlineData("a string", true, "s")]
    [InlineData("an indirect name", true, "/N")]
    [InlineData("an integer", false, null)]
    [InlineData("the null object", false, null)]
    [InlineData("a reference with nothing behind it", false, null)]
    public void TryGetStringAnswersWhereGetStringWould(string shape, bool found, string value)
    {
        var document = new PdfDocument();
        var dictionary = new PdfDictionary(document);
        dictionary.Elements["/Key"] = Make(document, shape);

        dictionary.Elements.TryGetString("/Key", out var read).Should().Be(found);
        read.Should().Be(value);
    }

    /// <summary>
    ///   The nulls a file can hold, as the reader hands them over: a reference to an object the
    ///   file never writes, which the reader has already turned into the null object; a
    ///   reference to an object that is the null object; and the null object spelled out.
    /// </summary>
    [Fact]
    public void TheNullsAFileCanHoldAreReadAsNoValue()
    {
        var document = Pdf.IO.PdfReader.Open(new MemoryStream(RawPdf.Build([
            "<</Type/Catalog/Pages 2 0 R/D 3 0 R/A 4 0 R>>",
            "<</Type/Pages/Kids[]/Count 0>>",
            "<</Dangling 9 0 R/Null 6 0 R/Spelled null>>",
            "[9 0 R 6 0 R null]",
            "<<>>",
            "null"
        ])), Pdf.IO.PdfDocumentOpenMode.Modify);

        var dictionary = (PdfDictionary)document.Internals.GetObject(new PdfObjectID(3));
        foreach (var key in new[] { "/Dangling", "/Null", "/Spelled" })
            OutcomesOf(dictionary, key).Should().Be(Absent, "the dictionary's {0}", key);

        var array = (PdfArray)document.Internals.GetObject(new PdfObjectID(4));
        OutcomesOf(array, 0).Should().Be(Absent, "the array's dangling reference");
        OutcomesOf(array, 1).Should().Be(Absent, "the array's reference to the null object");
        OutcomesOf(array, 2).Should().Be(Absent, "the array's null");
    }

    /// <summary>
    ///   A form's <c>/Matrix</c> is read a number at a time, and a number is allowed to be
    ///   indirect. The page draws the image upright inside a form that turns it over, so the
    ///   image is shown upside down only if the form's matrix is read.
    /// </summary>
    [Fact]
    public void AFormMatrixHoldingAnIndirectNumberIsIgnored()
    {
        var file = RawPdf.Build([
            "<</Type/Catalog/Pages 2 0 R>>",
            "<</Type/Pages/Kids[3 0 R]/Count 1>>",
            "<</Type/Page/Parent 2 0 R/MediaBox[0 0 200 200]/Resources<</XObject<</Fm0 5 0 R>>>>/Contents 4 0 R>>",
            RawPdf.Stream("", "q 1 0 0 1 0 0 cm /Fm0 Do Q"),
            RawPdf.Stream("/Type/XObject/Subtype/Form/BBox[0 0 200 200]/Matrix[1 0 0 7 0 R 0 200]" +
                          "/Resources<</XObject<</Im0 6 0 R>>>>", "q 100 0 0 100 10 10 cm /Im0 Do Q"),
            RawPdf.Stream("/Type/XObject/Subtype/Image/Width 40/Height 30/ColorSpace/DeviceGray/BitsPerComponent 8",
                new string('A', 1200)),
            "-1"
        ]);

        var page = Pdf.IO.PdfReader.Open(new MemoryStream(file), Pdf.IO.PdfDocumentOpenMode.Modify).Pages[0];

        page.GetImagePlacements().Single().Orientation.Should().Be(PdfImageOrientation.Normal);
    }

    /// <summary>
    ///   A key in a name tree is a string, and any object in a file is allowed to be indirect.
    /// </summary>
    [Fact]
    public void ANamedDestinationWhoseNameIsIndirectIsNotListed()
    {
        var document = Pdf.IO.PdfReader.Open(new MemoryStream(RawPdf.Build([
            "<</Type/Catalog/Pages 2 0 R/Names<</Dests 4 0 R>>>>",
            "<</Type/Pages/Kids[3 0 R]/Count 1>>",
            "<</Type/Page/Parent 2 0 R/MediaBox[0 0 200 200]>>",
            "<</Names[(Direct)[3 0 R/Fit]5 0 R[3 0 R/Fit]]>>",
            "(Indirect)"
        ])), Pdf.IO.PdfDocumentOpenMode.Modify);

        document.NamedDestinations.Names.Should().Equal("Direct");
    }

    private static PdfItem Make(PdfDocument document, string shape) => shape switch
    {
        "a boolean" => new PdfBoolean(true),
        "an indirect boolean" => IndirectTo(document, new PdfBooleanObject(document, true)),
        "an integer" => new Pdf.PdfInteger(3),
        "an indirect integer" => IndirectTo(document, new PdfIntegerObject(document, 3)),
        "a real" => new PdfReal(2.5),
        "an indirect real" => IndirectTo(document, new PdfRealObject(document, 2.5)),
        "a string" => new PdfString("s"),
        "an indirect string" => IndirectTo(document, new PdfStringObject(document, "s")),
        "a name" => new PdfName("/N"),
        "an indirect name" => IndirectTo(document, new PdfNameObject(document, "/N")),
        "the null object" => PdfNull.Value,
        "a null object written out" => new PdfNullObject(document),
        "an indirect null object" => IndirectTo(document, new PdfNullObject(document)),
        "a reference with nothing behind it" => new PdfReference(new PdfObjectID(99), 0),
        "an unsigned integer" => new PdfUInteger(7),
        "an indirect unsigned integer" => IndirectTo(document, new PdfUIntegerObject(document, 7)),
        "an unsigned integer too large for an int" => new PdfUInteger(3_000_000_000),
        "a long" => new PdfLong(5_000_000_000),
        "an indirect long" => IndirectTo(document, new PdfLongObject(document, 5_000_000_000)),
        _ => throw new ArgumentOutOfRangeException(nameof(shape), shape, null)
    };

    private static PdfReference IndirectTo(PdfDocument document, PdfObject value)
    {
        document.Internals.AddObject(value);
        return value.Reference;
    }

    private static string OutcomesOf(PdfDictionary dictionary, string key = "/Key") => Outcomes(
        () => dictionary.Elements.GetBoolean(key),
        () => dictionary.Elements.GetInteger(key),
        () => dictionary.Elements.GetReal(key),
        () => dictionary.Elements.GetString(key),
        () => dictionary.Elements.GetName(key));

    private static string OutcomesOf(PdfArray array, int index = 0) => Outcomes(
        () => array.Elements.GetBoolean(index),
        () => array.Elements.GetInteger(index),
        () => array.Elements.GetReal(index),
        () => array.Elements.GetString(index),
        () => array.Elements.GetName(index));

    private static string Outcomes(params Func<object>[] reads)
    {
        var words = new List<string>();
        foreach (var read in reads)
        {
            try
            {
                words.Add(read() switch
                {
                    bool value => value ? "true" : "false",
                    string value => "'" + value + "'",
                    IFormattable value => value.ToString(null, CultureInfo.InvariantCulture),
                    var value => value?.ToString() ?? "null"
                });
            }
            catch (InvalidCastException)
            {
                words.Add("!");
            }
        }

        return string.Join(" ", words);
    }
}
