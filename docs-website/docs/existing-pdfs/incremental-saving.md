---
title: Incremental saving
description: Change a PDF by appending a revision to it with SaveIncremental, so that signatures stay valid and every earlier version stays in the file.
demos: [Revise]
---

`Save` writes a whole new file from the document in memory. `SaveIncremental` does not: it copies
the bytes the document was read from, unchanged, and appends only what you changed after them, as a
new **revision**. A PDF reader shows the latest revision. The earlier ones are still in the file.

You need an incremental save in two cases:

- **The document is signed.** A signature covers the bytes of the file as they were when it was
  signed. `Save` rewrites those bytes and breaks every signature. An incremental save leaves them
  alone, so it is the only way to change a signed document and keep its signatures valid.
- **You must keep every earlier state of the document.** Each revision stays in the file and can be
  recovered, which gives you an audit trail.

An incremental save does not make a file smaller. It only ever makes it larger.

`SaveIncremental` is a method of `PdfDocument` in the core `PdfPinata` package.

## Open for appending, change, save

1. Open the document with `PdfDocumentOpenMode.Append`.
2. Change it as you would in `Modify` mode.
3. Call `SaveIncremental` with an empty stream.

The Revise demo saves a document, opens it again for appending and changes its subject line:

```csharp demo=Revise snippet=append
```

It then adds a page, saves the second revision and opens that for a third:

```csharp demo=Revise snippet=save-incremental
```

With files on disk, write the new revision to a new file:

```csharp
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;

using (PdfDocument document = PdfReader.Open("contract.pdf", PdfDocumentOpenMode.Append))
{
    document.Info.Subject = "Countersigned";

    using FileStream output = new FileStream("contract-new.pdf", FileMode.Create);
    document.SaveIncremental(output);
}
File.Move("contract-new.pdf", "contract.pdf", overwrite: true);
```

## Why the mode has to be `Append`

An incremental save replaces an object by writing a new version of it under the same object number.
The reader reads the file from the end, finds the newest version of each object first, and ignores
the older ones.

That only works if the object numbers are the ones in the file. `Modify` renumbers every object when
it opens the file, and `Import` and `ReadOnly` do not permit changes. `Append` keeps the numbers and
also keeps the original bytes, so that `SaveIncremental` can copy them. On any other document,
`SaveIncremental` throws `InvalidOperationException` and says why. A document you created with
`new PdfDocument()` has nothing to append to, so it cannot be saved incrementally either.

## `Save` and `SaveIncremental` compared

| | `Save` | `SaveIncremental` |
|---|---|---|
| Writes | The whole document, from memory. | The original bytes, then the changed and new objects. |
| Signatures | Broken. | Kept valid. |
| Earlier revisions | Discarded. | Kept in the file. |
| File size | Only what the document holds now. | Grows with every revision. |
| Open mode | `Modify` or `Append`. | `Append` only. |

A document opened in `Append` mode can still be saved with `Save`. That writes a single-revision
file, which is how you reclaim the space that many revisions take, and how you remove content for
good. Do not do it to a signed document.

## What a revision contains

A revision holds every object you changed, written out in full, and every new object, followed by a
new cross-reference section. It holds more than you might expect:

- **Adding a page changes the page list**, so the page tree node is written again as well.
- **Changing a document property**, such as `Info.Subject`, writes the document information
  dictionary again.
- **Fonts are embedded again.** A document opened for appending does not reuse the font objects
  already in the file. A new page drawn in a face the file already uses embeds that face a second
  time, and that is usually most of the size of the revision. The Revise demo shows the numbers.

The library tracks what you change. Changes you make through the document, its pages and the
`Elements` of its objects mark those objects as changed. If you change an object some other way,
call `MarkAsChanged` on it, or the revision will not contain it and the file will still show the old
version.

## Things to know

- **The stream must be empty.** `SaveIncremental` writes the whole file, original bytes and all.
  Given a stream that already holds data, such as the file the document came from, it throws
  `ArgumentException`. Overwriting the source in place would leave the end of the old file behind the
  new revision, and a reader would find the old end first and ignore the revision.
- **`Append` mode holds the original file in memory** for as long as the document is open. A 200 MB
  file takes 200 MB, and a file larger than 2 GB cannot be opened in this mode at all.
- **An earlier revision still holds everything it ever said.** If you cover a name with a black
  rectangle and save incrementally, the name is still in the file, one revision back, and anyone can
  recover it. To remove content, change it and call `Save`, which also removes any signature.
- **Readers need nothing special.** Any PDF reader opens an incrementally saved file and shows the
  latest revision. Only a tool that looks at the bytes sees the earlier ones.
- **Reading a page's content can mark it as changed.** `ContentReader.ReadContent(page)` rearranges
  how the page stores its content streams, so the page goes into the next revision even if you
  change nothing else. See [Reading content streams](./reading-content-streams.md).
- **Signing uses the same mechanism.** `PdfSigner` appends a revision that carries the signature,
  which is why signing an already-signed document keeps the first signature valid. See
  [Digital signatures](../standards/digital-signatures.md).

## See it in action

[The Revise demo](../demos.mdx#revise) writes one file with three revisions. Page one is revision
one, page three was added by revision two, and page four by revision three. The pages report the size
of each revision and count the markers a reader follows from one revision to the one before.

<details>
<summary>The full Revise demo</summary>

```csharp demo=Revise
```

</details>
