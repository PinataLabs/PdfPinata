using System;
using AwesomeAssertions;
using PdfPinata.Pdf.Filters;
using Xunit;

namespace PdfPinata.Test.Pdfs.Filters;

/// <summary>
/// <see cref="Filtering.GetFilter"/> turns the name written in a stream dictionary into the filter
/// that decodes it. Three things decide what a caller gets back, and they are easy to confuse: a
/// name it implements returns a filter, a name in the specification it does not implement returns
/// null, and a name that is neither throws. A caller that treats "not implemented" as "unknown"
/// crashes on a file it could have skipped over, so the difference is worth pinning.
/// </summary>
public class FilteringTests
{
    [Theory]
    [InlineData("ASCIIHexDecode", typeof(AsciiHexDecode))]
    [InlineData("ASCII85Decode", typeof(Ascii85Decode))]
    [InlineData("LZWDecode", typeof(LzwDecode))]
    [InlineData("FlateDecode", typeof(FlateDecode))]
    public void AnImplementedFilterIsReturnedByItsFullName(string name, Type expected)
    {
        Filtering.GetFilter(name).Should().BeOfType(expected);
    }

    /// <summary>
    /// Some writers use the abbreviations the specification allows for inline images, and they
    /// reach this lookup from the same place the full names do.
    /// </summary>
    [Theory]
    [InlineData("AHx", typeof(AsciiHexDecode))]
    [InlineData("A85", typeof(Ascii85Decode))]
    [InlineData("LZW", typeof(LzwDecode))]
    [InlineData("Fl", typeof(FlateDecode))]
    public void AnImplementedFilterIsReturnedByItsAbbreviation(string name, Type expected)
    {
        Filtering.GetFilter(name).Should().BeOfType(expected);
    }

    /// <summary>
    /// The name arrives as a PDF name, so it may still carry the slash that introduces one.
    /// </summary>
    [Theory]
    [InlineData("/ASCIIHexDecode", typeof(AsciiHexDecode))]
    [InlineData("/ASCII85Decode", typeof(Ascii85Decode))]
    [InlineData("/LZWDecode", typeof(LzwDecode))]
    [InlineData("/FlateDecode", typeof(FlateDecode))]
    [InlineData("/AHx", typeof(AsciiHexDecode))]
    [InlineData("/Fl", typeof(FlateDecode))]
    public void ALeadingSlashIsStrippedBeforeTheLookup(string name, Type expected)
    {
        Filtering.GetFilter(name).Should().BeOfType(expected);
    }

    /// <summary>
    /// Named in the specification, recognised here, and not implemented — the caller gets null
    /// rather than an exception, which is what lets a reader carry on past a stream it cannot
    /// decode.
    /// </summary>
    [Theory]
    [InlineData("CCITTFaxDecode")]
    [InlineData("JBIG2Decode")]
    [InlineData("DCTDecode")]
    [InlineData("JPXDecode")]
    [InlineData("Crypt")]
    [InlineData("/DCTDecode")]
    public void AFilterThatIsRecognisedButNotImplementedIsNull(string name)
    {
        Filtering.GetFilter(name).Should().BeNull();
    }

    [Theory]
    [InlineData("NoSuchDecode")]
    [InlineData("")]
    [InlineData("/")]
    public void AnUnknownFilterThrows(string name)
    {
        Action lookup = () => Filtering.GetFilter(name);

        lookup.Should().Throw<NotImplementedException>().WithMessage("*" + name.TrimStart('/') + "*");
    }

    /// <summary>
    /// The lookup is case sensitive, as its own summary says. A name in the wrong case is not
    /// quietly accepted — it is simply unknown.
    /// </summary>
    [Theory]
    [InlineData("flatedecode")]
    [InlineData("FLATEDECODE")]
    [InlineData("Ascii85decode")]
    [InlineData("fl")]
    public void AFilterNameInTheWrongCaseIsUnknown(string name)
    {
        Action lookup = () => Filtering.GetFilter(name);

        lookup.Should().Throw<NotImplementedException>();
    }

    /// <summary>
    /// Each filter is held as a singleton, and the abbreviation reaches the same instance the
    /// full name does — the abbreviation is a second way in, not a second filter.
    /// </summary>
    [Fact]
    public void AFilterIsTheSameInstanceHoweverItIsAskedFor()
    {
        Filtering.GetFilter("FlateDecode").Should().BeSameAs(Filtering.GetFilter("Fl"));
        Filtering.GetFilter("/FlateDecode").Should().BeSameAs(Filtering.FlateDecode);

        Filtering.GetFilter("ASCII85Decode").Should().BeSameAs(Filtering.ASCII85Decode);
        Filtering.GetFilter("ASCIIHexDecode").Should().BeSameAs(Filtering.ASCIIHexDecode);
        Filtering.GetFilter("LZWDecode").Should().BeSameAs(Filtering.LzwDecode);
    }

    /// <summary>
    /// A null name is refused as a bad argument. It used to reach the test for a leading slash
    /// and fail there instead, which named neither the parameter nor the caller's mistake.
    /// </summary>
    [Fact]
    public void ANullFilterNameThrows()
    {
        Action lookup = () => Filtering.GetFilter(null);

        lookup.Should().Throw<ArgumentNullException>().WithParameterName("filterName");
    }
}
