using System;
using System.IO;
using System.Threading.Tasks;
using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using PdfPinata.Test.Helpers;
using TUnit.Core;
using Reader = PdfPinata.Pdf.IO.PdfReader;

namespace PdfPinata.Test.IO;

/// <summary>
///   Direct arrays and dictionaries, built in code, that contain themselves or share what they
///   contain (#433). A file cannot hold either, because the parser builds a tree, but the object
///   model can. The check that refuses an object nested too deep to read back walked every way down
///   through what an object holds: one containing itself was refused as too deep, or never finished
///   when it contained itself twice, and one sharing what it holds was walked once for every path
///   to each shared object. Every save here runs under a timeout on a thread of its own, because
///   what is being tested is that it ends.
/// </summary>
public class SelfContainingObjectTests
{
    [Test, Timeout(10_000)]
    public async Task ADictionaryThatContainsItselfIsRefusedWhenSaved()
    {
        var document = new PdfDocument();
        document.AddPage();
        var loop = new PdfDictionary(document);
        loop.Elements["/Self"] = loop;
        var holder = new PdfDictionary(document);
        holder.Elements["/Loop"] = loop;
        Hold(document, holder);

        var saving = () => Interruptibly.Run(() => Saved(document));

        await saving.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("The document cannot be saved. Object * contains an array or dictionary that is inside itself*");
    }

    [Test, Timeout(10_000)]
    public async Task ADictionaryThatContainsItselfIsRefusedWhenWrittenWithoutTheCheckBeforeSaving()
    {
        // PdfInternals.WriteObject writes one object, with nothing asked of it first. The writer's
        // own depth guard is what ends it.
        var document = new PdfDocument();
        var loop = new PdfDictionary(document);
        loop.Elements["/Self"] = loop;
        loop.Elements["/Again"] = loop;

        var writing = () => Interruptibly.Run(() =>
        {
            document.Internals.WriteObject(new MemoryStream(), loop);
            return true;
        });

        await writing.Should().ThrowAsync<InvalidOperationException>().WithMessage("*cannot be saved*");
    }

    [Test, Timeout(10_000)]
    public async Task AnIndirectObjectHoldingItselfInPlaceIsSavedAsReferringToItself()
    {
        // Put in its own entry while it was still direct, and made indirect afterwards: the writer
        // writes it there as a reference, so the file is an ordinary cycle of references.
        var document = new PdfDocument();
        document.AddPage();
        var loop = new PdfDictionary(document);
        loop.Elements["/Self"] = loop;
        Hold(document, loop);

        var pdf = await Interruptibly.Run(() => Saved(document));

        using var read = Reader.Open(new MemoryStream(pdf), PdfDocumentOpenMode.Import);
        var held = read.Internals.Catalog.Elements.GetDictionary("/Held");
        held.Elements.GetDictionary("/Self").Should().BeSameAs(held);
    }

    [Test, Timeout(10_000)]
    public async Task AnArrayThatContainsItselfTwiceThroughADictionaryIsRefusedWhenSaved()
    {
        var document = new PdfDocument();
        document.AddPage();
        var outer = new PdfDictionary(document);
        var inner = new PdfArray(document);
        inner.Elements.Add(outer);
        inner.Elements.Add(outer);
        outer.Elements["/Inner"] = inner;
        var holder = new PdfDictionary(document);
        holder.Elements["/Outer"] = outer;
        Hold(document, holder);

        var saving = () => Interruptibly.Run(() => Saved(document));

        await saving.Should().ThrowAsync<InvalidOperationException>().WithMessage("*inside itself*");
    }

    [Test, Timeout(10_000)]
    public async Task AnObjectSharingWhatItHoldsIsWalkedOnceForEachObjectNotOnceForEachPath()
    {
        // /A is too deep to read back. /B is a ladder of rungs that each name the next one twice,
        // so there are two to the sixty-fourth ways down it. A walk that followed every way would
        // never reach /A, and one that walks each shared rung once refuses the document at once.
        var document = new PdfDocument();
        document.AddPage();
        var holder = new PdfDictionary(document);
        holder.Elements["/A"] = Nested(document, ParserProbe.MaximumNesting + 1);
        holder.Elements["/B"] = Ladder(document, 64);
        Hold(document, holder);

        var saving = () => Interruptibly.Run(() => Saved(document));

        await saving.Should().ThrowAsync<InvalidOperationException>().WithMessage("*nested deeper*");
    }

    [Test, Timeout(10_000)]
    public async Task AnObjectSharedAtTwoDepthsIsJudgedAtTheDeeperOfThem()
    {
        // The shared object is met first one level down, where everything below it fits, and then
        // a hundred levels down, where it does not.
        var document = new PdfDocument();
        document.AddPage();
        var shared = Nested(document, 150);
        var holder = new PdfDictionary(document);
        holder.Elements["/A"] = shared;
        var deep = new PdfArray(document, shared);
        for (var level = 1; level < 100; level++)
            deep = new PdfArray(document, deep);
        holder.Elements["/B"] = deep;
        Hold(document, holder);

        var saving = () => Interruptibly.Run(() => Saved(document));

        await saving.Should().ThrowAsync<InvalidOperationException>().WithMessage("*nested deeper*");
    }

    private static void Hold(PdfDocument document, PdfDictionary holder)
    {
        document.Internals.AddObject(holder);
        document.Internals.Catalog.Elements["/Held"] = holder.Reference;
    }

    /// <summary>
    ///   A direct array with <paramref name="inside"/> more nested inside it, one in each.
    /// </summary>
    private static PdfArray Nested(PdfDocument document, int inside)
    {
        var array = new PdfArray(document);
        for (var level = 0; level < inside; level++)
            array = new PdfArray(document, array);
        return array;
    }

    /// <summary>
    ///   <paramref name="rungs"/> direct dictionaries, each naming the one below it as both
    ///   <c>/L</c> and <c>/R</c>.
    /// </summary>
    private static PdfDictionary Ladder(PdfDocument document, int rungs)
    {
        var rung = new PdfDictionary(document);
        for (var level = 1; level < rungs; level++)
        {
            var above = new PdfDictionary(document);
            above.Elements["/L"] = rung;
            above.Elements["/R"] = rung;
            rung = above;
        }
        return rung;
    }

    private static byte[] Saved(PdfDocument document)
    {
        var saved = new MemoryStream();
        document.Save(saved);
        return saved.ToArray();
    }
}
