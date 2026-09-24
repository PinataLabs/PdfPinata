using System.IO;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Annotations;
using Xunit;

namespace PdfPinata.Test.Annotations;

/// <summary>
///   What a <c>/FreeText</c> read from a file takes back out of its <c>/DA</c>: the last colour
///   operator and the last <c>Tf</c> that can be read, and the defaults for whatever cannot.
/// </summary>
public sealed class FreeTextDefaultAppearanceReadingTests
{
    [Fact]
    public void AGreyAndASizeAreTakenBack()
    {
        var read = ReadWith("/Helv 12 Tf 0.2 g");

        read.Font.Size.Should().Be(12);
        read.TextColor.ColorSpace.Should().Be(XColorSpace.Rgb);
        (read.TextColor.R, read.TextColor.G, read.TextColor.B).Should().Be(((byte)51, (byte)51, (byte)51));
    }

    [Fact]
    public void TheLastColourAndTheLastSizeWin()
    {
        var read = ReadWith("0 0 1 rg /Helv 9 Tf 1 0 0 rg /Helv 14 Tf");

        read.Font.Size.Should().Be(14);
        (read.TextColor.R, read.TextColor.G, read.TextColor.B).Should().Be(((byte)255, (byte)0, (byte)0));
    }

    [Fact]
    public void AFourComponentColourIsReadAsCmyk()
    {
        var read = ReadWith("/Helv 8 Tf 0.1 0.2 0.3 0.4 k");

        read.Font.Size.Should().Be(8);
        read.TextColor.ColorSpace.Should().Be(XColorSpace.Cmyk);
        read.TextColor.C.Should().BeApproximately(0.1, 1e-6);
        read.TextColor.M.Should().BeApproximately(0.2, 1e-6);
        read.TextColor.Y.Should().BeApproximately(0.3, 1e-6);
        read.TextColor.K.Should().BeApproximately(0.4, 1e-6);
    }

    [Fact]
    public void ComponentsOutOfRangeAreClampedAndAZeroSizeIsIgnored()
    {
        var read = ReadWith("/Helv 0 Tf 2 -1 0.5 rg");

        read.Font.Size.Should().Be(10);
        (read.TextColor.R, read.TextColor.G, read.TextColor.B).Should().Be(((byte)255, (byte)0, (byte)128));
    }

    [Theory]
    [InlineData("rg")]
    [InlineData("1 k")]
    [InlineData("a b c rg /Helv x Tf")]
    [InlineData("Tf g")]
    public void WhatCannotBeReadLeavesTheDefaults(string appearance)
    {
        var read = ReadWith(appearance);

        read.Font.Size.Should().Be(10);
        (read.TextColor.R, read.TextColor.G, read.TextColor.B).Should().Be(((byte)0, (byte)0, (byte)0));
    }

    static PdfFreeTextAnnotation ReadWith(string defaultAppearance)
    {
        var document = new PdfDocument();
        var annotation = new PdfGenericAnnotation("/FreeText")
        {
            Rectangle = new PdfRectangle(new XPoint(100, 500), new XPoint(300, 600))
        };
        document.AddPage().Annotations.Add(annotation);
        annotation.Elements.SetString("/DA", defaultAppearance);

        using var output = new MemoryStream();
        document.Save(output, false);
        var reopened = PdfPinata.Pdf.IO.PdfReader.Open(new MemoryStream(output.ToArray()), PdfPinata.Pdf.IO.PdfDocumentOpenMode.Modify);

        return (PdfFreeTextAnnotation)reopened.Pages[0].Annotations[0];
    }
}
