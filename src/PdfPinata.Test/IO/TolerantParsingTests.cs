using System.Threading.Tasks;
using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Advanced;
using PdfPinata.Pdf.IO;
using Xunit;

namespace PdfPinata.Test.IO;

/// <summary>
///   The parsing the document parser does when the file it is reading is not what the
///   specification promises: an object that leaves "endobj" out, a stream whose dictionary does
///   not say how long it is, a dictionary entry with no key, and a reference to an object that is
///   not there. Every one of these is pinned today only through <c>PdfReader.Open</c>,
///   which means a whole file - header, cross-reference table, trailer with a /Root - has to be
///   assembled around the few bytes the test is actually about. These reach the parser through
///   <see cref="ParserProbe"/> instead: a plain <c>new PdfDocument()</c> to own what is read, and
///   the bytes themselves.
/// </summary>
/// <remarks>
///   Reading input the parser does not expect can hang rather than fail, so every test that scans
///   runs inside a <see cref="Task"/> xUnit can time out.
/// </remarks>
public class TolerantParsingTests
{
    // ----- What is taken as the end of an object that does not say "endobj" -------------------------

    [Theory(Timeout = 5000)]
    [InlineData("xref", Symbol.XRef)]
    [InlineData("trailer", Symbol.Trailer)]
    [InlineData("startxref", Symbol.StartXRef)]
    [InlineData("%%EOF", Symbol.Eof)]
    [InlineData("", Symbol.Eof)]
    public async Task EndsAnObject_takesWhatEndsTheBodyOfTheFileAsTheEndOfTheObject(string body, Symbol expected)
    {
        var (symbol, endsIt) = await Task.Run(() =>
        {
            var parser = ParserProbe.Over(new PdfDocument(), body);
            var scanned = ParserProbe.Scan(parser);
            return (scanned, ParserProbe.EndsAnObject(parser, scanned));
        });

        symbol.Should().Be(expected);
        endsIt.Should().BeTrue();
    }

    [Fact(Timeout = 5000)]
    public async Task EndsAnObject_takesTheObjectNumberOfTheNextObjectAsTheEndOfThisOne()
    {
        // The reported file: object 59 does not close, so the "60" opening object 60 is what
        // stands where "endobj" should have been.
        var endsIt = await Task.Run(() => EndsAnObject("60 0 obj"));

        endsIt.Should().BeTrue();
    }

    [Theory(Timeout = 5000)]
    [InlineData("60")]      // nothing behind it at all
    [InlineData("60 0")]    // a generation number, but no keyword
    [InlineData("60 0 R")]  // a reference, which is a value rather than a header
    [InlineData("0 0 obj")] // object numbers count from one; zero heads the list of free objects
    public async Task EndsAnObject_stillReportsAStrayNumberThatOpensNoObject(string body)
    {
        var endsIt = await Task.Run(() => EndsAnObject(body));

        endsIt.Should().BeFalse();
    }

    [Theory(Timeout = 5000)]
    [InlineData("/Name")]
    [InlineData("(a string)")]
    [InlineData("[")]
    [InlineData("endstream")]
    [InlineData("true")]
    public async Task EndsAnObject_reportsAnythingElseFoundWhereTheKeywordShouldBe(string body)
    {
        var endsIt = await Task.Run(() => EndsAnObject(body));

        endsIt.Should().BeFalse();
    }

    [Fact(Timeout = 5000)]
    public async Task BeginsAnIndirectObject_leavesTheInputWhereItFoundIt()
    {
        // Looking ahead is all it does: the caller goes on to read the number itself, so what it
        // reads to decide has to be put back.
        var (before, after) = await Task.Run(() =>
        {
            var parser = ParserProbe.Over(new PdfDocument(), "60 0 obj");
            ParserProbe.Scan(parser);
            var start = ParserProbe.Position(parser);
            ParserProbe.BeginsAnIndirectObject(parser);
            return (start, ParserProbe.Position(parser));
        });

        after.Should().Be(before);
    }

    // ----- The stream a dictionary does not describe -------------------------------------------------

    [Fact(Timeout = 5000)]
    public async Task TryReadStreamUpToEndOfStream_readsUpToTheKeywordWithoutTheEndOfLineBeforeIt()
    {
        var dict = await Task.Run(() => StreamOf("<< >>\nstream\r\nDATA\r\nendstream"));

        dict.Stream.Value.Should().Equal(ParserProbe.Bytes("DATA"));
    }

    [Fact(Timeout = 5000)]
    public async Task TryReadStreamUpToEndOfStream_readsAStreamThatEndsWithNoEndOfLineAtAll()
    {
        // A document that gets its stream lengths wrong is not one to be trusted to write the
        // separator either, and a stream ending without one still ends.
        var dict = await Task.Run(() => StreamOf("<< >>\nstream\nDATAendstream"));

        dict.Stream.Value.Should().Equal(ParserProbe.Bytes("DATA"));
    }

    [Fact(Timeout = 5000)]
    public async Task TryReadStreamUpToEndOfStream_recordsTheLengthItRead()
    {
        // The dictionary has to describe its stream correctly once the document is written again.
        var dict = await Task.Run(() => StreamOf("<< >>\nstream\r\nDATA\r\nendstream"));

        dict.Elements.GetInteger("/Length").Should().Be(4);
    }

    [Fact(Timeout = 5000)]
    public async Task TryReadStreamUpToEndOfStream_reportsADocumentThatHoldsNoKeywordAtAll()
    {
        var (found, dict) = await Task.Run(() =>
        {
            var owner = new PdfDocument();
            var parser = ParserProbe.Over(owner, "<< >>\nstream\r\nDATA and then nothing");
            var read = new PdfDictionary(owner);
            ParserProbe.Scan(parser);
            ParserProbe.ReadDictionary(parser, read);
            ParserProbe.Scan(parser);
            return (ParserProbe.TryReadStreamUpToEndOfStream(parser, read, ParserProbe.Position(parser)), read);
        });

        found.Should().BeFalse();
        dict.Stream.Should().BeNull("nothing was found to read");
    }

    [Theory]
    [InlineData("DATA\r\n", "DATA")]
    [InlineData("DATA\n", "DATA")]
    [InlineData("DATA\r", "DATA")]
    [InlineData("DATA\n\n", "DATA\n")] // one end-of-line belongs to the file; the rest is data
    [InlineData("\n", "")]
    [InlineData("", "")]
    public void WithoutTheEndOfLineBeforeTheKeyword_dropsTheOneSeparatorAndNoMore(string read, string expected)
    {
        var bytes = ParserProbe.WithoutTheEndOfLineBeforeTheKeyword(ParserProbe.Bytes(read));

        bytes.Should().Equal(ParserProbe.Bytes(expected));
    }

    [Fact]
    public void WithoutTheEndOfLineBeforeTheKeyword_handsBackTheDataItselfWhenThereIsNothingToDrop()
    {
        var read = ParserProbe.Bytes("DATA");

        ParserProbe.WithoutTheEndOfLineBeforeTheKeyword(read).Should().BeSameAs(read);
    }

    // ----- References to objects that are not there --------------------------------------------------

    [Fact(Timeout = 5000)]
    public async Task ParseObject_readsAReferenceToAnObjectTheTableDoesNotHoldAsNull()
    {
        // PDF Reference 3.2.9: an indirect reference to an undefined object is not an error, it is
        // simply a reference to the null object.
        var items = await Task.Run(() =>
        {
            var parser = ParserProbe.Over(new PdfDocument(), "7 0 R]");
            return ParserProbe.ParseObject(parser, Symbol.EndArray);
        });

        items.Should().ContainSingle().Which.Should().Be(PdfNull.Value);
    }

    [Fact(Timeout = 5000)]
    public async Task ParseObject_makesATemporaryReferenceWhileTheTableIsStillBeingBuilt()
    {
        // A document with more than one cross-reference table can have its first trailer refer to
        // an object whose entry has not been read yet. PdfTrailer.FixXRefs replaces these later.
        var items = await Task.Run(() =>
        {
            var owner = new PdfDocument();
            ParserProbe.IsUnderConstruction(ParserProbe.IrefTableOf(owner), true);
            var parser = ParserProbe.Over(owner, "7 0 R]");
            return ParserProbe.ParseObject(parser, Symbol.EndArray);
        });

        var iref = items.Should().ContainSingle().Which.Should().BeOfType<PdfReference>().Subject;
        iref.ObjectID.Should().Be(new PdfObjectID(7, 0));
        iref.Position.Should().Be(0, "where the object really is is not known yet");
    }

    [Fact(Timeout = 5000)]
    public async Task ParseObject_readsAReferenceToAKnownObjectAsTheTablesOwnEntry()
    {
        var (items, owner) = await Task.Run(() =>
        {
            var document = new PdfDocument();
            ParserProbe.AddReference(document, new PdfObjectID(7, 0), 4711);
            var parser = ParserProbe.Over(document, "7 0 R]");
            return (ParserProbe.ParseObject(parser, Symbol.EndArray), document);
        });

        items.Should().ContainSingle().Which.Should()
            .BeSameAs(ParserProbe.ReferenceTo(ParserProbe.IrefTableOf(owner), new PdfObjectID(7, 0)));
    }

    // ----- A dictionary whose entries do not pair up ---------------------------------------------------

    [Fact(Timeout = 5000)]
    public async Task ReadDictionary_keepsThePairsAroundAValueThatHasNoKey()
    {
        // pdfTeX writes its PTEX.FullBanner as two strings with no key in front of them.
        var dict = await Task.Run(() =>
        {
            var owner = new PdfDocument();
            var parser = ParserProbe.Over(owner, "<< /First 1 (stray) /Second 2 >>");
            ParserProbe.Scan(parser);
            return ParserProbe.ReadDictionary(parser, new PdfDictionary(owner));
        });

        dict.Elements.GetInteger("/First").Should().Be(1);
        dict.Elements.GetInteger("/Second").Should().Be(2);
        dict.Elements.Count.Should().Be(2);
    }

    // ----- Helpers ---------------------------------------------------------------------------------------

    /// <summary>
    ///   Whether the first symbol of the given input is taken as the end of the object before it.
    /// </summary>
    static bool EndsAnObject(string body)
    {
        var parser = ParserProbe.Over(new PdfDocument(), body);
        return ParserProbe.EndsAnObject(parser, ParserProbe.Scan(parser));
    }

    /// <summary>
    ///   Reads a dictionary and the stream behind it the way the parser does when the dictionary
    ///   does not say how long its stream is: up to the keyword that ends it.
    /// </summary>
    static PdfDictionary StreamOf(string body)
    {
        var owner = new PdfDocument();
        var parser = ParserProbe.Over(owner, body);
        var dict = new PdfDictionary(owner);
        ParserProbe.Scan(parser);
        ParserProbe.ReadDictionary(parser, dict);
        ParserProbe.Scan(parser);
        ParserProbe.TryReadStreamUpToEndOfStream(parser, dict, ParserProbe.Position(parser))
            .Should().BeTrue("the keyword ending the stream is there to be found");
        return dict;
    }
}
