# Core14MetricsGenerator

Writes the advance widths and built-in encodings of the standard 14 fonts that `PdfPinata.Fonts.Core14Metrics`
reads. Page rendering needs them for a standard 14 font whose dictionary has no `/Widths`.

```
dotnet run --project tools/Core14MetricsGenerator
```

Run it from the repository root. Options, both optional: `--afm` (default
`tools/Core14MetricsGenerator/afm`) and `--out` (default `src/PdfPinata/Fonts`). It reads the 14
`.afm` files in `afm/` and writes one file, `Core14Tables.g.cs`, which is checked in. It needs no
network: the AFM files are checked in too.

## Where the AFM files come from

Adobe's own download (`partners.adobe.com/public/developer/en/pdf/Core14_AFMs.zip`) no longer
resolves. The files here are the copies Apache PDFBox ships, taken byte for byte from the `3.0.3`
tag (commit `53ab0056d74daf7fe9f3fd426e0beb59ea615eb0`):

```
https://raw.githubusercontent.com/apache/pdfbox/3.0.3/pdfbox/src/main/resources/org/apache/pdfbox/resources/afm/<name>.afm
```

PDFBox's copies have a blank line after every line; that is theirs and is not a change made here.
`MustRead.html` is Adobe's notice from the same directory. It says the 14 AFM files "may be used,
copied, and distributed for any purpose and without charge, with or without modification, provided
that all copyright notices are retained; that the AFM files are not distributed without this file;
that all modifications to this file or any of the AFM files are prominently noted in the modified
file(s); and that this paragraph is not modified." So the two travel together: **never remove
`MustRead.html` from `afm/`, and never edit an AFM file.** The generated file carries Adobe's
copyright line.

## What it writes

Everything is indexed by the `Core14Font` enum, which the generator also writes, in ordinal order of
the PostScript names.

- Glyph names live in three sorted pools: one for the twelve text fonts, one for Symbol and one for
  ZapfDingbats. A pool is a single string with an array of start offsets, so a lookup is a binary
  search over the string without allocating.
- Each font has an array of widths indexed by its pool's glyph numbers, `0xFFFF` where the font has
  no such glyph. Fonts whose arrays are identical share one: the four Courier faces, and each
  upright Helvetica face with its oblique.
- Each font has a 256-entry array from character code to glyph number. The twelve text fonts share
  one, StandardEncoding; the generator refuses to run if their AFMs disagree about it. Symbol and
  ZapfDingbats have their own.

## Changing it

Run the generator, and check the output is the same file when nothing changed: it is deterministic,
so a diff after a rerun means the AFM files or the generator moved. If the layout of the tables
changes, `Core14Metrics.cs` is the only reader, and `Core14MetricsTests` checks widths against the
AFM files by hand-read values.
