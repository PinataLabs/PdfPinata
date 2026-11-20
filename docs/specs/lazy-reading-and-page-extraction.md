# Spec — reading a large PDF lazily and extracting pages from it, issue #366

[PinataLabs/PdfPinata#366](https://github.com/PinataLabs/PdfPinata/issues/366) asks for a way to take
ten pages out of a 6.7 GB image-heavy PDF without holding 6.7 GB. Today that cannot be done in any
open mode, because every one of them reads the whole file before `PdfReader.Open` returns.

This is a proposal. Nothing in it has been built.

| item | what | status |
|---|---|---|
| 1 | 64-bit offsets through the lexer, the cross-reference reader and the writer | not started |
| 2 | The import closure stops at page boundaries | done (`import-closure-stops-at-pages`) |
| 3 | `PdfDocumentOpenMode.Lazy`: objects resolved on first access | not started |
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
`Save`, and the `Version`, `PageLayout` and `PageMode` setters. `IsReadOnly` answers true for it.
`OpenModeEnforcementTests` gets a fifth row.

### Resolving on demand

`Open` reads the header, the trailer chain and every cross-reference section. That includes
`/Prev`, `/XRefStm` for hybrid files, and cross-reference streams. It parses no objects. The
cross-reference index holds an offset for each object, or an object-stream number and index for a
compressed object, and nothing else.

`PdfReference.Value` on an unresolved reference parses the object at that moment, through the same
`Parser` the eager path uses, and caches the result.

**When the cross-reference table is damaged**, the reader falls back to rebuilding it. That rebuild
is a sequential scan of the whole file. It keeps offsets and never object bodies.

### Source ownership

| overload | who owns the source | after `Dispose` |
|---|---|---|
| `Open(path, Lazy)` | the document opens its `FileStream`, keeps it and closes it on `Dispose` | resolving an unread object throws `ObjectDisposedException` |
| `Open(stream, Lazy)` | the caller, who must keep it open and seekable for the document's lifetime | the same |

A stream that cannot seek is refused when `Open` is called, not on the first read.

### Caching

| what | how long |
|---|---|
| Parsed dictionaries and arrays | strongly, for the document's lifetime. They are small, and a page tree of a few thousand nodes is a few MB at most. |
| Decoded object streams | an LRU cache of four. Most large files keep their dictionaries in object streams, and decoding one for every object looked up in it would be quadratic. |
| Stream bytes | never, unless the caller reads `.Value` (item 4) |

### Thread safety

A lazy document is not thread-safe, the same as every `PdfDocument`. Every resolution moves the one
source stream's position, so the seek and the read run under an internal lock anyway. Contention is
then merely slow, where without the lock two readers would silently corrupt each other's reads.

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
- **Making `Import` lazy.** It stays eager until `Lazy` has proved itself.
- **A memory bound below one stream.** A caller who reads `.Value` on a 500 MB image gets a 500 MB
  array. Only the writer's raw copy avoids that.
