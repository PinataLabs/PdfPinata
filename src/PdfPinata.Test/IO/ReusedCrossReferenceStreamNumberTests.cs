using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Advanced;
using PdfPinata.Pdf.IO;
using PdfPinata.Test.Helpers;
using Xunit;

// This namespace has a PdfReader of its own, so the one that opens documents needs saying in full.
using Reader = PdfPinata.Pdf.IO.PdfReader;

namespace PdfPinata.Test.IO;

/// <summary>
///   An incremental update may give a new object the number an earlier revision's cross-reference
///   stream had. Nothing in ISO 32000-1 keeps that number free, and tools that sign documents do
///   reuse it: the file attached to empira/PDFsharp#213 has its /AcroForm under the number of its
///   first revision's cross-reference stream. The newer revision is read first, so its entry for
///   the number is already in the table when the older stream is reached, with a position and no
///   value yet - and the reader took any such entry to be the stream itself and hung the stream on
///   it. The form then read as a cross-reference stream with no /Fields, which is what
///   empira/PDFsharp#353 saw: a signed document whose fields came back without names.
/// </summary>
public class ReusedCrossReferenceStreamNumberTests
{
    private const string Title = "Signed in a later revision";

    [Fact]
    public void AnObjectReusingTheNumberOfAnEarlierCrossReferenceStreamIsReadAsItself()
    {
        var reread = Open(WithFormUnderTheCrossReferenceStreamsNumber());

        reread.AcroForm.Should().NotBeNull("the catalog of the newest revision names one");
        reread.AcroForm.Fields.Names.Should().Equal(["Signature1"],
            "the number now belongs to the form the update added, not to the index it was taken from");
    }

    [Fact]
    public void TheObjectsTheDisplacedCrossReferenceStreamCompressedAreStillRead()
    {
        // The displaced stream is still what says where the first revision's compressed objects
        // are, so keeping it out of the table must not lose them.
        var reread = Open(WithFormUnderTheCrossReferenceStreamsNumber());

        reread.PageCount.Should().Be(2);
        reread.Pages[1].Width.Point.Should().BeApproximately(200, 0.5);
        reread.Info.Title.Should().Be(Title);
    }

    [Fact]
    public void ACrossReferenceStreamListedByTheStreamAfterItIsStillRead()
    {
        // The case an entry with a position and no value is really there for: a newer
        // cross-reference stream listing the older one among its objects, at the older one's own
        // offset. That entry is the stream, and hanging the stream on it is right.
        var reread = Open(WithSecondStreamListingTheFirst());

        reread.PageCount.Should().Be(2);
        reread.Pages[1].Width.Point.Should().BeApproximately(200, 0.5);
        reread.Info.Title.Should().Be(Title);
        reread.Info.Subject.Should().Be("Second revision");
    }

    /// <summary>
    ///   A first revision indexed by a cross-reference stream, its pages compressed into an object
    ///   stream, and a second revision appended by hand with a classic table that puts a form under
    ///   the stream's own number - the shape of the file attached to empira/PDFsharp#213.
    /// </summary>
    private static byte[] WithFormUnderTheCrossReferenceStreamsNumber()
    {
        var first = FirstRevision();
        var update = Begin(first);

        var catalogOffset = update.Length;
        update.Append(first.Root).Append(" 0 obj\n<</Type/Catalog/Pages ").Append(first.Pages)
            .Append(" 0 R/AcroForm ").Append(first.StreamNumber).Append(" 0 R>>\nendobj\n");
        var formOffset = update.Length;
        var fieldNumber = first.Size;
        update.Append(first.StreamNumber).Append(" 0 obj\n<</Fields[").Append(fieldNumber)
            .Append(" 0 R]/SigFlags 3>>\nendobj\n");
        var fieldOffset = update.Length;
        update.Append(fieldNumber).Append(" 0 obj\n<</FT/Sig/T(Signature1)>>\nendobj\n");

        var xrefOffset = update.Length;
        update.Append("xref\n");
        Entry(update, first.Root, catalogOffset);
        Entry(update, first.StreamNumber, formOffset);
        Entry(update, fieldNumber, fieldOffset);
        update.Append("trailer\n<</Size ").Append(fieldNumber + 1).Append("/Root ").Append(first.Root)
            .Append(" 0 R/Info ").Append(first.Info).Append(" 0 R/Prev ").Append(first.StreamOffset)
            .Append(">>\nstartxref\n").Append(xrefOffset).Append("\n%%EOF\n");

        // Everything written is ASCII, so a character is a byte and the offsets above hold.
        return Encoding.Latin1.GetBytes(update.ToString());
    }

    /// <summary>
    ///   The first revision, and a second appended by hand that is indexed by a cross-reference
    ///   stream of its own: it replaces the information dictionary, and lists the first revision's
    ///   stream at the offset it really has, the way a writer keeping every revision's index does.
    /// </summary>
    private static byte[] WithSecondStreamListingTheFirst()
    {
        var first = FirstRevision();
        var update = Begin(first);

        var infoOffset = update.Length;
        update.Append(first.Info).Append(" 0 obj\n<</Title(").Append(Title)
            .Append(")/Subject(Second revision)>>\nendobj\n");

        // Three entries of /W [1 4 1]: the new information dictionary, the first revision's stream
        // where it stands, and this stream itself. Unfiltered, one character to a byte.
        var newNumber = first.Size;
        var newOffset = update.Length;
        var data = new StringBuilder();
        foreach (var offset in new[] { infoOffset, first.StreamOffset, newOffset })
        {
            data.Append((char)1);
            data.Append((char)((offset >> 24) & 0xFF)).Append((char)((offset >> 16) & 0xFF))
                .Append((char)((offset >> 8) & 0xFF)).Append((char)(offset & 0xFF));
            data.Append((char)0);
        }

        update.Append(newNumber).Append(" 0 obj\n<</Type/XRef/Size ").Append(newNumber + 1)
            .Append("/Index[").Append(first.Info).Append(" 1 ").Append(first.StreamNumber).Append(" 1 ")
            .Append(newNumber).Append(" 1]/W[1 4 1]/Root ").Append(first.Root).Append(" 0 R/Info ")
            .Append(first.Info).Append(" 0 R/Prev ").Append(first.StreamOffset).Append("/Length ")
            .Append(data.Length).Append(">>stream\n").Append(data).Append("\nendstream\nendobj\n")
            .Append("startxref\n").Append(newOffset).Append("\n%%EOF\n");

        return Encoding.Latin1.GetBytes(update.ToString());
    }

    private static StringBuilder Begin(Revision first)
    {
        var update = new StringBuilder(first.Text);
        if (update[update.Length - 1] != '\n')
            update.Append('\n');
        return update;
    }

    private static void Entry(StringBuilder pdf, int number, int offset)
        => pdf.Append(number).Append(" 1\n").Append(offset.ToString("D10")).Append(" 00000 n \n");

    /// <summary>
    ///   A two-page document indexed by a cross-reference stream, so that its page dictionaries are
    ///   compressed into an object stream, and the numbers an update to it has to know.
    /// </summary>
    private static Revision FirstRevision()
    {
        var document = new PdfDocument();
        document.Options.CrossReferenceFormat = PdfCrossReferenceFormat.Stream;
        document.Info.Title = Title;
        _ = document.AddPage();
        var second = document.AddPage();
        second.Width = XUnit.FromPoint(200);
        second.Height = XUnit.FromPoint(400);

        using var output = new MemoryStream();
        document.Save(output);
        var bytes = output.ToArray();
        var text = Encoding.Latin1.GetString(bytes);

        var streamOffset = int.Parse(Regex.Match(text, @"startxref\s+(\d+)\s+%%EOF\s*$").Groups[1].Value);
        var stream = text.Substring(streamOffset);

        int pages;
        using (var input = new MemoryStream(bytes))
        {
            var reread = Reader.Open(input, PdfDocumentOpenMode.Import);
            pages = ((PdfReference)reread.Internals.Catalog.Elements["/Pages"]!).ObjectNumber;
        }

        return new Revision(
            text,
            streamOffset,
            Number(stream, @"^(\d+) 0 obj"),
            Number(stream, @"/Size (\d+)"),
            Number(stream, @"/Root (\d+) 0 R"),
            Number(stream, @"/Info (\d+) 0 R"),
            pages);
    }

    private static int Number(string text, string pattern)
        => int.Parse(Regex.Match(text, pattern).Groups[1].Value);

    private sealed record Revision(
        string Text, int StreamOffset, int StreamNumber, int Size, int Root, int Info, int Pages);

    private static PdfDocument Open(byte[] bytes) => Saved.Open(bytes, PdfDocumentOpenMode.Import);
}
