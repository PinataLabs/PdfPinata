# Proposal — writing cross-reference streams and object streams

What the compressed write path covers, and what it deliberately leaves out.
Gap **G1** of the competitive gap analysis.

| item | what | status |
|---|---|---|
| 1 | `PdfDocumentOptions.CrossReferenceFormat` — `Classic` \| `Stream` | done |
| 2 | Object-stream writer half — `PdfObjectStreamWriter` | done |
| 3 | Cross-reference stream emission, with type-2 entries | done |
| 4 | Encryption interaction — an object inside an `ObjStm` is not separately encrypted | done, **and it found a reader defect** |
| 5 | Reading a cross-reference stream and writing one back | done |

Covered by `PdfPinata.Test/IO/CrossReferenceStreamTests.cs`.

## Two things the proposal got wrong

**The encryption item was not only a writer concern.** Writing the object stream encrypted exactly
once was the easy half and worked first time. What the test caught was on the *reading* side:
`PdfReader` finishes by calling `PdfStandardSecurityHandler.EncryptDocument`, which walked **every**
reference and decrypted it — including objects it had just parsed out of an object stream, whose
strings were never separately encrypted. Decrypting them a second time turned every string in the
document to nonsense, which is exactly the failure shape the proposal predicted and exactly not the
place it predicted it.

This is a defect in reading **any** encrypted file that uses object streams, whoever wrote it. It was
unreachable before only because this library could not produce one to read. The fix is
`PdfObject.IsFromObjectStream`, set in `Parser.ReadCompressedObject` and honoured in
`EncryptDocument`.

That the strings come out *correct* once the second decryption is removed is also what proves the
writer encrypts the object stream exactly once — a writer that had it wrong would not produce
correct plaintext by accident.

**The AES-256 round-trip test in the proposal could not be written.** `PdfDocumentSecurityLevel`
offers `Encrypted40Bit` and `Encrypted128Bit` and nothing else; AES-256 (AESV3) is supported for
*reading* only, in `Pdf.Security/EncryptorFactory.cs`. The test covers both levels the writer
actually has. Writing AES-256 is a separate piece of work and is not part of this.

---

## The defect

The writer emits a PDF 1.4-era classic `trailer` plus a plain cross-reference table, always — even
when the document it is saving was read from a file that used a cross-reference stream.
`Pdf/PdfDocument.cs:336-341` does this on purpose:

```csharp
if (_trailer is PdfCrossReferenceStream)
{
    // Convert cross-reference stream to cross-reference table.
    _trailer = new PdfTrailer((PdfCrossReferenceStream)_trailer);
    ...
}
```

`Pdf.Advanced/PdfObjectStream.cs` is read-only — `ReadCompressedObject` and nothing that writes one.
So the library understands compressed objects perfectly well when someone else produced them and
cannot produce them itself.

Three consequences.

**Files are larger than they need to be.** Every dictionary — page, font, annotation, form field,
resource map — is written out uncompressed, one indirect object at a time, each with its own
`N 0 obj … endobj` frame and its own xref row. On a drawing-heavy document the content streams
dominate and nobody notices. On a document that is mostly *objects* — a filled form, a document with
several hundred link annotations, and above all a **tagged** document, where every paragraph, cell and
list item becomes a structure element — the dictionaries are the file. This is the reason to do it
before tagged PDF rather than after: tagging multiplies the object count, so shipping it on a classic
writer ships a size regression with it.

**A read/write round trip loses information about the file's own shape.** A PDF 1.5+ document opened
and saved comes back as a PDF 1.4-shaped one.

**PDF 2.0 is out of reach.** ISO 32000-2 assumes the cross-reference stream. There is no path to it
from a writer that cannot emit one.

---

## Item 1 — the option

```csharp
document.Options.CrossReferenceFormat = PdfCrossReferenceFormat.Stream;
document.Options.MaxObjectsPerObjectStream = 200;   // what Acrobat uses
```

Default `Classic` for one release so nobody's byte-comparison tests break without warning, then flip
the default and keep `Classic` as the escape hatch. `PdfDocument.Version` moves to 15 when the format
is `Stream`, because a 1.4 header on a file with an xref stream is simply invalid.

## Item 2 — the object stream

An object stream is a stream whose data is a run of object bodies with a small offset table in front:

```text
/Type /ObjStm  /N 4  /First 22  /Filter /FlateDecode
┌──────────────────────────────────────────────────────────┐
│ 12 0  15 48  19 96  23 131 │ <</Type/Page …>> <</Font…>> …│
└─── the /First prologue ────┴── the bodies, concatenated ──┘
  objnum offset pairs           each body starts at /First+offset
```

The whole thing is then Flate-compressed as one unit, which is where the saving comes from: two
hundred small dictionaries share a compression window instead of each getting its own.

**What may not go in.** Streams (a stream cannot nest inside a stream), the `/Encrypt` dictionary, the
document `/ID`, the cross-reference stream itself, and anything an outside reference needs a real file
offset for. Everything else is eligible.

## Item 3 — the cross-reference stream

A stream of fixed-width binary rows, widths declared in `/W`:

| type | field 2 | field 3 | means |
|---|---|---|---|
| 0 | next free object | generation | free-list entry |
| 1 | byte offset in file | generation | an ordinary object, as the classic table had it |
| **2** | **object number of the containing `ObjStm`** | **index within it** | **a compressed object** |

Type 2 is the whole point and the only genuinely new row. `/Index` carries the subsections; the
trailer keys — `/Root`, `/Info`, `/ID`, `/Size` — move onto the stream dictionary itself, which is why
`PdfTrailer` and `PdfCrossReferenceStream` have to stay interchangeable rather than one converting to
the other.

## Item 4 — encryption is the trap

Strings and streams inside an object stream are **not** encrypted individually. The containing
`ObjStm` stream is encrypted, once, and its contents ride along inside. Encrypting them twice produces
a file that opens, appears to work, and yields mojibake for every string in it — the worst failure
shape, because it survives a smoke test.

`Pdf.Security` has to learn the distinction, and the test that pins it writes an AES-256 document with
`CrossReferenceFormat.Stream`, reopens it, and reads a string back out. `Pdf/PdfDocument.cs:352` shows
where the `/Encrypt` reference goes into the trailer today; that path needs the same treatment on the
stream side.

---

## What this deliberately does not cover

- **Linearization.** A different problem — first-page-first object ordering plus hint streams — and of
  little value to a server-side generation workload. Out of scope here and low priority everywhere.
- **Choosing which objects to group.** The first implementation buckets eligible objects in the order
  they are written, capped at `MaxObjectsPerObjectStream`. Grouping by kind (all fonts together, all
  structure elements together) compresses better because it puts similar dictionaries in the same
  window, but it is an optimisation to measure later, not a design constraint now.
- **PDF 2.0 features.** This unblocks them. It does not deliver any of them.

## Tests

`PdfPinata.Test/IO/CrossReferenceStreamTests.cs`, fourteen of them. The ones that carry weight
rather than merely pass:

- **A page keeps its size across the round trip.** Page dictionaries are exactly what gets moved into
  an object stream, so this is the assertion that says a type-2 entry resolves to the object it names.
- **An object-heavy document is smaller than its classic form.** Measured, not assumed — it is the
  entire point of the feature and the one claim worth failing the build over.
- **A string in an encrypted document survives**, at both security levels. This is the one that found
  the reader defect above.
- **The encryption dictionary is not moved into an object stream**, because a reader has to reach it
  before it can decrypt the stream that would otherwise be hiding it.
- **The classic table is still the default.** Changing the bytes of every document written by someone
  who asked for nothing is not a change to make silently.
- **More objects than fit one stream are split across several**, with `MaxObjectsPerObjectStream` set
  low enough to force it, and the document still reopens.

`PdfPinata.Test/IO/CrossReferenceStreamDecodingTests.cs` covers the reading half from the other
side: `Parser.ReadXRefStream` reached directly through `ParserProbe`, with `/W`, `/Index` and `/Size`
varied against a hand-packed stream body rather than against whatever shapes a written document
happens to produce. The entries it decodes and what it puts in the cross-reference table are what
those assert; the round trips above are what say the two halves agree.

## Related

- `docs/specs/incremental-update-save.md` — needs the same two-pass writer refactor.
- `docs/specs/tagged-pdf-accessibility.md` — the object-count multiplier that makes this urgent.
