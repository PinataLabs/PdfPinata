# Spec — narrowing what the parser needs of a document (T13)

`Parser` (`PdfPinata/Pdf.IO/Parser.cs`) read and wrote two members of `PdfDocument`:
`_irefTable`, a real dependency every indirect object needs to be numbered, and `_trailer`, which
turned out to be dead weight its one caller already overwrote. This narrowed the second away,
tidied three places that reached past the first into its backing dictionary, and added
`ParserProbe` — a reflection wrapper in `PdfPinata.Test` that stands a `Parser` up over a plain
`new PdfDocument()` and a `MemoryStream`, no `PdfReader.Open` required. `TolerantParsingTests` and
`CrossReferenceStreamDecodingTests` are what that probe is for.

## The write surface: `_document._trailer` is gone

`ReadTrailer` (`Parser.cs:1182`) used to decide which trailer to keep and write it straight onto
the document — `if (_document._trailer == null) _document._trailer = trailer;` — then return the
same value. `PdfReader.Open` (`PdfReader.cs:396`) already does `document._trailer =
parser.ReadTrailer(accuracy);` on the very next line, so the write inside `ReadTrailer` was a
second assignment of a value its only caller was about to assign itself. The "first trailer wins"
tie-break now lives in a local, `firstTrailer` (`Parser.cs:1225-1232`), and `ReadTrailer` returns
it (`:1242`) without touching the document at all. `Parser`'s write surface on `PdfDocument` is now
nothing, exactly as proposed.

## The read surface: `ObjectTable` reached through the table's own members

Three call sites reached past `PdfCrossReferenceTable` into its public `ObjectTable` dictionary for
`ContainsKey`/`TryGetValue`, where the table's own `Contains` and indexer already say the same
thing. `ReadIRefsFromCompressedObject` (`:1032-1033`) and `ReadCompressedObject`
(`:1088-1089`) now read `Contains` for the assert and the indexer for the value, matching the
`Debug.Assert(...Contains(objectID)); PdfReference iref = _document._irefTable[objectID];` shape the
rest of the file already used. `ReadXRefStream`'s own iref lookup (`:1411`, was `:1410`) changed the
same way — `PdfReference iref = xrefTable[objectID]; if (iref != null)` in place of
`xrefTable.ObjectTable.TryGetValue(objectID, out PdfReference iref)`. The plan's Solution section
named this as "the two compressed-object methods"; the Problem Statement and Implementation
Decisions sections both already listed the `ReadXRefStream` site as a third instance of the same
pattern, and the diff fixes all three. `Parser.cs` now touches exactly one member of `PdfDocument`
— `grep -n "_document\._trailer" PdfPinata/Pdf.IO/Parser.cs` returns nothing.

## `new PdfDocument()` is not empty

Something the plan didn't anticipate turned up while standing the probe's owner document up: a
plain `new PdfDocument()` already owns object 1 before a single byte is parsed — its document
information dictionary is built in the constructor and takes the first slot in `_irefTable`. A test
that writes its own object 1 and expects to read it back finds the constructor's entry instead,
since `PdfCrossReferenceTable.Add` is a no-op when the object ID is already present
(`PdfCrossReferenceTable.cs:67-75`). `ParserProbe.cs`'s remarks document this directly
(`:27-32`), CLAUDE.md's new paragraph on `Parser` carries the same warning, and
`CrossReferenceStreamDecodingTests.Placeholder` numbers its stand-in object 2 rather than 1 for
exactly this reason (`CrossReferenceStreamDecodingTests.cs:26-34`). This is the kind of thing the
plan's "costs nothing to add" framing for `new PdfDocument()` (Implementation Decisions) didn't
have visibility into until someone tried to write a table entry against it.

## `ParserProbe`

The shape matches what was proposed — a cached `Type` per internal type it reaches (`Parser`,
`Lexer`, `ShiftStack`, `PdfCrossReferenceTable`, `PdfCrossReferenceStream`), one
`BindingFlags Any = Public | NonPublic | Instance | Static`, and one small typed method per member a
test needs (`ParserProbe.cs:39-58`) — and it follows `FormTableProbe`'s pattern, which actually
lives in `PdfPinata.Test/Pdfs/DocumentPlumbingTests.cs` rather than `IO/`. Two details the plan
didn't spell out this precisely:

- **Every member goes through the same reflection path.** The plan's Testing Decisions distinguished
  "direct calls to `ReadDictionary` and `ReadArray`, both already accessible without `NonPublic`"
  from "`NonPublic`-reflected calls" to the tolerant-parsing methods — a distinction that doesn't
  survive into the code. `Method(name, parameters)` (`:198-202`) always passes `Any`, so `ReadDictionary`
  (`internal`, `Parser.cs:558`) is reached exactly the way `EndsAnObject` (`private`) is: `Parser`
  itself is `internal sealed`, so accessibility of an individual member does not change what a
  cross-assembly caller needs. `ReadArray` (`public`, `Parser.cs:535`) was never added to the probe
  at all — no test ended up needing it, so it stayed unexposed rather than added speculatively.
- **The probe carries more surface than the plan named**, because driving the parser directly means
  driving its lexer directly too: `Scan`, `Position`, and `MoveTo` (`:82-93`) move the lexer to a
  byte offset and pull the next token the way `PdfReader` does before handing an object to `Parser`.
  `EntriesOf` (`:149-161`) reflects into `PdfCrossReferenceStream`'s nested
  `CrossReferenceStreamEntry` struct to read back what a decoded stream produced. `ObjectIdsIn`,
  `ReferenceTo`, and `IsUnderConstruction` (`:176-190`) inspect and, for the last one, mutate the
  cross-reference table directly, needed for tests that check what a table now holds or that set up
  the "table is still being built" state `ParseObject`'s reference-resolution branch checks
  (`Parser.cs:669`). None of this widens what `PdfPinata` exposes; all of it stays inside the test
  project, reaching through the same reflection surface `Over` and `AddReference` already use.

## `TolerantParsingTests`: further than the plan's first slice

The plan's Testing Decisions section named four things as the first slice: `EndsAnObject` for its
four terminating symbols and its false cases, `BeginsAnIndirectObject` for a real next-object header
versus a stray trailing number, `TryReadStreamUpToEndOfStream` with and without a preceding CRLF,
and `WithoutTheEndOfLineBeforeTheKeyword` for LF-only, CRLF, and no trailing newline. All four
landed, and the shipped tests are wider than each one:

- **`EndsAnObject`** gets five terminating-symbol cases (`xref`, `trailer`, `startxref`, `%%EOF`, and
  end of input) rather than four, plus a positive recovery case (`"60 0 obj"` standing where
  `endobj` should have been) and two negative groups: a bare number, a number with a generation but
  no `obj`, a reference rather than a header, and — the one the plan didn't list — object number
  zero, which heads the free list and is correctly refused even though it parses as an integer
  (`TolerantParsingTests.cs:57-67`).
- **`BeginsAnIndirectObject`** ended up pinned differently than the plan's phrasing suggested.
  `EndsAnObject` calls it directly for `Integer`/`UInteger`/`Long` (`Parser.cs:329-332`), so the
  "real header versus stray number" contrast the plan described is what the `EndsAnObject` theories
  above already pin end to end. The one test named after `BeginsAnIndirectObject` itself instead
  pins a different, narrower contract — that it is pure lookahead and restores the lexer's position
  whatever it finds (`BeginsAnIndirectObject_leavesTheInputWhereItFoundIt`,
  `TolerantParsingTests.cs:82-97`), which matters because its caller relies on being able to keep
  scanning from where it started.
- **`TryReadStreamUpToEndOfStream`** gained two cases beyond CRLF-vs-none: one that asserts the
  `/Length` it back-fills onto the dictionary (`:119-126`), and one that pins the failure path — a
  stream with no `endstream` anywhere in it, which the fallback reports as not found rather than
  hanging or throwing (`:128-144`). Neither is in the plan.
- **`WithoutTheEndOfLineBeforeTheKeyword`** covers six shapes instead of three: CRLF, LF, and
  bare CR (the plan named only LF and CRLF), a double-newline case pinning that only one separator
  is ever dropped, a lone newline reducing to empty, and already-empty input. A seventh test pins an
  implementation detail the plan didn't ask for: when there is nothing to drop, the method hands
  back the exact same array instance rather than a copy (`:160-166`).
- **Two whole groups pin behaviour the plan's Testing Decisions never named as in scope for the
  first slice**, though both sit squarely on the `_irefTable` reads the narrowing exists to make
  legible. `ParseObject_readsAReferenceToAnObjectTheTableDoesNotHoldAsNull`,
  `_makesATemporaryReferenceWhileTheTableIsStillBeingBuilt`, and
  `_readsAReferenceToAKnownObjectAsTheTablesOwnEntry` (`:170-215`) pin the three-way branch at
  `Parser.cs:664-669` — an undefined reference reads as `PdfNull.Value` per the spec's own note that
  this is not an error, a reference read while `IsUnderConstruction` is true becomes a temporary
  `PdfReference` at position zero for `PdfTrailer.FixXRefs` to patch later, and a reference to an
  object already in the table returns that table's own instance rather than a new one.
  `ReadDictionary_keepsThePairsAroundAValueThatHasNoKey` (`:219-234`) pins the pdfTeX
  `PTEX.FullBanner` case — two strings with no key between them — which the plan's Implementation
  Decisions section only mentioned as something the existing `DictionaryEntryWithoutKeyTests` already
  covers through a whole file, not as something the probe would also cover directly.

## `CrossReferenceStreamDecodingTests`

This is the file the diff's stat line adds beside `ParserProbe.cs` and `TolerantParsingTests.cs`,
and it is exactly what the plan's Testing Decisions section asked for under "`ReadXRefStream` is
worth its own theory": a hand-packed stream body — three big-endian numbers per entry, each as wide
as a chosen `/W` says — read back through `ParserProbe.ReadXRefStream` with `/W`, `/Index`, and
`/Size` varied directly, rather than however `CrossReferenceStreamTests`' written documents happen
to shape them. It covers the width arithmetic across the reference's own example, a field wide
enough for a file over 64 KiB, the widest a single byte can say, and an all-zero free entry
(`:41-55`); which entries a stream holds with and without an `/Index` (`:59-80`); and what reaches
the cross-reference table — a type 1 entry adding at its given position, an object already in the
table keeping its first entry, a free or compressed entry adding nothing, and the stream itself
being entered as its own value so `PdfReader` doesn't parse it twice (`:84-140`). The plan didn't
say whether this belonged in its own file or folded into `TolerantParsingTests`; it landed in its
own file, and `docs/specs/cross-reference-streams.md` picked up a short paragraph pointing at it
(`cross-reference-streams.md:167-172`), wiring the new direct coverage into the spec that already
described the write side of the same format.

## Timeout discipline

Every test that drives the lexer over hand-written bytes carries `[Theory(Timeout = 5000)]` or
`[Fact(Timeout = 5000)]` with a `Task.Run` wrapper, per CLAUDE.md's standing rule that a lexer or
parser change can hang the test host rather than fail it. `WithoutTheEndOfLineBeforeTheKeyword`'s
tests are the one exception, and correctly so: that method trims a byte array and never touches the
lexer, so there is nothing in it that can hang.

## The framing this grew out of, checked against what shipped

`open-mode-enforcement.md`'s Out of Scope line reads "Making `Parser` testable without a
`PdfDocument`. Worth doing; unrelated." This document's own pre-implementation draft already flagged
that as a promise wider than what it intended to deliver, and what shipped confirms the smaller
reading. `ParserProbe.Over` (`ParserProbe.cs:65-67`) always takes a `PdfDocument owner` and hands it
straight to `Parser`'s constructor; there is no path anywhere in the diff that constructs a `Parser`
without one, and `PdfObject.cs` does not appear in the changed files at all — its constructor
(`PdfObject.cs:51`) still stores a concrete `PdfDocument`, and `SetObjectID` still reaches into
`_document._irefTable`. `Parser` is not testable *without* a `PdfDocument` after this lands, for the
same reason it wasn't before: numbering an indirect object is a document lookup in this object
model. What changed is which document a test has to build — `new PdfDocument()` plus a
`MemoryStream`, not a byte-exact file with a header, a real cross-reference table, a trailer with
`/Root`, and `startxref` found by scanning the last kilobyte.

## What stayed out, and held

Every item the plan's Out of Scope section named is untouched in the diff. No `IPdfObjectOwner`, no
`IPdfReferenceResolver` — `PdfCrossReferenceTable.cs` does not appear among the changed files, so its
four members (indexer, `Contains`, `Add`, `IsUnderConstruction`) are unchanged, only how `Parser`
reaches them is. No `InternalsVisibleTo` — `ParserProbe` reaches both `Parser` and
`PdfCrossReferenceTable` by reflection alone. The static `ReadObject(PdfDocument owner, PdfObjectID
objectID)` overload (`Parser.cs:1017-1024`) still constructs its own `Parser` and calls the same
instance method every other entry point does; nothing about it changed. `open-mode-enforcement.md`
itself is untouched by this diff, and this one still does not touch `PdfDocumentOpenMode`.
