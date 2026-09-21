using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Advanced;
using PdfPinata.Pdf.IO;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PdfPinata.Test.Fonts;

/// <summary>
///   <see cref="PdfDocumentOptions.RespectFontEmbeddingRestrictions"/> against the OS/2
///   <c>fsType</c> a font declares: off, every font is embedded as it always was; on, a font whose
///   licence forbids embedding is refused and one that forbids subsetting goes in whole.
/// </summary>
/// <remarks>
///   No font shipped with the tests restricts anything, and a commercial font that does cannot be
///   checked in, so each face here is Liberation Sans with its <c>fsType</c> rewritten. Each value
///   gets a name of its own, because the library caches a face by its name and a second face by the
///   same name is refused.
/// </remarks>
public class FontEmbeddingRestrictionTests
{
    private const ushort Installable = 0x0000;
    private const ushort RestrictedLicense = 0x0002;
    private const ushort PreviewAndPrint = 0x0004;
    private const ushort Editable = 0x0008;
    private const ushort NoSubsetting = 0x0100;
    private const ushort BitmapEmbeddingOnly = 0x0200;

    private static readonly Dictionary<ushort, char> Letters = new Dictionary<ushort, char>
    {
        [Installable] = 'A',
        [PreviewAndPrint] = 'B',
        [Editable] = 'C',
        [RestrictedLicense | Editable] = 'D',
        [RestrictedLicense] = 'E',
        [BitmapEmbeddingOnly] = 'F',
        [NoSubsetting] = 'G',
        [RestrictedLicense | PreviewAndPrint] = 'H',
        [Editable | BitmapEmbeddingOnly] = 'J',
    };

    private static readonly Lazy<byte[]> Liberation = new Lazy<byte[]>(() => File.ReadAllBytes(
        PathHelper.GetInstance().GetAssetPath("Fonts", "LiberationSans-Regular.ttf")));

    [Fact]
    public void WithTheOptionOffARestrictedFontIsEmbeddedAsBefore()
    {
        var saved = Save(Draw(RestrictedLicense, respect: false));

        var descriptor = FontDescriptorOf(saved);
        descriptor.Elements.GetName("/FontName").Should().MatchRegex("^/[A-Z]{6}\\+");
        FontProgramOf(descriptor).Length.Should().BeLessThan(Font(RestrictedLicense).Length,
            "the option being off, the font is subsetted exactly as it always was");
    }

    [Theory]
    [InlineData(RestrictedLicense, "Restricted License")]
    [InlineData(BitmapEmbeddingOnly, "Bitmap Embedding Only")]
    [InlineData(Editable | BitmapEmbeddingOnly, "Bitmap Embedding Only")]
    public void WithTheOptionOnAFontThatForbidsEmbeddingIsRefusedWhereItIsDrawn(int value, string restriction)
    {
        var fsType = (ushort)value;
        var drawing = () => Draw(fsType, respect: true);

        var thrown = drawing.Should().Throw<InvalidOperationException>().Which;
        thrown.Message.Should().Contain(Letters[fsType] + "iberation Sans",
            "the message has to say which face was refused");
        thrown.Message.Should().Contain(restriction);
        thrown.Message.Should().Contain("0x" + fsType.ToString("X4"));
        thrown.Message.Should().Contain(nameof(PdfDocumentOptions.RespectFontEmbeddingRestrictions));
    }

    [Fact]
    public void SettingTheOptionAfterDrawingStillRefusesTheFontAtSaveTime()
    {
        var document = Draw(RestrictedLicense, respect: false);
        document.Options.RespectFontEmbeddingRestrictions = true;

        var saving = () => Save(document);

        saving.Should().Throw<InvalidOperationException>().WithMessage("*Restricted License*");
    }

    [Theory]
    [InlineData(Installable)]
    [InlineData(PreviewAndPrint)]
    [InlineData(Editable)]
    [InlineData(RestrictedLicense | Editable)]
    [InlineData(RestrictedLicense | PreviewAndPrint)]
    public void WithTheOptionOnAFontThatPermitsEmbeddingIsSubsettedAsBefore(int value)
    {
        var fsType = (ushort)value;

        // The last two are what an OS/2 table older than version 3 may say, and are read by the
        // least restrictive bit set: Editable, or Preview & Print, not Restricted License.
        var saved = Save(Draw(fsType, respect: true));

        var descriptor = FontDescriptorOf(saved);
        descriptor.Elements.GetName("/FontName").Should().MatchRegex("^/[A-Z]{6}\\+");
        FontProgramOf(descriptor).Length.Should().BeLessThan(Font(fsType).Length);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void WithTheOptionOnAFontThatForbidsSubsettingIsEmbeddedWholeAndNamedSo(bool unicode)
    {
        var saved = Save(Draw(NoSubsetting, respect: true, unicode));

        var descriptor = FontDescriptorOf(saved);
        FontProgramOf(descriptor).Should().Equal(Font(NoSubsetting),
            "a font that may not be subsetted is embedded as it came");

        var name = descriptor.Elements.GetName("/FontName");
        name.Should().NotMatchRegex("^/[A-Z]{6}\\+", "the subset tag says the program is a subset, and it is not");
        BaseFontNamesOf(saved).Should().OnlyContain(baseFont => baseFont == name);
    }

    [Fact]
    public void WithTheOptionOffAFontThatForbidsSubsettingIsSubsettedAsBefore()
    {
        var saved = Save(Draw(NoSubsetting, respect: false));

        var descriptor = FontDescriptorOf(saved);
        descriptor.Elements.GetName("/FontName").Should().MatchRegex("^/[A-Z]{6}\\+");
        FontProgramOf(descriptor).Length.Should().BeLessThan(Font(NoSubsetting).Length);
    }

    private static byte[] Font(ushort fsType)
    {
        return new TrueTypeGlyphs(new TrueTypeGlyphs(Liberation.Value).WithFsType(fsType))
            .WithADistinctFontName(Letters[fsType]);
    }

    private static string FamilyOf(ushort fsType)
    {
        var family = "Embedding Probe " + fsType.ToString("X4");
        PinnedFontResolver.Register(family, Font(fsType));
        return family;
    }

    private static PdfDocument Draw(ushort fsType, bool respect, bool unicode = true)
    {
        var document = new PdfDocument();
        document.Options.RespectFontEmbeddingRestrictions = respect;

        using var gfx = XGraphics.FromPdfPage(document.AddPage());
        var font = new XFont(FamilyOf(fsType), 12, XFontStyle.Regular,
            new XPdfFontOptions(unicode ? PdfFontEncoding.Unicode : PdfFontEncoding.WinAnsi));
        gfx.DrawString("Embedding", font, XBrushes.Black, new XPoint(20, 40));
        return document;
    }

    private static PdfDocument Save(PdfDocument document)
    {
        using var stream = new MemoryStream();
        document.Save(stream, false);
        return Pdf.IO.PdfReader.Open(new MemoryStream(stream.ToArray()), PdfDocumentOpenMode.Modify);
    }

    private static PdfDictionary FontDescriptorOf(PdfDocument saved)
    {
        return saved.Internals.GetAllObjects()
            .OfType<PdfDictionary>()
            .Single(d => d.Elements.GetName("/Type") == "/FontDescriptor");
    }

    private static IEnumerable<string> BaseFontNamesOf(PdfDocument saved)
    {
        return saved.Internals.GetAllObjects()
            .OfType<PdfDictionary>()
            .Where(d => d.Elements.GetName("/Type") == "/Font")
            .Select(d => d.Elements.GetName("/BaseFont"));
    }

    private static byte[] FontProgramOf(PdfDictionary descriptor)
    {
        var program = descriptor.Elements["/FontFile2"];
        return ((PdfDictionary)(program is PdfReference reference ? reference.Value : program))
            .Stream.UnfilteredValue;
    }
}
