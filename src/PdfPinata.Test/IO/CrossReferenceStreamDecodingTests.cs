using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using Xunit;

namespace PdfPinata.Test.IO;

/// <summary>
///   A cross-reference stream says how wide each of the three fields of an entry is (/W), which
///   objects the entries are about (/Index, or /Size when it is left out), and packs the entries
///   into its stream as plain big-endian numbers. That arithmetic is exercised today only by
///   whichever shapes the documents in <c>CrossReferenceStreamTests</c> happen to have. Here it is
///   varied directly: a hand-built stream body against a hand-written /W and /Index, read through
///   <see cref="ParserProbe"/> with nothing around it but a <c>new PdfDocument()</c> to own what
///   comes out.
/// </summary>
/// <remarks>
///   Reading input the parser does not expect can hang rather than fail, so every test here runs
///   inside a <see cref="Task"/> xUnit can time out.
/// </remarks>
public class CrossReferenceStreamDecodingTests
{
    /// <summary>
    ///   An object for a type 1 entry to point at, written before the cross-reference stream so
    ///   that its offset is zero whatever the stream itself looks like. It is object 2 rather than
    ///   object 1 because a plain <c>new PdfDocument()</c> already owns object 1 - its document
    ///   information dictionary - and an entry for an object the table already holds is ignored.
    /// </summary>
    const string Placeholder = "2 0 obj\n<< /Kind /Placeholder >>\nendobj\n";

    static readonly PdfObjectID PlaceholderID = new PdfObjectID(2, 0);

    /// <summary>The object number the cross-reference stream itself is written under.</summary>
    static readonly PdfObjectID StreamID = new PdfObjectID(4, 0);

    // ----- The /W arithmetic --------------------------------------------------------------------------

    [Theory(Timeout = 5000)]
    [InlineData(1, 2, 1, 2u, 300u, 5u)]        // the widths of the reference's own example
    [InlineData(1, 4, 2, 2u, 70000u, 1000u)]   // a field wide enough for a file over 64 kiB
    [InlineData(1, 1, 1, 2u, 255u, 255u)]      // the widest each single byte can say
    [InlineData(2, 2, 2, 0u, 0u, 0u)]          // a free entry, every field zero
    public async Task AnEntryIsReadAsThreeBigEndianNumbersOfTheWidthsTheStreamGives(
        int type, int field2, int field3, uint expectedType, uint expectedField2, uint expectedField3)
    {
        var w = new[] { type, field2, field3 };
        var entries = await Task.Run(() => EntriesOf(
            Build(w, index: new[] { 10, 1 }, size: 11,
                data: Encode(w, (expectedType, expectedField2, expectedField3)))));

        entries.Should().Equal((expectedType, expectedField2, expectedField3));
    }

    // ----- Which entries the stream holds ---------------------------------------------------------------

    [Fact(Timeout = 5000)]
    public async Task WithoutAnIndexTheSizeSaysHowManyEntriesThereAre()
    {
        var w = new[] { 1, 2, 1 };
        var entries = await Task.Run(() => EntriesOf(
            Build(w, index: null, size: 3,
                data: Encode(w, (2u, 11u, 0u), (2u, 22u, 1u), (2u, 33u, 2u)))));

        entries.Select(entry => entry.Field2).Should().Equal(11u, 22u, 33u);
    }

    [Fact(Timeout = 5000)]
    public async Task TheEntriesOfEverySubsectionAreReadInTheOrderTheIndexGivesThem()
    {
        var w = new[] { 1, 2, 1 };
        var entries = await Task.Run(() => EntriesOf(
            // One entry for object 1, then two for objects 5 and 6.
            Build(w, index: new[] { 1, 1, 5, 2 }, size: 7,
                data: Encode(w, (2u, 11u, 0u), (2u, 55u, 0u), (2u, 66u, 1u)))));

        entries.Select(entry => entry.Field2).Should().Equal(11u, 55u, 66u);
    }

    // ----- What reaches the cross-reference table ---------------------------------------------------------

    [Fact(Timeout = 5000)]
    public async Task AnEntryForAnObjectInTheFileAddsItAtThePositionTheEntryGives()
    {
        var w = new[] { 1, 2, 1 };
        var table = await Task.Run(() => TableAfterReading(
            Build(w, index: new[] { 2, 1 }, size: 3, data: Encode(w, (1u, 0u, 0u)))));

        ParserProbe.ReferenceTo(table, PlaceholderID).Position.Should()
            .Be(0, "the entry says the object is written at the start of the file");
    }

    [Fact(Timeout = 5000)]
    public async Task AnObjectAlreadyInTheTableKeepsTheEntryItAlreadyHas()
    {
        // An object may be described by more than one section, and the first one read wins.
        var w = new[] { 1, 2, 1 };
        var table = await Task.Run(() =>
        {
            var owner = new PdfDocument();
            ParserProbe.AddReference(owner, PlaceholderID, 4711);
            return TableAfterReading(
                Build(w, index: new[] { 2, 1 }, size: 3, data: Encode(w, (1u, 0u, 0u))), owner);
        });

        ParserProbe.ReferenceTo(table, PlaceholderID).Position.Should().Be(4711);
    }

    [Fact(Timeout = 5000)]
    public async Task AFreeOrCompressedEntryPutsNothingInTheTable()
    {
        // Only the cross-reference stream's own object is entered; a type 0 entry describes a free
        // object and a type 2 entry describes one inside an object stream, which is reached
        // through that stream rather than by position.
        var w = new[] { 1, 2, 1 };
        var table = await Task.Run(() => TableAfterReading(
            Build(w, index: new[] { 2, 2 }, size: 4, data: Encode(w, (0u, 0u, 0u), (2u, 9u, 0u)))));

        ParserProbe.ObjectIdsIn(table).Should()
            .NotContain(PlaceholderID, "neither entry names an object written in the file")
            .And.Contain(StreamID);
    }

    [Fact(Timeout = 5000)]
    public async Task TheStreamItselfIsEnteredInTheTableAsItsOwnValue()
    {
        // PdfReader resolves compressed objects by finding the cross-reference stream in the
        // entry's value, and would parse it a second time if it were left null.
        var w = new[] { 1, 2, 1 };
        var (stream, table) = await Task.Run(() =>
        {
            var owner = new PdfDocument();
            var built = Build(w, index: new[] { 4, 1 }, size: 5, data: Encode(w, (1u, 0u, 0u)));
            return Read(built, owner);
        });

        ParserProbe.ReferenceTo(table, StreamID).Value.Should().BeSameAs(stream);
    }

    // ----- Building one, and reading it back -----------------------------------------------------------------

    /// <summary>
    ///   The bytes of a file holding one placeholder object and one cross-reference stream, and
    ///   where in them that stream begins.
    /// </summary>
    sealed class BuiltFile
    {
        internal byte[] Bytes;
        internal long Position;
    }

    static BuiltFile Build(int[] w, int[] index, int size, byte[] data)
    {
        var indexEntry = index == null ? "" : $" /Index [{string.Join(" ", index)}]";
        var header = $"4 0 obj\n<< /Type /XRef /Size {size} /W [{string.Join(" ", w)}]{indexEntry}"
                     + $" /Length {data.Length} >>\nstream\n";

        var bytes = new List<byte>();
        bytes.AddRange(ParserProbe.Bytes(Placeholder));
        bytes.AddRange(ParserProbe.Bytes(header));
        bytes.AddRange(data);
        bytes.AddRange(ParserProbe.Bytes("\nendstream\nendobj\n"));

        return new BuiltFile { Bytes = bytes.ToArray(), Position = Placeholder.Length };
    }

    /// <summary>
    ///   The entries of a cross-reference stream, packed the way a stream body packs them: three
    ///   big-endian numbers per entry, each as wide as /W says.
    /// </summary>
    static byte[] Encode(int[] w, params (uint Type, uint Field2, uint Field3)[] entries)
    {
        var bytes = new List<byte>();
        foreach (var entry in entries)
        {
            Append(bytes, entry.Type, w[0]);
            Append(bytes, entry.Field2, w[1]);
            Append(bytes, entry.Field3, w[2]);
        }

        return bytes.ToArray();
    }

    static void Append(List<byte> bytes, uint value, int width)
    {
        for (var shift = width - 1; shift >= 0; shift--)
            bytes.Add((byte)(value >> (8 * shift)));
    }

    static (object Stream, object Table) Read(BuiltFile file, PdfDocument owner = null)
    {
        owner ??= new PdfDocument();
        var parser = ParserProbe.Over(owner, file.Bytes);
        ParserProbe.MoveTo(parser, file.Position);
        ParserProbe.Scan(parser).Should().Be(Symbol.Integer, "the object number is what is read first");

        var table = ParserProbe.IrefTableOf(owner);
        return (ParserProbe.ReadXRefStream(parser, table), table);
    }

    static (uint Type, uint Field2, uint Field3)[] EntriesOf(BuiltFile file) =>
        ParserProbe.EntriesOf(Read(file).Stream);

    static object TableAfterReading(BuiltFile file, PdfDocument owner = null) => Read(file, owner).Table;
}
