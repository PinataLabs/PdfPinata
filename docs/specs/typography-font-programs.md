# Font programs for page rendering from PinataLabs.Typography

A decision note: whether page rendering reads embedded CFF and Type 1 font programs through
`PinataLabs.Typography` 0.1.1-alpha.26 rather than through parsers of its own, and what else in this
repository that package could replace. It covers PinataLabs/PdfPinata#379 and #380 and touches #370,
#374 and #388.

| item | what | status |
|---|---|---|
| 1 | `FontProgram` checked against this library's own output and against damaged fonts | done (2026-11-27, below) |
| 2 | Let a glyph-source factory fall back to the built-in sources, and keep the document's font cache when one is passed | not started |
| 3 | A `PageGlyphSource` over `FontProgram` for CFF (`/FontFile3` with `/Type1C`, `/CIDFontType0C` or `/OpenType`), #379 | not started |
| 4 | Type 1 and CID-keyed CFF sample PDFs in the test assets | not started |
| 5 | The same source for Type 1 (`/FontFile`), #380 | not started |
| 6 | The embedded TrueType source on `FontProgram` too, once its rasterized pages match today's | optional, not started |
| 7 | Typography publishes its Unicode algorithms as a package with no font code, PinataLabs/Typography#151 to #154 | requested |
| 8 | `src/PdfPinata/Text` replaced by that package, at the Unicode version it reports | waits on 7 |
| 9 | CFF subsetting, then TrueType, from Typography, keeping glyph IDs, PinataLabs/Typography#155 | requested |
| 10 | A managed `ITextShaper` over a shaping API that returns clusters, PinataLabs/Typography#156 | requested |
| 11 | Font descriptor metrics and `fsType`, and the standard encodings and Adobe Glyph List, from Typography, PinataLabs/Typography#157 and #158 | requested |
| — | A dependency on `PinataLabs.Typography` from the core `PdfPinata` assembly | deliberately not done |

## Context

`page-rendering.md` lists CFF and Type 1 font programs as item 10, deferred from v1. They are the
largest font items left. Every OpenType face with PostScript outlines is embedded as `/FontFile3`,
and this library's own writer produces such files, so a page this library wrote is one page
rendering cannot yet draw. Writing the parsers here would mean a CFF table reader, a Type 2
charstring interpreter, eexec decryption, a Type 1 charstring interpreter with `seac`, and the
hardening against hostile input that `EmbeddedTrueTypeGlyphSource` needed.

`PinataLabs.Typography` is PinataLabs' Apache-2.0 fork of SixLabors.Fonts, forked at `32bef429`. It
targets `netstandard2.1;net8.0;net10.0;net11.0`, the same frameworks as the shipped packages, and
depends on nothing but `System.Runtime.CompilerServices.Unsafe` on the `netstandard2.1` leg. It has
no native code.

Of its 84 public types, the one that matters here is `FontProgram`: the glyph outlines of one font,
read by glyph ID without text layout. It offers:

- **`Load(ReadOnlyMemory<byte>, FontProgramOptions)`.** Detects TrueType, CFF or Type 1. A Type 1
  font may be a PFB, a PFA or the `/FontFile` stream of a PDF. The load refuses, with
  `FontProgramTooLargeException`, any font that would retain more than `MaxRetainedBytes`.
- **`TryWriteOutline(glyphId, ref sink)`.** Writes the unhinted outline into an `IGlyphOutlineSink`,
  in glyph space with y up. `FontMatrix` maps glyph space to ems.
- **`TryGetGlyphIdForCid`, `TryGetGlyphId(name)`, `GetGlyphName`,
  `TryGetGlyphIdFromCmap(platform, encoding, code)` and `TryGetBuiltInEncoding`.** These are the
  lookups a PDF font's `/Encoding`, `/Differences` and CID mapping are built from.
- **`TryGetAdvanceWidth`, `PaintType`, `StrokeWidth`, `IsCidKeyed`, and the missing and damaged
  optional tables, for reporting.**

Its bidi and line-break implementations, `BidiAlgorithm` and `LineBreakEnumerator`, are internal.

## What was checked

Each check is a throwaway program against the published package, outside the repository.

**This library's own CFF output loads.** The conformance corpus's `pdfa-2b-cff.pdf` embeds Source
Code Pro as `/FontFile3 /OpenType`.

| input | format | glyphs | outlined | failed |
|---|---|---|---|---|
| the `/FontFile3` stream as embedded | `Cff` | 1,568 | 1,568 | 0 |
| its `CFF ` table cut out bare, as a `/Type1C` stream holds it | `Cff` | 1,568 | 1,568 | 0 |

The bare table's built-in encoding maps code 65 to glyph 2, and `TryGetGlyphId("A")` and
`GetGlyphName(2)` agree.

**Every TrueType subset in the corpus loads.** That is ten files, each embedding Arial
(Liberation Sans) as `/FontFile2`. Every glyph of each one outlines. A subset keeps the full glyph
count, 4,652, and empties the glyphs it drops.

**Damaged fonts fail cleanly.** For each of one CFF and one TrueType font, 1,500 copies were made,
with 1 to 40 random bytes overwritten and a quarter of them truncated, from one fixed seed. Each copy
was loaded under a 64 MiB limit, and its first 400 glyphs were outlined, measured and named.

| font | loaded and walked | threw | exception | hung or ended the process | slowest |
|---|---|---|---|---|---|
| CFF | 1,142 | 358 | `InvalidFontFileException` only | none | 31 ms |
| TrueType | 1,118 | 382 | `InvalidFontFileException` only | none | 12 ms |

This is a smoke test, not a fuzzing campaign: two fonts and one seed.

**Not checked.** No Type 1 font, no CID-keyed CFF font and no `seac` composite was available, in the
repository or on the machine. Item 4 exists for that reason, and it comes before item 5.

## Decision

**Adopt `FontProgram` for embedded CFF and Type 1 font programs.** The repository keeps the PDF
side: the code-to-glyph mapping from `/Encoding`, `/Differences`, the CID system and `/Widths`,
which no font library knows about. It stops owning outline decoding for those two formats. #379
becomes an adapter from `FontProgram` to `PageGlyphSource`, a couple of hundred lines by estimate,
where it was a parser and an interpreter.

**The core assembly does not take the dependency.** The core package carries no imaging or font
dependency by design, so Unity and every backend start from nothing (see `CLAUDE.md`). It does not
need one here. `PageRendering.Render` already takes a public
`Func<PdfDictionary, PageGlyphSource> glyphSources`, and `PageGlyphSource` is a public abstract
class. The adapter lives outside the core. In order of preference:

- **In `PdfPinata.Rendering.Skia`.** It is already the package that draws pages, and it already
  carries a dependency (SkiaSharp).
- **In a small package of its own.** That would let a `PageDevice` other than Skia's use it.

**That seam needs two changes first (item 2).**

- **A factory cannot fall back to the built-in sources.** `BuiltInGlyphSources` is internal, so a
  factory that knows CFF must also reimplement TrueType and the standard 14 fonts, or leave them
  undrawn.
- **Passing any factory throws the font cache away.** `PageRendering.Render` uses the document's
  `RenderFontCache` only when `glyphSources` is null. A factory therefore makes every render read
  its fonts afresh, which a multi-page render will notice.

**Start with CFF.** It is the format this library writes, it is the one verified above, and it is
item 10's more urgent half.

## What it does not replace yet

**The text algorithms in `src/PdfPinata/Text`.** The package has its own bidi and line breaking,
but they are internal. This repository's versions are pinned to Unicode 17.0.0. They are checked
against the full UAX #9 and UAX #14 conformance suites, and they carry tailorings that layout
depends on: South-East Asian letters as ideographs, and Hangul as letters (`line-breaking.md`).
Replacing them with what is public today would lose all three. The next section asks Typography for
what replacing them would take.

**HarfBuzz, for now.** A shaper with no native code would suit Blazor WebAssembly and Unity, and the
package does apply GSUB and GPOS. But its public layout API hands a glyph renderer a glyph ID and a
code point, not the character-to-glyph cluster map that `ShapedGlyph.Cluster` is. Three things read
that map: `CMapInfo.AddShapedRun`, `PdfToUnicodeMap` and word spacing. A shaper built on the current
API would write a `/ToUnicode` that is wrong for ligatures. PinataLabs owns the package, so exposing
clusters there is the way to reopen this. That is a guess, and was not tried.

**Fonts that are not outlines.** Type 3 glyphs (#381) are PDF content streams, drawn by the
interpreter.

## Expanding PinataLabs.Typography

Most of the font and text code in this repository duplicates what Typography already has, but keeps
internal. This section records what Typography would have to expose or add for this repository to
depend on it rather than grow, and the decision about where that dependency may live. Each item is
a feature request on PinataLabs/Typography. Recorded 2026-11-27.

### Decision: split the Unicode algorithms into a package with no font code

Most of the duplicated code is in the core assembly, which by design takes no font dependency.
There were two ways through that:

- **Allow the core to take `PinataLabs.Typography`.** It is PinataLabs' own, Apache-2.0, targets
  `netstandard2.1` and has no native code. But it would bring a font library into every consumer
  of the core, Unity included, and the rule exists so that a consumer picks its font stack.
- **Ask Typography to publish its Unicode algorithms as a package of their own**, with no font code,
  which `PinataLabs.Typography` then depends on. The core can take that package without bending the
  rule, because bidi, line breaking and script detection are text processing, not fonts.

**The split is chosen** (PinataLabs/Typography#151). The font-level items below stay outside the
core, as the font programs above do. They go in `PdfPinata.Rendering.Skia`, the backends, or a
package of their own.

### The Unicode package: what it must carry

| request | what PdfPinata needs | why |
|---|---|---|
| Typography#151 | The package, and a public `UnicodeVersion` | A caller pins its conformance suites to the version, so it has to be told what it is. |
| Typography#152 | Bidi: levels and runs in visual order for a paragraph, with U+200C and U+200D kept inside their run | The two layout engines, `DrawString` and `MeasureString` all reorder through it. The joining controls are what tell a shaper how letters join (`text-shaping-and-bidi.md`). |
| Typography#153 | Line breaking: an enumerator, a question about one boundary between two runs, and two tailorings | PinataLayout asks about the seam between two runs (`ParagraphRenderer.IsJoinedTo`). It treats South-East Asian letters as ideographs and Hangul as letters (`line-breaking.md`). |
| Typography#154 | Script: the property per code point, ISO 15924 codes, and an itemiser | `TextItemizer.Itemize` cuts text into runs of one script and one direction before shaping. |

**The two sets of tables already disagree.** `CodePoint.GetBidiClass` and
`CodePoint.GetLineBreakClass` in 0.1.1-alpha.26 were compared with this repository's Unicode 17.0.0
tables across all 1,112,064 scalar values (measured):

| property | code points that differ | example |
|---|---|---|
| bidi class | 112 | U+2E60, `L` here and `ON` in Typography |
| line-break class | about 1,690 | U+18D1F, `XX` here and `ID` in Typography |

Most of the differences are code points that Typography gives a class and that this repository
treats as unassigned. So the two appear to be built from different Unicode versions (inferred).
The package does not report its version, which is why #151 asks for it. This is drift between two
PinataLabs implementations of one standard, and every Unicode bump is done twice today.

**What switching over takes, once the package exists (item 8).**

- Move to the Unicode version it reports. That moves the four things `CLAUDE.md` says move
  together: the tables, the line-break tables, the gzipped conformance suites and the version
  asserted in `UnicodePropertyTests`.
- Keep `BidiConformanceTests` and `LineBreakConformanceTests`, run against the package, as the proof
  that the switch held.
- Delete `src/PdfPinata/Text`'s algorithms and generated tables (4,286 lines, 1,908 generated) and
  `tools/UnicodeTableGenerator`.
- The public types in `PdfPinata.Text`, `BidiParagraphDirection` among them, stay, as thin types
  over the package, or are retired through the usual deprecation. That choice belongs to the PR
  that makes the switch.

### Font-level requests, outside the core

| request | what it gives | what it saves or fixes here |
|---|---|---|
| Typography#155 | Subsetting, CFF first, then TrueType, keeping glyph IDs, with composite, `seac` and subroutine closure | **Fixes a gap:** a CFF font is embedded whole today (`font-embedding-gaps.md`). TrueType could follow, retiring the subsetting half of `Fonts.OpenType` (about 4,700 lines with the reading code). Glyph IDs have to stay put, because `/CIDToGIDMap /Identity` is written and PDF/A checks it. |
| Typography#156 | Shaping that returns each glyph's cluster, advance and offset in design units | A managed `ITextShaper` with no native code, for Blazor WebAssembly and Unity, beside or instead of `PdfPinata.HarfBuzz`. Clusters are what `/ToUnicode` is written from. Whether it matches HarfBuzz needs a side-by-side comparison. |
| Typography#157 | Font descriptor metrics and `OS/2` `fsType` | Parts of `OpenTypeDescriptor`, and the embedding permissions `font-embedding-restrictions.md` enforces. |
| Typography#158 | The standard encodings and the Adobe Glyph List | The 1,324 lines of `Fonts/GlyphNames`. Type 1 support in Typography needs Standard Encoding anyway. |

Where the core needs one of these, such as `fsType` when writing a font, it is reached through a
seam, as the glyph sources are, or the item waits for a decision of its own.

**Already possible, with no change to Typography:**

- A backend-free `IGlyphOutlineProvider` over `FontProgram.TryWriteOutline`.
- Replacing SixLabors.Fonts in the ImageSharp backends' font resolver and outline provider (471
  lines).

**What stays here regardless:** the Core14 AFM widths, and Type 3 glyphs. Both are PDF-specific.

## Consequences

**Issues that benefit.**

| issue | how much | why |
|---|---|---|
| #379 CFF font programs | most of it | The parser, the charstring interpreter and CID-keyed support come from the package. |
| #380 Type 1 font programs | most of it, unverified | eexec, the charstring engine and the built-in encoding come from the package. |
| #370 page rendering | indirectly | The umbrella over #379 and #380. |
| #388 text extraction on the interpreter | a little | Built-in encodings and glyph names help map a code with no `/ToUnicode`. `Fonts/GlyphNames` already holds the Adobe Glyph List. |
| #374 standard 14 through the resolver | a little | Its TrueType prerequisite is done. `FontProgram` would also let a resolver serve an OpenType CFF face. |

**No benefit:** #368, #381, #383, #384, #385, #389, #423, #445 and #350, which are not about font
programs.

**Code that could go.** `EmbeddedTrueTypeGlyphSource` and `TrueTypeOutlinePaths`, about 600 lines,
could move onto `FontProgram`, so that one parser serves all three formats (item 6). That code works
and is hardened, so this is optional. It should happen only when the rendering-fidelity tests
against Ghostscript show no change.

**Not tied to an issue.** The ImageSharp backends take SixLabors.Fonts for font scanning and glyph
outlines, at 1.0.1 for 2.x and at 3.x for `PdfPinata.ImageSharp4`. The package could replace both,
and would take the Six Labors licence off the 4.x package's font side. Nothing here measured that.

**Costs.**

- **The package is an alpha.** NuGet warns when a stable package depends on a pre-release one
  (NU5104), so whatever package takes the dependency stays a pre-release until Typography ships a
  stable version. `PdfPinata.Rendering.Skia` is a pre-release already (#378), which is why it is
  the first candidate.
- **Its `.nuspec` sets `requireLicenseAcceptance`.** A consumer installing it through Visual Studio
  is shown a licence prompt.
- **A bug in outline decoding becomes a fix in another repository.** It is PinataLabs' own, so the
  release follows the same hands, but a fix is two releases rather than one.
- **Process-ending input stays this repository's problem.** The smoke test above found nothing, but
  a font that overflows the stack ends the process wherever the parser lives. The interpreter's
  tests for malformed input should cover the adapter as they cover the TrueType source.
