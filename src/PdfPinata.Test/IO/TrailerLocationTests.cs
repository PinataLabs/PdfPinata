using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using AwesomeAssertions;
using PdfPinata.Pdf.IO;
using PdfPinata.Test.Helpers;
using TUnit.Core;

namespace PdfPinata.Test.IO;

/// <summary>
///   How "startxref" is found, and what looking for it costs. Appendix H's implementation note 18
///   asks only that "%%EOF" appear within the last 1024 bytes of the file, which leaves the word
///   that points at the cross-reference table free to sit anywhere at all - SAP writes several
///   MByte of padding behind it, and the file reported as empira/PDFsharp#390 a comment of a
///   gigabyte. A reader that gives up on the last kilobyte and then reads the whole file into one
///   string cannot open either: <see cref="string" /> tops out at 1,073,741,791 characters, so a
///   file larger than that is refused with an <see cref="OutOfMemoryException" /> on a machine
///   with any amount of memory free.
/// </summary>
/// <remarks>
///   The two large documents here are never a gigabyte in memory or on disk. <see
///   cref="SplicedStream" /> serves the comment a buffer at a time from a single repeated byte, so
///   what the test costs is the scan itself.
/// </remarks>
public class TrailerLocationTests
{
    // ----- What the scan costs ---------------------------------------------------------------

    [Test]
    public void NoSingleReadIsLargerThanTheScanBuffer_whenStartxrefIsNotNearTheEndOfTheFile()
    {
        const int commentLength = 2 * 1024 * 1024;
        var recorder = new RecordingStream(SyntheticPdf.WithTrailingComment(commentLength));

        using (var document = PdfPinata.Pdf.IO.PdfReader.Open(recorder, PdfDocumentOpenMode.Import))
            document.PageCount.Should().Be(1);

        // Reading the whole file into one string to search it is what ran out of memory on the
        // reported document. The scan that replaced it holds one buffer - 64 kiB, and the eight
        // bytes of overlap that catch a "startxref" lying across a chunk boundary - however long
        // the file is.
        recorder.LargestRead.Should().BeLessThanOrEqualTo(64 * 1024 + "startxref".Length - 1);
    }

    // ----- The documents the old scan could not open ------------------------------------------

    [Test]
    public void AFileLongerThanAStringCanBeIsStillOpened()
    {
        // 1.1 GiB: past the 1,073,741,791 characters a string holds, so reading the file into one
        // is an OutOfMemoryException rather than a slow success.
        const long commentLength = 1181116006L;
        var stream = SyntheticPdf.WithTrailingComment(commentLength);
        stream.Length.Should().BeGreaterThan(1073741791L);

        using var document = PdfPinata.Pdf.IO.PdfReader.Open(stream, PdfDocumentOpenMode.Import);

        document.PageCount.Should().Be(1);
    }

    [Test]
    public void AFileLongerThanTwoGigabytesIsStillOpened()
    {
        // 2.5 GiB: past int.MaxValue, which the scan used to refuse outright.
        const long commentLength = 2684354560L;
        var stream = SyntheticPdf.WithTrailingComment(commentLength);
        stream.Length.Should().BeGreaterThan(int.MaxValue);

        using var document = PdfPinata.Pdf.IO.PdfReader.Open(stream, PdfDocumentOpenMode.Import);

        document.PageCount.Should().Be(1);
    }

    // ----- A file with no cross-reference table at all -----------------------------------------

    [Test]
    public void AFileWithNoStartxrefInItIsRefusedRatherThanRead()
    {
        var bytes = Encoding.Latin1.GetBytes("%PDF-1.7\nnothing to see here\n%%EOF\n");

        var open = () => PdfPinata.Pdf.IO.PdfReader.Open(new MemoryStream(bytes), PdfDocumentOpenMode.Import);

        open.Should().Throw<Exception>().WithMessage("*StartXRef*");
    }

    /// <summary>
    ///   Assembles a one-page document that keeps "startxref" a stated number of bytes from the
    ///   end of the file, behind a comment of that length - the shape of the document reported as
    ///   empira/PDFsharp#390.
    /// </summary>
    private static class SyntheticPdf
    {
        internal static SplicedStream WithTrailingComment(long commentLength)
        {
            var objects = new List<string>
            {
                "<</Type/Catalog/Pages 2 0 R>>",
                "<</Type/Pages/Count 1/Kids[3 0 R]>>",
                "<</Type/Page/MediaBox[0 0 595 842]/Parent 2 0 R>>"
            };

            var pdf = new StringBuilder("%PDF-1.7\n");
            var offsets = new List<int>();
            for (var i = 0; i < objects.Count; i++)
            {
                offsets.Add(pdf.Length);
                pdf.Append(i + 1).Append(" 0 obj\n").Append(objects[i]).Append("\nendobj\n");
            }

            var startOfCrossReferenceTable = pdf.Length;
            pdf.Append("xref\n0 ").Append(objects.Count + 1).Append('\n');
            pdf.Append("0000000000 65535 f \n");
            foreach (var offset in offsets)
                pdf.Append(offset.ToString("D10")).Append(" 00000 n \n");
            pdf.Append("trailer\n<</Size ").Append(objects.Count + 1).Append("/Root 1 0 R>>\n");
            pdf.Append("startxref\n").Append(startOfCrossReferenceTable).Append("\n%");

            // The comment opened by that "%" runs until the newline the tail begins with.
            return new SplicedStream(
                Encoding.Latin1.GetBytes(pdf.ToString()),
                commentLength,
                (byte)'A',
                Encoding.Latin1.GetBytes("\n%%EOF\n"));
        }
    }
}
