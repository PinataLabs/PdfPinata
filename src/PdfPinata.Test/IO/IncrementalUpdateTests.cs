using System;
using System.IO;
using System.Linq;
using System.Text;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using Xunit;

// This namespace has a PdfReader of its own, so the one that opens documents needs saying in full.
using Reader = PdfPinata.Pdf.IO.PdfReader;

namespace PdfPinata.Test.IO;

/// <summary>
///   Every <c>Save</c> overload wrote the whole file from the object graph. There was no way to
///   append, which for a signed document is not an inconvenience but a prohibition: a signature
///   covers a byte range, so rewriting the file invalidates it, and rewriting was all there was.
///
///   <see cref="PdfDocument.SaveIncremental"/> copies the original bytes through and appends only
///   what changed. It needs <see cref="PdfDocumentOpenMode.Append"/>, because
///   <see cref="PdfDocumentOpenMode.Modify"/> renumbers every object on the way in and an appended
///   definition shadows an object <em>by number</em>.
/// </summary>
public class IncrementalUpdateTests
{
    [Fact]
    public void TheOriginalBytesAreLeftExactlyWhereTheyWere()
    {
        // The whole point. Anything the reader did not fully understand survives untouched, and a
        // signature over the original range still covers what it signed.
        var original = OriginalDocument();

        var updated = AppendChange(original, document => document.Info.Subject = "Changed");

        updated.Length.Should().BeGreaterThan(original.Length);
        updated.Take(original.Length).Should().Equal(original,
            "an incremental update appends and never rewrites");
    }

    [Fact]
    public void TheChangeIsReadBackFromTheAppendedRevision()
    {
        var updated = AppendChange(OriginalDocument(), document => document.Info.Subject = "Changed");

        Reopen(updated).Info.Subject.Should().Be("Changed");
    }

    [Fact]
    public void WhatWasNotChangedIsStillThere()
    {
        var updated = AppendChange(OriginalDocument(), document => document.Info.Subject = "Changed");

        var reread = Reopen(updated);
        reread.Info.Title.Should().Be("Original title");
        reread.PageCount.Should().Be(2);
    }

    [Fact]
    public void TheAppendedSectionNamesTheNewRevisionsPredecessor()
    {
        // Without /Prev a reader sees the handful of objects in this revision and nothing else in
        // the document.
        var updated = AppendChange(OriginalDocument(), document => document.Info.Subject = "Changed");

        Appended(updated, OriginalDocument().Length).Should().Contain("/Prev");
    }

    [Fact]
    public void OnlyWhatChangedIsWrittenAgain()
    {
        var original = OriginalDocument();
        var updated = AppendChange(original, document => document.Info.Subject = "Changed");

        // Asserted by what the appended bytes contain rather than by how many there are. A byte
        // count is a proxy, and a poor one in a debug build, where the verbose layout puts a
        // hundred-character rule between every object. What matters is that changing a document
        // property does not drag the expensive objects along with it.
        var appended = Appended(updated, original.Length);
        appended.Should().NotContain("/BaseFont", "the font was not touched and must not be rewritten");
        appended.Should().NotContain("/FontDescriptor");
        appended.Should().Contain("/Subject", "and the thing that did change is there");
    }

    [Fact]
    public void AnUnchangedContentStreamIsNotWrittenAgain()
    {
        var original = OriginalDocument();
        var updated = AppendChange(original, document => document.Info.Subject = "Changed");

        // The rectangle's operators are in the original content stream. If they turn up in the
        // appended revision, the page's content was rewritten for no reason.
        Appended(updated, original.Length).Should().NotContain("200 100 re",
            "the drawing did not change, so its content stream stays where it was");
    }

    [Fact]
    public void ADocumentIdentifiesItselfAcrossRevisionsAndIdentifiesEachRevisionApart()
    {
        var original = OriginalDocument();
        var updated = AppendChange(original, document => document.Info.Subject = "Changed");

        var before = Reopen(original).Internals;
        var after = Reopen(updated).Internals;

        after.FirstDocumentID.Should().Be(before.FirstDocumentID,
            "/ID[0] names the document for its whole life");
        after.SecondDocumentID.Should().NotBe(before.SecondDocumentID,
            "/ID[1] names this revision of it");
    }

    [Fact]
    public void TwoSuccessiveUpdatesBothResolve()
    {
        // One update is easy to get right by accident. Two is not: the second has to point its
        // /Prev at the first rather than at the original.
        var once = AppendChange(OriginalDocument(), document => document.Info.Subject = "First");
        var twice = AppendChange(once, document => document.Info.Keywords = "second");

        var reread = Reopen(twice);
        reread.Info.Subject.Should().Be("First", "the first revision is still reachable");
        reread.Info.Keywords.Should().Be("second");
        reread.Info.Title.Should().Be("Original title", "and so is the original");
    }

    [Fact]
    public void APageAddedByAnUpdateIsThere()
    {
        var updated = AppendChange(OriginalDocument(), document => document.AddPage());

        Reopen(updated).PageCount.Should().Be(3);
    }

    [Fact]
    public void SomethingDrawnByAnUpdateIsThere()
    {
        var updated = AppendChange(OriginalDocument(), document =>
        {
            var gfx = XGraphics.FromPdfPage(document.Pages[0]);
            gfx.DrawRectangle(XBrushes.Red, 10, 10, 50, 50);
            gfx.Dispose();
        });

        Reopen(updated).PageCount.Should().Be(2);
        Appended(updated, OriginalDocument().Length).Should().Contain("obj",
            "the page's content stream was rewritten into the appended revision");
    }

    [Fact]
    public void ChangingNothingStillProducesAReadableDocument()
    {
        // The producer string is always rewritten, so "nothing changed" is never literally nothing.
        var updated = AppendChange(OriginalDocument(), _ => { });

        Reopen(updated).Info.Title.Should().Be("Original title");
    }

    [Fact]
    public void ADocumentOpenedToModifyCannotBeAppendedTo()
    {
        // Modify renumbers every object on the way in, so its numbers no longer mean what the file
        // says they mean. The message has to say that, because the mode names suggest otherwise.
        using var source = new MemoryStream(OriginalDocument());
        var document = Reader.Open(source, PdfDocumentOpenMode.Modify);

        var appending = () => document.SaveIncremental(new MemoryStream());

        appending.Should().Throw<InvalidOperationException>().WithMessage("*Append*");
    }

    [Fact]
    public void ADocumentCreatedFromScratchCannotBeAppendedTo()
    {
        var document = new PdfDocument();
        document.AddPage();

        var appending = () => document.SaveIncremental(new MemoryStream());

        appending.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AppendingKeepsTheObjectNumbersTheFileWasWrittenWith()
    {
        // The assertion behind the whole design. An appended definition shadows the object its
        // number already named, so the numbers may not move between reading and writing — and the
        // second page's number in the appended revision has to be the number the original file
        // gave it.
        var original = OriginalDocument();

        using var source = new MemoryStream(original);
        var appended = Reader.Open(source, PdfDocumentOpenMode.Append);
        var asRead = appended.Pages[1].Reference.ObjectNumber;

        appended.Info.Subject = "Changed";
        using var output = new MemoryStream();
        appended.SaveIncremental(output);

        using var rereadStream = new MemoryStream(output.ToArray());
        var reread = Reader.Open(rereadStream, PdfDocumentOpenMode.Append);

        reread.Pages[1].Reference.ObjectNumber.Should().Be(asRead,
            "appending must not move an object to a different number");
    }

    [Fact]
    public void ChangingAnEntryMarksItsObjectAsChanged()
    {
        var document = new PdfDocument();
        var page = document.AddPage();

        page.Elements.SetInteger("/Rotate", 90);

        page.IsDirty.Should().BeTrue("every path that mutates a dictionary has to record it");
    }

    [Fact]
    public void RemovingAnEntryMarksItsObjectAsChanged()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        page.Elements.SetInteger("/Rotate", 90);

        page.Elements.Remove("/Rotate");

        page.IsDirty.Should().BeTrue();
    }

    [Fact]
    public void ChangingAStreamMarksItsObjectAsChanged()
    {
        // The stream's Value setter writes /Length back into the dictionary, so it is caught by the
        // same choke point — but it is the path that would silently lose a redrawn page, so it gets
        // an assertion of its own.
        var document = new PdfDocument();
        var dictionary = new PdfDictionary(document);
        dictionary.CreateStream(new byte[] { 1, 2, 3 });
        document.Internals.AddObject(dictionary);

        dictionary.Stream.Value = new byte[] { 4, 5, 6, 7 };

        dictionary.IsDirty.Should().BeTrue();
    }

    [Fact]
    public void ACallerThatReachesPastTheApiCanSayItChangedSomething()
    {
        var document = new PdfDocument();
        var page = document.AddPage();

        page.MarkAsChanged();

        page.IsDirty.Should().BeTrue();
    }

    [Fact]
    public void AChangeMadeThroughATypedSetterIsAppendedToo()
    {
        // Six setters wrote to the backing dictionary directly and never said the object had
        // changed, so an incremental save left them out and the reader went on resolving the old
        // definition — the change lost, in a file that opens and looks right. Page boxes go through
        // SetRectangle, which is how ordinary code reaches this.
        var original = OriginalDocument();

        var updated = AppendChange(original, document =>
            document.Pages[0].Elements.SetRectangle("/CropBox", new PdfRectangle(new XRect(10, 10, 190, 290))));

        Reopen(updated).Pages[0].Elements.GetRectangle("/CropBox").X1.Should().Be(10);
    }

    [Fact]
    public void ADateSetThroughSetDateTimeIsAppendedToo()
    {
        var original = OriginalDocument();

        var updated = AppendChange(original, document =>
            document.Info.Elements.SetDateTime("/ModDate", new DateTime(2026, 5, 6, 7, 8, 9)));

        Reopen(updated).Info.Elements.ContainsKey("/ModDate").Should().BeTrue();
    }

    [Fact]
    public void ChangingAnArrayHeldInsideAPageRewritesThePage()
    {
        // The array is direct — it lives inside the page dictionary rather than being an object in
        // its own right — so saying only the array changed tells an incremental save nothing at all.
        // What changed, as far as the file is concerned, is the page.
        var original = OriginalDocument();

        var updated = AppendChange(original, document =>
        {
            var annotations = new PdfArray(document);
            document.Pages[0].Elements["/Annots"] = annotations;
            // PdfInteger names a test class of this assembly as well, hence the full name.
            annotations.Elements.Add(new PdfPinata.Pdf.PdfInteger(0));
        });

        Reopen(updated).Pages[0].Elements.GetArray("/Annots").Elements.Count.Should().Be(1);
    }

    [Fact]
    public void AnIncrementalSaveRefusesAStreamThatIsNotEmpty()
    {
        // Handing this the file it was read from is the tempting mistake: the original is rewritten
        // over itself, the revision appended, and the tail of the old file survives past the new end
        // — including its startxref, which a reader scanning backwards finds first. The appended
        // revision would then be ignored in silence.
        using var source = new MemoryStream(OriginalDocument());
        var document = Reader.Open(source, PdfDocumentOpenMode.Append);

        var saving = () => document.SaveIncremental(source);

        saving.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void TheOpenModesKeepTheNumbersTheyAlwaysHad()
    {
        // The compiler inlines an enum constant at the call site, so an assembly compiled against an
        // earlier version goes on passing the old number. Adding a member anywhere but the end, or
        // closing the gap left by a removed one, silently redirects every such caller.
        ((int)PdfDocumentOpenMode.Modify).Should().Be(0);
        ((int)PdfDocumentOpenMode.Import).Should().Be(1);
        ((int)PdfDocumentOpenMode.ReadOnly).Should().Be(2);
        ((int)PdfDocumentOpenMode.Append).Should().Be(4);
    }

    [Fact]
    public void ThreeStaysVacantWhereInformationOnlyWas()
    {
        // InformationOnly is gone, and the number it had must not be reused: an assembly compiled
        // against it still passes 3, and giving 3 to another mode would silently hand that caller a
        // mode it never asked for. Append staying at 4 is the same rule seen from the other side.
        Enum.IsDefined(typeof(PdfDocumentOpenMode), 3).Should().BeFalse();
    }

    [Fact]
    public void ADocumentOpenedWithTheVacantNumberIsReadOnly()
    {
        // What an assembly compiled against InformationOnly does now. The value is not one this
        // enum defines, and IsReadOnly answers every mode that is not Modify or Append the same
        // way - so such a caller gets exactly the behaviour InformationOnly always had.
        using var source = new MemoryStream(OriginalDocument());

        var document = Reader.Open(source, (PdfDocumentOpenMode)3);

        document.IsReadOnly.Should().BeTrue();
        document.PageCount.Should().Be(2);
        var adding = () => document.AddPage();
        adding.Should().Throw<InvalidOperationException>();
    }

    /// <summary>A document to append to, with more than one page so a change is visibly partial.</summary>
    private static byte[] OriginalDocument()
    {
        var document = new PdfDocument();
        document.Info.Title = "Original title";
        for (var index = 0; index < 2; index++)
        {
            var gfx = XGraphics.FromPdfPage(document.AddPage());
            gfx.DrawRectangle(XBrushes.LightGray, 20, 20, 200, 100);
            gfx.DrawString("Page " + index, new XFont("Arial", 12), XBrushes.Black, 30, 160);
            gfx.Dispose();
        }

        using var output = new MemoryStream();
        document.Save(output, false);
        return output.ToArray();
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

    private static PdfDocument Reopen(byte[] bytes)
    {
        var saved = new MemoryStream(bytes);
        return Reader.Open(saved, PdfDocumentOpenMode.Modify);
    }

    /// <summary>What was appended, and nothing that was there before.</summary>
    private static string Appended(byte[] updated, int originalLength) =>
        Encoding.Latin1.GetString(updated, originalLength, updated.Length - originalLength);
}
