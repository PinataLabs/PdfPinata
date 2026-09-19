---
title: Compression and file size
description: What each PdfDocumentOptions setting does to the size of a saved PDF, when cross-reference streams pay, and how images decide most of the size.
demos: [Compress]
---

`document.Options`, a `PdfDocumentOptions`, decides how a document is written when you save it.
None of its settings changes how a page looks. They change only how many bytes it takes to say the
same thing. The defaults already compress what matters, so most documents need no change here.

Read this page when a file is larger than you expect, when you need the smallest file you can get,
or when you want to read what the library wrote. All the settings are in the core `PdfPinata`
package, in the `PdfPinata.Pdf` namespace.

## Measure before you change anything

The Compress demo saves the same page under each setting and compares the sizes. To do the same
with your own content, save to a `MemoryStream` and read its length:

```csharp demo=Compress snippet=measure
```

Set the options straight after you create the document, before you draw anything. Some of them are
applied when an image is added, not when the document is saved.

## The settings

| Setting | Default | What it does |
|---|---|---|
| `CompressContentStreams` | `true` | Compresses the drawing operators of each page, and of forms. |
| `NoCompression` | `false` | When `true`, leaves embedded fonts, object streams and cross-reference streams uncompressed. |
| `FlateEncodeMode` | `Default` | How hard the compressor works: `BestSpeed`, `Default` or `BestCompression`. |
| `UseFlateDecoderForJpegImages` | `Never` | Whether to compress JPEG images a second time: `Never`, `Automatic` or `Always`. |
| `ColorMode` | `Rgb` | Writes every colour as RGB, as CMYK, or as it was given (`Undefined`). |
| `CrossReferenceFormat` | `Classic` | `Stream` gathers small objects into compressed object streams. |
| `MaxObjectsPerObjectStream` | 200 | How many objects go into one object stream, when `CrossReferenceFormat` is `Stream`. |

### Content streams

```csharp demo=Compress snippet=compress-content
```

Turning `CompressContentStreams` off writes each page's operators as plain text. The file is much
larger, but you can open it in a text editor and read what the library drew. Do that once when you
debug, and ship the compressed file.

`NoCompression` does not affect content streams. For a file with as little compressed as possible,
set `CompressContentStreams` to `false` and `NoCompression` to `true`. Images are compressed either
way.

### How hard to compress

```csharp demo=Compress snippet=flate-mode
```

`BestCompression` gives a slightly smaller file and takes longer to save. `BestSpeed` saves faster
and gives a larger file. The difference is small on most documents.

### JPEG images

```csharp demo=Compress snippet=jpeg-flate
```

A JPEG image is already compressed, and compressing it again rarely gains much. `Automatic` tries
it and keeps the result only when it is smaller, so it can never make the file larger. `Always` keeps
the result whatever it costs, and is meant for testing. Whether `Automatic` gains anything depends
on the picture.

### Colour mode

```csharp demo=Compress snippet=cmyk
```

`ColorMode` is not a compression setting, but it changes the size a little: CMYK writes four
numbers for each colour where RGB writes three. It also converts the colours, so a document drawn
in RGB and saved as CMYK shows the nearest CMYK colours, not the same ones. Choose it for your
printer, not for size.

## Cross-reference streams and object streams

A PDF is a set of numbered objects: one for each page, font, annotation, form field and, in a
tagged document, each paragraph and table cell. By default each object is written out on its own,
uncompressed, and listed in a plain-text table at the end of the file.

`CrossReferenceFormat.Stream` gathers up to `MaxObjectsPerObjectStream` small objects into one
**object stream** and compresses them together, and writes the table as a compressed stream too:

```csharp demo=Compress snippet=xref-stream
```

The saving depends on the shape of the document. A page of drawing is mostly one large content
stream, which cannot go into an object stream, so the setting gains little there. A document made
mostly of objects gains a lot: many short pages, many links or form fields, and above all tagged
documents. The demo measures a hundred nearly empty pages both ways:

```csharp demo=Compress snippet=many-objects
```

A larger `MaxObjectsPerObjectStream` gives a smaller file, but a reader must decompress a whole
object stream to reach any one object in it, so the file opens a little more slowly.

## Images decide most of the size

In most documents the images outweigh everything else, and no option above shrinks them much. What
matters is how you load them:

- **A PNG is stored without loss**, compressed, with its transparency as a separate mask.
- **Every other format is stored as a JPEG**, encoded again at quality 75 by default. This includes
  JPEG files, so a JPEG you load is decoded and compressed a second time.

To choose the quality, load the image through `ImageSource` with a quality from 0 to 100:

```csharp
using PdfPinata.Drawing;
using PinataLayout.DocumentObjectModel.Shapes;

XImage photo = XImage.FromImageSource(ImageSource.FromFile("photo.jpg", 60));
```

`ImageSource` is in the `PinataLayout.DocumentObjectModel.Shapes` namespace, although it ships in the
core `PdfPinata` package.

PDF stores an image's pixels, not a resolution. A 4000-pixel photograph drawn 5 cm wide is stored
with all 4000 pixels. Scale large pictures down before you load them. When a merged document holds
the same image twice, or pages name fonts they do not use, see
[Merge, split and assemble](./merge-split-and-assemble.md) for `ConsolidateImages` and
`PruneUnusedResources`. The [Images](../drawing/images.md) page covers loading images in full.

## Things to know

- **`CompressContentStreams` is `true` in every build.** In PDFsharp it was `false` in debug builds,
  so the same code wrote a larger file from a debug build than from a release build. If you compare
  file sizes with older output, check how that output was built.
- **Cross-reference streams need a PDF 1.5 reader.** Every current reader is one. Saving with
  `Stream` raises the document's version to 1.5 if it was lower.
- **PDF/A-1 refuses cross-reference streams.** PDF/A-1 is based on PDF 1.4, so a document that
  claims it cannot be saved with `CrossReferenceFormat.Stream`. PDF/A-2 and later allow it. See
  [PDF/A](../standards/pdf-a.md).
- **A document you open and save does not keep its old format.** A file that used cross-reference
  streams is saved with a classic table unless you set `CrossReferenceFormat` to `Stream`.
- **`MaxObjectsPerObjectStream` does nothing on its own.** It applies only when
  `CrossReferenceFormat` is `Stream`.
- **An incremental save cannot make a file smaller.** It only appends. See
  [Incremental saving](./incremental-saving.md).

## See it in action

[The Compress demo](../demos.mdx#compress) saves one page of text, paths and a photograph under
each setting and prints the size of each file, then compares the two cross-reference formats on a
hundred-page document.

<details>
<summary>The full Compress demo</summary>

```csharp demo=Compress
```

</details>
