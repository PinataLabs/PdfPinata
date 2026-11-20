---
title: Split and extract large files
description: Take pages out of a PDF of any size into new files with PdfSplitter, and read a large file lazily with PdfDocumentOpenMode.Lazy, in memory that grows with the pages you take and not with the file.
demos: [Split]
---

`PdfSplitter` takes pages out of a PDF file into new files. It can take a range of pages, a list of
pages, or every page in parts of a fixed size. It reads the source in
`PdfDocumentOpenMode.Lazy` mode, so the memory it uses grows with the pages you take out and not with
the size of the file. Taking the first ten pages out of a 6.7 GB file of scanned images takes about
a quarter of a second, and the managed heap stays under 22 MB.

`PdfSplitter` and `PdfDocumentOpenMode` are in the `PdfPinata.Pdf.IO` namespace of the core
`PdfPinata` package. They need no backend.

## Take a range of pages

Give the source, the output, the first page and the number of pages. Pages are numbered from 1, as
a PDF reader shows them:

```csharp
using PdfPinata.Pdf.IO;

PdfSplitter.ExtractPages("book.pdf", "chapter-2.pdf", firstPage: 41, pageCount: 25);
```

If the output file exists, it is overwritten. Each method also takes streams in place of paths.
This is how the Split demo takes pages 3 to 6 out of a document it has in memory:

```csharp demo=Split snippet=extract-range
```

The source stream must support seeking. Neither stream is closed. After the save, the output stream
is rewound to its start, as `PdfDocument.Save(Stream)` leaves it.

## Take a list of pages

Pass the page numbers in the order you want them. A page that you name twice is written twice:

```csharp demo=Split snippet=extract-list
```

## Split a file into parts

`Split` writes every page into parts of the size you give. The last part holds the pages that are
left over. Your function gets the number of each part, from 1, and returns the path or stream to
write it to. `Split` returns the number of parts:

```csharp
int parts = PdfSplitter.Split("book.pdf", 100, part => $"book-part-{part:D2}.pdf");
```

`Split` opens the source once. It writes each part and releases it before it starts the next, so it
holds one part in memory at a time. The Split demo writes its parts to streams:

```csharp demo=Split snippet=split
```

## What is copied

- **The pages**, with their content, the fonts and images they draw with, and their annotations.
  The images are copied as they are in the source, with no decoding or compression, so the output
  looks the same as the source.
- **The document information**: the title, author, subject, keywords, creator and creation date.
  The output gets a new modification date. Its producer names PdfPinata, followed by the producer of
  the source.

Bookmarks, the interactive form, the structure tree and page labels are not copied. They belong to
the source document. A link to a page that you did not take out stays on the page, but goes nowhere.
These are the same limits as for any page you copy; see
[Merge, split and assemble](./merge-split-and-assemble.md).

## What is refused

- **A page that is not in the document.** `ArgumentOutOfRangeException` names the page and the
  number of pages in the document. The check is made before anything is written, so an existing
  output file stays as it was.
- **No pages.** A `pageCount` or `pagesPerFile` less than 1 throws `ArgumentOutOfRangeException`. An
  empty list throws `ArgumentException`.
- **An output path that is the source.** The source is still being read while the output is written.
- **An encrypted source.** `Lazy` mode cannot read one, so `PdfSplitter` throws
  `PdfReaderException`. Open the file in `Import` mode with its password and copy the pages
  yourself, as [Merge, split and assemble](./merge-split-and-assemble.md) shows.

## Read a large file lazily

Every other open mode reads the whole file before `PdfReader.Open` returns. A 6.7 GB file needs
6.7 GB of memory, even to count its pages. `PdfDocumentOpenMode.Lazy` reads only the
cross-reference table when it opens the file. It reads each object the first time you ask for it:

- **The page count, or any page,** reads the page tree and every page dictionary. It reads no
  content, image or font. For a file of 714 pages, this is about 100 KB of the file.
- **A stream's data** stays in the file until something asks for it. Reading `Stream.Value` reads
  the data and keeps it.
- **An object that has been read is kept** for as long as the document is open. Memory grows with
  what you read, not with the file.

A `Lazy` document is read-only. You can read it and copy pages out of it with `AddPage`,
`Pages.Add`, `Pages.Insert` and `Pages.InsertRange`, as from an `Import` document. When you save
the document that you copied the pages into, each image is copied from the source file to the output
in small chunks. A 200 MB image costs the same memory as a 2 MB one.

This is what `PdfSplitter` does, written out:

```csharp demo=Split snippet=by-hand
```

### When to use Lazy, and when Import

| | `Import` | `Lazy` |
|---|---|---|
| Reads when it opens | the whole file | the cross-reference table |
| Memory | the whole file | what you read, and the pages you copy |
| Needs the source | only while `Open` runs | until the document is disposed, and until every document that you copied pages into is saved |
| Encrypted files | yes | no |

Use `Lazy` for a file that is large compared to the memory you have, or when you need a few pages of
a large file. Use `Import` for a small file, for an encrypted file, or when you want to close the
source as soon as it is open.

### Keep the source until you save

A `Lazy` document reads from its source for as long as it is open:

- `PdfReader.Open(path, PdfDocumentOpenMode.Lazy)` keeps the file open. Dispose the document to close
  the file.
- `PdfReader.Open(stream, PdfDocumentOpenMode.Lazy)` needs a stream that supports seeking. Keep the
  stream open until the document is disposed. The document does not close it.

**Save every document that you copied pages into before you dispose the source.** Their images are
still in the source until they are saved. A save after the source is disposed throws
`ObjectDisposedException`, which names the source and the object it could not read.

## Things to know

- **Pages are numbered from 1** in `PdfSplitter`, and so are the parts of a split. `document.Pages`
  and the other page methods count from 0.
- **The parts of a split are larger than the source in total.** Each part carries its own copy of
  every font and image that its pages use.
- **A `Lazy` document is not thread-safe**, the same as every `PdfDocument`.
- **A damaged cross-reference table is not rebuilt.** A file that `Import` cannot open, `Lazy`
  cannot open either. A cross-reference entry that points at the wrong place is found when its
  object is read, not when the file is opened.

## See it in action

[The Split demo](../demos.mdx#split) builds a ten-page document, takes a range and a list of pages
out of it, splits it into parts of four, and does the same by hand with `Lazy`.

<details>
<summary>The full Split demo</summary>

```csharp demo=Split
```

</details>
