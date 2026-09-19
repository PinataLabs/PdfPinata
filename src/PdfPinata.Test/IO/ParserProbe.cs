using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Advanced;
using PdfPinata.Pdf.IO;

namespace PdfPinata.Test.IO;

/// <summary>
///   Reaches the document parser and the cross-reference table it writes into, both of which are
///   internal and neither of which this repository makes visible to the test assembly. The shape
///   is the one <c>FormTableProbe</c> in <c>DocumentPlumbingTests</c> already established: a
///   cached type, one set of binding flags, and one small typed method per member a test needs.
/// </summary>
/// <remarks>
///   What this buys is a test that builds bytes rather than a document. The parser still needs a
///   <see cref="PdfDocument"/> to own the objects it makes - every <c>PdfObject</c> is numbered by
///   looking itself up in a document's cross-reference table - but it needs nothing else that
///   <c>PdfReader.Open</c> builds: no <c>%PDF</c> header, no real cross-reference table, no
///   trailer, no <c>startxref</c> found by scanning the end of a file. A plain
///   <c>new PdfDocument()</c> and a <see cref="MemoryStream"/> of hand-written bytes are the whole
///   of what a test needs to stand one up.
///   <para>
///   One thing that document is not is empty: <c>new PdfDocument()</c> builds its information
///   dictionary, which is object 1 in its cross-reference table before a parser has read a byte.
///   A test writing its own objects should number them from two, or the entry it writes for
///   object 1 is the one already there and its own is ignored.
///   </para>
///   <para>
///   Every method here fails loudly rather than quietly when the member it names is gone: a
///   rename or a changed signature is reported as a missing member by the probe, not as a
///   <c>NullReferenceException</c> in the middle of a test.
///   </para>
/// </remarks>
static class ParserProbe
{
    const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic
        | BindingFlags.Instance | BindingFlags.Static;

    // Named through PdfDocument rather than through PdfReader: this namespace has a test class of
    // its own by that name, which is what typeof would find here.
    static readonly Assembly Library = typeof(PdfDocument).Assembly;

    static readonly Type ParserType = Library.GetType("PdfPinata.Pdf.IO.Parser", throwOnError: true);

    static readonly Type LexerType = Library.GetType("PdfPinata.Pdf.IO.Lexer", throwOnError: true);

    static readonly Type ShiftStackType = Library.GetType("PdfPinata.Pdf.IO.ShiftStack", throwOnError: true);

    static readonly Type TableType =
        Library.GetType("PdfPinata.Pdf.Advanced.PdfCrossReferenceTable", throwOnError: true);

    static readonly Type XRefStreamType =
        Library.GetType("PdfPinata.Pdf.Advanced.PdfCrossReferenceStream", throwOnError: true);

    // ----- Standing a parser up ------------------------------------------------------------------

    /// <summary>
    ///   A parser reading the given bytes, owned by the given document.
    /// </summary>
    internal static object Over(PdfDocument owner, byte[] pdf) =>
        Activator.CreateInstance(ParserType, Any, null,
            new object[] { owner, new MemoryStream(pdf) }, null);

    /// <summary>
    ///   The same, for input that is text. PDF syntax is bytes, one character to one byte, which
    ///   is how the lexer reads it back.
    /// </summary>
    internal static object Over(PdfDocument owner, string pdf) => Over(owner, Bytes(pdf));

    /// <summary>
    ///   The bytes of a string, one character to one byte, the way the document lexer reads them.
    /// </summary>
    internal static byte[] Bytes(string text) => text.Select(ch => (byte)ch).ToArray();

    // ----- Driving the lexer behind it ------------------------------------------------------------

    internal static Symbol Scan(object parser) =>
        (Symbol)Invoke(Method("ScanNextToken", Type.EmptyTypes), parser);

    // ReSharper disable PossibleNullReferenceException
    internal static long Position(object parser) =>
        (long)LexerMember("Position").GetValue(Lexer(parser));
    // ReSharper restore PossibleNullReferenceException

    /// <summary>
    ///   Moves the input to the given byte offset, the way the reader does before handing an
    ///   object over to the parser.
    /// </summary>
    internal static void MoveTo(object parser, long position) =>
        LexerMember("Position").SetValue(Lexer(parser), position);

    // ----- The tolerant parsing an object's end is read with ---------------------------------------

    internal static bool EndsAnObject(object parser, Symbol symbol) =>
        (bool)Invoke(Method("EndsAnObject", new[] { typeof(Symbol) }), parser, symbol);

    internal static bool BeginsAnIndirectObject(object parser) =>
        (bool)Invoke(Method("BeginsAnIndirectObject", Type.EmptyTypes), parser);

    // ----- Reading composite objects ---------------------------------------------------------------

    internal static PdfDictionary ReadDictionary(object parser, PdfDictionary dict,
        bool includeReferences = false) =>
        (PdfDictionary)Invoke(Method("ReadDictionary", new[] { typeof(PdfDictionary), typeof(bool) }),
            parser, dict, includeReferences);

    /// <summary>
    ///   Parses until the stop symbol and hands back what was left on the shift-reduce stack, which
    ///   is where the parser puts what it read and what ReadArray and ReadDictionary go on to
    ///   reduce into an array's items or a dictionary's entries.
    /// </summary>
    internal static PdfItem[] ParseObject(object parser, Symbol stop)
    {
        var stack = Field(ParserType, "_stack").GetValue(parser);
        var pointer = Member(ShiftStackType, "SP");
        // ReSharper disable once PossibleNullReferenceException
        var before = (int)pointer.GetValue(stack);

        Invoke(Method("ParseObject", new[] { typeof(Symbol) }), parser, stop);

        // ReSharper disable once PossibleNullReferenceException
        var count = (int)pointer.GetValue(stack) - before;
        return (PdfItem[])Invoke(
            Method(ShiftStackType, "ToArray", new[] { typeof(int), typeof(int) }), stack, before, count);
    }

    // ----- The /Length fallback --------------------------------------------------------------------

    internal static bool TryReadStreamUpToEndOfStream(object parser, PdfDictionary dict, long startOfStream) =>
        (bool)Invoke(Method("TryReadStreamUpToEndOfStream", new[] { typeof(PdfDictionary), typeof(long) }),
            parser, dict, startOfStream);

    internal static byte[] WithoutTheEndOfLineBeforeTheKeyword(byte[] bytes) =>
        (byte[])Invoke(Method("WithoutTheEndOfLineBeforeTheKeyword", new[] { typeof(byte[]) }), null, bytes);

    // ----- Cross-reference streams ------------------------------------------------------------------

    /// <summary>
    ///   Reads a cross-reference stream into the given table. The parser must have just scanned the
    ///   object number of the stream, which is where ReadXRefTableAndTrailer hands over to it.
    /// </summary>
    internal static object ReadXRefStream(object parser, object xrefTable) =>
        Invoke(Method("ReadXRefStream", new[] { TableType }), parser, xrefTable);

    /// <summary>
    ///   The entries a cross-reference stream decoded, as the three fields of each.
    /// </summary>
    internal static (uint Type, uint Field2, uint Field3)[] EntriesOf(object xrefStream)
    {
        var entryType = XRefStreamType.GetNestedType("CrossReferenceStreamEntry", Any)
            ?? throw new MissingMemberException(XRefStreamType.FullName, "CrossReferenceStreamEntry");
        var type = Field(entryType, "Type");
        var field2 = Field(entryType, "Field2");
        var field3 = Field(entryType, "Field3");

        // ReSharper disable once AssignNullToNotNullAttribute
        // ReSharper disable PossibleNullReferenceException
        return ((IEnumerable)Field(XRefStreamType, "Entries").GetValue(xrefStream))
            .Cast<object>()
            .Select(entry => ((uint)type.GetValue(entry), (uint)field2.GetValue(entry), (uint)field3.GetValue(entry)))
            .ToArray();
        // ReSharper restore PossibleNullReferenceException
    }

    // ----- The one member of PdfDocument the parser reads --------------------------------------------

    internal static object IrefTableOf(PdfDocument owner) =>
        Field(typeof(PdfDocument), "_irefTable").GetValue(owner);

    /// <summary>
    ///   Seeds the document's cross-reference table with an entry, for the tests that need an
    ///   object to be known before the parser is asked to read it.
    /// </summary>
    internal static void AddReference(PdfDocument owner, PdfObjectID id, long position) =>
        Invoke(Method(TableType, "Add", new[] { typeof(PdfReference) }),
            IrefTableOf(owner), new PdfReference(id, position));

    internal static PdfObjectID[] ObjectIdsIn(object xrefTable) =>
        (PdfObjectID[])Member(TableType, "AllObjectIDs").GetValue(xrefTable);

    internal static PdfReference ReferenceTo(object xrefTable, PdfObjectID id) =>
        (PdfReference)Invoke(
            TableType.GetProperty("Item", Any)?.GetGetMethod(nonPublic: true)
            ?? throw new MissingMemberException(TableType.FullName, "this[PdfObjectID]"),
            xrefTable, id);

    /// <summary>
    ///   Says whether the table is still being built, which is what the reader sets while it reads
    ///   the trailers and what decides how a reference to an object not yet in it is read.
    /// </summary>
    internal static void IsUnderConstruction(object xrefTable, bool value) =>
        Member(TableType, "IsUnderConstruction").SetValue(xrefTable, value);

    // ----- Reflection, kept in one place -------------------------------------------------------------

    static object Lexer(object parser) => Field(ParserType, "_lexer").GetValue(parser);

    static PropertyInfo LexerMember(string name) => Member(LexerType, name);

    static MethodInfo Method(string name, Type[] parameters) => Method(ParserType, name, parameters);

    static MethodInfo Method(Type type, string name, Type[] parameters) =>
        type.GetMethod(name, Any, null, parameters, null)
        ?? throw new MissingMethodException(type.FullName, name);

    static FieldInfo Field(Type type, string name) =>
        type.GetField(name, Any) ?? throw new MissingFieldException(type.FullName, name);

    static PropertyInfo Member(Type type, string name) =>
        type.GetProperty(name, Any) ?? throw new MissingMemberException(type.FullName, name);

    /// <summary>
    ///   Invokes and lets what the member itself threw out, rather than the reflection wrapper
    ///   around it, so that a test asserting on an exception sees the one the parser threw.
    /// </summary>
    static object Invoke(MethodBase member, object instance, params object[] arguments)
    {
        try
        {
            return member.Invoke(instance, arguments);
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }
}
