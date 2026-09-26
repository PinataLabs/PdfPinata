using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using PdfPinata.Test.Helpers;
using Xunit;

// This namespace has a PdfReader of its own, so the one that opens documents needs saying in full.
using Reader = PdfPinata.Pdf.IO.PdfReader;

namespace PdfPinata.Test.IO;

/// <summary>
///   <see cref="PdfDocument.SaveIncremental"/> on a file indexed by a cross-reference stream wrote
///   the keyword <c>trailer</c> and then the <em>old</em> cross-reference stream, object header and
///   all, where a trailer dictionary belonged. The result could not be read back, so appending to
///   such a file destroyed it (issue #55). A classic file was unaffected, which is why
///   <see cref="IncrementalUpdateTests"/>, whose fixture is classic, never saw it.
///
///   The appended revision is now indexed the way the one before it was: by a cross-reference stream
///   of its own, whose <c>/Prev</c> names the previous one.
/// </summary>
public class IncrementalUpdateOfCrossReferenceStreamTests
{
    [Fact]
    public void TheChangeIsReadBackFromTheAppendedRevision()
    {
        var updated = AppendChange(OriginalDocument(), document => document.Info.Subject = "Changed");

        Saved.Open(updated).Info.Subject.Should().Be("Changed");
    }

    [Fact]
    public void WhatWasNotChangedIsStillThere()
    {
        // The information dictionary and the pages live in object streams in the original, so this
        // is what says an entry of type 2 is still followed through the /Prev chain.
        var updated = AppendChange(OriginalDocument(), document => document.Info.Subject = "Changed");

        var reread = Saved.Open(updated);
        reread.Info.Title.Should().Be("Original title");
        reread.PageCount.Should().Be(2);
    }

    [Fact]
    public void TheOriginalBytesAreLeftExactlyWhereTheyWere()
    {
        var original = OriginalDocument();

        var updated = AppendChange(original, document => document.Info.Subject = "Changed");

        updated.Take(original.Length).Should().Equal(original,
            "an incremental update appends and never rewrites");
    }

    [Fact]
    public void TheAppendedRevisionIsIndexedByACrossReferenceStreamOfItsOwn()
    {
        var original = OriginalDocument();
        var updated = AppendChange(original, document => document.Info.Subject = "Changed");

        var appended = Appended(updated, original.Length);
        appended.Should().NotContain("trailer",
            "a revision after a cross-reference stream is indexed by another one, not by a table");
        CrossReferenceStreamsIn(appended).Should().Be(1);
        appended.Should().Contain("/Prev");
    }

    [Fact]
    public void ThePreviousCrossReferenceStreamIsNotWrittenAgain()
    {
        // The old stream is where the reader found the trailer's entries, but it is an index of the
        // previous revision and nothing else. Writing it again, under its old number, would shadow
        // it with an object that indexes nothing in this one.
        var original = OriginalDocument();
        var updated = AppendChange(original, document => document.Info.Subject = "Changed");

        CrossReferenceStreamsIn(Appended(updated, original.Length)).Should().Be(1,
            "exactly one cross-reference stream is appended, and it is the new one");
    }

    [Fact]
    public void ThePreviousCrossReferenceStreamIsNotWrittenAgainWhenTheFileHadNoInfo()
    {
        // The old stream is the document's trailer, and an object in the table besides. A file with
        // no /Info has one created on save, on the trailer, which marks the stream changed - and it
        // was then written again under its old number, stale entries and all.
        var original = WithoutInfo(OriginalDocument());
        var updated = AppendChange(original, document => document.Info.Subject = "Changed");

        CrossReferenceStreamsIn(Appended(updated, original.Length)).Should().Be(1);
        var reread = Saved.Open(updated);
        reread.Info.Subject.Should().Be("Changed");
        reread.PageCount.Should().Be(2);
    }

    [Fact]
    public void TwoSuccessiveUpdatesBothResolve()
    {
        var once = AppendChange(OriginalDocument(), document => document.Info.Subject = "First");
        var twice = AppendChange(once, document => document.Info.Keywords = "second");

        var reread = Saved.Open(twice);
        reread.Info.Subject.Should().Be("First");
        reread.Info.Keywords.Should().Be("second");
        reread.Info.Title.Should().Be("Original title");
    }

    [Fact]
    public void APageAddedByAnUpdateIsThere()
    {
        var updated = AppendChange(OriginalDocument(), document => _ = document.AddPage());

        Saved.Open(updated).PageCount.Should().Be(3);
    }

    [Fact]
    public void ChangingNothingStillProducesAReadableDocument()
    {
        var updated = AppendChange(OriginalDocument(), _ => { });

        Saved.Open(updated).PageCount.Should().Be(2);
    }

    [Fact]
    public void ADocumentIdentifiesItselfAcrossRevisionsAndIdentifiesEachRevisionApart()
    {
        var original = OriginalDocument();
        var updated = AppendChange(original, document => document.Info.Subject = "Changed");

        var before = Saved.Open(original).Internals;
        var after = Saved.Open(updated).Internals;

        after.FirstDocumentID.Should().Be(before.FirstDocumentID);
        after.SecondDocumentID.Should().NotBe(before.SecondDocumentID);
    }

    [Theory]
    [InlineData(PdfDocumentOpenMode.Import)]
    [InlineData(PdfDocumentOpenMode.ReadOnly)]
    [InlineData(PdfDocumentOpenMode.Append)]
    public void TheResultOpensInEveryMode(PdfDocumentOpenMode mode)
    {
        var updated = AppendChange(OriginalDocument(), document => document.Info.Subject = "Changed");

        using var stream = new MemoryStream(updated);
        Reader.Open(stream, mode).PageCount.Should().Be(2);
    }

    /// <summary>
    /// A document indexed by a cross-reference stream, with its compressible objects in object
    /// streams — the shape a PDF 1.5 writer produces, and the one the defect was found with.
    /// </summary>
    private static byte[] OriginalDocument()
    {
        var document = new PdfDocument();
        document.Options.CrossReferenceFormat = PdfCrossReferenceFormat.Stream;
        document.Info.Title = "Original title";
        for (var index = 0; index < 2; index++)
        {
            var gfx = XGraphics.FromPdfPage(document.AddPage());
            gfx.DrawRectangle(XBrushes.LightGray, 20, 20, 200, 100);
            gfx.DrawString("Page " + index, new XFont("Arial", 12), XBrushes.Black, 30, 160);
            gfx.Dispose();
        }

        return Saved.Bytes(document);
    }

    /// <summary>
    /// The same file with the cross-reference stream's <c>/Info</c> entry blanked out to spaces, so
    /// that every offset still holds - the shape many PDF 1.5 producers write.
    /// </summary>
    private static byte[] WithoutInfo(byte[] bytes)
    {
        var text = Encoding.Latin1.GetString(bytes);
        var match = Regex.Match(text, @"/Info \d+ 0 R(?=[^%]*?/Type\s*/XRef)", RegexOptions.RightToLeft);
        match.Success.Should().BeTrue("the fixture's cross-reference stream names its /Info");
        return Encoding.Latin1.GetBytes(text.Remove(match.Index, match.Length)
            .Insert(match.Index, new string(' ', match.Length)));
    }

    private static byte[] AppendChange(byte[] original, Action<PdfDocument> change)
    {
        using var source = new MemoryStream(original);
        var document = Reader.Open(source, PdfDocumentOpenMode.Append);

        change(document);

        using var output = new MemoryStream();
        document.SaveIncremental(output);
        return output.ToArray();
    }

    private static string Appended(byte[] updated, int originalLength) =>
        Encoding.Latin1.GetString(updated, originalLength, updated.Length - originalLength);

    /// <summary>How many cross-reference stream dictionaries the text holds, however it is spaced.</summary>
    private static int CrossReferenceStreamsIn(string text) =>
        Regex.Count(text, @"/Type\s*/XRef");
}
