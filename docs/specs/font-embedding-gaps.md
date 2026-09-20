# Spec — the real gaps in font embedding

Embedding itself is not a gap. `PdfTrueTypeFont.PrepareForSave` (*"Fonts are always embedded"*) and
`PdfCIDFont.PrepareForSave` (*"CID fonts must be always embedded"*) both write a subset to
`/FontFile2` unconditionally, through `OpenTypeFontface.CreateFontSubSet` — managed OpenType table
surgery with no P/Invoke. It behaves identically on Windows, Linux and macOS. The upstream claim
that embedding is impossible off Windows is about `GdiGetFontData`/`CreateFontPackage`, Win32 APIs
this codebase has never called.

What is actually missing is everything *around* that: which font files can be found, which of them
can then be embedded at all, and what happens when the requested style does not exist as a file.

Decisions taken before implementation, recorded here so the reasoning below is read against them:

* **CFF is embedded whole, not subsetted** — `/FontFile3` with `/Subtype /OpenType`. See G2.
* **All six gaps are in scope**, built in dependency order: G5, G4, G2, G3, G6, G1.
* **`PlatformFontResolver` and `PlatformFontResolverInfo` are deleted**, not obsoleted. `IFontResolver`
  is untouched; `FontResolverBase` face names gain the `file.ttc#1` form.
* **A CFF `.otf` is checked in** under `src/PdfPinata.Test/Assets/Fonts`, licence alongside.

| id | gap | depends on | status |
|---|---|---|---|
| G1 | Only `*.ttf` is discovered — `.otf`, `.ttc`, `.otc` are invisible | G2, G4 | done, `7b68980` |
| G2 | CFF/OpenType-PS outlines cannot be embedded; the simple-font path throws | — | done, `e91cc8e` |
| G3 | Style simulation is implemented but never requested | — | done, `27f4279` |
| G4 | TrueType Collections throw on read | G5 | done, `555746e` |
| G5 | `GetFont` resolves a face by substring match over a string array | — | done, `0f59c89` |
| G6 | `PlatformFontResolver` is unreachable dead code | — | done, `5d7a160` |

All six are built on `feat/font-embedding-gaps`. Three things turned out differently from the
design below; each is marked *changed* where it comes up.

G1 is deliberately last in build order despite being first in the list: widening discovery *before*
G2 and G4 land would surface fonts that are found and then fail at save time, which is worse than
not finding them.

---

## G2 — CFF / OpenType-PS embedding

### What happens today

`OpenTypeFontface.Read()` recognises the `OTTO` signature and sets
`FontTechnology = FontTechnology.PostscriptOutlines`. That field is then **never read anywhere in
the codebase** — `FontTechnology` appears only in its own enum declaration and in that one
assignment. Parsing continues; `glyf` and `loca` are simply absent, because a CFF font has neither.

From there the two font paths diverge, and both are wrong:

*Simple (WinAnsi) fonts* — `PdfTrueTypeFont.PrepareForSave` calls `CreateFontSubSet`
unconditionally, whose second statement is `locaNew.ShortIndex = loca.ShortIndex`. `loca` is null.
**NullReferenceException**, from user code that did nothing but ask for an `.otf`.

*Type0 (Unicode) fonts* — `PdfCIDFont.PrepareForSave` has a guard:

```csharp
if (FontDescriptor.Descriptor.FontFace.loca == null)
    subSet = FontDescriptor.Descriptor.FontFace;   // no subsetting
else
    subSet = ...CreateFontSubSet(CmapInfo.GlyphIndices, true);
```

so it survives, but then writes the whole `OTTO` file into `/FontFile2` under a descendant font
whose `/Subtype` is hardcoded `/CIDFontType2`. Both are spec violations: `/FontFile2` is defined as
a TrueType sfnt, and `CIDFontType2` means glyf outlines. The file usually opens, because viewers
sniff the signature, but it is not a conforming PDF and strict consumers reject it.

Note this guard is the only reason `.otf` "sort of works" today when a resolver is pointed at one
by hand.

### What to build

Route CFF fonts to `/FontFile3` with `/Subtype /OpenType`, which PDF 1.6 defines for exactly this,
and set the descendant `/Subtype` to `/CIDFontType0`.

1. Expose the technology: `OpenTypeFontface.IsPostscriptOutlines => FontTechnology ==
   FontTechnology.PostscriptOutlines`, and prefer testing that over `loca == null`.
2. `PdfCIDFont` — take `/Subtype` off the constructor's hardcoded string and set it from the
   descriptor's technology at `PrepareForSave` time (the fontface is known by then; the constructor
   already receives the descriptor, so either point works — `PrepareForSave` is chosen because it
   is where the outline data is actually inspected).
3. Write `/FontFile3` + `/Subtype /OpenType` in the stream dictionary for CFF, `/FontFile2` for
   TrueType. `PdfFontDescriptor.Keys.FontFile3` already exists.
4. `PdfTrueTypeFont` — a simple font with CFF outlines gets the same `/FontFile3 /Subtype /OpenType`
   treatment (legal for simple fonts since PDF 1.6) instead of crashing.
5. Raise `PdfDocument.Version` to at least 16 when such a stream is written. The setter accepts
   12–17 and 20, so this is a clamp, not a new value.
6. Fix `TableTagNames.Cff`, which is `"CFF"` where the OpenType tag is the four bytes `"CFF "`.
   Currently unused, so this is latent rather than live, but it will bite the moment anyone seeks
   that table.

### What not to build

**No CFF subsetting.** A real one means parsing the CFF INDEX/DICT structures, the Type 2
charstrings, and the local and global subroutine sets, tracing subroutine calls per glyph, then
rebuilding all of it with renumbered indices — a self-contained project several times the size of
everything else in this spec, and the part most likely to produce silently corrupt output. The full
font gets embedded instead.

The cost is honest and should be documented: an `.otf` embeds whole. For a CJK OpenType face that
is megabytes where a TrueType subset would have been kilobytes. TrueType subsetting is unaffected.

This is a deliberate stopping point, not an oversight — it is the line between "OTF fonts work" and
"OTF fonts are as small as TTF fonts", and only the first is being claimed.

---

## G4 — TrueType Collections

### What happens today

`OpenTypeFontface.Read()`:

```csharp
if (startTag == TTCF)
{
    FontTechnology = FontTechnology.TrueTypeCollection;
    throw new InvalidOperationException("TrueType collection fonts are not yet supported by PdfPinata.");
}
```

`FontResolverInfo`'s internal constructor throws `NotImplementedException` for any
`collectionNumber != 0`, and `XFontSource` carries a `const uint ttcf` next to a bare `// TODO: ttcf`.

This matters most on the platforms where discovery is weakest anyway: most CJK system faces on
Windows and macOS ship as collections.

### Two designs, and why the second wins

**(a) Plumb `collectionNumber` end to end.** `FontResolverInfo` already has the field. It would need
to reach `IFontResolver.GetFont` so the right face comes back — but `GetFont(string faceName)` takes
a name and nothing else, and it is public API on a public interface. Changing that signature breaks
every custom resolver in the wild. So the index has to travel inside the face name *regardless*, at
which point the parallel `collectionNumber` channel is redundant plumbing.

**(b) Extract the face in the resolver.** `FontResolverBase.GetFont` returns a standalone sfnt
synthesised from the requested member of the collection. The core never sees a `ttcf` byte, so
`OpenTypeFontface`, `XFontSource`, `FontResolverInfo` and the caches are all untouched.

Design (b). `collectionNumber` stays 0 and gets a comment saying it is vestigial and why, rather
than a `NotImplementedException` that reads like unfinished work.

### What to build

A `TrueTypeCollection` helper in `src/PdfPinata/Utils`:

- `int Count(byte[] data)` — read the ttc header (`ttcf`, version, `numFonts`, then `numFonts`
  offsets from byte 12).
- `byte[] ExtractFace(byte[] data, int index)` — emit a plain sfnt: a fresh 12-byte offset table,
  `numTables` × 16 directory records with rewritten offsets, then each table's bytes, 4-byte
  aligned. Tables shared between faces in the collection are copied, not shared — that is the point
  of extracting.

Face names become `<file>#<index>` (`msgothic.ttc#1`), which G5's exact-match dictionary handles
directly and the current substring match could never have.

`head.checkSumAdjustment` will be stale in the extracted font. No viewer verifies it, the subsetter
rewrites the file anyway, and computing it correctly requires a second pass over the assembled
bytes. Left stale, with a comment saying so — the alternative is a comment saying nothing and a
reader wondering.

`OpenTypeFontMetadata.Read` already skips a ttc header but only ever reads font 0; it needs to read
a nominated index so every face in a collection can be enumerated during discovery. The ImageSharp
backend's `FontDescription.LoadDescription` does not do collections at all and needs
`LoadFontCollectionDescriptions`, which also means `FontResolverParityTest` must compare per-face.

---

## G3 — Style simulation is never requested

### What happens today

The rendering side is complete and has been all along:

- `XGraphicsPdfRenderer.cs:513-537` — italic simulation applies `Const.ItalicSkewAngleSinus` through
  a text matrix, with `AdjustTdOffset` compensating the offset while the skew is on.
- `XGraphicsPdfRenderer.cs:400` — bold simulation calls `Realize(font, brush, 2)`, PDF text render
  mode 2, fill-then-stroke.
- `FontHelper.MeasureString` widens the measurement by `Const.BoldEmphasis` per character so layout
  agrees with what is drawn.

What never happens is anyone asking for it. `FontResolverBase.ResolveTypeface` falls back through
BoldItalic → Bold → Italic → Regular → first-file-in-family and returns
`new FontResolverInfo(fileName)` — the single-argument constructor, which hardcodes both simulation
flags to false. A family shipping only a Regular file renders bold text as regular text, silently.

`FontResolverInfo`'s own XML doc says `mustSimulateBold` is *"Not implemented and must be false"*.
That comment predates the renderer support and is simply wrong now; it should go, or it will keep
deterring the next person from passing `true`.

### What to build

Rewrite the fallback so it simulates only the axis it could not satisfy from a file:

| requested | family has | use | simulate |
|---|---|---|---|
| BoldItalic | BoldItalic | BoldItalic | — |
| BoldItalic | Bold | Bold | italic |
| BoldItalic | Italic | Italic | bold |
| BoldItalic | Regular | Regular | bold + italic |
| Bold | Bold | Bold | — |
| Bold | Regular | Regular | bold |
| Italic | Italic | Italic | — |
| Italic | Regular | Regular | italic |

**Changed.** Two things had to happen that this section did not anticipate.

The flags had nowhere to go. `XGlyphTypeface.GetOrCreateFrom` built the typeface through a
constructor that takes no simulations, so `FontResolverInfo.StyleSimulations` was dropped before the
renderer could read it. Rewriting the resolver alone would have changed nothing observable — which
is presumably why nobody noticed the resolver never asked. The constructor now takes them.

And filing had to change with it. `DeserializeFontFamily` recorded a family shipping a single file
under `Regular` whatever that file actually was. Harmless while nothing was simulated, since
`Regular` was what the fallback looked for last; not harmless once the missing weight is drawn on,
because a family with only a bold face would have had that face stroked bolder still. Faces are now
filed under the style they report, which the candidate list below covers.

Two cache interactions to verify rather than assume:

- `FontResolverInfo.Key` folds the simulation flags in (`…/b+i-`), and `XGlyphTypeface.ComputeKey`
  appends its own `|b+/i-` suffix. Two typefaces differing only in simulation must land on distinct
  keys through both, or one will evict the other.
- `FontFactory.ResolveTypeface` registers the resolver info under both the typeface key and
  `fontResolverInfo.Key`. Same face file with two different simulation combinations must reach the
  "font source already exists" branch and not the `Add` that throws on a duplicate key.

Behaviour change: documents that currently render un-bolded will start rendering bolded. That is the
fix, but it moves layout, so the golden images that exercise bold or italic have to be re-approved
deliberately rather than blanket-regenerated.

In the event none moved. The resolver the tests pin ships all four Liberation faces, so nothing in
the existing suite ever falls back far enough to simulate anything. The new tests reach the
behaviour through a family that deliberately ships one face.

---

## G5 — Face lookup by substring

`FontResolverBase.GetFont`:

```csharp
ttfPathFile = _supportedFonts.ToList().First(x => x.ToLower().Contains(
    System.IO.Path.GetFileName(faceName).ToLower()));
```

A `ToList()` copy of the whole font-file array, then a linear scan, then a substring test, on every
call. It is loose (any path *containing* the name wins, so the same file name under `%SystemRoot%`
and `%LOCALAPPDATA%` is decided by array order), it allocates on a hot path, and it cannot express a
collection member at all — which makes it a prerequisite for G4, not merely an adjacent cleanup.

Build the map once in `SetupFontsFiles`, where every file is already being opened and parsed:
`Dictionary<string, string>` from face name to full path, ordinal-ignore-case. `GetFont` becomes a
lookup with a `KeyNotFoundException` carrying the face name. Collisions between identically-named
files in different directories resolve first-wins, which at least is stated rather than emergent.

`SetupFontsFiles` is public, so the map must be rebuilt whenever it is called, not merely appended.

---

## G6 — `PlatformFontResolver`

```csharp
XFontSource fontSource = null;
if (fontSource == null)
    return null;
// ...unreachable, and uses fontResolverInfo before assignment
if (fontResolvingOptions.OverrideStyleSimulations) { } else { }
FontFactory.CacheFontResolverInfo(typefaceKey, fontResolverInfo);
```

Always returns null. The remainder cannot execute. The two empty branches are the residue of the
platform-specific code this fork removed.

Its one caller is the `else` in `FontFactory.ResolveTypeface`, taken only when
`GlobalFontSettings.FontResolver` is null — and that property now throws instead of returning null.
So the call site is unreachable too.

Delete `PlatformFontResolver`, `PlatformFontResolverInfo`, and the dead branch in `FontFactory`,
which also removes the `is PlatformFontResolverInfo` test that guards the caching path. The type is
public, so this is a source-breaking change for anyone calling it — but calling it returns null and
always has, so nothing that works today stops working.

---

## G1 — Widen discovery

Last, once the fonts it will surface can be embedded.

`FontResolverBase.GetPlatformFontFiles` globs `*.ttf` on all three platforms.
`LinuxSystemFontResolver.Resolve` filters `.EndsWith(".ttf")` over *both* the fontconfig result and
the directory-walk fallback — so even where fontconfig has enumerated the machine's fonts correctly,
most of a modern Linux font set is thrown away on the way out.

Extend both to `.ttf`, `.otf`, `.ttc`, `.otc`. Collections expand to one entry per contained face
during `SetupFontsFiles`.

Discovery gets slower in proportion to how many more files it finds, and it happens on first font
use behind `EnsureInitialized`. Worth measuring on a full Windows font directory before and after
rather than assuming it is free.

**Changed.** Measured, and it was not free. On a Windows 11 font directory — 519 `.ttf` against 540
files holding 557 faces — discovery went from 170 ms to 309 ms: a 4% increase in files for an 84%
increase in time.

Most of that was not the files but how they were read. `SetupFontsFiles` asked the backend for one
face at a time, and both backends answer by opening the file, so a fourteen-face collection was
opened fourteen times. A `ReadCollectionMetadata` that reads a whole collection in one pass, which
both backends override, brings it to 238 ms. The remaining increase over the `.ttf`-only baseline is
real work: the collections on a Windows machine are the CJK faces, and they are megabytes each.

The default implementation of that method is the per-face loop, so a backend that cannot do better
keeps working; and a collection that throws under the batch falls back to the loop, so one
unreadable face still costs only itself rather than the whole file.

---

## Testing

Unit, no rasterizer needed:

- ttc extraction — synthesise a collection from the four checked-in Liberation faces, extract each,
  assert the result parses as a standalone font and its `name` table matches the original.
- Face-name map — two files with the same name in different directories resolve deterministically;
  a `#n` face name resolves to the right member.
- Style simulation — a resolver offering only Regular, asked for bold, returns
  `BoldSimulation` and a Regular face; and the key derivations stay distinct across combinations.
- CFF — a document built from an `.otf` has `/FontFile3` with `/Subtype /OpenType`, a descendant
  `/Subtype /CIDFontType0`, and no `/FontFile2`. Assert against the saved bytes.
- Regression — the same document built from a `.ttf` still emits `/FontFile2` and a subset smaller
  than the source font.

Golden image, via the existing Ghostscript harness:

- Bold and italic simulation actually change the raster. These are new reference images; the
  existing ones must be checked for movement caused by G3 rather than regenerated wholesale.

Test assets needed: a CFF `.otf`. The collection can be synthesised at test time from the Liberation
faces already checked in, which doubles as a test of the extractor. There is no `.otf` in the
Liberation family, so one has to be added.

### As built

`SourceCodePro-Regular.otf` was added under `src/PdfPinata.Test/Assets/Fonts` — SIL OFL 1.1, the same
licence as the Liberation faces, whose text now covers both. 131 KB, smaller than any of them.

Two assertions ended up doing more than planned, and one less:

* The collection test compares every table of every extracted face against the source font byte for
  byte. Reading the name back only proves the `name` table landed where the directory said, and a
  wrong offset on any other table is exactly the bug that would not show up that way.
* The CFF test compares the embedded stream against the font file byte for byte, which is the whole
  claim for an unsubsetted embed.
* The Ghostscript test asserts only that a page carrying the program renders. Ghostscript
  substitutes silently for a font it cannot use, so ink on the page does not prove which glyphs
  drew it — what it does prove is that a reader which rejects malformed font dictionaries accepts
  this one, which is what the old output failed at. Identity is settled against the bytes instead.

Suite: 280 tests, passing on `net8.0` and `net10.0`.
