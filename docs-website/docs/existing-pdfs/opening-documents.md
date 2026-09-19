---
title: Opening documents
description: Open an existing PDF with PdfReader.Open, choose the open mode that permits what you want to do, supply a password, and read damaged files.
---

`PdfReader.Open` reads an existing PDF into a `PdfDocument`. You then read it, change it, take
pages out of it or add a revision to it. Which of those you may do depends on the **open mode** you
pass to `Open`, so choose it before you write anything else.

`PdfReader` and `PdfDocumentOpenMode` are in the `PdfPinata.Pdf.IO` namespace of the core
`PdfPinata` package. Reading a document needs no backend. Drawing on its pages does, as it does for
any document: see [Installation](../installation.md).

## Open a file or a stream

```csharp
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;

using PdfDocument document = PdfReader.Open("report.pdf", PdfDocumentOpenMode.Modify);
```

`Open` reads the whole file into memory before it returns. When you pass a path, it also closes the
file, so you can save back to the same path afterwards.

You can also open a `Stream`. The stream must support seeking, because the reader starts at the end
of the file. To read from a network stream or an upload, copy it into a `MemoryStream` first. After
`Open` returns, the document no longer needs the stream. This is how the Assemble demo reopens a
document it built in memory:

```csharp demo=Assemble snippet=reopen-for-import
```

The overloads of `Open` that take no mode open the document in `Modify` mode.

## Choose an open mode

| Mode | What it permits | Use it to |
|---|---|---|
| `Modify` | Change the document: add, remove and reorder pages, draw on pages, add annotations, fill in form fields, save. | Edit a file and save the result. |
| `Import` | Copy pages out of the document into another one. Nothing else: you cannot change it or save it. | Merge and split documents. |
| `ReadOnly` | Read everything. Change nothing. | Inspect a file, extract text or images. |
| `Append` | Everything `Modify` permits, and `SaveIncremental`. | Change a signed document, or keep every earlier version of a file. |

The modes differ in more than permission:

- `Modify` drops objects that nothing refers to and renumbers the rest when it opens the file. The
  document you save is a new file with the same content.
- `ReadOnly` and `Import` keep the objects and their numbers as the file has them.
- `Append` keeps the numbers and also holds the original bytes in memory for as long as the
  document is open. A 200 MB file opened in this mode holds 200 MB. See
  [Incremental saving](./incremental-saving.md).

Pages can only move from a document opened in `Import` mode. A page of a document you created with
`new PdfDocument()`, or opened in `Modify` mode, cannot be added to another document. Save it and
open it again in `Import` mode. [Merge, split and assemble](./merge-split-and-assemble.md) shows how.

## When an operation needs another mode

An operation that changes the document throws `InvalidOperationException` if the mode does not
permit it. The message names the mode you used and the modes the operation needs:

```text
This document was opened with PdfDocumentOpenMode.ReadOnly and adding a page needs a document
opened with PdfDocumentOpenMode.Modify or PdfDocumentOpenMode.Append.
```

These operations need `Modify` or `Append`:

- adding, inserting, importing, duplicating, moving and removing pages;
- drawing on a page with `XGraphics.FromPdfPage`;
- resizing pages;
- adding annotations, filling in form fields and creating a form;
- setting `Version`, `PageLayout`, `PageMode` and `Language`;
- `Save`.

Two refusals have their own messages. Copying a page from a document that was not opened in
`Import` mode throws "A PDF document must be opened with PdfDocumentOpenMode.Import to import pages
from it." `SaveIncremental` on a document that was not opened in `Append` mode says why only that
mode keeps what an incremental save needs.

To ask before you try, read `document.IsReadOnly`. It is `false` only for `Modify` and `Append`.

A document with a certifying signature can refuse a change for a second reason: the certification
does not permit it. That message names the certification level rather than the open mode. See
[Digital signatures](../standards/digital-signatures.md).

## Open an encrypted document

Pass the password as the second argument:

```csharp
using PdfDocument document = PdfReader.Open("secret.pdf", "password", PdfDocumentOpenMode.Import);
```

- If you give no password, or the wrong one, `Open` throws `PdfReaderException`.
- The **user** password is enough for `Import`, `ReadOnly` and `Append`. Only `Modify` needs the
  **owner** password. With the user password, `Open` in `Modify` mode throws "To modify the
  document the owner password is required".
- `document.SecuritySettings.HasOwnerPermissions` tells you which password opened the document.

If you do not know the password in advance, pass a `PdfPasswordProvider` instead. The reader calls
it each time it needs a password, including after a wrong one. Set `Abort` to stop, and `Open` then
returns `null`:

```csharp
PdfDocument? document = PdfReader.Open("secret.pdf", PdfDocumentOpenMode.Modify, args =>
{
    Console.Write("Password: ");
    args.Password = Console.ReadLine();
    args.Abort = string.IsNullOrEmpty(args.Password);
});
```

[Encryption](../standards/encryption.md) covers passwords and permissions when you save.

## Check a file before you open it

`PdfReader.TestPdfFile` reads the header of a file, a stream or a byte array. It returns the PDF
version as a number, such as `14` for PDF 1.4 or `20` for PDF 2.0, or `0` if the data is not a
PDF. It never throws.

```csharp
if (PdfReader.TestPdfFile("upload.bin") == 0)
    return;
```

## Read damaged files

Many PDF files break the rules a little. The reader accepts these faults in every mode:

- a stream whose `/Length` is missing, too short, too long, or longer than the file itself. The
  reader finds the `endstream` keyword and corrects the length;
- an object that does not end with `endobj`;
- a reference to an object that is not in the file, which reads as `null`;
- a dictionary value that has no key in front of it;
- a cross-reference entry whose object number is off by one;
- a `startxref` that is not near the end of the file.

Files that use PDF 1.5 cross-reference streams and object streams open like any other.

For worse damage, pass `PdfReadAccuracy.Moderate`. The reader then also skips cross-reference
entries that point at the wrong object, and objects the table points at but that are not there,
instead of throwing:

```csharp
using PdfPinata.Pdf.IO.enums;

using PdfDocument document =
    PdfReader.Open("damaged.pdf", PdfDocumentOpenMode.Modify, PdfReadAccuracy.Moderate);
```

`PdfReadAccuracy.Strict` is the default. Neither setting rebuilds a cross-reference table that is
missing altogether: a file with no `startxref` does not open.

## Things to know

- **`PdfReadAccuracy` is in a namespace of its own**, `PdfPinata.Pdf.IO.enums`, with a lower-case
  `enums`. Add that `using` as well as `PdfPinata.Pdf.IO`.
- **The wrong mode fails at the call.** PDFsharp lets a `ReadOnly` or `Import` document accept
  changes it can never write. PdfPinata refuses them when you make them. If code ported from
  PDFsharp throws here, it passes the wrong mode.
- **You can save back into the file or stream you opened.** `Save(path)` overwrites the file.
  `Save(stream)` on the stream the document came from rewinds and truncates it, so the file does not
  keep the old content in front of the new. `SaveIncremental` is the exception: it needs an empty
  stream.
- **Opening a document in `Modify` or `Append` mode gives it a new revision identifier** in its
  `/ID`, and saving it stamps a new modification date. Opening it to read changes nothing.
- **Text extraction and content reading work in every mode.** Use `ReadOnly` when you only look.

## See it in action

No demo is about opening documents, but several open one. [The Assemble demo](../demos.mdx#assemble)
opens its sources in `Import` mode, and [the Revise demo](../demos.mdx#revise) opens a document in
`Append` mode twice.
