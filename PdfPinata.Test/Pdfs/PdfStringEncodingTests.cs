using System;
using System.Linq;
using AwesomeAssertions;
using PdfPinata.Pdf;
using Xunit;

namespace PdfPinata.Test.Pdfs;

/// <summary>
///   <see cref="PdfStringEncoding.MacRomanEncoding"/> was declared as the MacExpert flag, a
///   copy-paste slip carried from PDFsharp 1.5 through PdfSharpCore, so the two encodings were
///   one value and a string made as either reported itself as whichever name came first. The
///   ISO 32000-1 encodings are distinct (Annex D), and PDFsharp 6 declares MacRoman as its own
///   flag; so does this library now.
/// </summary>
public class PdfStringEncodingTests
{
    [Fact]
    public void EveryEncodingIsAValueOfItsOwn()
    {
        var values = Enum.GetValues<PdfStringEncoding>();

        values.Should().OnlyHaveUniqueItems();
        ((int)PdfStringEncoding.MacRomanEncoding).Should().Be(4);
        ((int)PdfStringEncoding.MacExpertEncoding).Should().Be(5);
    }

    [Theory]
    [InlineData(PdfStringEncoding.StandardEncoding)]
    [InlineData(PdfStringEncoding.PDFDocEncoding)]
    [InlineData(PdfStringEncoding.MacRomanEncoding)]
    [InlineData(PdfStringEncoding.MacExpertEncoding)]
    [InlineData(PdfStringEncoding.Unicode)]
    public void AStringReportsTheEncodingItWasMadeWith(PdfStringEncoding encoding)
    {
        var text = new PdfString("text", encoding);

        text.Encoding.Should().Be(encoding);
    }

    [Fact]
    public void ANameEveryEncodingHasIsAcceptedByTheConstructor()
    {
        // The constructor switches over the names one by one; a name with no case of its own
        // falls to the default and throws. MacExpert had none while it shared MacRoman's value.
        var refused = Enum.GetValues<PdfStringEncoding>()
            .Where(encoding => encoding is not (PdfStringEncoding.RawEncoding or PdfStringEncoding.WinAnsiEncoding))
            .Where(encoding => Record.Exception(() => new PdfString("text", encoding)) != null);

        refused.Should().BeEmpty();
    }
}
