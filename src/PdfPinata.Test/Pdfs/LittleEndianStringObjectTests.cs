using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Advanced;
using PdfPinata.Pdf.IO;
using TUnit.Core;

namespace PdfPinata.Test.Pdfs;

/// <summary>
///   An indirect string read from bytes that open FF FE is written back in that byte order only
///   while it is unchanged. A value or an encoding the caller sets is written as any other
///   Unicode string is, as UTF-16BE.
/// </summary>
public class LittleEndianStringObjectTests
{
    private static readonly byte[] LittleEndianZ = [0xFF, 0xFE, (byte)'Z', 0x00];

    [Test]
    public void AStringObjectReadLittleEndianIsWrittenBackAsItWasRead()
    {
        var saved = Edited(_ => { });

        Contains(saved, [(byte)'(', .. LittleEndianZ, (byte)')']).Should().BeTrue();
    }

    [Test]
    public void ANewValueOnAStringObjectReadLittleEndianIsWrittenBigEndian()
    {
        var saved = Edited(text => text.Value = "ZedΩ");

        Encoding.Latin1.GetString(saved).Should().Contain("<FEFF005A0065006403A9>");
        Contains(saved, [0xFF, 0xFE]).Should().BeFalse();
    }

    [Test]
    public void SettingTheEncodingOfAStringObjectReadLittleEndianForgetsTheByteOrder()
    {
        var saved = Edited(text =>
        {
            text.Encoding = PdfStringEncoding.PDFDocEncoding;
            text.Encoding = PdfStringEncoding.Unicode;
        });

        Encoding.Latin1.GetString(saved).Should().Contain("<FEFF005A>");
        Contains(saved, [0xFF, 0xFE]).Should().BeFalse();
    }

    private static byte[] Edited(System.Action<PdfStringObject> edit)
    {
        using var document = global::PdfPinata.Pdf.IO.PdfReader.Open(new MemoryStream(Document()), PdfDocumentOpenMode.Modify);
        var text = (PdfStringObject)((PdfReference)document.Info.Elements["/Subject"]).Value;
        text.Value.Should().Be("Z", "the string is read low byte first");
        edit(text);
        var output = new MemoryStream();
        document.Save(output, false);
        return output.ToArray();
    }

    private static bool Contains(byte[] haystack, byte[] needle) => haystack.AsSpan().IndexOf(needle) >= 0;

    private static byte[] Document()
    {
        var pdf = new MemoryStream();
        var offsets = new List<long>();
        Write("%PDF-1.4\n");
        Object("<</Type/Catalog/Pages 2 0 R>>");
        Object("<</Type/Pages/Kids[3 0 R]/Count 1>>");
        Object("<</Type/Page/Parent 2 0 R/MediaBox[0 0 200 100]>>");
        Object("<</Subject 5 0 R>>");
        offsets.Add(pdf.Position);
        Write("5 0 obj\n(");
        pdf.Write(LittleEndianZ);
        Write(")\nendobj\n");

        var start = pdf.Position;
        Write("xref\n0 6\n0000000000 65535 f \n");
        foreach (var offset in offsets)
            Write(offset.ToString("0000000000") + " 00000 n \n");
        Write("trailer\n<</Size 6/Root 1 0 R/Info 4 0 R>>\nstartxref\n" + start + "\n%%EOF\n");
        return pdf.ToArray();

        void Write(string text) => pdf.Write(Encoding.Latin1.GetBytes(text));

        void Object(string body)
        {
            offsets.Add(pdf.Position);
            Write(offsets.Count + " 0 obj\n" + body + "\nendobj\n");
        }
    }
}
