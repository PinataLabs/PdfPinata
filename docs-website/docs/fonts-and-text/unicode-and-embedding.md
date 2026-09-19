---
title: Unicode and font embedding
description: The two font encodings, how fonts are embedded and subset, copying text back out, and what DrawString does with control characters.
demos: [Unicode]
---

Every `XFont` has an encoding, and the encoding decides which characters the font can carry into the
PDF. There are two: **Unicode**, the default, and **WinAnsi**, a single-byte encoding with room for
256 characters. This page explains the difference, how PdfPinata embeds the font file, and what
happens to control characters in the strings you draw.

## Choose an encoding

Pass `XPdfFontOptions` as the last argument of the `XFont` constructor. There is no property to
change the encoding afterwards:

```csharp demo=Unicode snippet=encodings
```

| | `XPdfFontOptions.UnicodeDefault` | `XPdfFontOptions.WinAnsiDefault` |
| --- | --- | --- |
| Characters | Any character the font has a glyph for | The 256 characters of Windows code page 1252 |
| Written as | A composite (CID) font that refers to glyphs by number | A simple font with one byte per character |
| Reordering, shaping and fallback | Yes | No |
| Copy and paste | Through a `/ToUnicode` map | Through the standard WinAnsi encoding |

WinAnsi covers English and most Western European languages, but not Greek, Cyrillic, Polish, Czech,
Turkish, arrows or most mathematical symbols. The demo draws the same strings in both encodings, side
by side:

```csharp demo=Unicode snippet=side-by-side
```

Unicode is the right choice for almost every document. If you do not pass options, `XFont` uses
`GlobalFontSettings.DefaultFontEncoding`, which is `PdfFontEncoding.Unicode` unless you change it.

### PinataLayout uses WinAnsi unless you ask

`PdfDocumentRenderer` picks the encoding for every font in a PinataLayout document. Its
parameterless constructor picks WinAnsi. Pass `true` to get Unicode:

```csharp
var renderer = new PdfDocumentRenderer(true);   // Unicode encoding for all text
```

## How fonts are embedded

PdfPinata always embeds the font file, whichever encoding you choose. What goes into the PDF depends
on the kind of outlines the font has:

- **TrueType outlines**, as in most `.ttf` files, are subset. Only the glyphs the document uses go
  into the file, so a document with one word in it carries a small part of the font.
- **PostScript (CFF) outlines**, as in many `.otf` files, are embedded whole. A document that uses
  one character carries the same bytes as one that uses them all.

The second page of the demo saves five small documents and reads back the font object each one wrote.
The embedded TrueType font holds only the few glyphs the word needs. The embedded CFF font is the
whole file.

Chinese, Japanese and Korean text works with the Unicode encoding and a font that has the glyphs. CJK
fonts are large, and CJK fonts with CFF outlines are embedded whole, so each document that uses one
can grow by several megabytes. If a TrueType version of the font exists, use it.

## Copying text back out

A reader can select, search and copy the text in a PdfPinata document. Each Unicode font carries a
`/ToUnicode` map that says which characters each glyph stands for. The map covers glyphs from a
shaper too, so a ligature copies out as the two or three letters it replaced. WinAnsi fonts use the
standard encoding, which readers already understand.

To read text out of a PDF in your own code, see [Text extraction](../existing-pdfs/text-extraction.md).

## Control characters

`DrawString` and `MeasureString` filter every string the same way before they use it:

- A tab becomes a single space.
- Every other character below 32 is dropped, including the line feed (`\n`) and the carriage return
  (`\r`).

`DrawString` always draws one line. A line feed does not start a new line; the words either side of
it run together. It does not wrap long text either:

```csharp demo=Text snippet=no-wrap
```

`MeasureString` treats a line feed differently: it splits the string there and reports the height of
all the lines. For text with more than one line, use [XTextFormatter](../drawing/text-layout.md),
which breaks at line feeds and wraps to a width, or a PinataLayout paragraph.

## Things to know

- **A character WinAnsi cannot hold is lost without an error.** It is dropped or replaced when the
  document is written. You find out when somebody reads the document.
- **Set `DefaultFontEncoding` once, before the first font.** Creating an `XFont` without options
  reads the setting, and reading it fixes it at Unicode. A later attempt to set a different value
  throws `InvalidOperationException`. Setting the same value again does nothing.
- **There is no way to turn embedding off.** Every font the document uses is in the file. This is
  also what PDF/A requires.
- **Characters above U+FFFF work**, for example many emoji, if the font has glyphs for them.
- **`XPdfFontOptions` takes only an encoding.** It has no `PdfFontEmbedding` argument, because every
  font is embedded. See [Migrating](../reference/migrating.md).

## See it in action

[The Unicode demo](../demos.mdx#unicode) draws the same strings in both encodings, then saves small
documents and reads back which kind of font object and which embedded font file each one wrote.

<details>
<summary>The full Unicode demo</summary>

```csharp demo=Unicode
```

</details>
