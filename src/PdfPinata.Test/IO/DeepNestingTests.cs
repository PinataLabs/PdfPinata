using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Advanced;
using PdfPinata.Pdf.IO;
using PdfPinata.Pdf.IO.enums;
using PdfPinata.Test.Helpers;
using TUnit.Core;
using Reader = PdfPinata.Pdf.IO.PdfReader;

namespace PdfPinata.Test.IO;

/// <summary>
///   Arrays and dictionaries nested inside one another in a document's body (#414). The parser
///   reads each one by a call inside the call reading what holds it, and before it counted them
///   five thousand brackets in a file of ten kilobytes ended the process with a stack overflow,
///   which nothing can catch. The files past the limit here nest a hundred thousand deep, so a
///   reader that still recursed into them would take the test host down rather than go red.
/// </summary>
public class DeepNestingTests
{
    private const int FarTooDeep = 100_000;

    public enum Nesting
    {
        Arrays,
        Dictionaries
    }

    [Test, Timeout(10_000), MatrixDataSource]
    public async Task AnObjectNestedAsDeepAsMayBeReadIsRead(Nesting nesting, PdfDocumentOpenMode mode,
        PdfReadAccuracy accuracy)
    {
        var limit = ParserProbe.MaximumNesting;

        var depth = await Interruptibly.Run(() => DepthOfDeep(Plain(Nested(nesting, limit)), mode, accuracy));

        depth.Should().Be(limit + 1, "the outermost array or dictionary is the object itself");
    }

    [Test, Timeout(10_000), MatrixDataSource]
    public async Task AnObjectNestedOneDeeperIsRefused(Nesting nesting, PdfDocumentOpenMode mode,
        PdfReadAccuracy accuracy)
    {
        var pdf = Plain(Nested(nesting, ParserProbe.MaximumNesting + 1));

        var reading = () => Interruptibly.Run(() => DepthOfDeep(pdf, mode, accuracy));

        await reading.Should().ThrowAsync<PdfReaderException>().WithMessage("*nested deeper*");
    }

    [Test, Timeout(10_000), MatrixDataSource]
    public async Task AnObjectNestedFarTooDeepIsRefusedRatherThanOverflowingTheStack(Nesting nesting,
        PdfDocumentOpenMode mode, PdfReadAccuracy accuracy)
    {
        var pdf = Plain(Nested(nesting, FarTooDeep));

        var reading = () => Interruptibly.Run(() => DepthOfDeep(pdf, mode, accuracy));

        await reading.Should().ThrowAsync<PdfReaderException>().WithMessage("*nested deeper*");
    }

    [Test, Timeout(10_000), MatrixDataSource]
    public async Task AnObjectInAnObjectStreamIsHeldToTheSameLimit(Nesting nesting, PdfDocumentOpenMode mode)
    {
        var limit = ParserProbe.MaximumNesting;
        var deepest = Compressed(Nested(nesting, limit));
        var tooDeep = Compressed(Nested(nesting, FarTooDeep));

        var depth = await Interruptibly.Run(() => DepthOfDeep(deepest, mode, PdfReadAccuracy.Moderate));
        var reading = () => Interruptibly.Run(() => DepthOfDeep(tooDeep, mode, PdfReadAccuracy.Moderate));

        depth.Should().Be(limit + 1);
        await reading.Should().ThrowAsync<PdfReaderException>().WithMessage("*nested deeper*");
    }

    [Test, Timeout(10_000)]
    [Arguments(PdfDocumentOpenMode.Modify)]
    [Arguments(PdfDocumentOpenMode.Lazy)]
    public async Task ATrailerNestedFarTooDeepIsRefused(PdfDocumentOpenMode mode)
    {
        var pdf = RawPdf.Build(Objects("<</Type/Catalog/Pages 2 0 R>>"),
            "/Deep " + Nested(Nesting.Arrays, FarTooDeep));

        var reading = () => Interruptibly.Run(() => Reader.Open(new MemoryStream(pdf), mode).Dispose());

        await reading.Should().ThrowAsync<PdfReaderException>().WithMessage("*nested deeper*");
    }

    [Test, Timeout(10_000)]
    public async Task AnObjectRefusedForItsDepthLeavesALazyDocumentReadingTheObjectsAfterIt()
    {
        // A document opened lazily reads every object through one parser, so whatever the refused
        // read counted has to be given back, or every nested object read after it is refused too.
        var limit = ParserProbe.MaximumNesting;
        var pdf = RawPdf.Build(Objects("<</Type/Catalog/Pages 2 0 R/Deep 4 0 R/Fine 5 0 R>>",
            Nested(Nesting.Arrays, FarTooDeep), Nested(Nesting.Arrays, limit)));

        var depth = await Interruptibly.Run(() =>
        {
            using var document = Reader.Open(new MemoryStream(pdf), PdfDocumentOpenMode.Lazy);
            var catalog = document.Internals.Catalog;
            var refused = () => catalog.Elements.GetObject("/Deep");
            refused.Should().Throw<PdfReaderException>();
            return DepthOf(catalog.Elements.GetObject("/Fine"));
        });

        depth.Should().Be(limit + 1);
    }

    [Test, Timeout(10_000)]
    public async Task TheParserCountsWhatIsOpenedInsideTheObjectItIsReading()
    {
        var limit = ParserProbe.MaximumNesting;

        var items = await Interruptibly.Run(() => ParseArray(Nested(Nesting.Arrays, limit)));
        var parsing = () => Interruptibly.Run(() => ParseArray(Nested(Nesting.Arrays, limit + 1)));

        items.Should().ContainSingle().Which.Should().BeOfType<PdfArray>();
        await parsing.Should().ThrowAsync<PdfReaderException>().WithMessage("*nested deeper*");
    }

    [Test, Timeout(10_000)]
    public async Task AStringWithParenthesesNestedFarTooDeepIsReadAsOneString()
    {
        // Balanced parentheses belong to a literal string and the lexer counts them rather than
        // recursing, so they are no nesting at all.
        var text = new string('(', FarTooDeep) + new string(')', FarTooDeep);

        var symbol = await Interruptibly.Run(() => ParserProbe.Scan(ParserProbe.Over(new PdfDocument(), text)));

        symbol.Should().Be(Symbol.String);
    }

    [Test, Timeout(10_000)]
    [Arguments(PdfCrossReferenceFormat.Classic)]
    [Arguments(PdfCrossReferenceFormat.Stream)]
    public async Task AnObjectBuiltAsDeepAsMayBeReadIsSavedAndReadBack(PdfCrossReferenceFormat format)
    {
        var limit = ParserProbe.MaximumNesting;

        var depth = await Interruptibly.Run(() =>
        {
            var saved = SavedWithDeep(limit + 1, format);
            return DepthOfDeep(saved, PdfDocumentOpenMode.Modify, PdfReadAccuracy.Moderate);
        });

        depth.Should().Be(limit + 1);
    }

    [Test, Timeout(10_000)]
    [Arguments(1)]
    [Arguments(FarTooDeep)]
    public async Task AnObjectBuiltDeeperThanCanBeReadIsRefusedWhenSaved(int pastTheLimit)
    {
        var depth = ParserProbe.MaximumNesting + 1 + pastTheLimit;

        var saving = () => Interruptibly.Run(() => SavedWithDeep(depth, PdfCrossReferenceFormat.Classic));

        await saving.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("The document cannot be saved. Object * has arrays and dictionaries nested deeper*");
    }

    [Test, Timeout(10_000)]
    public async Task AnObjectBuiltDeeperThanCanBeReadLeavesTheFileItWasToBeSavedOverUntouched()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{nameof(DeepNestingTests)}-{Guid.NewGuid():N}.pdf");
        byte[] before = [.. "%PDF-1.7 what was there before"u8];
        await File.WriteAllBytesAsync(path, before);
        try
        {
            var document = WithDeep(ParserProbe.MaximumNesting + 2, PdfCrossReferenceFormat.Classic);

            var saving = () => Interruptibly.Run(() => document.Save(path));

            await saving.Should().ThrowAsync<InvalidOperationException>().WithMessage("*cannot be saved*");
            (await File.ReadAllBytesAsync(path)).Should().Equal(before);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test, Timeout(30_000)]
    [Arguments(PdfDocumentOpenMode.Modify)]
    [Arguments(PdfDocumentOpenMode.Import)]
    [Arguments(PdfDocumentOpenMode.ReadOnly)]
    [Arguments(PdfDocumentOpenMode.Append)]
    public async Task AnEncryptedObjectAsDeepAsMayBeReadOpensOnAThreadOf256Kilobytes(PdfDocumentOpenMode mode)
    {
        // Opening an encrypted document parses each object and then decrypts it, by two walks that
        // each recurse once per level. Both have to fit a small stack at the limit. Overflowing it
        // ends the test host rather than failing this test, and the run then exits non-zero.
        var depth = ParserProbe.MaximumNesting + 1;
        var pdf = SavedEncryptedWithDeep(depth);

        var read = await Interruptibly.Run(() =>
        {
            using var document = Reader.Open(new MemoryStream(pdf), "owner", mode);
            var catalog = document.Internals.Catalog;
            var dictionaries = catalog.Elements.GetObject("/Deep");
            return (Arrays: DepthOf(catalog.Elements.GetObject("/DeepArrays")),
                Dictionaries: DepthOf(dictionaries), Bottom: BottomOf(dictionaries));
        }, maxStackSize: 256 * 1024);

        read.Arrays.Should().Be(depth);
        read.Dictionaries.Should().Be(depth);
        read.Bottom.Should().BeOfType<PdfString>().Which.Value.Should().Be("secret");
    }

    /// <summary>
    ///   An array or dictionary with <paramref name="inside"/> more nested inside it, one in each.
    /// </summary>
    private static string Nested(Nesting nesting, int inside)
    {
        var count = inside + 1;
        return nesting == Nesting.Arrays
            ? new string('[', count) + new string(']', count)
            : string.Concat(Enumerable.Repeat("<</A", count)) + " 0" + string.Concat(Enumerable.Repeat(">>", count));
    }

    private static string[] Objects(string catalog, params string[] more) =>
    [
        catalog,
        "<</Type/Pages/Kids[3 0 R]/Count 1>>",
        "<</Type/Page/Parent 2 0 R/MediaBox[0 0 100 100]>>",
        ..more
    ];

    /// <summary>
    ///   A file whose catalog names <paramref name="deep"/>, written as an object of its own.
    /// </summary>
    private static byte[] Plain(string deep) =>
        RawPdf.Build(Objects("<</Type/Catalog/Pages 2 0 R/Deep 4 0 R>>", deep));

    /// <summary>
    ///   A file whose catalog names <paramref name="deep"/>, written as object 5, the one object in
    ///   object stream 4, which only the cross-reference stream, object 6, can find.
    /// </summary>
    private static byte[] Compressed(string deep)
    {
        var pdf = new StringBuilder("%PDF-1.7\n");
        var offsets = new int[7];

        void Add(int id, string body)
        {
            offsets[id] = pdf.Length;
            pdf.Append(id).Append(" 0 obj\n").Append(body).Append("\nendobj\n");
        }

        var objects = Objects("<</Type/Catalog/Pages 2 0 R/Deep 5 0 R>>");
        for (var id = 1; id <= objects.Length; id++)
            Add(id, objects[id - 1]);
        const string header = "5 0 ";
        Add(4, RawPdf.Stream("/Type/ObjStm/N 1/First " + header.Length, header + deep));
        offsets[6] = pdf.Length;

        // /W [1 4 2]: the type, then the offset or the object stream's number, then the generation
        // or the index in the object stream.
        var entries = new StringBuilder();

        void Entry(int type, int field2, int field3)
        {
            entries.Append((char)type);
            for (var shift = 24; shift >= 0; shift -= 8)
                entries.Append((char)((field2 >> shift) & 0xFF));
            entries.Append((char)(field3 >> 8)).Append((char)(field3 & 0xFF));
        }

        Entry(0, 0, 0xFFFF);
        for (var id = 1; id <= 4; id++)
            Entry(1, offsets[id], 0);
        Entry(2, 4, 0);
        Entry(1, offsets[6], 0);
        Add(6, RawPdf.Stream("/Type/XRef/Size 7/W[1 4 2]/Root 1 0 R", entries.ToString()));
        pdf.Append("startxref\n").Append(offsets[6]).Append("\n%%EOF\n");

        return Encoding.Latin1.GetBytes(pdf.ToString());
    }

    private static int DepthOfDeep(byte[] pdf, PdfDocumentOpenMode mode, PdfReadAccuracy accuracy)
    {
        using var document = Reader.Open(new MemoryStream(pdf), mode, accuracy);
        return DepthOf(document.Internals.Catalog.Elements.GetObject("/Deep"));
    }

    /// <summary>
    ///   How many arrays or dictionaries lie one inside the next, starting from
    ///   <paramref name="item"/>.
    /// </summary>
    private static int DepthOf(PdfItem item)
    {
        var depth = 0;
        while (true)
        {
            switch (item)
            {
                case PdfArray array:
                    depth++;
                    item = array.Elements.Count == 0 ? null : array.Elements[0];
                    break;
                case PdfDictionary dictionary:
                    depth++;
                    item = dictionary.Elements["/A"];
                    break;
                default:
                    return depth;
            }
        }
    }

    private static PdfItem[] ParseArray(string text)
    {
        var parser = ParserProbe.Over(new PdfDocument(), text);
        ParserProbe.Scan(parser).Should().Be(Symbol.BeginArray);
        return ParserProbe.ParseObject(parser, Symbol.EndArray);
    }

    /// <summary>
    ///   A document of one page whose catalog names an array, built in code, with
    ///   <paramref name="depth"/> arrays one inside the next counting itself, saved and handed back.
    /// </summary>
    private static byte[] SavedWithDeep(int depth, PdfCrossReferenceFormat format)
    {
        var saved = new MemoryStream();
        WithDeep(depth, format).Save(saved);
        return saved.ToArray();
    }

    /// <summary>
    ///   A document of one page whose catalog names an array, built in code, with
    ///   <paramref name="depth"/> arrays one inside the next counting itself.
    /// </summary>
    private static PdfDocument WithDeep(int depth, PdfCrossReferenceFormat format)
    {
        var document = new PdfDocument();
        document.AddPage();
        document.Options.CrossReferenceFormat = format;

        var deep = new PdfArray(document);
        for (var level = 1; level < depth; level++)
            deep = new PdfArray(document, deep);
        document.Internals.AddObject(deep);
        document.Internals.Catalog.Elements["/Deep"] = deep.Reference;
        return document;
    }

    /// <summary>
    ///   A document of one page, encrypted with the owner password "owner", whose catalog names
    ///   <paramref name="depth"/> dictionaries one inside the next as <c>/Deep</c> and as many
    ///   arrays as <c>/DeepArrays</c>, each with the string "secret" at the bottom.
    /// </summary>
    private static byte[] SavedEncryptedWithDeep(int depth)
    {
        var document = new PdfDocument();
        document.AddPage();

        PdfItem dictionaries = new PdfString("secret");
        for (var level = 0; level < depth; level++)
        {
            var dictionary = new PdfDictionary(document);
            dictionary.Elements["/A"] = dictionaries;
            dictionaries = dictionary;
        }
        var arrays = new PdfArray(document, new PdfString("secret"));
        for (var level = 1; level < depth; level++)
            arrays = new PdfArray(document, arrays);

        document.Internals.AddObject((PdfDictionary)dictionaries);
        document.Internals.AddObject(arrays);
        document.Internals.Catalog.Elements["/Deep"] = ((PdfDictionary)dictionaries).Reference;
        document.Internals.Catalog.Elements["/DeepArrays"] = arrays.Reference;
        document.SecuritySettings.OwnerPassword = "owner";

        var saved = new MemoryStream();
        document.Save(saved);
        return saved.ToArray();
    }

    /// <summary>
    ///   What lies at the bottom of the dictionaries one inside the next under <c>/A</c>.
    /// </summary>
    private static PdfItem BottomOf(PdfItem item)
    {
        while (item is PdfDictionary dictionary)
            item = dictionary.Elements["/A"];
        return item;
    }
}
