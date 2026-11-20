# GlyphNameTableGenerator

Writes the tables that `PdfPinata.Fonts.GlyphNames` reads. Page rendering needs them to choose the
glyph of a simple TrueType font's code: the code names a glyph through `/Encoding` and `/Differences`,
and the font program finds a glyph by character.

```
dotnet run --project tools/GlyphNameTableGenerator
```

Run it from the repository root. Options, both optional: `--in` (default
`tools/GlyphNameTableGenerator`) and `--out` (default `src/PdfPinata/Fonts`). It writes one file,
`GlyphNameTables.g.cs`, which is checked in. It needs no network: its inputs are checked in too.

## The inputs

- `glyphlist.txt` is the Adobe Glyph List 2.0, from
  `https://github.com/adobe-type-tools/agl-aglfn` (`glyphlist.txt` on `master`). Its licence is
  `glyphlist-LICENSE.md`, the same repository's `LICENSE.md`, and asks that the copyright notice be
  kept. The notice is in `glyphlist.txt` itself, and the generated file names it.
- `mac-standard-names.txt` is Apple's 258 standard glyph names, in order, as the `post` table
  documentation gives them
  (`https://developer.apple.com/fonts/TrueType-Reference-Manual/RM06/Chap6post.html`). The list was
  taken from fontTools' `standardGlyphOrder.py`, which follows Apple, so the last name is `dcroat`.
- WinAnsiEncoding and MacRomanEncoding are not files. They are code pages 1252 and 10000 from .NET,
  changed where ISO 32000-1 Annex D differs: in WinAnsiEncoding 0xA0 is `space`, 0xAD is `hyphen`
  and every code above 0x20 the code page leaves undefined is `bullet`, and in MacRomanEncoding 0xCA
  is `space` and 0xDB is `currency`.

StandardEncoding is not written here: `GlyphNames` reads it from the Core14 tables, which
`tools/Core14MetricsGenerator` writes.

## What it writes

- The glyph list's names, sorted ordinally and run together into one string, with an array of where
  each begins, so a lookup is a binary search without allocating, and the character each stands for.
  A name that stands for several characters is left out, because no `cmap` has one entry for it.
- Apple's standard names, as an array.
- The character of each code of WinAnsiEncoding and MacRomanEncoding, 0 where a code stands for none.

## Changing it

The generator is deterministic, so rerunning it with nothing changed writes the same file. If the
layout of the tables changes, `GlyphNames.cs` is the only reader.
