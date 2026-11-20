# Spec — reading a large PDF lazily and extracting pages from it, issue #366

[PinataLabs/PdfPinata#366](https://github.com/PinataLabs/PdfPinata/issues/366) asks for a way to take
ten pages out of a 6.7 GB image-heavy PDF without holding 6.7 GB. Today that cannot be done in any
open mode, because every one of them reads the whole file before `PdfReader.Open` returns.

All five items are built.

| item | what | status |
|---|---|---|
| 1 | 64-bit offsets through the lexer, the cross-reference reader and the writer | done (#367) |
| 2 | The import closure stops at page boundaries | done (#369) |
| 3 | `PdfDocumentOpenMode.Lazy`: objects resolved on first access | done |
| 4 | Lazy streams, copied raw and in chunks by `PdfWriter` | done |
| 5 | `PdfSplitter`, a SampleApp demo and a docs page | done |

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

### The data shape

`PdfStream` holds one of two things: its bytes, or a `DeferredStreamBytes` saying where they are.

| state | `_value` | `_unread` |
|---|---|---|
| in memory, as in every other mode | the bytes | null |
| still in the file | null | source, object, offset, length |
| no data yet (`CreateStream` not called) | null | null |

`DeferredStreamBytes` (`Pdf.IO/DeferredStreamBytes.cs`) is immutable: the `LazyObjectSource`, the
object's ID in the source, a `long` offset and a `long` length. It names no owning document, which
is what lets a clone share it.

- **`Value`** reads the bytes through the source, keeps them and drops the `DeferredStreamBytes`.
  `UnfilteredValue`, `TryUnfilter`, `Zip` and `ToString` go through `Value`. The setter and
  `SetContent` drop it too.
- **`Length`** answers the deferred length without reading. `LongLength` is the internal form the
  writer and `/Length` use. `Length` throws for a deferred stream over `int.MaxValue`.
- **`Clone`** is `MemberwiseClone`, so the clone has the same `DeferredStreamBytes`.
- **`PdfWriter.WriteStream`** asks the stream for `Unread`. When there is one it calls
  `CopyTo(output)`, and the stream stays deferred, so a second save copies again.
- **`PdfDictionary.DeclareStreamLength`** writes `/Length` as a `PdfLong` over `int.MaxValue`.

**Reading is the source's job.** `LazyObjectSource.ReadStreamBytes` and `CopyStreamBytes` run under
the lock every resolution runs under, because they move the same stream. When an object is being
resolved at the time, the parser's state is saved and put back, as it is for a nested resolution.
The copy uses one 80 KB buffer the source keeps.

**Parsing.** The source gives its parser to itself (`Parser.DeferStreamsTo`). With that set,
`ReadStreamData` calls `DeferStreamData`:

1. The declared length is read, through its reference if it has one. `PdfInteger`, `PdfUInteger`
   and `PdfLong` are all accepted, so a length over 2 GiB is.
2. It is believed when `endstream` follows that many bytes, after white space. This reads 17 bytes
   and not a token, because a wrong length lands in the data, where a token can be as long as the
   stream.
3. Otherwise `Lexer.FindMarker` looks for `endstream` from the start of the data, 64 KB at a time,
   and the end-of-line before it is left out. No keyword is a `PdfReaderException`.
4. `/Length` is replaced by the direct number, as the eager reader replaces an indirect one.

Every other parser has no `DeferStreamsTo` and reads as before. That includes the one reading the
cross-reference sections during `Open`, and the one each `PdfObjectStream` builds over its bytes.

**Errors.** After the source is disposed, both methods throw `ObjectDisposedException`. An
`ObjectDisposedException` from the caller's own stream is rethrown as the same. The message names
the source's path, or says it was opened from a stream, and the object. A file that has become
shorter is a `PdfReaderException`.

### What touches stream bytes on the way from source to output

| where | what it does | in a `Lazy` import |
|---|---|---|
| `PdfPages.ImportExternalPage`, `CloneElement` | `Clone` of page entries | No bytes. |
| `PdfObject.ImportClosure`, `DeepCopyClosure` | `Clone` of every object reached | Copies the `DeferredStreamBytes`. |
| `PdfObject.FixUpObject`, `PdfImportedObjectTable` | references only | No bytes. |
| `PdfAnnotations.FixImportedAnnotation`, `DetachImportedDestinations` | dictionaries only | No bytes. |
| `PdfDocument.PrepareForSave`, `PdfCatalog`, `PdfPages` | page tree and fonts of the target | No bytes. |
| `PdfPage.RemoveEmptyContentStreams` | `Stream.Length == 0`, for a page whose `Contents` was read | Reads the length, not the data. |
| `PdfCrossReferenceTable.Compact`, `Renumber` | references only | No bytes. |
| `PdfObjectStreamWriter.MayBeCompressed` | `Stream != null` | No bytes. |
| `PdfDictionary.WriteObject`, `DeclareStreamLength` | `/Length` from `LongLength` | No bytes. |
| `PdfWriter.WriteStream` | writes the data | Chunked copy. With a security handler, one stream is read, encrypted and written at a time, and is not kept. |
| `PdfContent` (constructor, `WriteObject`) | unfilters, and deflates on save | Reads the stream. Only when the caller asks for `page.Contents` or draws on the page. |
| `PdfResourceDeduplicator` (`Options.DeduplicateResources`) | compares `Stream.Value` | Reads every stream it compares. Opt-in. |
| `PdfDocument.ConsolidateImages` | hashes `Stream.Value` | Reads every image. Opt-in. |
| `PdfDocument.PruneUnusedResources` | decodes content streams | Reads content streams, not images. Opt-in. |
| `PdfFormXObject` (importing a page as a form) | joins content streams | Reads the content streams. Not the page import path. |

Nothing on the save path compresses or re-filters a stream it did not make. `PdfContent` is the
one class that deflates on save, and an imported page's content is a plain dictionary until the
caller makes it one.

### Verification

`LazyStreamTests`. The large documents are `SplicedPdf`s whose images are runs of one byte
(`StreamOfFiller`), saved to a `RunCollapsingStream`, which keeps a run as its length and can be
read back.

- **Memory.** Three pages placed beyond 2³² are taken out of a 4 GiB file with 16 MiB images and an
  8 GiB file with 1 GiB images. The second allocates under 1 MB, within 10% of the first, and reads
  each image from the source once. The output is opened again and has three pages.
- **Laziness.** Reading a stream's dictionary, and importing its page, read none of its data.
  `Value` reads it once.
- **Bytes.** A stream of 300 007 scrambled bytes is written as `Value` answers it, with its
  `/Filter`. The same into an encrypted document. Three test assets and a document with a JPEG and
  a PNG are saved the same from `Lazy` as from `Import`, object by object, and their images keep
  their filters and their bytes.
- **Over 2 GiB.** A 3 GiB stream is copied, its `/Length` is a `PdfLong`, and `Value` throws.
- **`/Length`.** Indirect, missing, too short, past the end of the file and naming the stream
  itself all copy the whole stream. A 256 MiB stream with no `/Length` is measured in under 1 MB.
- **Order.** Saving after the source is disposed throws for a path and for a stream, and after the
  caller closes the stream. A stream read before the dispose is still saved.
- **Threads.** Sixteen streams read at once are each their own.

**On the 6.7 GB file,** in a Release build: the first ten pages are 67 849 697 bytes. Taking them
out reads 67 941 217 bytes of the source and took 326 ms and 195 ms in two runs. The managed heap
peaked at 19.8 MB in the higher of them, sampled every 2 ms, and the working set at 54 MB. 36 MB is allocated in all; item 3
measured 31 MB for counting the pages. Ghostscript renders the first three pages of the output to the same pixels as
the same pages of the source.

### Not done

- A save that fails because the source is gone has already written part of the output.
- A target saved with encryption holds one stream at a time, so a stream over 2 GiB cannot be
  written encrypted.
- `Import` is not lazy. A stream read by any other mode is in memory, as before.

## Item 5 — `PdfSplitter`

A thin static class over `Lazy` and `PdfPages.Add`, in `PdfPinata.Pdf.IO` beside `PdfReader`,
because what it does is open a file and write files, and a caller who splits one already has that
`using` for `PdfReader` and `PdfDocumentOpenMode`:

```csharp
void PdfSplitter.ExtractPages(string sourcePath, string outputPath, int firstPage, int pageCount)
void PdfSplitter.ExtractPages(string sourcePath, string outputPath, IEnumerable<int> pageNumbers)
int  PdfSplitter.Split(string sourcePath, int pagesPerFile, Func<int, string> outputPathForPart)

void PdfSplitter.ExtractPages(Stream source, Stream output, int firstPage, int pageCount)
void PdfSplitter.ExtractPages(Stream source, Stream output, IEnumerable<int> pageNumbers)
int  PdfSplitter.Split(Stream source, int pagesPerFile, Func<int, Stream> outputForPart)
```

- **Page numbers are 1-based**, as a reader shows them, and **so are part numbers**: part 1 holds
  pages 1 to `pagesPerFile`. Two counting conventions in one call would be the bug.
- **A page named twice is written twice**, as a print range would. Importing the same page twice
  shares what it reaches through the imported-object table, so the second copy costs a page
  dictionary: page 1 of the 6.7 GB file is 2 672 085 bytes alone and 2 672 648 twice.
- **Every page is checked before anything is written.** A page outside the document is an
  `ArgumentOutOfRangeException` naming the page and the page count; for a range, the first page it
  misses. An empty list is an `ArgumentException`, a `pageCount` or `pagesPerFile` below 1 an
  `ArgumentOutOfRangeException`. So a refused call leaves an existing output as it was.
- **An output path that is the source is refused**, before the source is opened. The source is read
  while the output is written, and opening the output empties it. Windows would refuse the open,
  because a `Lazy` document keeps its file; Linux would not.
- **Streams are the caller's.** The source must seek, as `Lazy` already requires, and is not
  closed. An output is written, rewound and left open, as `PdfDocument.Save(Stream)` leaves it.
- **An existing output file is overwritten**, by `PdfDocument.Save(string)`.
- **`/Info`.** The title, author, subject, keywords, creator, producer and creation date are copied
  when they are direct strings. `/ModDate` is the time of writing, and the producer becomes
  "PdfPinata … (Original: …)" through the stamp every save applies. That is what opening the source
  with `Modify`, deleting the other pages and saving would give, which is the rule: the output is a
  changed copy of the source, not a new document.
- **No other document-level structure is copied:** no outlines, forms, structure tree or page
  labels. These are the same limits as importing a page by hand
  (`docs/specs/import-size-and-annotations.md`, "Not in scope").
- **`Split` opens the source once** and writes each part in turn. It saves and disposes one target
  before it builds the next, so peak memory is one part's worth plus what the source has read. It
  **returns the number of parts**, 0 for a document with no pages. The caller knows the paths,
  because it chose them, but not how many parts a file it has not opened will make.
- **Encrypted sources** are refused by `Lazy`, with the `PdfReaderException` that names `Import`.
  `PdfSplitter` takes no password.

**The demo.** `Split` builds ten pages in memory and takes a range, a list and parts of four out of
them, and does the same once by hand through `Lazy`. Its markers `extract-range`, `extract-list`,
`split` and `by-hand` are what `docs-website/docs/existing-pdfs/large-files.md` shows, and that page
is where `Lazy` is documented for users: when to choose it over `Import`, the source's lifetime and
the save order.

### Verification

`PdfSplitterTests`, over `SplicedPdf` documents:

- A range, and a list out of order with a repeat, are written as the source's pages, compared by
  their content.
- `/Info` is copied, the producer names the source's, and the output is dated after it.
- An existing output is overwritten; the source file is closed afterwards.
- `Split` of seven pages by three writes 3, 3 and 1, asks for parts 1, 2 and 3, and returns 3.
- Each stream overload writes the same pages and leaves its streams open.
- Every refusal above, and a refused call leaves an existing output as it was.
- **Memory.** Taking two pages out of a `SplicedPdf` of 8 GiB with 1 GiB images allocates under
  1 MB, within 10% of taking them out of one of 4 GiB with 16 MiB images. Splitting either in two
  allocates under 2 MB.
- **The real file**, when `PDFPINATA_LARGE_PDF` names it: its first ten pages allocate under 64 MB
  and reopen as ten pages. The test is skipped otherwise; the file is never committed.

**On the 6.7 GB file,** from a Release console app on .NET 10, the managed heap sampled every 2 ms:

| | time | peak managed heap | peak working set | allocated |
|---|---|---|---|---|
| `ExtractPages(src, out, 1, 10)`, three runs | 226, 301, 246 ms | 21.4 MB | 53.7 MB | 35.3 MB |
| `Split(src, 100, …)` | 14.8 s | 26.9 MB | 66.8 MB | 95.2 MB |

The ten pages are 67 849 529 bytes. The split wrote eight parts, seven of 100 pages and one of 14,
from 122 MB to 1.1 GB and 6 747 197 280 bytes in all, 50 MB more than the source because each part
carries its own fonts. Parts 2 and 8 reopen as 100 and 14 pages in both `Lazy` and `Import`. Most of
what extraction allocates is counting the pages, which item 3 measured at 31 MB; the split's
allocations grow with the page dictionaries each part imports, and its heap does not.

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
