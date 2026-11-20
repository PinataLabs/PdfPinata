using System;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Rendering;
using PdfPinata.Test.Helpers;
using TUnit.Core;

namespace PdfPinata.Test.Interpreter;

/// <summary>
/// An Indexed palette written as a literal string, read from the bytes of a file. The lexer reads a
/// string that opens FF FE or FE FF as UTF-16, which is right for text and wrong for a palette, so
/// what reaches the table has to be the bytes the file holds, whichever way the mark points.
/// </summary>
public class PaletteStringReadingTests
{
    private static readonly byte[] LittleEndianMark = [0xFF, 0xFE, 0x00, 0x01, 0x02, 0x03];
    private static readonly byte[] BigEndianMark = [0xFE, 0xFF, 0x00, 0x01, 0x02, 0x03];

    private static PagePaint Rgb(int red, int green, int blue) => new(red / 255.0, green / 255.0, blue / 255.0);

    [Test, Timeout(30_000)]
    public async Task APaletteThatOpensFfFeIsReadAsTheBytesTheFileHolds()
    {
        var fills = await Draw(Document(LittleEndianMark, indirect: false));

        fills.Should().Equal(Rgb(255, 254, 0), Rgb(1, 2, 3));
    }

    [Test, Timeout(30_000)]
    public async Task APaletteThatOpensFeFfIsReadAsTheBytesTheFileHolds()
    {
        var fills = await Draw(Document(BigEndianMark, indirect: false));

        fills.Should().Equal(Rgb(254, 255, 0), Rgb(1, 2, 3));
    }

    [Test, Timeout(30_000)]
    public async Task AnIndirectPaletteThatOpensFfFeIsReadAsTheBytesTheFileHolds()
    {
        var fills = await Draw(Document(LittleEndianMark, indirect: true));

        fills.Should().Equal(Rgb(255, 254, 0), Rgb(1, 2, 3));
    }

    [Test, Timeout(30_000)]
    public async Task AnOddLengthPaletteThatOpensFfFeEndsInAZeroAndTheBytesBeforeItAreKept()
    {
        var fills = await Draw(Document([0xFF, 0xFE, 0x00, 0x01, 0x02], indirect: false));

        fills.Should().Equal(Rgb(255, 254, 0), Rgb(1, 2, 0));
    }

    [Test, Timeout(30_000)]
    public async Task AnOddLengthPaletteThatOpensFeFfEndsInAZeroAndTheBytesBeforeItAreKept()
    {
        var fills = await Draw(Document([0xFE, 0xFF, 0x00, 0x01, 0x02], indirect: false));

        fills.Should().Equal(Rgb(254, 255, 0), Rgb(1, 2, 0));
    }

    [Test, Timeout(30_000)]
    [Arguments(false)]
    [Arguments(true)]
    public async Task APaletteThatOpensFfFeIsSavedAndReadBackByteForByte(bool indirect)
    {
        var saved = await Interruptibly.Run(() => Save(Document(LittleEndianMark, indirect)));

        Contains(saved, [(byte)'(', .. LittleEndianMark, (byte)')']).Should().BeTrue("the string is written as it was read");
        (await Draw(saved)).Should().Equal(Rgb(255, 254, 0), Rgb(1, 2, 3));
    }

    [Test, Timeout(30_000)]
    [Arguments(false)]
    [Arguments(true)]
    public async Task APaletteThatOpensFeFfIsSavedAsHexAndReadBackTheSame(bool indirect)
    {
        var saved = await Interruptibly.Run(() => Save(Document(BigEndianMark, indirect)));

        Contains(saved, Encoding.Latin1.GetBytes("<FEFF00010203>")).Should().BeTrue("a UTF-16 string is always written as hex");
        (await Draw(saved)).Should().Equal(Rgb(254, 255, 0), Rgb(1, 2, 3));
    }

    [Test, Timeout(30_000)]
    [Arguments(true)]
    [Arguments(false)]
    public async Task AnIndirectPaletteThatOpensFfFeSurvivesAnEncryptedSaveAndReopen(bool littleEndian)
    {
        var saved = await Interruptibly.Run(() =>
            Save(Document(littleEndian ? LittleEndianMark : BigEndianMark, indirect: true), encrypt: true));

        var fills = await Interruptibly.Run(() =>
        {
            using var document = PdfPinata.Pdf.IO.PdfReader.Open(new MemoryStream(saved), "owner",
                PdfPinata.Pdf.IO.PdfDocumentOpenMode.Import);
            var device = new RecordingDevice();
            PageRendering.Render(document.Pages[0], device);
            return device.Fills.Select(f => f.Paint).ToArray();
        });

        fills.Should().Equal(littleEndian
            ? [Rgb(255, 254, 0), Rgb(1, 2, 3)]
            : [Rgb(254, 255, 0), Rgb(1, 2, 3)]);
    }

    [Test, Timeout(30_000)]
    public async Task ATextStringThatOpensFfFeSurvivesAnEncryptedSaveAndReopen()
    {
        byte[] title = [0xFF, 0xFE, (byte)'A', 0x00, (byte)'B', 0x00];

        var text = await Interruptibly.Run(() =>
        {
            var saved = Save(InfoDocument(title), encrypt: true);
            using var document = PdfPinata.Pdf.IO.PdfReader.Open(new MemoryStream(saved), "owner",
                PdfPinata.Pdf.IO.PdfDocumentOpenMode.Import);
            return document.Info.Title;
        });

        text.Should().Be("AB");
    }

    private static byte[] InfoDocument(byte[] title)
    {
        byte[] head = Encoding.Latin1.GetBytes("%PDF-1.4\n1 0 obj\n<</Type/Catalog/Pages 2 0 R>>\nendobj\n"
            + "2 0 obj\n<</Type/Pages/Kids[3 0 R]/Count 1>>\nendobj\n"
            + "3 0 obj\n<</Type/Page/Parent 2 0 R/MediaBox[0 0 200 100]>>\nendobj\n4 0 obj\n<</Title (");
        byte[] tail = Encoding.Latin1.GetBytes(")>>\nendobj\n");
        return Renumbered([.. head, .. title, .. tail, .. Encoding.Latin1.GetBytes(
            "xref\n0 1\ntrailer\n<</Size 5/Root 1 0 R/Info 4 0 R>>\nstartxref\n0\n%%EOF\n")]);
    }

    private static Task<PagePaint[]> Draw(byte[] file) => Interruptibly.Run(() =>
    {
        using var document = PdfPinata.Pdf.IO.PdfReader.Open(new MemoryStream(file), PdfPinata.Pdf.IO.PdfDocumentOpenMode.Import);
        var device = new RecordingDevice();
        PageRendering.Render(document.Pages[0], device);
        return device.Fills.Select(f => f.Paint).ToArray();
    });

    private static byte[] Save(byte[] file, bool encrypt = false)
    {
        using var document = PdfPinata.Pdf.IO.PdfReader.Open(new MemoryStream(file), PdfPinata.Pdf.IO.PdfDocumentOpenMode.Modify);
        if (encrypt)
        {
            document.SecuritySettings.DocumentSecurityLevel = PdfPinata.Pdf.Security.PdfDocumentSecurityLevel.Encrypted128Bit;
            document.SecuritySettings.OwnerPassword = "owner";
            document.SecuritySettings.UserPassword = "";
        }
        var output = new MemoryStream();
        document.Save(output, false);
        return output.ToArray();
    }

    private static bool Contains(byte[] haystack, byte[] needle) =>
        haystack.AsSpan().IndexOf(needle) >= 0;

    /// <summary>
    /// A page that fills entry 0 and entry 1 of a two entry RGB palette, which is the literal string
    /// <paramref name="palette"/> in the page's resources or an object of its own.
    /// </summary>
    private static byte[] Document(byte[] palette, bool indirect)
    {
        var pdf = new MemoryStream();
        var offsets = new Dictionary<int, long>();
        const string content = "/Cs1 cs 0 sc 0 0 5 5 re f 1 sc 0 0 5 5 re f";
        byte[] literal = [(byte)'(', .. palette, (byte)')'];

        Write("%PDF-1.4\n");
        WriteObject(1, "<</Type/Catalog/Pages 2 0 R>>");
        WriteObject(2, "<</Type/Pages/Kids[3 0 R]/Count 1>>");
        WriteObject(3, "<</Type/Page/Parent 2 0 R/MediaBox[0 0 200 100]/Contents 4 0 R"
                       + "/Resources<</ColorSpace<</Cs1[/Indexed/DeviceRGB 1 "
                       + (indirect ? "5 0 R" : "<<PALETTE>>") + "]>>>>>>");
        WriteObject(4, "<</Length " + content.Length + ">>stream\n" + content + "\nendstream");
        if (indirect)
            WriteBytes(5, literal);

        var start = pdf.Position;
        var count = indirect ? 5 : 4;
        Write("xref\n0 " + (count + 1) + "\n0000000000 65535 f \n");
        for (var number = 1; number <= count; number++)
            Write(offsets[number].ToString("0000000000") + " 00000 n \n");
        Write("trailer\n<</Size " + (count + 1) + "/Root 1 0 R>>\nstartxref\n" + start + "\n%%EOF\n");

        var bytes = pdf.ToArray();
        return indirect ? bytes : Splice(bytes, "<<PALETTE>>", literal);

        void Write(string text) => pdf.Write(Encoding.Latin1.GetBytes(text));

        void WriteObject(int number, string body)
        {
            offsets[number] = pdf.Position;
            Write(number + " 0 obj\n" + body + "\nendobj\n");
        }

        void WriteBytes(int number, byte[] body)
        {
            offsets[number] = pdf.Position;
            Write(number + " 0 obj\n");
            pdf.Write(body);
            Write("\nendobj\n");
        }
    }

    /// <summary>
    /// Replaces a marker with bytes, then rewrites the offsets, because the marker and the bytes
    /// differ in length.
    /// </summary>
    private static byte[] Splice(byte[] file, string marker, byte[] replacement)
    {
        var text = Encoding.Latin1.GetString(file);
        var at = text.IndexOf(marker, StringComparison.Ordinal);
        var spliced = new MemoryStream();
        spliced.Write(file, 0, at);
        spliced.Write(replacement);
        spliced.Write(file, at + marker.Length, file.Length - at - marker.Length);
        return Renumbered(spliced.ToArray());
    }

    private static byte[] Renumbered(byte[] file)
    {
        // The reader falls back to scanning for objects when the table is wrong, but a test of
        // the string should not lean on that, so the table is written again from what is there.
        var text = Encoding.Latin1.GetString(file);
        var table = text.IndexOf("xref\n", StringComparison.Ordinal);
        var body = text[..table];
        var entries = new StringBuilder();
        var objects = Regex.Matches(body, @"(?m)^(\d+) 0 obj\n");
        entries.Append("xref\n0 " + (objects.Count + 1) + "\n0000000000 65535 f \n");
        foreach (Match match in objects)
            entries.Append(match.Index.ToString("0000000000") + " 00000 n \n");
        var trailer = text[text.IndexOf("trailer\n", table, StringComparison.Ordinal)..];
        entries.Append(trailer[..trailer.IndexOf("startxref", StringComparison.Ordinal)]);
        entries.Append("startxref\n" + table + "\n%%EOF\n");
        return [.. file.AsSpan(0, table), .. Encoding.Latin1.GetBytes(entries.ToString())];
    }
}
