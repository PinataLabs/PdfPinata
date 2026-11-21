using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using TUnit.Core;
using Reader = PdfPinata.Pdf.IO.PdfReader;

namespace PdfPinata.Test.IO;

/// <summary>
/// A cross-reference stream whose predicted data ends partway through its last row used to stop
/// the document opening, and now opens it without that entry.
/// </summary>
public class PartialPngRowCrossReferenceTests
{
    private static byte[] Deflated(byte[] data)
    {
        using var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, CompressionLevel.Optimal, leaveOpen: true))
            zlib.Write(data, 0, data.Length);
        return output.ToArray();
    }

    /// <summary>
    /// A file whose cross-reference stream has five rows of one predictor byte and the fields
    /// <c>/W [1 2 1]</c>, the data cut short by <paramref name="dropped"/> bytes.
    /// </summary>
    private static byte[] FileWithCrossReferenceRowsCutBy(int dropped)
    {
        var file = new MemoryStream();
        void Write(string text) => file.Write(Encoding.Latin1.GetBytes(text));

        Write("%PDF-1.5\n");
        var offsets = new long[5];
        offsets[1] = file.Position;
        Write("1 0 obj << /Type /Catalog /Pages 2 0 R >> endobj\n");
        offsets[2] = file.Position;
        Write("2 0 obj << /Type /Pages /Kids [3 0 R] /Count 1 >> endobj\n");
        offsets[3] = file.Position;
        Write("3 0 obj << /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] >> endobj\n");
        offsets[4] = file.Position;

        var rows = new List<byte>();
        for (var i = 0; i < 5; i++)
        {
            rows.Add(0);
            rows.Add((byte)(i == 0 ? 0 : 1));
            var offset = i == 0 ? 0 : (int)offsets[i];
            rows.Add((byte)(offset >> 8));
            rows.Add((byte)offset);
            rows.Add((byte)(i == 0 ? 255 : 0));
        }

        var deflated = Deflated(rows.ToArray()[..^dropped]);
        Write($"4 0 obj << /Type /XRef /Size 5 /W [1 2 1] /Root 1 0 R /Filter /FlateDecode " +
              $"/DecodeParms << /Predictor 12 /Columns 4 >> /Length {deflated.Length} >> stream\n");
        file.Write(deflated);
        Write("\nendstream endobj\n");
        Write($"startxref\n{offsets[4]}\n%%EOF\n");
        return file.ToArray();
    }

    [Test]
    [Arguments(0, PdfDocumentOpenMode.Import)]
    [Arguments(1, PdfDocumentOpenMode.Import)]
    [Arguments(3, PdfDocumentOpenMode.Modify)]
    [Arguments(1, PdfDocumentOpenMode.ReadOnly)]
    public async Task ACrossReferenceStreamEndingInAPartRowOpensAndLosesOnlyThatEntry(int dropped, PdfDocumentOpenMode mode)
    {
        var bytes = FileWithCrossReferenceRowsCutBy(dropped);

        using var document = Reader.Open(new MemoryStream(bytes), mode);

        await Task.CompletedTask;
        document.PageCount.Should().Be(1, "the entries before the part row are all the document needs");
    }
}
