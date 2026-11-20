# Spec — reading a large PDF lazily and extracting pages from it, issue #366

[PinataLabs/PdfPinata#366](https://github.com/PinataLabs/PdfPinata/issues/366) asks for a way to take
ten pages out of a 6.7 GB image-heavy PDF without holding 6.7 GB. Today that cannot be done in any
open mode, because every one of them reads the whole file before `PdfReader.Open` returns.

Items 1 and 3 are built. The rest is a proposal.

| item | what | status |
|---|---|---|
| 1 | 64-bit offsets through the lexer, the cross-reference reader and the writer | done (#367) |
| 2 | The import closure stops at page boundaries | done (#369) |
| 3 | `PdfDocumentOpenMode.Lazy`: objects resolved on first access | done |
| 4 | Lazy streams, copied raw and in chunks by `PdfWriter` | not started |
| 5 | `PdfSplitter`, a SampleApp demo and a docs page | not started |

Each item is one PR, in that order, and each can be verified on its own.

---

## Where memory goes today

The reader does not copy the file up front. `Lexer` reads from the caller's `Stream` with seeks, and
only the first 1 KB is buffered for the header. Even so, by the time `Open` returns, everything is in
memory:

- **Every object is parsed eagerly, in every mode.** `PdfReader.Open` calls
  `ReadObjectStreamReferences`, `ReadCompressedObjects` and `ReadIndirectObjects` unconditionally,
  before any branch on the mode. The last of these loops over `_irefTable.AllReferences` and parses
  each one.
- **Every stream's data becomes a `byte[]` when its object is parsed.** `Parser.ReadStream` calls
  `Lexer.ReadStream(length)`, which allocates the array, and `PdfStream` holds it in `_value`.
  There is no deferred form.
- **The source is closed when `Open` returns.** The path overload disposes its `FileStream` in a
  `finally` block. `PdfDocument` says why it can: "All objects were read into memory when the
  document was opened, so the stream is no longer needed."
- **Importing a page holds each stream twice.** `ImportClosure` and `DeepCopyClosure` call
  `Clone()` on every object in the page's closure, and `PdfStream.Clone` copies the byte array.
- **`Append` reads the whole file into `_originalBytes`** and refuses a file over 2 GB.
- **`PdfWriter.WriteStream` writes `Stream.Value`**, so the writer can only write from memory.

The file that prompted this is 6 696 509 437 bytes. Its `startxref` is at 6 696 503 442, which is
beyond `int.MaxValue`, and the last object before it is a stream, almost certainly a
cross-reference stream.

## What is wanted

**In scope:** extracting a range or a set of pages into a new file, splitting a file into parts of N
pages, and reading a large file lazily without producing anything, for example its page count or its
metadata.

**Out of scope:**

- Merging several large sources. It needs two lazy sources open at once and de-duplication across
  them. It is a follow-up.
- Modifying a large file in place. That is a different problem.
- Encrypted sources. They are refused with a clear exception. Decrypting means decoding every
  string and stream, which defeats the raw copy in item 4.

**The memory target:** peak memory grows with the output, meaning the extracted pages and
everything they reference, plus an index of the cross-reference table that grows with the object
count. It never grows with the size of the file. For the first ten pages of the 6.7 GB file, the
target is a peak managed heap under about 64 MB.

---

## Item 1 — 64-bit offsets

The file needs offsets beyond 2³¹ and beyond 2³² everywhere: lexer positions, cross-reference
entries (classic tables and cross-reference streams with a wide `/W` field), `/Length`, seeking,
and the offsets the writer emits when an output exceeds 2 GB. A split into two halves of this file
produces such an output.

Item 1 is an audit and its fixes. It benefits the eager modes too, which can read a file of this
size today when the machine has the memory.

**Verification.** `docs/specs/large-file-trailer-scan.md` already presents a huge file through a
`SplicedStream` without storing it. Extend that to build a PDF whose objects sit beyond 2³², with
virtual padding in between, and read it.

## Item 2 — the import closure stops at page boundaries

`PdfCrossReferenceTable.TransitiveClosure` follows every reference. A link annotation's `/Dest`, a
`/GoTo` action's `/D`, or an annotation's `/P` reaches another page. That page's `/Parent` reaches
the page tree, and through it every page in the document.

#461 already repairs the **output**. `DetachImportedDestinations` rewrites each destination into a
deferred one, `FixImportedAnnotation` points `/P` at the new page, and `Compact` drops the
orphaned clones on save (`docs/specs/import-size-and-annotations.md`). But the clone of the whole
page tree is still made and then thrown away. In eager mode that costs time. In `Lazy` mode it would
resolve every page dictionary and resource dictionary in the file, which item 3 exists to avoid.

**The rule,** in both `Import` and `Lazy`: the closure treats a dictionary of `/Type /Page` or
`/Type /Pages` as a boundary and does not follow it. The page being imported is the one exception,
because it is the root. A reference to a page that is being imported in the same operation is
remapped to its copy. Any other page reference becomes null. The link then goes nowhere, but the
annotation stays.

**Traps:**

- The deferred-destination mechanism reads the destination from the **external** annotation, not
  from the clone, and resolves it at save time. It must still see the original page reference, so
  the boundary applies to what is cloned, not to what is read.
- A page dictionary with no `/Type` exists in the wild. The boundary has to recognise a page by
  where it was reached from as well: anything that `PdfPages` knows as a page of the source, and
  any `/Parent` of one.

**Verification:**

- Importing a page whose link points at another page clones no other page dictionary. This is
  asserted by counting what the closure visits, not by the size of the output, which #461 already
  fixed.
- `InsertRangeTests` and the split tests pass unchanged.

## Item 3 — `PdfDocumentOpenMode.Lazy`

### The mode

It takes the value **`Lazy = 5`**, after `Append`. It never takes the vacant 3:
`docs/specs/open-mode-enforcement.md` explains that an old assembly passing 3 must keep meaning
read-only.

It is opt-in. Making `Import` lazy for everyone would change what every existing caller has to do:
keep the source open, and dispose the document. That can be decided later, once `Lazy` has proved
itself.

**What a `Lazy` document allows:**

- reading: the page count, page dictionaries, metadata, outlines, and `.Value` on any stream;
- serving as the source of `PdfPages.Add`, `Insert` and `InsertRange`.

`ImportExternalPage`'s check, `== Import`, becomes "is `Import` or `Lazy`".

**What it refuses:** everything `EnsureCanModify` guards. That covers the page-tree methods,
`Save`, and the `Version`, `PageLayout` and `PageMode` setters. Nothing has to change for this.
`IsReadOnly` is written as "neither `Modify` nor `Append`", so it already answers true for a new
member, and the refusal already names the mode it was given. `OpenModeEnforcementTests` gets a fifth
row to pin it.

### What `Open` reads today, and what `Lazy` leaves out

`PdfReader.Open(Stream, …)` does seven things in order. `Lazy` keeps the first two and replaces the
rest.

| step | today | in `Lazy` |
|---|---|---|
| `ReadHeaderVersion` | the first 1 KB | the same |
| `ReadTrailers` | the backward scan for `startxref`, then every section along `/Prev` and `/XRefStm` | the same, without the probe described below |
| `ReadEncryptDictionary`, `TryUnlock` | parses the encryption dictionary | refused before it is read |
| `ReadObjectStreamReferences` | reads and decodes **every** object stream to list what is in it | not called |
| `ReadCompressedObjects` | parses every compressed object | not called; an index is built from the entries instead |
| `ReadIndirectObjects` | parses every other object, stream bytes included, and records `MaxObjectNumber` | not called; `MaxObjectNumber` is taken from the table's keys |
| `_trailer.Finish` | swaps the trailer's stand-in `/Root` and `/Info` references for the table's | the same, changed as described under "What has to change" |

Nothing after that runs for `Lazy`: `RenewRevisionId`, `Compact`, `Renumber` and
`PrepareForAppending` belong to `Modify` and `Append` alone.

**Reading a cross-reference section already touches every object, and `Lazy` stops that.** A
classic table entry is checked by `CheckXRefTableEntry`, which seeks to the object and reads its
`n g obj` header. A type 1 entry of a cross-reference stream is filed by `ReadObjectNumber`, which
does the same to learn the generation. For the 6.7 GB file that is 1 887 seeks spread over the whole
file before `Open` returns. In `Lazy` the parser is told not to probe
(`Parser.DeferEntryChecks`): an entry is filed under the number and generation the section gives
it, and **the check moves to the read**. When an object is resolved, the `n g obj` header at its
offset is compared with the entry that led there (`Parser.CheckObjectHeader`):

| what is at the offset | `Strict` | `Moderate` |
|---|---|---|
| the object the entry names | read | read |
| no `n g obj` at all | `PdfReaderException`, naming the object and the offset | the same |
| another object, one number below the entry's | `PdfReaderException`, saying to open with `Import` | the same |
| any other object | `PdfReaderException`, naming the object, the offset and what is there | read under the entry's number |

The last row is what the eager reader does with such an entry, at `Open`. The third row is the one
difference. The eager reader repairs a table whose numbers are off by one by filing the entry under
the number the header gives. That changes the table, and by the time a lazy document meets the
header, other objects have been read through the table as it stands. The repair is neither cheap
nor local there, so the object is refused under either accuracy and the message says to use an
eager mode. Reading it under a number it does not have would be silently wrong.

A type 1 entry of a cross-reference stream is held to the same table. The eager reader files such an
entry under whatever the header says, and never reports a disagreement.

So an entry pointing at nothing is found when the object is asked for, not when the file is opened.
`PdfReadAccuracy.Strict` cannot report it at `Open`.

So `Open` in `Lazy` reads the first kilobyte, the tail it scans for `startxref`, and the
cross-reference sections. It reads nothing else.

**Encrypted sources** are refused as soon as the trailer is known, with a `PdfReaderException` that
names `Lazy` and says to use `Import`. The test is "the trailer has an `/Encrypt` that is not
null", not "it is a reference", so a direct encryption dictionary is refused too.

**A stream that cannot seek** is refused before the `Lexer` is built, with an `ArgumentException`.
Today the `Lexer` constructor fails on `Stream.Length` with whatever the stream throws.

### The data shape

Nothing new is needed on `PdfReference` to say that it is unresolved. It already has two fields that
say so between them, and the eager reader already relies on the same reading (`IsReadAlready`):

| state | `_value` | `Position` | compressed index |
|---|---|---|---|
| unread, in the file | null | ≥ 0 | |
| unread, in an object stream | null | −1 | has the number |
| read | the object | unchanged | |
| nothing there | null | −1 | does not have the number |

**The compressed index** is the one new structure: a dictionary from object number to
`(object stream number, index)`, held by the lazy source. It is built once, from
`Parser.CrossReferenceStreams`, which is newest first. An entry is taken when the table's reference
for that number is still "position −1" and the index does not have the number yet. That is the rule
`ReadCompressedObjects` and `IsReadAlready` apply today: an object a newer revision wrote out in
full is never replaced by an older revision's compressed copy.

**The lazy source** is a new internal class, `Pdf.IO/LazyObjectSource`. The document holds one in a
field that is null in every other mode. It owns:

- the source `Stream`, and whether to close it;
- one `Parser`, bound to the document's `Lexer`, for the document's lifetime;
- the compressed index;
- the four decoded object streams;
- the set of objects being resolved, and the lock.

**The hook is the `Value` getter.** It becomes "the value, or else what the document's lazy source
resolves". Every reader of a reference in the library goes through that property, so nothing else
has to learn about the mode. A reference reaches its source through `PdfReference.Document`. The
parser does not set that on a table entry until the object is read (`SetObjectID` does it), so
`Open` sets it on every entry as its last step. That step is what arms the mode: while the
cross-reference sections are being read the getter still answers the bare field, which
`RegisterXRefStream` and `PdfTrailer.Finish` depend on.

In every other mode the document has no lazy source, the getter answers the field, and the cost is
one null test on a path that already had one.

### Resolving

`Resolve(reference)`, under the lock:

1. If the reference has a value by now, answer it. Another thread read it while this one waited.
2. If the reference is not the table's own entry for its number, or its position is −1 and the
   compressed index does not have the number, answer null. This is a reference with nothing behind
   it, which is the null object. It stays the null object after `Dispose`.
3. If the document is disposed, throw `ObjectDisposedException`, naming the object.
4. If the object is already being resolved, answer null. See "Re-entrancy".
5. If `Position` is 0 or more, call `Parser.ReadObject(null, id, false, false)`. It seeks, checks the
   header, parses, and `SetObjectID` stores the result in the table's own reference. The cache is
   the table. No second structure is kept.
6. Otherwise read it from its object stream.

A compressed object that turns out not to be there is removed from the index, so it is looked for
once. A malformed object throws
`PdfReaderException` from `.Value`, as it would have thrown from `Open`. It is not remembered, and
asking again throws again.

**Type transformation needs no change.** A typed wrapper such as `PdfCatalog` or `PdfPage` is built
from a dictionary that has been resolved, and its constructor points the reference at the wrapper.
The reference is resolved by then, so the getter never asks the source again.

### Compressed objects

Today an object stream is turned into a `PdfObjectStream` by type transformation, which leaves it in
the table for good with its decoded bytes. `ReadCompressedObject` then builds a second `Parser` over
a `MemoryStream` of those bytes. That second parser has its own lexer, so reading a compressed
object never moves the source.

`Lazy` keeps that and changes who holds the result:

- The lazy source reads the object stream, builds the `PdfObjectStream`, and then **puts the
  table's entry back to what it held** (`PdfReference.SetObject`), so the table does not pin it.
  That is nothing, unless a caller has asked for the object stream itself; the caller's object is
  then left as it was.
- The `PdfObjectStream` lives in a list of four, most recently used first. A fifth evicts the last.
  An evicted stream is read and decoded again if one of its objects is asked for later.
- The object is read at the index the cross-reference entry gives. `PdfObjectStream` files what it
  reads under the number its own header gives at that index. So the header is asked first
  (`ObjectNumberAt`): when the two disagree, or the stream has no such index, nothing is read, the
  number asked for stays unresolved and it is removed from the index. Reading it would put the
  object in another object's place.

Objects the header of an object stream lists and no cross-reference entry names are not given
references. `ReadObjectStreamReferences` adds them today. Nothing can refer to them.

`/Extends` is ignored, as it is today.

### Re-entrancy

Resolving one object can start resolving another on the same parser. There is one place this
happens: `Parser.ResolveLengthObject`, which reads `reference.Value` for an indirect `/Length`
while the lexer stands at the start of the stream's data. With a lazy getter that read is a
resolution.

- **The lexer's place is saved and put back.** The lazy source counts its depth. A resolution that
  starts inside another saves the parser's state before it and restores it after, with the
  `SaveState` and `RestoreState` the parser already uses for exactly this. A resolution at depth
  zero does neither, because nothing is waiting on the position, and a restore costs a seek.
- **Every resolution starts with a seek.** `MoveToObject` sets `Lexer.Position`, which sets the
  stream's position and reads the two characters the lexer keeps ahead. So a caller who moves their
  own stream between two calls does no harm.
- **The parse stack is already safe.** `ReadArray` and `ReadDictionary` reduce to the stack depth
  they started at, and a stream's data is read after its dictionary has been reduced.
- **A length that leads back to its own stream** used to be caught by `_streamsBeingRead`, which is
  consulted after `reference.Value`. The getter would now recurse before that check is reached. The
  set of objects being resolved answers null for one already on it, which is the same answer: the
  length is unknown, and the stream is read up to `endstream` (#128). In a lazy document
  `ResolveLengthObject` stops there. Asking for the value was the read, so it does not read the
  object a second time itself.
- **An indirect `/Length` inside an object stream** has always been parsed by the time the eager
  reader wants it, because compressed objects are read first. In `Lazy` the getter resolves it
  through the compressed index, on the object stream's own parser, and the source does not move.

The lock is a monitor, so the nested resolution enters it again on the same thread.

### The page tree

`PdfDocument.Pages` flattens the tree the first time it is read, in every mode, and `PageCount` is
`Pages.Count`. `FlattenPageTree` visits every node and every page, because it pushes inherited
`/MediaBox`, `/CropBox`, `/Rotate` and `/Resources` down to the pages and replaces `/Kids` with one
flat array.

`Lazy` keeps it. So **asking for the page count, or for any page, resolves every page tree node and
every page dictionary.** It resolves a page's `/Resources` dictionary only when a node above the
page states resources of its own, because that is the one case where the two are compared. It
resolves no content stream, no image and no font. `PdfPage` wrappers are still made one at a time,
by the indexer.

That is the cost the caching table accepts: dictionaries, a few MB for a few thousand pages. Reading
`/Count` and walking to page N without flattening would need `PagesArray`, which is public and is
indexed throughout `PdfPages`, to stop being an array. It is not worth it for dictionaries.

Flattening changes the page tree in memory. It does so in `ReadOnly` and `Import` too.

### Stream bytes, in this item alone

**Item 3 reads a stream's bytes when its object is resolved, and keeps them.** `Parser.ReadObject`
calls `ReadStreamData`, which allocates the array, and the table then holds the object for the
document's lifetime. Item 4 is what stops this.

So with item 3 alone:

- Opening, counting pages and reading page dictionaries cost what the tables above say.
- Reading a page's content, or importing a page, holds every stream that page reaches: once in the
  source, and once more in the target, because `PdfStream.Clone` copies the array.
- Memory therefore grows with what has been touched, not with the file. Taking ten pages costs about
  twice the bytes of those pages' streams. The 64 MB target is met by item 4, not here.
- A stream longer than 2 GiB cannot be resolved at all. `GetStreamLength` and `Lexer.ReadStream`
  take an `int`.
- This depends on item 2. Without the page boundary, importing a page with one link on it reaches
  the page tree, and through it every stream in the file.

Item 4 changes one method to get there. `ReadStreamData` is the only place stream bytes are
allocated for an indirect object, and it already knows the offset and the length.

### Source ownership

| overload | who owns the source | after `Dispose` |
|---|---|---|
| `Open(path, Lazy)` | the document opens its `FileStream`, keeps it and closes it on `Dispose` | resolving an unread object throws `ObjectDisposedException` |
| `Open(stream, Lazy)` | the caller, who must keep it open and seekable for the document's lifetime | the same |

Today the path overload disposes its `FileStream` in a `finally` block, and `PdfDocument.Dispose`
does nothing but set the document's state. For `Lazy`:

- The path overload keeps the stream when `Open` succeeds and closes it when `Open` throws.
- `Dispose` disposes the lazy source, which closes the stream if the document opened it.
- Objects already resolved keep answering after `Dispose`. Only a resolution throws.
- `Close` is unchanged. It writes only for a document built on an output stream.

### Caching

| what | how long |
|---|---|
| Parsed objects | strongly, for the document's lifetime, in the cross-reference table. Dictionaries and arrays are small. In this item a stream's bytes come with it. |
| Decoded object streams | the four most recently used. Most large files keep their dictionaries in object streams, and decoding one for every object looked up in it would be quadratic. |
| Stream bytes | with their object in this item; never, unless the caller reads `.Value`, from item 4 on |

### Thread safety

A lazy document is not thread-safe, the same as every `PdfDocument`. Every resolution moves the one
source stream's position, so a whole resolution, from its seek to the end of its parse, runs under
the lazy source's lock. Contention is then merely slow, where without the lock two readers would
silently corrupt each other's reads.

### A damaged cross-reference table

**There is no rebuild to fall back to.** The reader has none in any mode. A file with no
`startxref` fails with "The StartXRef table could not be found", and a section that is neither a
table nor a stream fails with "no trailer found". `Lazy` fails the same way.

A rebuild is worth having and would be a sequential scan that keeps offsets and never bodies, as
this section used to describe. It is a feature for every mode and a PR of its own. It is listed
under "Not in scope".

### What has to change

Each of these reads `Value` in a way that was harmless when everything was loaded.

| where | what it assumes | what happens to it |
|---|---|---|
| `PdfTrailer.Finish` | `Debug.Assert(iref.Value != null)` after each swap | Removed. An assertion that reads `Value` resolves the catalog in a Debug build and not in a Release build. `Finish` runs before the mode is armed. |
| `Parser.ResolveLengthObject` | reads `reference.Value` before the cycle check | Covered by the set of objects being resolved. In a lazy document it does not go on to read the object itself. |
| `Parser.ObjectStream` | `iref.Value == null` means "not read yet", and the result stays in the table | Not used. The lazy source decodes an object stream itself and puts the table's entry back. |
| `Parser.ReadXRefSubsection`, `AddXRefStreamEntry` | may seek to every object | Skipped when the parser is told not to probe. The header is checked when the object is read. |
| `PdfReader.ReadIndirectObjects` | the only place `MaxObjectNumber` is set | `Lazy` sets it from the table's keys. |
| `PdfPages.ImportExternalPage` | `== Import` | "is `Import` or `Lazy`". |
| `PdfCrossReferenceTable.TransitiveClosure` | "from trailer hack": a null value is looked up again in the table | Works for a well-formed file. With item 2 the closure stops at pages. **Not handled:** a table entry with nothing behind it, reached from an imported page. A lazy document has one when a type 2 entry names an index its object stream does not have; the eager reader fails on that file at `Open`. The closure asserts on it in a Debug build and imports a null in a Release build. |
| `PdfInternals.GetAllObjects` | every value is in memory | Works, and resolves the whole file. The caller asked for every object. |
| `Compact`, `Renumber`, the writers, `PdfResourceDeduplicator`, `EncryptDocument` | walk every reference | Not reachable. All are behind `Save`, `Modify` or encryption. |

Reading `.Value` can now throw `IOException`, `PdfReaderException` or `ObjectDisposedException`. No
caller in the library catches around it, and none needs to.

### Size, and the order of the work

About 450 lines of library code in ten files, one of them new, and about 750 lines of tests.

| file | change |
|---|---|
| `Pdf.IO/enums/PdfDocumentOpenMode.cs` | the member and its remarks |
| `Pdf.IO/LazyObjectSource.cs` (new) | the index, `Resolve`, the four object streams, the lock, `Dispose` |
| `Pdf.IO/PdfReader.cs` | a private `Open` that knows who owns the stream; the `Lazy` branch |
| `Pdf.IO/Parser.cs` | the "do not probe" switch and the header check that replaces the probe; `SaveState` and `RestoreState` reachable by the lazy source; one line in `ResolveLengthObject` |
| `Pdf.Advanced/PdfReference.cs` | the getter, and `ValueIfRead` for the two places that must not read |
| `Pdf.Advanced/PdfObjectStream.cs` | `ObjectNumberAt` |
| `Pdf.Advanced/PdfTrailer.cs` | the two assertions |
| `Pdf/PdfDocument.cs` | the field; `Dispose` |
| `Pdf/PdfPages.cs` | the import check |
| `PSSR.cs` | the two refusals, and the import refusal that now names both modes |

It is one PR, in three commits that can each be checked:

1. **The tests,** failing. `RecordingStream` moves out of `TrailerLocationTests` into `Helpers/`, and
   the hand-written document of `LargeOffsetTests` becomes `SplicedPdf`.
2. **No behaviour changes.** `Finish` without its assertions, and the probe switch, which nothing
   sets. The whole suite passes unchanged.
3. **The mode.** Everything else.

### Verification

`LazyOpenModeTests` reads through `RecordingStream`, which keeps every range read, over a
`SplicedPdf`, which knows the byte range of every object it wrote. Two things about the reader shape
the fixtures. The search for `startxref` reads the last 64 KiB of any file, so each document keeps
its cross-reference section further than that from its objects. And the lexer holds two characters
ahead, so reading one object reads the first two bytes of the next; an object's range starts after
them.

- **`Open` reads no object.** For a classic table, a cross-reference stream and a hybrid file, no
  read falls inside an object. Behind 4 GiB and behind 8 GiB of filler `Open` reads the same number
  of bytes, under 96 KiB.
- **The page count reads dictionaries only.** After `PageCount`, every page dictionary has been read
  and no content stream.
- **A page reads its own objects.** Reading page 2's content, past 4 GiB, reads that stream and
  neither of the others, in under a kilobyte. Importing a page reads no other page's content.
- **Four object streams.** With six object streams, an object from each resolves. Going back to the
  last one used reads nothing from the file; going back to the first reads it again.
- **Lazy agrees with eager.** The three test assets that are neither encrypted nor invalid, a
  document this library wrote with a cross-reference stream, and hand-written documents behind a
  classic table, a cross-reference stream and a hybrid table are opened `Lazy` and `Import`. The
  page count, every page's content and every indirect object's entries and stream data are the same.
- **Newest revision wins.** An object written out in full by a later revision, over a compressed
  copy in an earlier one, resolves to the later one.
- **`/Length`.** An indirect length after its stream, one inside an object stream, and one that is
  its own stream all resolve to the right bytes, and the last terminates.
- **Ownership.** `Open(path, Lazy)` keeps the file: an object resolves after `Open` returns and the
  file cannot be opened exclusively until `Dispose`, after which it can. A file that fails to open is
  not left open. `Open(stream, Lazy)` leaves the stream open after `Dispose`. A stream that cannot
  seek is refused by `Open` before a byte is read. After `Dispose`, an unread object throws
  `ObjectDisposedException` and a read one still answers.
- **A moved stream.** The caller sets the stream's position between two resolutions and both are
  right.
- **The header check.** The four rows of the table under "What `Open` reads today".
- **Refusals.** An encrypted file is refused by name, with its password and with a direct
  encryption dictionary. `OpenModeEnforcementTests` has its fifth row. A page imported from a
  `Lazy` source is written the same as one imported from an `Import` source.

**On the 6.7 GB file,** measured in a Release build: `Open` takes 40 ms, reads 72 553 bytes and
allocates 0.9 MB. `PageCount` (714) takes a further 72 ms, reads a further 30 037 bytes and
allocates 31 MB, which is what parsing 714 page dictionaries out of object streams allocates and is
not kept. The peak working set is 49 MB. Taking one page out reads 2.7 MB, the page's streams,
and the peak working set is 55 MB.

## Item 4 — lazy streams and the raw chunked copy

**A stream that has not been read** is held as its source document, its offset and its length,
instead of a `byte[]`. Three things touch it:

- **`.Value`** reads the bytes and caches them, so every existing caller sees what it always saw.
- **`Clone`**, during import, copies the reference to the source bytes, not the bytes. The clone
  belongs to the target document and still reads from the source.
- **`PdfWriter.WriteStream`** copies the bytes from the source to the output in fixed 80 KB chunks.
  They stay compressed, under the same `/Filter`. Nothing is decoded or re-encoded, so the copy is
  lossless, fast, and uses the same memory for a 2 MB image as for a 200 MB one.

**`/Length`:**

- An indirect `/Length` is resolved when the stream's dictionary is parsed.
- A missing or wrong `/Length` falls back to scanning for `endstream`, as the eager reader does
  (`docs/specs/wrong-stream-length.md`). Here that scan is a streamed pass over the stream's bytes,
  not a read into memory.

**Save order.** A target holding an imported lazy stream needs its source open until the target is
saved. If the source is disposed first, `Save` throws `ObjectDisposedException`, naming the source
document and the object it could not read. There is no fallback that loads everything into memory
when the source is disposed. That would turn a mistake about ordering into a memory blow-up long
after its cause. `PdfSplitter` gets the order right for its callers.

**Verification:**

- On the `SplicedStream` file over 4 GB, with the extracted pages placed beyond 2³², extracting
  pages gives the right pages, compared object by object with the same pages taken eagerly from a
  small file.
- The same extraction from a 4 GB and an 8 GB virtual file allocates totals within 10% of each
  other. That shows memory does not grow with the file.
- A chunked copy writes bytes identical to what `.Value` returns.
- Disposing the source before `Save` throws, with the message naming the source and the object.

## Item 5 — `PdfSplitter`

A thin static class over `Lazy` and `PdfPages.Add`:

```csharp
PdfSplitter.ExtractPages(string sourcePath, string outputPath, int firstPage, int pageCount)
PdfSplitter.ExtractPages(string sourcePath, string outputPath, IEnumerable<int> pageNumbers)
PdfSplitter.Split(string sourcePath, int pagesPerFile, Func<int, string> outputPathForPart)
```

- **Page numbers are 1-based**, as a reader shows them.
- **Each path overload has a stream overload.** A source stream must be seekable.
- **An existing output file is overwritten.**
- **The output's `/Info` is copied from the source's:** title, author and so on.
- **No other document-level structure is copied:** no outlines, forms or structure tree. These are
  the same limits as importing a page by hand
  (`docs/specs/import-size-and-annotations.md`, "Not in scope").
- **`Split` opens the source once** and writes each part in turn. It saves and disposes one target
  before it starts the next, so peak memory is one part's worth.

**Also shipped with item 5:** a SampleApp demo that splits a small embedded PDF, with `docs:begin`
markers for the documentation site, and a docs page on the site that presents the feature.

**Acceptance.** This is a manual run of the 6.7 GB file: take its first ten pages and confirm the
peak managed heap stays under about 64 MB. Alongside it there is a test that is skipped when the
file is absent and reads its path from an environment variable. The file is never committed.

---

## Not in scope

- **Merging several large sources.** It needs two lazy sources at once and de-duplication of shared
  resources across them.
- **Modifying a large file in place**, by incremental append or by any other means. `Append` still
  reads the file into memory and still refuses anything over 2 GB.
- **Encrypted sources** in `Lazy`. They are refused.
- **Rebuilding a damaged cross-reference table.** No mode does it today. `Lazy` fails on such a
  file as the others do.
- **Making `Import` lazy.** It stays eager until `Lazy` has proved itself.
- **A memory bound below one stream.** A caller who reads `.Value` on a 500 MB image gets a 500 MB
  array. Only the writer's raw copy avoids that.
