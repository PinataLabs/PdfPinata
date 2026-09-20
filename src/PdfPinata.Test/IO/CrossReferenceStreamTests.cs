using System.IO;
using System.Text;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using PdfPinata.Pdf.Security;
using Xunit;

// This namespace has a PdfReader of its own, so the one that opens documents needs saying in full.
using Reader = PdfPinata.Pdf.IO.PdfReader;

namespace PdfPinata.Test.IO;

/// <summary>
///   The writer indexed every document with a cross-reference table and a trailer, the form PDF 1.4
///   had, even when the document it was saving had been read from a file that used a cross-reference
///   stream — <c>PdfDocument.DoSave</c> converted one back to the other on the way out. So every
///   object went out on its own and uncompressed, which costs most on a document that is mostly
///   objects rather than mostly content, and there was no way to reach PDF 1.5 or anything above it.
///
///   <see cref="PdfCrossReferenceFormat.Stream"/> gathers what may be gathered into object streams
///   and indexes everything in a stream of fixed-width rows.
/// </summary>
public class CrossReferenceStreamTests
{
    private const string Title = "A document indexed by a cross-reference stream";

    [Fact]
    public void TheClassicCrossReferenceTableIsStillWhatADocumentGetsByDefault()
    {
        var bytes = Save(_ => { });

        Latin1(bytes).Should().Contain("trailer",
            "changing the default would change the bytes of every document written by anyone who "
            + "has not asked for anything, and that is not a change to make silently");
    }

    [Fact]
    public void ADocumentIndexedByACrossReferenceStreamHasNoTrailerAndNoCrossReferenceTable()
    {
        var bytes = Save(document => document.Options.CrossReferenceFormat = PdfCrossReferenceFormat.Stream);

        var text = Latin1(bytes);
        text.Should().NotContain("trailer", "the trailer entries live on the cross-reference stream instead");
        text.Should().Contain("/Type /XRef", "the index is now an object rather than a section of its own");
    }

    [Fact]
    public void ADocumentIndexedByACrossReferenceStreamIsReadBack()
    {
        var bytes = Save(document =>
        {
            document.Options.CrossReferenceFormat = PdfCrossReferenceFormat.Stream;
            document.Info.Title = Title;
        });

        using var saved = new MemoryStream(bytes);
        var reread = Reader.Open(saved, PdfDocumentOpenMode.Modify);

        reread.PageCount.Should().Be(3);
        reread.Info.Title.Should().Be(Title);
    }

    [Fact]
    public void APageOfADocumentIndexedByACrossReferenceStreamKeepsItsSize()
    {
        // The page dictionaries are exactly the objects that get moved into an object stream, so
        // this is the assertion that says a type-2 entry resolves to the object it names.
        var bytes = Save(document =>
        {
            document.Options.CrossReferenceFormat = PdfCrossReferenceFormat.Stream;
            document.Pages[1].Width = XUnit.FromPoint(200);
            document.Pages[1].Height = XUnit.FromPoint(400);
        });

        using var saved = new MemoryStream(bytes);
        var reread = Reader.Open(saved, PdfDocumentOpenMode.Modify);

        reread.Pages[1].Width.Point.Should().BeApproximately(200, 0.5);
        reread.Pages[1].Height.Point.Should().BeApproximately(400, 0.5);
    }

    [Fact]
    public void TheObjectsThatMayBeCompressedAreGatheredIntoAnObjectStream()
    {
        var bytes = Save(document => document.Options.CrossReferenceFormat = PdfCrossReferenceFormat.Stream);

        Latin1(bytes).Should().Contain("/ObjStm",
            "the dictionary of an object stream is in plain sight; only the bodies it holds are compressed");
    }

    [Fact]
    public void AContentStreamIsNotMovedIntoAnObjectStream()
    {
        // A stream cannot nest inside a stream, so a page's content has to stay an object of its
        // own however much else is gathered up.
        var bytes = Save(document => document.Options.CrossReferenceFormat = PdfCrossReferenceFormat.Stream);

        Latin1(bytes).Should().Contain("stream",
            "the content streams are still written out one by one, outside any object stream");
    }

    [Fact]
    public void TheVersionIsRaisedToOnePointFive()
    {
        var bytes = Save(document => document.Options.CrossReferenceFormat = PdfCrossReferenceFormat.Stream);

        Latin1(bytes).Should().StartWith("%PDF-1.5",
            "a cross-reference stream is a PDF 1.5 construct and a file carrying one may not "
            + "announce itself as anything earlier");
    }

    [Fact]
    public void AnObjectHeavyDocumentIsSmallerThanItIsInTheClassicForm()
    {
        var classic = Save(WithManyOutlines);
        var compressed = Save(document =>
        {
            document.Options.CrossReferenceFormat = PdfCrossReferenceFormat.Stream;
            WithManyOutlines(document);
        });

        compressed.Length.Should().BeLessThan(classic.Length,
            "gathering the dictionaries into one compression window is the entire point of doing this");
    }

    [Theory]
    [InlineData(PdfDocumentSecurityLevel.Encrypted40Bit)]
    [InlineData(PdfDocumentSecurityLevel.Encrypted128Bit)]
    public void AStringInAnEncryptedDocumentSurvivesBeingMovedIntoAnObjectStream(
        PdfDocumentSecurityLevel level)
    {
        // The trap this format sets. Strings and streams inside an object stream are not encrypted
        // individually — the object stream that contains them is encrypted, once, and they ride
        // along inside it. Encrypting them a second time produces a document that opens, looks
        // well, and yields mojibake for every string in it, which is the worst shape a defect can
        // take because it survives a smoke test.
        const string ownerPassword = "12343";

        var bytes = Save(document =>
        {
            document.Options.CrossReferenceFormat = PdfCrossReferenceFormat.Stream;
            document.Info.Title = Title;
            document.Info.Author = "Ångström";

            var settings = document.SecuritySettings;
            settings.DocumentSecurityLevel = level;
            settings.OwnerPassword = ownerPassword;
            settings.UserPassword = "";
        });

        using var saved = new MemoryStream(bytes);
        var reread = Reader.Open(saved, ownerPassword, PdfDocumentOpenMode.Modify);

        reread.Info.Title.Should().Be(Title);
        reread.Info.Author.Should().Be("Ångström");
    }

    [Fact]
    public void TheEncryptionDictionaryIsNotMovedIntoAnObjectStream()
    {
        // A reader has to reach the encryption dictionary before it can decrypt anything, and that
        // includes the object stream the dictionary would otherwise be hiding in.
        var bytes = Save(document =>
        {
            document.Options.CrossReferenceFormat = PdfCrossReferenceFormat.Stream;
            var settings = document.SecuritySettings;
            settings.DocumentSecurityLevel = PdfDocumentSecurityLevel.Encrypted128Bit;
            settings.OwnerPassword = "12343";
            settings.UserPassword = "";
        });

        Latin1(bytes).Should().Contain("/Filter /Standard",
            "the encryption dictionary has to be readable before anything else is");
    }

    [Fact]
    public void ADocumentSavedBothWaysHasTheSamePagesAndTheSameProperties()
    {
        var classic = ReopenAndDescribe(Save(WithManyOutlines));
        var compressed = ReopenAndDescribe(Save(document =>
        {
            document.Options.CrossReferenceFormat = PdfCrossReferenceFormat.Stream;
            WithManyOutlines(document);
        }));

        compressed.Should().Be(classic,
            "the format decides how the objects are indexed and nothing about what they say");
    }

    [Fact]
    public void MoreObjectsThanFitOneObjectStreamAreSplitAcrossSeveral()
    {
        var bytes = Save(document =>
        {
            document.Options.CrossReferenceFormat = PdfCrossReferenceFormat.Stream;
            document.Options.MaxObjectsPerObjectStream = 4;
            WithManyOutlines(document);
        });

        Occurrences(Latin1(bytes), "/ObjStm").Should().BeGreaterThan(1);

        using var saved = new MemoryStream(bytes);
        Reader.Open(saved, PdfDocumentOpenMode.Modify).PageCount.Should().Be(3,
            "splitting changes which stream an object is in and nothing about finding it");
    }

    [Fact]
    public void AnObjectStreamHasToHoldAtLeastOneObject()
    {
        var document = new PdfDocument();

        var setting = () => document.Options.MaxObjectsPerObjectStream = 0;

        setting.Should().Throw<System.ArgumentOutOfRangeException>();
    }

    /// <summary>
    ///   Something with many small dictionaries and little content, which is the shape of document
    ///   this format exists for.
    /// </summary>
    private static void WithManyOutlines(PdfDocument document)
    {
        for (var index = 0; index < 40; index++)
            document.Outlines.Add("Section " + index, document.Pages[index % document.PageCount]);
    }

    /// <summary>
    ///   What the document says, in a form two documents can be compared by. Deliberately not the
    ///   bytes: the two formats are meant to differ there and agree about everything else.
    /// </summary>
    private static string ReopenAndDescribe(byte[] bytes)
    {
        using var saved = new MemoryStream(bytes);
        var document = Reader.Open(saved, PdfDocumentOpenMode.Modify);

        var description = new StringBuilder();
        description.Append(document.PageCount).Append('|').Append(document.Info.Title).Append('|');
        foreach (var page in document.Pages)
            description.Append(page.Width.Point).Append('x').Append(page.Height.Point).Append(';');
        description.Append('|').Append(document.Outlines.Count);
        return description.ToString();
    }

    private static byte[] Save(System.Action<PdfDocument> arrange)
    {
        var document = new PdfDocument();
        for (var index = 0; index < 3; index++)
        {
            var page = document.AddPage();
            var gfx = XGraphics.FromPdfPage(page);
            gfx.DrawRectangle(XBrushes.LightGray, 20, 20, 100, 40);
        }
        document.Info.Title = Title;

        arrange(document);

        using var output = new MemoryStream();
        document.Save(output, false);
        return output.ToArray();
    }

    private static string Latin1(byte[] bytes) => Encoding.Latin1.GetString(bytes);

    private static int Occurrences(string text, string value)
    {
        var count = 0;
        for (var at = text.IndexOf(value, System.StringComparison.Ordinal); at >= 0;
             at = text.IndexOf(value, at + 1, System.StringComparison.Ordinal))
            count++;
        return count;
    }
}
