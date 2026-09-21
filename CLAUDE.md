# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```powershell
dotnet build src/PdfPinata.slnx                     # SDK is pinned to 10.0.100 by global.json
dotnet test src/PdfPinata.slnx                      # whole suite, both test target frameworks
dotnet test src/PdfPinata.slnx -f net10.0           # one framework; the test project targets net8.0;net10.0
dotnet test src/PdfPinata.slnx --filter "FullyQualifiedName~CLexerTests"                  # one class
dotnet test src/PdfPinata.slnx --filter "FullyQualifiedName~CLexerTests.ScanNextToken"    # one test
./verapdf-check.ps1                                 # conformance corpus + veraPDF; needs Docker. Gates.
./verapdf-check.ps1 -NoGate                         # the same, but always succeeds — for reading a failure
```

Every project, and the solution, lives under `src/`; the build props and targets, `global.json`,
`tools/` and `docs/` stay at the root. Name the solution explicitly — a bare
`dotnet test` at the root finds nothing to run.

CI (`.github/workflows/build-and-test.yml`) runs on Linux only, builds `src/PdfPinata.slnx` in Release,
installs Ghostscript, then runs `dotnet test` with coverlet/opencover coverage, which goes to Codecov
and, with the Debug build it ran against, to SonarCloud (`PinataLabs_PdfPinata`).

**Versions come from git tags, not from the project files.** MinVer (referenced for packable
projects in `Directory.Build.targets`) versions all nine packages in lockstep from the latest
`v1.2.3` tag, so no csproj carries a `<Version>`; an untagged commit is a pre-release. Releasing is
the *Publish Release* workflow (`release-publish.yml`): it takes the version Release Drafter has
drafted from merged PRs' labels, rolls `CHANGELOG.md`'s `[Unreleased]` section, tags, and calls
`release.yml` to pack and push to NuGet. The labels come from a Conventional Commits prefix on the
**PR title** (`feat:`, `fix:`, `feat!:` …) — commit subjects keep the plain-prose convention below.
A new packable project has to be added to `PACKAGES` in both `release.yml` and
`release-prerelease.yml`.

**veraPDF runs the same script CI does, and it gates.** `ConformanceCorpus` writes one PDF per claim
the library can make into `artifacts/conformance-corpus`; each document *makes* a claim, because
flavour detection is automatic and a file claiming nothing would be held to the fallback flavour and
fail for saying nothing. All nine conform, so a failure is a regression. **No document claims
PDF/UA-2**: one would fail a single clause, structure destinations, whose syntax the standard's own
errata leave undefined — `docs/specs/conformance-completeness.md` says why none is written.

Its first run found three defects, all in the writer and none reachable by any test here — worth
knowing because two of them are easy to reintroduce. **`/Length` counts the stream data alone**: the
end-of-line marker before `endstream` sits outside the count and `PdfWriter` writes it
unconditionally, where it used to skip it when the data already ended with a newline, leaving the
data's last byte to serve as the separator. **A Type 2 CIDFont says `/CIDToGIDMap /Identity` out
loud**, because PDF/A and PDF/UA require what ISO 32000-1 leaves to a default. And **`/CIDSet` is
written for PDF/A-1 alone**, the one profile that asks for it. `StreamLengthTests` and
`CidFontConformanceTests` pin all three.

One document sets a page in a face with **PostScript outlines**, which is the one path where a
CID font is embedded whole rather than subsetted — so it carries no `/CIDToGIDMap` and is not named
as a subset, both the opposite of every other document.

`docs/specs/verapdf-validation.md` has the rest. The entry worth carrying is that **the corpus sets
no output intent at all**: `src/assets/icc/sRGB-v2-micro.icc` is embedded by the *core* package and an
RGB document claiming PDF/A that names no profile is given it, so what veraPDF passes here is the
default path rather than a profile the corpus built for itself. It was built in code for several
months, and `SrgbProfile.cs` is gone. **A CMYK or `Undefined` document is still refused** — the same
four CMYK numbers are a different colour on every press — and that asymmetry is the whole of the
rule, in `PdfConformanceWriter.Enforce` and `PdfOutputIntents`.

There is no lint or format step in the build or in CI.

**Judge a test run by its exit code, not by the word `Passed`.** Every rasterization on Windows
runs Ghostscript *inside* the test host, and its way of giving up is to end the process — so a
document it will not draw reads as `Test host process crashed`, exit 1, no failing test, and a
`Passed!` line printed anyway with a total short of what was discovered. Reading the tail of
`dotnet test` reports such a run as green. A run whose total is below what `dotnet test
--list-tests` finds did not pass; it stopped. `--blame-crash` then names the test that never
finished, and **whether that name repeats is the diagnosis**: the same one twice is a document to go
and look at, and it settled this twice. A different name every run is the machine rather than the
suite — the third episode crashed five runs in eight at a tenth of the memory of the first two, was
reproduced on a commit predating everything suspected, and then stopped for good with nothing
changed. Re-run before believing it. See `docs/specs/test-host-crash-investigation.md`.

A lexer or parser change can hang the test host rather than fail it. Tests that scan malformed
input carry `[Fact(Timeout = …)]`, which xUnit honours only on `async` tests — hence the
`Task.Run` wrappers in `CLexerTests`.

## Layout and dependency direction

```
PdfPinata ─────────────┬── PdfPinata.Skia        (SkiaSharp; the default backend)
   (no imaging or font    ├── PdfPinata.ImageSharp   (ImageSharp 2.1.x, Fonts 1.0.1)
    dependency of its own)├── PdfPinata.HarfBuzz     (HarfBuzzSharp; shaping, either backend)
                          ├── PdfPinata.Signing      (CMS signing; net8.0;net10.0 only)
                          └── PdfPinata.EInvoice     (Factur-X / ZUGFeRD; no dependency at all)
   ▲       ▲
   │       └── PinataLayout.DocumentObjectModel ── PinataLayout.Rendering ── PdfPinata.Charting
   │              ▲                                                              ▲
   │              └── PinataLayout.DocumentObjectModel.Tests  (the DOM alone; no backend)
   │                                                                             │
   │                             PdfPinata.Charting.Tests  (charting alone; no backend) ─┘
   └── PdfPinata.Test  (the broad one; covers PinataLayout and SampleApp too)
           ▲
           └── SampleApp  (the demonstration CLI; net8.0 alone, so both test legs can reference it)
```

Five test projects, and which one a new test belongs in is worth a moment. `PdfPinata.Test`
is the broad one and the default. `PinataLayout.DocumentObjectModel.Generators.Tests` drives the
DOM's source generator through `CSharpGeneratorDriver`. `PinataLayout.Rendering.Tests` covers
PinataLayout's own layout — paragraphs, tables, fields, the paragraph iterator — and its tagged output,
and deliberately rasterizes nothing, so it needs neither Ghostscript nor ImageMagick. It links four
content-stream readers out of `src/PdfPinata.Test/Helpers` rather than keeping copies; edit those in
place and both projects get the change.

`PdfPinata.Charting.Tests` covers the charting renderers — axis scales, category axes, plot
areas, data labels, axis titles — and links three of those same readers. Every renderer in the
package is `internal` and this repository carries no `InternalsVisibleTo`, so they are reached the
only way a caller can reach them: a `Chart` handed to a `ChartFrame`, drawn, saved, reopened, and
read back out of the content stream. The three helpers under `Helpers/` are what make that legible —
`Drawn` puts a chart on a page, `PaintedRectangles` reads the `re` operators the columns and bars
are drawn with, and `ShownText` turns the Identity-H glyph runs back into text through the font's
own `/ToUnicode` map, so a test can assert `"0.0"` rather than a glyph number. Like the DOM's tests
it references no backend: a chart draws lines, rectangles, wedges and strings, so of the three
static seams it reads only `GlobalFontSettings.FontResolver`.

Writing those tests turned up seven defects, all reachable through public API and all since fixed:
`docs/specs/charting-renderer-findings.md` sets out each with the code, the fix and the test that
pins it. Two are worth carrying in your head, because both were one renderer having a guard its
twin lacked, and the pairs are still near-copies of each other. **The category axis renderers are
copies of one another** — `HorizontalXAxisRenderer` and `VerticalXAxisRenderer`, and likewise the
horizontal and vertical Y renderers — so a change to one nearly always belongs in the other.
**A blank is a null**, both in a series (`Series.AddBlank`) and in a category series
(`XSeries.AddBlank`); read a point's value through `PointRendererInfo.Value`, which answers `NaN`
for one, rather than through `point.value`, which throws.

`PinataLayout.DocumentObjectModel.Tests` covers the DOM itself — `Unit`, page sizes, the chart
object model, MDDDL reading and writing, and the flattening visitors. It references the DOM **and
nothing else**: no renderer, and so no backend, no Ghostscript and no font files. The one
qualification is `NamedFontsOnly.cs`, a module initializer serving a font *name*, because building
a `Document` builds its standard styles and the Normal style asks the resolver what the default
font is called. It resolves no face and throws if asked to, which is the line saying a test needing
a real font belongs in `PinataLayout.Rendering.Tests`. Note that `src/PdfPinata.Test/Dom/` also
covers the DOM, from the other side: the value model, colours, styles and the generated property
machinery. The two do not overlap.

`SampleApp` is the demonstration app: `dotnet run --project src/SampleApp -- list` says what it covers,
`… -- run` writes one PDF per demo into `src/SampleApp/output` and prints the source that drew each. Its
demos are covered by `src/PdfPinata.Test/Demos/DemoSmokeTests.cs`, so a demo that throws or changes
its page count fails the build.

Three rules there are load-bearing rather than stylistic, all explained in
`docs/specs/demonstration-app.md`: **a demo never registers a backend** (the smoke test runs demos
inside a test host that has already installed `PinnedFontResolver`, and
`GlobalFontSettings.FontResolver` throws once a font has been used — which is also why
`Backends.EnsureRegistered` rather than a demo sets `TextShaper` and `FontFallback`, and why no
demo's page count may depend on either); **its fonts, images and sources are embedded resources, not content files** (a referenced
project's content items do not reach the referencing project's output directory); and **a demo whose
output `Save` would destroy overrides `PdfDemo.Save`** — `Save` rewrites a file from the object
model, which invalidates every signature and discards every earlier revision, so `Signing` writes
through `PdfSigner.Sign` and `Revise` through `SaveIncremental`.

`SampleApp` references `PdfPinata.HarfBuzz` and `PdfPinata.Signing` for one demo each. Neither
is a dependency the library forces on a consumer, and both are written out in the project file so
that what those two demos cost is visible.

**The demos are also the documentation website's code.** `docs-website/` is a Docusaurus site
(pnpm, Node 22) published to GitHub Pages by `.github/workflows/docs.yml`; `docs-website/README.md`
has the commands. It keeps no copies of code: a block written ```` ```csharp demo=Protect snippet=encrypt ````
is filled at build time with the lines between `// docs:begin encrypt` and `// docs:end encrypt` in
`ProtectDemo.cs`, and a missing excerpt fails the site's build. So **renaming or deleting a marker
line breaks a page**, and moving code across one changes what a page shows — grep `docs-website/docs`
for the excerpt's name first. `DemoSource` drops the marker lines from the source the app prints,
and `DemoSmokeTests` checks every excerpt is begun once and ended. The workflow also runs the
SampleApp and Ghostscript to put every demo's PDF and first page in the gallery, so a new demo in
`DemoRegistry` appears there with no further step; a page claims it with `demos: [Name]` in its
front matter. The old `docs/PdfPinata` and `docs/PinataLayout` pages now live on the site;
`docs/specs` stays where it is.

The core package deliberately carries no imaging or font dependency. All five seams are static, and
the first three throw a descriptive `InvalidOperationException` when read unset:

- `GlobalFontSettings.FontResolver` — an `IFontResolver`; backends supply `SkiaFontResolver` /
  `ImageSharpFontResolver`, both built on `Utils/FontResolverBase`. Must be set before any font is
  created: the setter throws once one has been.
- `ImageSource.ImageSourceImpl` — an `ImageSource`; backends supply `SkiaImageSource` /
  `ImageSharpImageSource`. Must be set before any image is loaded.
- `GlobalFontSettings.GlyphOutlineProvider` — an `IGlyphOutlineProvider`; backends supply
  `SkiaGlyphOutlineProvider` / `ImageSharpGlyphOutlineProvider`. Wanted by `XGraphicsPath.AddString`
  and nothing else, so it can be set, replaced or cleared at any time. A provider takes its font
  bytes **through** `FontResolver` rather than resolving a family itself, or the two seams will
  disagree about which face a family means.
- `GlobalFontSettings.TextShaper` — an `ITextShaper`; `PdfPinata.HarfBuzz` supplies
  `HarfBuzzTextShaper`. **One of the two seams whose unset state is not an error**: reading it
  answers null, and then every path does what this library always did, one character to one `cmap`
  lookup to one glyph. It can be set, replaced or cleared at any time, and a shaper that returns
  null for a run has declined it rather than failed, so the unshaped result stands.
- `GlobalFontSettings.FontFallback` — an `IFontFallback`, which says which families to try for a
  character the chosen face has no glyph for. **The other seam whose unset state is not an error**:
  null means a missing glyph stays `.notdef`, exactly as it always was. `FontFallbackList` is the
  whole of what most documents need. Reading it answers the registered `FontResolver` when that
  resolver implements `IFontFallback` too, so a resolver that already knows what is installed need
  not be registered twice. **Nothing about coverage is looked at while it is null** — that is what
  keeps the common path free.

Note the asymmetry between the last two and `FontResolver`: a new member on `IFontResolver` would
break every consumer who has written one, and netstandard2.1 rules out a default interface method
Unity's runtime would accept. That is why capability keeps arriving as a seam of its own rather
than as a wider resolver, and it is the answer to "why is this not just on `IFontResolver`".

`PdfPinata.HarfBuzz` is a package of its own rather than a class in a backend, because shaping
must not oblige a consumer to pick an imaging backend — `PdfPinata.ImageSharp` is pinned to
SixLabors.Fonts 1.0.1 for licence reasons and cannot shape for itself. It takes `HarfBuzzSharp`
alone, not `SkiaSharp.HarfBuzz`.

Everything that turns a character into a glyph goes through the internal `Fonts/TextShaping` — both
`FontHelper.MeasureString` and `XGraphicsPdfRenderer.DrawString`, so that the glyphs measured are the
glyphs drawn. A shaper is handed a `ShapingFont`, not an `XFont`, because the typeface and font source
are internal; it carries the already-resolved face and its bytes, so a shaper cannot disagree with the
renderer about which file a family means. Advances are in **font design units**, read against
`ShapedRun.UnitsPerEm`.

Both of them also filter the same characters first, through `Fonts/TextNormalization`: **a tab is a
single space and every other character below 32 is dropped**, `\n` and `\r` included. It runs
*before* `ShapeText` in both callers rather than inside it, because both keep using the original
string afterwards — for the text a run stands for, for `/ToUnicode` and for the WinAnsi bytes — so a
shaper filtering underneath them would answer cluster indices into a string nobody else has.
`DrawString` normalizes once, before the `font.Unicode` branch, and returns early when nothing
survives. **It does not split on `\n`**: it draws one line and drops the newline, while
`MeasureString` still splits and reports several — the one remaining disagreement, pinned on purpose
by `TextStateOperatorTests.ALineFeedIsAbsorbedByDrawStringWhileMeasureStringStillReportsTwoLines`.
Multi-line text is `XTextFormatter`'s job and PinataLayout's, and both split before `DrawString` is
reached.

Both of those call `TextShaping.ShapeText`, not `TextShaping.Shape`: a *string* is not a run, and
`ShapeText` is where it is cut into runs — one direction and one script each — and each is shaped on
its own terms. It answers a `ShapedText`, a list of `ShapedSegment` in **visual order**. Drawing them
back to back is all that reordering takes, because PDF has no notion of direction: a show-text
operator paints glyphs at the pen and moves the pen along. **A string of characters all below
`U+02B0` skips itemisation entirely** — everything there is Latin or Common and left to right, so
there can only be one run — which is what keeps the common path free of a bidi resolution and a
string copy, and what kept every existing golden image exactly where it was.

**`ShapedGlyph.Cluster` is load-bearing.** It is the character↔glyph map, and three separate things
read it. `CMapInfo.AddShapedRun` — which *replaces* `AddChars` on the Unicode path, because the
characters' own glyphs are not the ones drawn — records both the glyphs to embed and give widths to
and what each of them stands for. `PdfToUnicodeMap` writes a one-character glyph as a `bfrange` and a
several-character one as a `bfchar`, because a `bfrange` destination is a single code and cannot say
that one glyph swallowed two characters. And `XGraphicsPdfRenderer.ShowTextOperators` reads it to put
a word spacing after the space's *last* glyph rather than at the space's character index.

A glyph a shaper wants displaced is written as `-dx … +dx` inside a `TJ` array for the horizontal and
with `Ts` for the vertical — nothing else in the renderer writes `Ts`, so it is zero on entry and is
put back. A run needing no displacement is still a plain `Tj`, which is what keeps every existing
document byte-identical.

## Bidi and script itemisation

`src/PdfPinata/Text/` holds the Unicode Bidirectional Algorithm (UAX #9) and script itemisation
(UAX #24). Pure text processing, no font and no backend, which is why it is in the core rather than
behind the shaping seam. `TextItemizer.Itemize` is the entry point worth knowing: it hands back runs
that are each one direction **and** one script, in the order they are drawn — which is exactly what
`ITextShaper.Shape` takes. `TextShaping.ShapeText` is its one caller inside the library, and through
it every `DrawString` and every `MeasureString` reorders.

**A joining control is inside a run, not between two.** U+200C and U+200D are bidi class `BN` and so
removed by rule X9, which is right for ordering and was wrong for shaping — they are exactly the
characters that tell the face how the letters either side of them join. `BidiResult.Runs` therefore
reaches over one rather than breaking at it, and `TextShaping.Unshaped` skips it explicitly so that
no glyph is drawn for a character that is zero width by definition. Both halves are load-bearing, and
it is only those two characters, not the whole of `BN`: `UnicodeProperties.IsJoiningControl` says
which, and widening it would change what existing documents look like.

**Reordering is not shaping, and does not need a shaper.** `TextShaping.Unshaped` reverses a
right-to-left run, because `ShapedRun` promises visual order and the renderer relies on it — so a
consumer who takes no HarfBuzz dependency still gets Hebrew and Arabic the right way round, unjoined.
That is the older half of the complaint in `empira/PDFsharp-1.5#144` and it is fixed in the core.

A layout engine that places each word itself has to order them, and two do. `XTextFormatter` hands
whole lines to `DrawString` for every alignment but one, so only justifying needed changing.
`src/PinataLayout.Rendering/ParagraphRenderer.cs` draws one show-text operator per leaf and needed the
most: **it walks each line twice**, once with `probing` set to learn how wide every leaf is without
drawing anything, then again for real with each leaf placed where the bidirectional algorithm says.

Both order a word by **the leftmost position any of its characters ends up at**, not by its first
character — a right-to-left word's first character is its rightmost. That is also what keeps an
English phrase inside a Hebrew sentence in its own order, where reversing the line turns it round.

Two things about the PinataLayout pass are load-bearing. **The second walk is still in the order the
leaves were written** — only the x changes — so the marked content stays in reading order, which
is what a structure tree is for; `TheMarksStayInTheOrderTheTextIsRead` asserts both orders at once.
And **a line with a tab in it is reordered one segment at a time** — the tabs stay put and the text
between them is ordered on its own — with the tab-width list replayed for the second walk, and
`RenderTab` drawing nothing while `probing`. While reordering, the underline, strikethrough and
hyperlink rules are drawn per leaf rather than per stretch, or one rectangle would run backwards
across the line.

`ParagraphFormat.TextDirection`, `XTextFormatter.TextDirection` and `XStringFormat.TextDirection`
all take `BidiParagraphDirection` — one type, not three saying the same thing.
`Drawing/Layout/BidirectionalLayoutTests.cs` and `src/PinataLayout.Rendering.Tests/BidirectionalParagraphTests.cs`
pin the two engines.

The character property tables are **generated and checked in** — `tools/UnicodeTableGenerator`,
deliberately outside `PdfPinata.slnx` so the build and CI never see it, run by hand on a Unicode
bump. Read its README before touching them; the short version is that `DerivedBidiClass.txt`'s
`@missing` lines live inside comments, are not all `Left_To_Right`, and are what make unassigned code
points in the Hebrew and Arabic blocks default to `R` and `AL`.

Everything is pinned to **Unicode 17.0.0**, and three things move together on a bump: the generated
tables, the gzipped conformance suites in `src/PdfPinata.Test/Assets/Unicode/`, and the version
asserted in `UnicodePropertyTests`. Bumping one without the others tests one Unicode against
another's expectations.

`BidiConformanceTests` runs `BidiTest.txt` and `BidiCharacterTest.txt` in full — 861,948 cases, about
two seconds — as one `[Fact]` per suite rather than a theory per case, because half a million xUnit
cases is a denial of service on the runner rather than a test run. If a change to the algorithm
breaks something, that is what says so, and it reports the failing case in UAX #9's own rule terms.

`docs/specs/text-shaping-and-bidi.md` has the rest, including what is still missing: font fallback,
the measurement paths, and the DOM property.

`PdfPinata.Signing` is the one package that does **not** multi-target `netstandard2.1`, and the
one that carries a dependency the core deliberately refuses: `System.Security.Cryptography.Pkcs`,
which ships in the runtime but not in the reference pack and so needs a version-matched
`PackageReference` per leg. The core's own `Pdf.Signatures` namespace holds all the PDF machinery —
the placeholder, the byte range, the patching — and no cryptography at all, behind the `IPdfSigner`
seam. `docs/specs/digital-signatures.md` says why that split is where it is.

`PdfPinata.EInvoice` is the opposite kind of package: no dependency of its own, all three target
frameworks, and one class over machinery the core already had. `FacturXInvoice.AttachTo` names the
attachment `factur-x.xml`, relates it as `/Data`, claims PDF/A-3 and writes the XMP extension schema
that declares the four `fx:` properties the packet then uses. **That declaration is what only veraPDF
can check** — a packet writing `fx:DocumentType` without declaring it fails PDF/A for its metadata
rather than for its invoice — which is why `pdfa-3b-facturx` in the corpus is built through this
package rather than by hand, and why a change to what the packet says has to be validated rather than
unit-tested. The hook it writes through is *chained*, because `PdfDocument.CustomizeMetadata` is a
single property and assigning over it drops whatever the caller put there.
`docs/specs/pdf-a-conformance.md` has the rest.

`ImageSource` is a trap for the eye: the file is `src/PdfPinata/Drawing/ImageSource.cs` and it ships
in the **PdfPinata** assembly, but its namespace is `PinataLayout.DocumentObjectModel.Shapes`.
Registering it needs that `using`, from code that otherwise has nothing to do with PinataLayout.

## PDF object model and IO

`PdfItem` is the root of everything. Types derived from `PdfItem` but not from `PdfObject` are
simple types and **must be immutable** — `PdfName`, `PdfString`, `PdfInteger` and friends. `PdfObject`
adds identity and indirect references; `PdfDictionary`/`PdfArray` compose them.

`PdfReader.Open` takes a `PdfDocumentOpenMode` that decides far more than access: `Modify` reads
everything into memory and lets pages be inserted or deleted but not extracted, `Import` allows
extraction but no modification, `ReadOnly` preserves the original internal structure, and `Append`
is `Modify` that keeps the bytes and the numbers so a revision can be appended.

**That mode is now enforced where it is named, and it used to enforce nothing.** `CanModify`
returned `true` unconditionally with the real check commented out beside it, so the twelve
operations that guarded on it — the page-tree methods, `Save`, and the `Version`, `PageLayout` and
`PageMode` setters — read exactly like a mode check and let everything through. It is `!IsReadOnly`
now, so there is **one question, asked one way**: `EnsureCanModify` on `PdfDocument`, which
`PdfPages`, `XGraphics.FromPdfPage` and `PdfPageResizer` all go through, and whose message names
both the mode the document was opened with and the modes the operation needs. Two of the twelve went
the other way and are the ones to remember: **`Close` is deliberately unguarded**, because it writes
only when the document was constructed on an output stream and a document read by `PdfReader` never
is; and **`PageCount` lost the second way of counting** that sat behind the same dead guard.
**`InformationOnly` is gone from the enum**, and the four remaining members now state their values
with **3 left vacant** — the compiler inlines an enum constant at the call site, so letting `Append`
slide from 4 to 3 to close the gap would have silently redirected every already-compiled caller into
the removed mode's place. An old assembly passing 3 gets a value the enum does not define, which
`IsReadOnly` answers as read-only: exactly what `InformationOnly` always did. Add a new member after
`Append`, never into 3. `OpenModeEnforcementTests` is the matrix of four modes against nineteen
operations, and `docs/specs/open-mode-enforcement.md` has the rest.

**Strings and names are byte strings, one char per byte.** `Lexer` reads with
`(char)stream.ReadByte()`, `PdfEncoders.RawEncoding` converts back, and `PdfWriter` asserts
`ch < 256` on the way out. Nothing in the name path re-decodes to Unicode, so a name written in a
legacy code page keeps its bytes across a read/write round trip. Do not "fix" this by decoding to
UTF-8 — it breaks the writer. `LexerNameEncodingTests` pins the invariant down.

There are two independent lexers, and a change to one usually belongs in the other:

- `Pdf.IO/Lexer.cs` + `Parser.cs` — the document body, reading from a `Stream`.
- `Pdf.Content/CLexer.cs` + `CParser.cs` — content streams, reading from a `byte[]`, with its own
  `Chars` and `CSymbol`. It is the older and rougher of the two; the document lexer usually has the
  guard the content lexer is missing.

`Parser` touches exactly one member of `PdfDocument`, `_irefTable`, and reaches it through
that table's own four members rather than through its backing `ObjectTable` dictionary. It still
needs *a* document — a `PdfObject` gets its number by looking itself up in one — but no longer the
one `PdfReader.Open` builds, so a test reaches it through `src/PdfPinata.Test/IO/ParserProbe.cs`:
a plain `new PdfDocument()`, a `MemoryStream` of hand-written bytes, and no `%PDF` header, no
cross-reference table, no trailer and no `startxref`. **That document is not empty**, though — its
information dictionary is object 1 before a byte is read, so a test writing its own objects numbers
them from two, or the entry it writes for object 1 is ignored in favour of the one already there.
`TolerantParsingTests` and `CrossReferenceStreamDecodingTests` are what the probe is for; the
`PdfReader.Open`-based tests beside them still cover the same logic through a whole file.

## The interactive layer

The part of a PDF a reader *does* something with. `docs/specs/interactive-layer-gaps.md` is the
inventory, and nine of its ten items are now closed; everything below is what a change in this area
has to know.

**An appearance-bearing annotation builds its own appearance.** `/Square`, `/Circle`, `/Line` and
`/FreeText` are drawn by a reader from `/AP` and from nothing else, so a well-formed one carrying a
rectangle and a colour rasterizes to nothing at all. Each class therefore draws itself — through
`XForm` and `XGraphics`, never by writing operators — and redraws from `OnAppearanceInvalidated`
whenever anything it is drawn from changes. **Asked for nothing, each one removes the appearance it
had** rather than writing an empty stream, or a border set back to zero stays on the page. Tests for
these count pixels; asserting keys would pass on a document nobody can see.

**`/Line` computes its own `/Rect`**, from `Start`, `End`, the width and how much the line endings
take — the opposite of `PdfSquareCircleAnnotation`, where the rectangle *is* the geometry. Assigning
`Rectangle` on one is overwritten rather than honoured. It also reads every property back out of the
dictionary, so it survives a round trip through the file, where `PdfSquareCircleAnnotation` keeps its
interior and border width in fields and does not.

**Both endpoints of a `/Line` are in default user space**, measured up from the bottom left, not the
top-left world space `XGraphics` draws in. `gfx.Transformer.WorldToDefaultPage` has an `XPoint`
overload for exactly this; the `XRect` one encloses rather than maps, so which corner a point became
is lost.

**A form field and its widget are always separate objects.** `PdfAcroField.AddWidget(page, rect)` is
the only way to put a field on a page, and it never merges the two dictionaries even though ISO
32000-1 12.7.3.1 allows it for a field with one widget — so a field that gains a second widget does
not change shape. Consequences worth carrying: `/Kids` holds **both** nested fields and widget
annotations, and only the first sort has a `/T`, so anything walking it has to allow for a kid with
no name; and `PdfTextField` renders its value onto each kid that has a `/Rect`, not onto the field.

**A partial field name may not contain a period.** `Name = "name.full"` is refused, because a period
joins partial names into the path `Fields["name.full"]` looks a field up by — so writing one produces
a field that cannot be found. Nest the fields instead.

**The same collection is a form's `/Fields` and a field's `/Kids`, and `Add` writes `/Parent` for
the second.** ISO 32000-1 Table 220 requires the back-reference of a field that is the child of
another and forbids it of a root field, and the collection cannot tell which it is from the outside —
so `PdfAcroField.Fields` tells it, and `PdfAcroForm.Fields` leaves it unset. Nothing here reads
`/Parent`; every lookup walks *down* from `/Fields`, which is why its absence went unnoticed. A
reader assembling a field's full name walks up.

**A field's kind is written in `/Ff`, and the `Flags` setter puts those bits back.** `/Btn` is a push
button, a radio group or a check box by two bits and `/Ch` is a combo or a list box by one, so
assigning `Flags` outright would change the field's *type* behind the caller —
`new PdfComboBoxField(document) { Flags = Required }` used to write a list box, and only reopening
the file said so. `KindMask` and `KindFlags` are what each class declares; the other flags assign
normally, and `SetFlags` (internal) bypasses the whole thing because it only ever sets one bit.

**An appearance cannot be made of a rectangle under a point.** `XForm` throws below 1 in either
direction, so every class that draws its own appearance tests against 1 rather than against 0 and
removes the appearance instead. It bites a `/Line` most: a hairline lying flat is half its width
either side and no more.

**A text field's own appearance suppresses `/MK`.** An appearance is what a reader shows *in place
of* building one from the appearance characteristics, so `PdfTextField` removes `/AP` when it has no
background, no border and no value, and `BorderColor` exists so that a field which does draw itself
can still look like a field. `BackColor`, `ForeColor`, `Font` and `BorderColor` all redraw, as `Text`
does; they used to be read only when the value changed, which made them silent no-ops.

`PdfAcroForm.AddStandardFont` leaves `/Symbol` and `/ZapfDingbats` without an `/Encoding` — WinAnsi
would override the built-in one, and ZapfDingbats is how a check box draws its tick.

## Drawing

`XGraphics` is the drawing surface and holds an `IXGraphicsRenderer`; `XGraphicsPdfRenderer`
(`Drawing.Pdf/`) is the only implementation and emits content-stream operators. PinataLayout renders
through the same surface, so a layout fix lands in `PinataLayout.Rendering` and a drawing fix in
`Drawing.Pdf`.

Fonts are always embedded, with no setting to disable it. TrueType outlines are subsetted;
PostScript (CFF) outlines cannot be and embed whole. A weight or slant with no font file is
simulated by stroking or skewing.

**An image's two masks are alternatives, never a pair.** `PdfImage.InitializeNonJpeg` writes the
8-bit `/SMask` when any alpha is neither 0 nor 255 and the document is 1.4 or later, and the 1-bit
`/Mask` stencil only where that soft mask is *not* written — binary transparency, or a pre-1.4
document. Writing both is the shape this inherited from upstream and it made images disappear:
ISO 32000-1 Table 89 has `/SMask` override `/Mask`, and pdf.js obeys that, but Ghostscript and
macOS Quartz apply both, so the stencil's rounding of alpha at 128 threw away every pixel below it —
an image whose alpha lay wholly under 128 was embedded, referenced and invisible. That is
empira/PDFsharp#392, whose palette PNG was a red herring: both backends decode to BGRA whatever
the source format is, so this fork has no palette path to differ on. Judging this by the XObject's
keys is what hid it for so long — `TranslucentImageRenderingTests` rasterizes instead, and
`docs/specs/image-pixel-seam.md` has the measurements.

## Tagged output

**PinataLayout tags what it draws, and that is the default** — `PdfDocumentRenderer.TagContent` is `true`,
so every document it renders carries a structure tree. Two consequences bite immediately.
`PdfPage.Resize` refuses a tagged document, so code that renders through PinataLayout and then resizes has
to set `TagContent = false`. And a renderer that draws anything must say what it is drawing:
`Renderer.Tagger` hands out the scopes, content goes in `Tagger.Block`/`Container`/`Marks` and
decoration in `Tagger.Artifact`, and **anything inside an artifact scope is not tagged at all** — the
tagger counts depth and refuses, because a running head drawn by the paragraph renderer would otherwise
appear in the tree as a paragraph.

That refusal has a consequence worth knowing before you write a renderer: **`Tagger.Parent` is not
"the element I just opened".** A refused scope pushes nothing, so `Parent` still names what was
current before it — and a renderer that opened a scope and then wrote alternate text onto `Parent`
wrote it onto an unrelated element. Take the element from the `out` parameter of `Tagger.Block` /
`Tagger.Container` instead, and treat null as "not tagged".

`docs/specs/tagged-pdf-accessibility.md` has the rest, including why an element is keyed by its DOM
object rather than built per render pass.

## Multi-targeting

Every shipped package targets `netstandard2.1;net8.0;net10.0`. `netstandard2.1` exists **for Unity**,
whose scripting runtime cannot consume a `net8.0` assembly — check with the maintainer before
dropping it. Two consequences:

- `Directory.Build.props` sets `LangVersion=latest` for every leg, or the netstandard2.1 leg would
  compile shared source as C# 8. This unlocks *syntax* only. Anything needing a BCL type
  netstandard2.1 lacks (`IsExternalInit` for records and `init`, `RequiredMemberAttribute`,
  `InlineArray`) still fails as a missing-predefined-type error, and has to be polyfilled.
- **Types** come from PolySharp, referenced in `Directory.Build.props` for the netstandard legs
  alone (the generator's netstandard2.0 included) with `PolySharpIncludeRuntimeSupportedAttributes`
  on, for `DynamicallyAccessedMembers`. It generates `internal` source for what each compilation
  lacks, so every assembly gets its own copy and the net8.0/net10.0 legs get nothing. **Members**
  — a throw helper, a span overload — PolySharp never supplies; those are C# 14 static extension
  members in `src/Polyfills/`, compiled into the netstandard2.1 leg only. Write shared source the
  modern way and add to one of the two, rather than fencing a line with `#if` or a pragma.

## Tests

`TestBackendSetup` is a `[ModuleInitializer]` that registers the Skia backend and Ghostscript for
the whole assembly — individual tests do not set up backends.

`PinnedFontResolver` serves Liberation Sans (Arial metrics) from `Assets/Fonts` instead of the
machine's fonts, because glyph widths decide where a line wraps and therefore what a layout
assertion sees. A test needing its own font calls `PinnedFontResolver.Register` rather than
swapping the resolver out from under everything else running beside it.

`[GoldenImageFact]` marks tests comparing against checked-in reference images; it self-skips with a
reason when Ghostscript cannot rasterize on the current machine. Anything that rasterizes belongs
to `[Collection(RasterizingCollection.Name)]` — ImageMagick drives one in-process Ghostscript, so a
second concurrent rasterization silently falls back to an executable that may not be installed.

Ghostscript comes from `Ghostscript.NativeAssets` on Windows. Linux and macOS shell out to the
system `gs`, so those tests need `apt-get install ghostscript` / `brew install ghostscript` locally.

Assertions use **AwesomeAssertions**, a FluentAssertions fork — the API is the same, the `using` is
not.

## Conventions

Commit subjects describe the behaviour change in plain prose, in the imperative, without a
conventional-commit prefix: *"Read a hex string by the digits in it rather than by what follows
them"*, *"Follow a composite glyph down to the glyphs it is really drawn from"*. Bodies explain what
was wrong and why the new way is right. Pure housekeeping does use `chore:` / `refactor:`.

Sources use file-scoped namespaces. `.editorconfig` enforces a final newline — a bulk conversion
once stripped it from 725 files, and every later diff touching the end of a file showed the damage.

`docs/specs/` holds design notes for this fork's features, each tied to an upstream issue number,
recording what was built and what was deliberately left out. Read the relevant one before extending
that feature area.
