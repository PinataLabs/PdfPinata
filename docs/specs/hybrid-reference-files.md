# Spec — a hybrid-reference file, issue #388

[empira/PDFsharp#388](https://github.com/empira/PDFsharp/issues/388) reports a one-page document
whose page cannot be imported: `KeyNotFoundException: The given key '9 0' was not present in the
dictionary`. The reporter had already found the cause and named it in the title — the trailer's
`/XRefStm` entry is ignored, so every object the file keeps in an object stream is invisible.

| item | what | status |
|---|---|---|
| 1 | Read the cross-reference stream a classic trailer names in `/XRefStm` | done |
| 2 | Say which section is at fault when that stream cannot be read | done |
| 3 | Open the file as its classic table describes it, under `Moderate` accuracy | done |
| 4 | Following the stream's own `/Prev` | deliberately not done |
| 5 | Writing a hybrid-reference file | deliberately not done |

---

## What a hybrid-reference file is

ISO 32000-1 7.5.8.4. PDF 1.5 added object streams, which pack many small objects into one
compressed stream, and cross-reference streams, which are the only kind of section that can say an
object is inside one. A file using them is unreadable by a PDF 1.4 reader — which is why the
standard describes a way to have both.

A hybrid-reference file describes **one revision twice**:

- a classic cross-reference table and `trailer`, in which every object that lives in an object
  stream is marked **free**, so that a reader knowing nothing of object streams reads a smaller
  document rather than a broken one; and
- a cross-reference stream elsewhere in the file, named by the trailer's `/XRefStm`, holding the
  type 2 entries that say where those objects really are.

The two are not alternatives to be chosen between. The table is deliberately incomplete and the
stream is what completes it, so a reader that supports compressed objects has to read both.

## What was wrong

`PdfTrailer.Keys.XRefStm` was declared, documented, and read by nothing. `Parser.ReadTrailer`
followed `/Prev` and only `/Prev`. The one other mention in the library is in
`PdfTrailer.WriteObject`, which deletes the entry on the way out — correctly, because what this
library writes is never a hybrid file.

So the compressed objects were not merely unavailable: they were **contradicted**. The table says
the object is free, nothing says otherwise, and what is left on the page is a reference to an object
the document does not have.

The two libraries were then loud in different ways:

- **Upstream** throws `KeyNotFoundException` out of `PdfImportedObjectTable` while the page is
  imported, which is the report.
- **Here** it is quieter and worse. A dangling reference reads as the null object by design — see
  `DanglingReferenceTests` and [empira/PDFsharp#248] — so nothing throws at all. The document opens,
  the page imports, the file saves, and the resource is simply gone. A font, an image, an
  annotation, a whole `/Resources` dictionary: whatever the writer chose to compress.

Everything needed to read one was already here. Object streams are read, type 2 entries are
resolved, and `ReadXRefStream` merges into the same table the classic reader fills. The only
missing step was going and reading the stream at all.

## What it does now

`Parser.ReadHybridCrossReferenceStream`, called from the trailer loop once per revision:

```csharp
var trailer = ReadXRefTableAndTrailer(_document._irefTable, accuracy);

if (firstTrailer == null)
    firstTrailer = trailer;

ReadHybridCrossReferenceStream(trailer, accuracy);

var prev = trailer != null ? trailer.Elements.GetInteger(PdfTrailer.Keys.Prev) : 0;
```

Three decisions are in where that call sits, and all three match what pdf.js and pypdf do:

**After the table, before `/Prev`.** The stream belongs to the revision just read, not to the one
before it. Reading it later would let an older revision's entries be seen first.

**Entries already in the table win.** That is not new — it is the rule `ReadXRefTableAndTrailer`
and `ReadXRefStream` are both already written under ("Ignore the latter one"), and it is what makes
the newest revision the one that counts. Nothing in a well-formed hybrid file collides anyway: the
objects the stream is there for are exactly the ones the table gave up on.

**The stream's own trailer is dropped.** A cross-reference stream is a trailer as well as a table,
and a real one carries `/Root`, `/Info` and `/ID` like any other. It is read here for its entries
alone, and the classic trailer of the same revision stays the document's.

## When the stream cannot be read

The file then says two things, and only one of them is damaged. The classic table is a complete
section of its own, which is the entire point of writing the file this way, so the document it
describes can still be opened. It is not the whole document, though: the table does not repeat
where the compressed objects are, so dropping the stream drops every one of them — one object or
many, whichever only the stream located.

So the failure is routed through `PdfReadAccuracy`, whose two members already say exactly this:

- **`Strict`** (the default): the read stops, with a `PdfReaderException` naming `/XRefStm` and the
  position, and the original failure as its inner exception. A cross-reference stream can be
  damaged in more ways than are worth enumerating, so what is caught is broad and what is reported
  is specific.
- **`Moderate`**: the stream is dropped and the document opens as the PDF 1.4 file its table
  describes — short of every object the stream alone located, which is what a 1.4 reader would
  have made of it. The fallback is lossy by nature, and chosen by asking for `Moderate`.

The stream is dropped **whole**. It is read into a cross-reference table of its own and merged into
the document's only once all of it has been read, so a stream damaged halfway through does not leave
the entries before the damage in effect.

A `/XRefStm` naming a position outside the file is refused the same way and says so in those words,
rather than arriving as an out-of-range exception from the lexer. So is one naming a position inside
the file where there is no cross-reference stream — a classic table, or anything else — which would
otherwise be read as nothing at all. `Moderate` drops either, as it drops a damaged stream.

## Deliberately not done

**The stream's `/Prev` is not followed.** In a file of several revisions it is the classic trailers
that form the chain, and each of them names its own stream, so following both chains would visit
the same sections twice and in an order that could let an older entry be seen before a newer one.
pypdf reads it the same way.

**No hybrid file is written.** `PdfTrailer.WriteObject` drops `/XRefStm` and this library either
writes a classic table or writes cross-reference streams throughout (see
[cross-reference-streams.md](cross-reference-streams.md)). A hybrid file is a compatibility measure
for readers older than PDF 1.5, and writing one would mean writing both kinds of section for every
revision. Note the consequence for anyone reading a hybrid file here: **saving it produces an
ordinary file**, so the objects now have to be reachable the plain way in the copy — which is what
`ItSurvivesBeingSavedAsAnOrdinaryFile` pins.

## Tests

`src/PdfPinata.Test/IO/HybridCrossReferenceTests.cs`. The documents are written by hand, because
this library writes neither kind of hybrid file; object 7 is a graphics state the page names, kept
in an object stream and named by nothing but the cross-reference stream, and every test turns on
whether it is there. Eight of the twelve fail without the change.

The four that do not are the ones guarding the new behaviour rather than reproducing the old: that
the stream's `/Root` does not become the document's, and that `Moderate` opens a file whose stream
is damaged, damaged part way through, or not a stream at all. The part-way one is written as a
stream whose first entry is good and whose second names no object, and fails against a reader that
merges entries as it goes — which is what the first version of this change did.

[empira/PDFsharp#248]: https://github.com/empira/PDFsharp/issues/248
