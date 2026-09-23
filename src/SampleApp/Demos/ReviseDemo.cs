using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using PdfPinata.Drawing;
using PdfPinata.Drawing.Layout;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using SampleApp.Infrastructure;

namespace SampleApp.Demos;

/// <summary>
///   Changing a document by adding to it rather than by rewriting it: three revisions of one file,
///   each appended to the last, with every earlier one still in the bytes.
/// </summary>
/// <remarks>
///   The file this writes is genuinely three revisions deep. Page one is revision one, page three
///   was added by revision two, and page four by the revision written on the way out - which is why
///   this demo, like Signing, writes its own bytes rather than letting the base class save it.
/// </remarks>
internal sealed class ReviseDemo : PdfDemo
{
    public ReviseDemo() : base() { }

    public override string Name => "Revise";

    public override string Summary => "Incremental update: three revisions in one file, none of them overwritten.";

    public override IReadOnlyList<string> Shows => new[]
    {
        "PdfDocument.SaveIncremental, against Save, and what each one does to the bytes",
        "PdfDocumentOpenMode.Append - the only mode that keeps the bytes and the object numbers",
        "The /Prev chain, which is how a reader walks backwards through the revisions",
        "That an object number means the same thing in every revision, so a later one shadows",
        "PdfObject.MarkAsChanged, and why a direct array inside a page needs it",
        "The trap: appending into the file it was read from, which silently loses the revision"
    };

    public override int PageCount => 4;

    protected override PdfDocument Build(DemoContext context)
    {
        #region example
        var heading = new XFont(BundledFontResolver.SansFamily, 16, XFontStyle.Bold);
        var label = new XFont(BundledFontResolver.SansFamily, 9.5, XFontStyle.Bold);
        var body = new XFont(BundledFontResolver.SansFamily, 9);
        var mono = new XFont(BundledFontResolver.MonoFamily, 7.5);
        var stamp = new XFont(BundledFontResolver.SansFamily, 11, XFontStyle.Bold);

        // ----- revision one: an ordinary document, saved the ordinary way --------------------------

        using var original = new PdfDocument();
        original.Info.Title = "Revise";
        original.Info.Author = "PdfPinata sample app";

        var first = original.AddPage();
        using (var gfx = XGraphics.FromPdfPage(first))
        {
            var prose = new XTextFormatter(gfx);

            gfx.DrawString("Revision one", heading, XBrushes.Black, 50, 60);

            prose.DrawString(
                "This page and the next were written by an ordinary Save. Nothing about them knows "
                + "anything is going to be added, and nothing about them has to: an incremental "
                + "update leaves the bytes it was given exactly as they were and writes after them.",
                body, XBrushes.Black, new XRect(50, 80, 495, 44));

            prose.DrawString(
                "A reader opening the finished file starts at the last startxref, follows /Prev "
                + "backwards through the cross-reference sections, and takes the first definition it "
                + "finds of each object - so a later revision shadows an earlier one without "
                + "erasing it. Everything on this page is still the first definition of itself; "
                + "nothing later contradicts it.",
                body, XBrushes.Black, new XRect(50, 136, 495, 62));

            gfx.DrawString("What an incremental update is for", label, XBrushes.Black, 50, 220);

            prose.DrawString(
                "Three things, and file size is not one of them. A signed document can only be "
                + "changed this way, because a signature covers a byte range of the file and "
                + "rewriting the file invalidates it. An audited document keeps every earlier state "
                + "recoverable. And a very large document can be annotated without being written out "
                + "again from end to end.",
                body, XBrushes.Black, new XRect(50, 235, 495, 62));

            gfx.DrawString("What it costs", label, XBrushes.Black, 50, 320);

            prose.DrawString(
                "The file only ever grows, and it grows by more than the change: every object "
                + "touched is written again in full, and so is a whole cross-reference section. A "
                + "document revised a hundred times carries a hundred copies of whatever kept "
                + "changing. Rewriting it with Save is how that is reclaimed, and is exactly what "
                + "must not be done to a signed one.",
                body, XBrushes.Black, new XRect(50, 335, 495, 62));
        }

        var second = original.AddPage();
        using (var gfx = XGraphics.FromPdfPage(second))
        {
            var prose = new XTextFormatter(gfx);

            gfx.DrawString("Append is a mode, not a flag", heading, XBrushes.Black, 50, 60);

            prose.DrawString(
                "PdfDocumentOpenMode.Append is the only mode a document can be appended to in, and "
                + "the reason is object numbers. Modify reads everything into memory and renumbers "
                + "it, so an appended definition of object 12 would shadow whatever happened to be "
                + "numbered 12 this time round - which is not what it was numbered before. Import "
                + "does not keep the bytes at all. Append keeps both, and SaveIncremental refuses "
                + "without them.",
                body, XBrushes.Black, new XRect(50, 80, 495, 62));

            gfx.DrawString("Only what changed is written", label, XBrushes.Black, 50, 165);

            prose.DrawString(
                "Which means something has to know what changed. An object modified through the "
                + "object model marks itself; a direct one - an array held inside a page dictionary "
                + "rather than indirectly in its own right - cannot, because changing it changes the "
                + "page and not the array. That is what PdfObject.MarkAsChanged is for, and "
                + "forgetting it on the page is how an appended annotation ends up in a file no "
                + "reader shows it in.",
                body, XBrushes.Black, new XRect(50, 180, 495, 76));

            gfx.DrawString("The trap worth knowing before you hit it", label, XBrushes.Firebrick, 50, 275);

            prose.DrawString(
                "SaveIncremental writes the whole file - original bytes and all - so the destination "
                + "has to be empty. Handing it the file it was read from is the tempting mistake and "
                + "the damaging one: the original is rewritten over itself, the revision is "
                + "appended, and because nothing truncates, whatever of the old file ran past the "
                + "new end survives. That includes its startxref, which a reader scanning backwards "
                + "finds first - and the appended revision is then ignored in silence, signature and "
                + "all. So it throws on a non-empty stream rather than letting that happen.",
                body, XBrushes.Black, new XRect(50, 290, 495, 90));
        }

        // docs:begin append
        var revisionOne = new MemoryStream();
        original.Save(revisionOne, false);
        var sizeOfOne = revisionOne.Length;

        // ----- revision two: opened for appending, a page added ------------------------------------

        revisionOne.Position = 0;
        using var appended = PdfReader.Open(revisionOne, PdfDocumentOpenMode.Append);

        // Changing something that was already there, as well as adding. The title is in the
        // information dictionary, which is an object like any other: the appended revision carries a
        // second definition of it and the reader takes that one.
        appended.Info.Subject = "Amended by revision two";
        // docs:end append

        var third = appended.AddPage();
        using (var gfx = XGraphics.FromPdfPage(third))
        {
            var prose = new XTextFormatter(gfx);

            gfx.DrawString("Revision two", heading, XBrushes.Black, 50, 60);

            gfx.DrawString("Added by appending, not by rewriting", stamp, XBrushes.Firebrick, 50, 84);

            prose.DrawString(
                "This page did not exist when the bytes above it were written. The document was "
                + "opened again with PdfDocumentOpenMode.Append, this page was added, the subject "
                + "line in the information dictionary was changed, and SaveIncremental wrote the "
                + "original bytes through untouched with a new cross-reference section after them.",
                body, XBrushes.Black, new XRect(50, 104, 495, 62));

            (string What, string Value)[] facts =
            {
                ("Revision one", Format(sizeOfOne) + " bytes"),
                // Counted after the page above was added, so this is both revisions' objects and
                // not revision one's. Named for what it counts rather than for the row above it.
                ("Objects reachable now", appended.Internals.GetAllObjects().Length
                    .ToString(CultureInfo.InvariantCulture) + " across both revisions"),
                ("Pages before this one", "2"),
                ("Changed as well as added", "/Info /Subject, which revision one had left empty")
            };

            double y = 190;
            foreach (var fact in facts)
            {
                gfx.DrawString(fact.What, label, XBrushes.Black, 50, y);
                gfx.DrawString(fact.Value, mono, XBrushes.Black, 200, y);
                y += 16;
            }

            gfx.DrawString("More changed than the page", label, XBrushes.Black, 50, y + 20);

            prose.DrawString(
                "Adding a page is not only a new page object. The /Pages node that lists them has to "
                + "say so, and its /Count has to agree, so the appended revision carries a second "
                + "definition of the page tree node as well. The larger cost is the fonts: a "
                + "document opened for appending does not adopt the font objects already in the "
                + "file, so a page drawn in a face the first revision also used embeds that face "
                + "again. Compare the two sizes on the next page - the revision is far larger than "
                + "anything visible on this one, and almost all of it is a second copy of the type.",
                body, XBrushes.Black, new XRect(50, y + 34, 495, 76));
        }

        // docs:begin save-incremental
        var revisionTwo = new MemoryStream();
        appended.SaveIncremental(revisionTwo);
        var afterTwo = revisionTwo.ToArray();

        // ----- revision three: opened again, and this is the one written to the file ----------------

        revisionTwo.Position = 0;
        var document = PdfReader.Open(revisionTwo, PdfDocumentOpenMode.Append);
        // docs:end save-incremental

        var fourth = document.AddPage();
        using (var gfx = XGraphics.FromPdfPage(fourth))
        {
            var prose = new XTextFormatter(gfx);

            gfx.DrawString("Revision three", heading, XBrushes.Black, 50, 60);

            prose.DrawString(
                "And this one was added to the file the last one produced. Below is what a byte "
                + "scanner finds in those bytes - counted, not described, by looking through the "
                + "file revision two wrote for the markers a reader uses to walk it.",
                body, XBrushes.Black, new XRect(50, 80, 495, 44));

            (string What, string Value)[] found =
            {
                ("Revision one", Format(sizeOfOne) + " bytes"),
                ("After revision two", Format(afterTwo.Length) + " bytes"),
                ("Appended by revision two", Format(afterTwo.Length - sizeOfOne) + " bytes"),
                ("startxref, at a line start", CountAtLineStart(afterTwo, "startxref")
                    .ToString(CultureInfo.InvariantCulture) + " - one per revision"),
                ("%%EOF, at a line start", CountAtLineStart(afterTwo, "%%EOF")
                    .ToString(CultureInfo.InvariantCulture) + " - one per revision"),
                ("/Prev, anywhere in the bytes", Count(afterTwo, "/Prev")
                    .ToString(CultureInfo.InvariantCulture) + " - one fewer, and rightly so"),
                ("This file", "one revision deeper again")
            };

            double y = 145;
            foreach (var fact in found)
            {
                gfx.DrawString(fact.What, label, XBrushes.Black, 50, y);
                gfx.DrawString(fact.Value, mono, XBrushes.Black, 200, y);
                y += 16;
            }

            gfx.DrawString("Why /Prev is one short, and why two rows say where", label,
                XBrushes.Firebrick, 50, y + 22);

            prose.DrawString(
                "Two revisions have two startxrefs and one /Prev, not two. Every revision writes a "
                + "cross-reference section; every section but the first points at the one before it, "
                + "and the first has nothing behind it to point at. So the chain has one fewer link "
                + "than it has sections, and a file with a /Prev per revision would be one with a "
                + "link into nothing.",
                body, XBrushes.Black, new XRect(50, y + 36, 495, 62));

            prose.DrawString(
                "The other caveat is how these were counted. A byte scan cannot tell a marker in a "
                + "trailer from the same characters inside a compressed stream or an embedded font, "
                + "and this file carries both - so the first two rows count the marker only where it "
                + "begins a line, which is where a trailer puts it and where a stream almost never "
                + "does. The third is a plain count and is worth exactly that much. Structure is what "
                + "a reader parses for; this page is looking at the file rather than reading it.",
                body, XBrushes.Black, new XRect(50, y + 104, 495, 76));

            gfx.DrawString("Reading it back", label, XBrushes.Black, 50, y + 192);

            prose.DrawString(
                "Nothing special is needed. A reader that understands PDF at all understands an "
                + "incrementally updated file, because following /Prev is how cross-reference "
                + "sections have always been chained - a linearised file has more than one section "
                + "too. Open the finished PDF in anything and it is a four page document; the three "
                + "revisions are visible only to something looking at the bytes.",
                body, XBrushes.Black, new XRect(50, y + 206, 495, 62));

            gfx.DrawString("What an earlier revision still holds", label, XBrushes.Black, 50, y + 282);

            prose.DrawString(
                "Everything it ever said. Redacting a document by drawing a black rectangle over a "
                + "name and appending the change leaves the name in the file, in plain text, one "
                + "revision back - and tools that recover it are not sophisticated. Redaction means "
                + "rewriting, which means Save, which means any signature goes with it. That "
                + "tension is real and has no trick to it: the two features want opposite things.",
                body, XBrushes.Black, new XRect(50, y + 296, 495, 76));

            gfx.DrawString("Where this meets signing", label, XBrushes.Black, 50, y + 386);

            prose.DrawString(
                "PdfSigner does exactly what this page does - it appends a revision - and that is "
                + "the whole reason signing a document that was already signed does not destroy the "
                + "first signature. See the Signing demo, whose output is one revision written the "
                + "same way with a hole patched into it.",
                body, XBrushes.Black, new XRect(50, y + 400, 495, 48));
        }
        #endregion

        return document;
    }

    /// <summary>
    ///   Appends the third revision to the bytes the second produced, rather than saving the
    ///   document afresh.
    /// </summary>
    /// <remarks>
    ///   <c>Save</c> would write a perfectly good four page PDF with one revision in it, and the
    ///   demo would then be a description of something its own output did not do. The destination
    ///   is a file opened with <see cref="FileMode.Create"/>, so it is empty - which is the
    ///   condition <see cref="PdfDocument.SaveIncremental"/> insists on, and the reason it does is
    ///   on page two.
    /// </remarks>
    protected override void Save(PdfDocument document, string path)
    {
        using var output = new FileStream(path, FileMode.Create, FileAccess.Write);
        document.SaveIncremental(output);
    }

    private static string Format(long value) => value.ToString("N0", CultureInfo.InvariantCulture);

    /// <summary>
    ///   How many times a marker appears anywhere in the raw bytes, stream data included.
    /// </summary>
    /// <remarks>
    ///   Latin-1 rather than UTF-8, because a PDF is a byte string throughout - the lexer reads one
    ///   character per byte - and decoding it as UTF-8 would turn a compressed stream into
    ///   replacement characters and lose count.
    ///   <para>
    ///   This counts occurrences and nothing more. Five bytes reading <c>/Prev</c> inside a flate
    ///   stream or an embedded font look exactly like five bytes reading <c>/Prev</c> in a trailer,
    ///   and a scanner cannot tell them apart. The page saying so is the honest way to use this.
    ///   </para>
    /// </remarks>
    private static int Count(byte[] bytes, string marker)
    {
        var text = Encoding.Latin1.GetString(bytes);

        var found = 0;
        var at = text.IndexOf(marker, StringComparison.Ordinal);
        while (at >= 0)
        {
            found++;
            at = text.IndexOf(marker, at + marker.Length, StringComparison.Ordinal);
        }

        return found;
    }

    /// <summary>
    ///   How many times a marker begins a line, which is where a trailer puts <c>startxref</c> and
    ///   <c>%%EOF</c> and where compressed data almost never does.
    /// </summary>
    private static int CountAtLineStart(byte[] bytes, string marker)
    {
        var text = Encoding.Latin1.GetString(bytes);

        var found = 0;
        var at = text.IndexOf(marker, StringComparison.Ordinal);
        while (at >= 0)
        {
            if (at == 0 || text[at - 1] == '\n' || text[at - 1] == '\r')
                found++;

            at = text.IndexOf(marker, at + marker.Length, StringComparison.Ordinal);
        }

        return found;
    }
}
