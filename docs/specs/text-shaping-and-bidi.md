# Spec — complex-script shaping, bidirectional text and font fallback

What real script support covers, and what it deliberately leaves out.
Gap **G3** of `autoresearch/improve-260816-1032/improvement-plan.md`.

**`"سلام"` now draws as `"سلام"`.** `GlobalFontSettings.TextShaper = new HarfBuzzTextShaper()` and
the font's own `GSUB` and `GPOS` run; the glyphs they choose are embedded and given widths;
`/ToUnicode` says what each of them stands for even when that is more than one character; a glyph a
shaper wants displaced is displaced; and every `DrawString` and every `MeasureString` now cuts its
string into runs that are each one direction and one script and draws them in the order they are
read, having resolved the Unicode Bidirectional Algorithm to do it.

Two things are worth separating out of that, because they are usually run together and are not the
same. **Reordering does not need a shaper**: a consumer who registers nothing at all gets
right-to-left text the right way round, unjoined, because reordering is a property of the text and
joining is a property of the font. And **reordering happens in the renderer, not in a layout
engine**: `DrawString` reorders whatever string it is given, so a formatter that hands over a whole
line gets it for free, and one that places each word itself does not.

A face with no Arabic in it no longer draws four empty boxes either: naming a family to fall back
to is enough, and the run is cut at the coverage boundary and drawn by the face that has the glyphs.

**Every item of this gap is now built.** A paragraph of Hebrew or Arabic laid out by MigraDoc has
its words in the order they are read as well as its letters, and `ParagraphFormat.TextDirection`
says which way a paragraph runs rather than leaving it to be guessed from the first strong
character it happens to start with.

| item | what | status |
|---|---|---|
| 1 | `ITextShaper` — a fourth static seam beside `GlyphOutlineProvider` | **built** |
| 1a | `ShapingFont`, `ShapedRun`, `ShapedGlyph`, `XTextDirection` | **built** |
| 1b | Measuring and drawing both ask the seam | **built** |
| 2 | `HarfBuzzTextShaper`, in a `PdfPinata.HarfBuzz` package of its own | **built** |
| 3 | UAX #9 bidirectional algorithm, in the core | **built** |
| 4 | UAX #24 script itemisation | **built** |
| 5 | Font fallback chain | **built** |
| 6 | The write path honours offsets, and `/ToUnicode` is built from clusters | **built** |
| 6b | Measuring and drawing itemise and reorder | **built** |
| 6c | Word order in a justified line | **built** |
| 6d | Word order in MigraDoc | **built** |
| 7 | Saying which way a paragraph runs | **built** |

Estimated effort was **9–13 engineer-weeks**: shaping seam 3 · bidi and itemisation 3 · fallback 2 ·
measurement rework 3–4 · golden-image churn 1. Stage 1 spent well under the first three, because
routing the two glyph-producing paths through the seam turned out to be the whole of it, and because
HarfBuzzSharp does the shaping.

---

## The defect

Three related absences, and together they are the oldest unresolved complaint against PdfSharp.

**Nothing shapes.** A string goes to the page one character at a time, each mapped to the glyph the
font's `cmap` gives for that code point. For Latin that is nearly right — it loses kerning and
ligatures. For Arabic it is *wrong*: the alphabet has initial, medial, final and isolated forms of most
letters, and Unicode stores the letter, not the form. So Arabic renders as a row of isolated shapes
that never join. For Devanagari, Thai and Khmer — reordering, conjuncts, split vowels — it is not even
close.

**Nothing reorders.** Right-to-left runs come out in logical order, which is to say backwards. The
documented user experience is that `"سلام"` draws as `"م ا ل س"`. Users work around it by detecting
Arabic, substituting positional forms from a hand-built table, and reversing the string.

**Nothing falls back.** `Drawing/XGlyphTypeface.cs:102` says it outright:

```csharp
// No fallback - just stop.
```

A code point the selected face has no glyph for becomes `.notdef` — an empty box, or nothing — with no
warning at any layer.

`empira/PDFsharp-1.5#144` has tracked the RTL half of this for years.

## Why it is worth doing here

iText does not include this either. It is **`pdfCalligraph`, a paid add-on** on top of a library that
is already AGPL-or-commercial. QuestPDF advertises RTL and bidirectional text but documents no shaping
engine and no font fallback. Shipping real shaping *in the box, under MIT* is a position neither
incumbent occupies, and it is the difference between "supports Arabic" and supports Arabic.

---

## Item 1 — the seam · **built**

Follow the pattern this repository already established for optional capability. `README.md` and
`CLAUDE.md` describe three static seams — `FontResolver`, `ImageSourceImpl`, `GlyphOutlineProvider` —
each throwing a descriptive `InvalidOperationException` when read unset, each supplied by a backend, so
the core package carries no font or imaging dependency. This is the fourth:

```csharp
public interface ITextShaper
{
    ShapedRun Shape(ReadOnlySpan<char> text, ShapingFont font,
                    XTextDirection direction, string script, string language);
}

public readonly struct ShapedGlyph
{
    public ushort GlyphId { get; }
    public int Cluster { get; }      // index into the shaped text
    public double Advance { get; }   // font design units
    public double OffsetX { get; }
    public double OffsetY { get; }
}
```

in `src/PdfPinata/Fonts/`, registered on `GlobalFontSettings.TextShaper`, and asked through the
internal `TextShaping` — the one place a character becomes a glyph.

**Clusters are not optional.** They are the character↔glyph map, and three separate things need it:
line breaking (you may only break at a cluster boundary), `/ToUnicode` construction (a ligature is one
glyph and two characters), and `/ActualText` for tagged output. A shaping API that returns glyphs and
advances but no clusters is not usable for a PDF writer, only for a screen.

The last two are both built now, and both read `TextShaping.CharactersOf` — one implementation, because
two would eventually disagree and a document would then say one thing to a text extractor and another
to a screen reader. See `docs/specs/tagged-pdf-accessibility.md` for what a ligature is written as, and
for why a joining control does not count as one.

Like `GlyphOutlineProvider`, a shaper must take its font bytes **through** `FontResolver` rather than
resolving a family itself, or the two seams disagree about which face a family means.

Unset, the shaper falls back to today's behaviour — one code point, one glyph, no reordering — so
nothing existing breaks by omission.

### What building it settled

**The seam cannot be handed an `XFont`, which is what this note used to say.** Everything a shaper
needs from a font — `GlyphTypeface`, and through it `FontSource.Bytes` — lives on
`XGlyphTypeface` and `XFontSource`, and **both of those classes are internal**. A public seam cannot
hand over what a caller cannot read. Making them public was the other way out and is much the worse
one: `XGlyphTypeface.GetOrCreateFrom` takes the internal `FontResolvingOptions`, so publishing the
class drags the resolution plumbing out with it. So the seam is handed **`ShapingFont`**, built for
the purpose and carrying exactly what a shaper needs — family, face name, cache key, bold, italic,
em size, units per em, and the bytes as a `ReadOnlyMemory<byte>`. This is a better answer than the
original in any case: the face in it is the one already resolved and about to be embedded, so a
shaper *cannot* disagree with the renderer about which file a family means, where the old wording
could only ask it not to.

**Advances are in font design units, not points.** A run measured in points is good for the one size
it was shaped at; a run in design units can be measured, cached and drawn at every size, which is
what the measuring paths actually do with it. It also matches what is already there —
`OpenTypeDescriptor.GlyphIndexToWidth` answers in design units and `ShapedRun.UnitsPerEm` is what
they are read against.

**A shaper that returns null has declined the run, not failed.** The unshaped result is always
available, and falling back to it beats throwing from the middle of a page being drawn. This turned
out to be a feature rather than a courtesy: a shaper that handles Arabic and nothing else can say so
by returning null, and it is what lets the tests install a shaper at all — see below.

**This seam's unset state is not an error, unlike the other three.** Reading
`GlobalFontSettings.TextShaper` before one is set answers null rather than throwing, because there
is a working default behind it. It can be set, replaced and cleared at any time; nothing is cached
against it.

**Testing it needed care the other seams did not.** `GlyphOutlineProvider` is read by one method, so
a test may swap it out and back under a `try`/`finally` and only race with itself. `TextShaper` is
read by every path that measures or draws a character, and xUnit runs collections in parallel, so a
test installing a shaper that answered *every* run would corrupt whichever other test happened to be
drawing text beside it. Every shaper in `TextShapingSeamTests` therefore answers for one sentinel
string of its own and returns null for all others, which is exactly the fall-back path — so
concurrent tests get the behaviour they would have had with no shaper at all.

## Item 2 — the HarfBuzz implementation · **built**

**DECISION MADE — a standalone `PdfPinata.HarfBuzz` package**, over `HarfBuzzSharp` 14.2.1.2
alone rather than `SkiaSharp.HarfBuzz`. The question was whether to accept that shaping requires the
Skia backend: `PdfPinata.ImageSharp` is pinned to `SixLabors.Fonts` 1.0.1 for licence reasons (the
2.0 relicensing) and that version's advanced typography support is limited, so putting the shaper in
`PdfPinata.Skia` would have made the two backends unequal in a way they currently are not. A
package of its own costs one more thing to ship and duplicates a native dependency Skia users already
have, and it means shaping does not oblige anybody to pick an imaging backend at all. It targets
`netstandard2.1;net8.0;net10.0` like everything else, so the Unity leg described in `CLAUDE.md`
survives.

`HarfBuzzTextShaper` is the whole package. Points worth knowing:

- **The HarfBuzz font is scaled to the face's units per em**, which is what makes advances come back
  in design units rather than in HarfBuzz's default 26.6 fixed point — see the note on units under
  item 1.
- **Faces are parsed once and cached on `ShapingFont.Key`.** Parsing a font per word would cost far
  more than shaping it, and `Key` exists on `ShapingFont` for exactly this. The key's contract —
  distinct per face, stable for one — is **enforced by the constructor** rather than merely
  documented, and the shaper has no fallback for a missing one. It had both, and the combination was
  the dangerous kind of quiet: a keyless face fell back to a shared cache entry, so the second such
  face shaped came back with glyph identifiers read out of the *first* one's file. A glyph
  identifier indexes a font and nothing downstream checks which font, so the page would have drawn
  whatever the other face happened to have at those numbers — wrong text, no exception, and only on
  the pages using the second font.
- **Shaping against one face is serialised by a lock.** An `hb_font_t` is not safe to shape with
  from two threads at once, and a shaper is registered once for the whole application domain, so it
  will be. Two different faces still shape concurrently.
- **A face HarfBuzz will not parse is declined, not thrown.** Returning null puts the caller back on
  the unshaped path, which is the same thing it would have done with no shaper registered — a
  consumer who cannot shape is still entitled to a document.
- `GuessSegmentProperties` fills in whatever of direction, script and language was left unsaid. It
  never overrides what was set, so a caller who knows stays in charge. This is not automatic language
  detection, which this note rules out below; it is HarfBuzz reading the characters in front of it.

### Three bugs worth not writing again

`OneShaperServesSeveralThreadsAtOnce` caught both, and both had the same symptom: a run coming back
with the **right glyphs and the wrong positions** — unkerned, once in a few hundred shapings, on one
target framework and not the other. Anything holding a font's bytes wrongly looks like this, because
the `cmap` is read early and `GPOS` late, so a face reading memory that has gone still finds the
glyphs and misses the kerning.

**The blob must own its memory.** `Blob.FromStream` over a `MemoryStream` was the first version, and
it is wrong: a HarfBuzz face reads its tables *lazily* and goes on reading them for as long as it
lives, so the bytes have to outlive the call that made the blob and stay where they were put. They
were a managed array the caller was about to drop. The fix is unmanaged memory and a release
delegate, so the blob frees it when HarfBuzz is done with it and not before.

**`ConcurrentDictionary.GetOrAdd` may run its factory more than once.** Only one result is kept; the
others are abandoned holding live native handles, for a finaliser to free at some unrelated later
moment. A `Lazy<T>` with `ExecutionAndPublication` builds the face exactly once however many threads
arrive together. This one is a leak in any case, whatever it does to correctness.

**`Dispose` and `Shape` needed ordering between them, and a flag is not one.** `Shape` read a plain
`bool _disposed`, then resolved a face, then called into native code — three steps with two windows
in them. A `Dispose` arriving in either window freed the `Font`, the `Face` and the block behind the
`Blob` while another thread was about to shape with them: a use-after-free in native memory, which
ends the process rather than raising the `ObjectDisposedException` the remarks promised and
`AShaperThatHasBeenDisposedSaysSoRatherThanCrashingTheProcess` asserts.

The guarantee had to move to where the resource is. `ShapedFace` already takes a lock around every
shaping call, because a HarfBuzz font cannot be shaped with from two threads at once — so its
`Dispose` takes the same lock and sets a flag that `Shape` checks *inside* it. A disposal now either
completes before a shaping call starts and is seen by it, or waits for the call to finish. The
shaper's own `_disposed` stays as the early answer for the ordinary case and is `volatile`, and its
`Dispose` drains the dictionary rather than snapshotting it, so a face added by a thread that read
the flag a moment too early is still freed. Nothing new is locked on the shaping path: it is the
lock that was already being taken.

Disposing a shaper other threads are still drawing with remains a mistake — the point is that it is
now a catchable one.

## Items 3 and 4 — bidi and itemisation, in the core · **built**

The Unicode Bidirectional Algorithm is pure text processing. It touches no font, no image and no
backend, so it goes in `PdfPinata` itself rather than behind the seam — a caller who only wants to
know which way a string runs should not have to install a shaper to find out. `src/PdfPinata/Text/`,
namespace `PdfPinata.Text`, about 1,100 lines including the itemiser.

The reason to be confident about it: **it is exactly testable.** The Unicode Character Database ships
`BidiTest.txt` and `BidiCharacterTest.txt`, conformance suites with expected levels and reorderings.
This is a data-driven test class, not a judgement call — either the implementation passes the suite or
it does not. **It passes both in full: 770,241 and 91,707 cases, 861,948 between them.**

UAX #24 script itemisation splits a mixed string into runs the shaper can handle one at a time
("this much is Arabic, this much is Latin"). Smaller, and it feeds both shaping and fallback.
`TextItemizer` is where the two meet: it hands back runs that are each one direction *and* one script,
in the order they are drawn, which is exactly what `ITextShaper.Shape` takes.

### The Unicode data — three decisions

**The property tables are checked-in generated C# source.** `tools/UnicodeTableGenerator` reads the
UCD and writes four `.g.cs` files; it is deliberately outside `PdfPinata.slnx` so the build and CI
never see it. Generating during the build was the alternative and is worse: it would put a network
fetch on the critical path of every build on every target framework and make an offline build
impossible. The tables are small — 1,611 Bidi_Class ranges and 984 Script ranges, about 57 KB of
source — so checking them in costs nothing and buys a reviewable diff when the version moves. It also
keeps the `netstandard2.1`/Unity leg and AOT honest, which an embedded-resource decoder would have
complicated for no gain at this size.

**The conformance suites are checked in gzipped**: 1.7 MB against 14.8 MB unpacked, in
`src/PdfPinata.Test/Assets/Unicode/`. That is about a quarter again on top of a 6.2 MB repository, and
it is worth it — a conformance claim that depends on the network is not one the build can make. The
alternatives were a sampled subset, which weakens "conformant" to "spot-checked" when UBA bugs live
in rare class combinations, or a download that silently skips. Reading and running all 861,948 cases
adds about two seconds to the suite.

**Everything is pinned to Unicode 17.0.0**, and the tables and the suites have to move together — a
test asserts the version for exactly that reason. Bumping one without the other tests one Unicode
against another's expectations, which fails in ways that look like algorithm bugs and are not.

### What building it settled

**The `@missing` lines in `DerivedBidiClass.txt` are load-bearing, and they live inside comments.**
The file lists the assigned characters and leaves the rest to defaults that are *not* all
`Left_To_Right`: unassigned code points in the Hebrew block default to `R`, and in the Arabic blocks
to `AL`. An implementation reading only the explicit ranges is quietly wrong for exactly the scripts
the algorithm exists for. The generator materialises them into a complete partition of
U+0000..U+10FFFF, so there is nothing left to default at run time and a lookup is one binary search.
Four tests pin the defaults specifically.

**W1 chains off the resolved type, not the original one.** This was the only rule the conformance
suite caught, and it caught it 352 times: `R NSM NSM` must resolve both marks to `R`, because the
second mark is attached to the first, which is by then an `R`. Carrying the unresolved `NSM` forward
instead makes the second one a mark attached to a mark, which is nothing at all. Everything else —
the overflow counters, the isolating run sequences, sos and eos, the bracket pairs — was right first
time, which is not something anybody should conclude about their own reading of UAX #9 without the
suite to say so.

**Script itemisation reads `sc` and not `Script_Extensions`.** A character can belong to several
scripts at once — U+0640 Arabic tatweel is used by Syriac and Adlam too — and `scx` is the property
that says so. Reading `sc` alone puts such a character in the script it is named for rather than in
whichever neighbouring script also claims it. The visible cost is a run boundary where there need not
be one, never a wrong glyph, and adding `scx` later is a third generated table and no change to any
caller.

## Item 5 — fallback · **built**

A run whose face lacks glyphs is split and re-resolved against a chain. Coverage comes from
`Fonts.OpenType/OpenTypeDescriptor.CharCodeToGlyphIndex`, which answers glyph 0 — `.notdef` — for a
character the face does not have, and that turned out to be the whole of the query needed.

```csharp
GlobalFontSettings.FontFallback = new FontFallbackList("Noto Sans Arabic", "Noto Sans Devanagari");
```

### It is not a wider `IFontResolver`, which is what this said before

The proposal was to grow `IFontResolver`, on the grounds that breaking changes are acceptable here
and one seam should mean one thing. Both halves of that turned out to be wrong.

**The mechanism is not available.** A default interface method would have made it non-breaking, and
`netstandard2.1` — which exists in this repository *for Unity*, whose scripting runtime cannot be
relied on to dispatch one — rules it out. So widening really would break every consumer who has
written a resolver, with no migration path but editing their code.

**And it is not one thing.** A resolver is asked *which file is this family*, which every resolver
must be able to answer or it is not a resolver. A fallback is asked *who else could draw this
character*, which is a judgement about the whole machine's font collection and which a consumer
serving three embedded files from a resource stream has no business being made to answer. Two
questions, two seams. `GlobalFontSettings.FontFallback` reads the registered resolver when that
resolver implements `IFontFallback` as well, so a resolver that *does* know gets to answer without
being registered twice — which recovers the one-seam convenience for the case it was wanted for.

### What it does not do: work the families out for itself

`FontFallbackList` takes the families from the caller. Discovering unprompted which of a machine's
installed faces can draw a character means reading the `cmap` of every one of them — hundreds of
files — and then choosing between the candidates on grounds of design, weight and language
preference that this library has no way to judge. A list the document's author wrote is faster and
better, and the seam is shaped so that a consumer who wants discovery can implement `IFontFallback`
themselves without any of this changing.

### Four characters are deliberately not asked about

Coverage is consulted per character, and four kinds of character get no opinion — in each case
because having one would do harm:

- **White space.** A space carries no shape worth choosing a face for, and giving it one cuts the
  run it sits in — so a sentence of Devanagari with spaces in it would be drawn as one run per word,
  losing the shaping across every boundary, for nothing visible.
- **A non-spacing mark**, which belongs to the letter in front of it. A mark placed by one font
  against a letter drawn by another is a worse defect than a mark that is missing, and splitting the
  two breaks the attachment the shaper was about to make. `UnicodeProperties.BidiClassOf` already
  knows which characters those are, which is the bidi table earning its keep somewhere unexpected.
- **A joining control**, U+200C and U+200D, which say how the letters on either side of them join
  and are read by the face those letters are drawn from. Moving one to a face of its own would put
  the instruction in one run and the letters it is about in another — the one arrangement that
  certainly cannot work. See *Joining controls are inside runs* below.
- **A character nothing offered can draw.** Cutting the run there costs the shaping across the
  boundary and buys the identical `.notdef`.

**A surrogate pair is never cut in half either.** The loop walks UTF-16 code units, so the trailing
half of a pair would be asked about as a lone surrogate — which no `cmap` covers — and a face
boundary could fall between the two halves of one character. The trailing half is skipped and goes
wherever the leading half went. Nothing above the basic multilingual plane resolves to a glyph at
all yet, so this buys nothing today; it is here so that the format 12 reader has one less thing to
fix when it arrives.

### What building it settled

**Fallback cuts runs in places the other two cuts know nothing about.** Direction, script and
coverage change at unrelated points, so `TextShaping.ShapeText` cuts by direction and script first
and by coverage inside each of those. The pieces of a *right-to-left* run then have to be reversed
among themselves — the piece written first is the rightmost — which is the bidirectional algorithm's
own reordering happening one level further down, for a reason it knows nothing about.

**Selecting a font does not move the pen, so the renderer needed no positioning work at all.** A
`Tf` between two show operators leaves the pen where it was, so a string drawn in two faces is the
two segments' operators with one `Tf` between them. The face the caller asked for is selected again
at the end whether or not it was the last one used, so `PdfGraphicsState` goes on being right about
what the content stream has selected — cheaper than telling the graphics state what happened, and
one fewer thing to keep in step.

**`ShapedText.Width` had to stop being in design units.** Two segments shaped against faces with
different units per em have no common em to be measured in, so the total is in points and each
segment is converted against its own. `FontHelper` adds character and word spacing to that rather
than to a design-unit sum.

~~**Style simulation is still decided once for the whole string**, from the face the caller asked
for.~~ **Since done** — see item 4 of *Where this goes next*. It is decided per face: the rendering
mode and the character spacing are written per segment and put back afterwards, and the measuring
path reads the same rule.

**Fallback reaches only Unicode-encoded fonts.** `DrawString` chooses the WinAnsi branch before any
of this, and a WinAnsi font is eight bits wide, so there is little to fall back *from*. Worth
knowing rather than worth fixing.

~~**Astral characters are never offered for fallback.**~~ **Since done** — see item 3 of *Where
this goes next*. The format 12 reader landed, and with it the whole of what this paragraph said was
waiting on it. **`IFontFallback.FamiliesFor` now takes an `int` code point, not a `char`** — the one
place in this document where the old text stated a public contract that has since changed, so read
the item rather than this paragraph.

## Item 6 — the invasive part · **built**

Everything above is additive and self-contained. This is neither.

Every measurement path has to go through shaped advances rather than summing per-character widths:

- ~~`Drawing/FontHelper.cs` — `MeasureString`, which `XGraphics.MeasureString` is a wrapper over~~
  **done**
- ~~`Drawing.Pdf/XGraphicsPdfRenderer.cs` — `DrawString`, which is where glyph identifiers are
  written~~ **done**
- ~~`Drawing.Layout/XTextFormatter.cs` — the line breaker~~ **done, and it took no change at all**:
  it measures word by word and draws line by line, both through the two above
- ~~`src/PinataLayout.Rendering/ParagraphRenderer.cs`~~ **measures and shapes correctly**; its word
  *order* is item 6c
- ~~`PdfPinata.Charting` — axis and data labels~~ **done**: a label is one string through one
  `DrawString`

Kerning and ligatures change advance widths, which changes where lines break, which changes where
everything below them sits. **This was expected to move existing golden images**, including ones for
features that have nothing to do with text shaping, and the budget included regenerating and
re-reviewing them.

**It moved none of them, and the reason is worth writing down.** Two things had to be true at once,
and both were. With no shaper registered the unshaped run is the same sum of the same per-character
widths it always was — so registering the seam changed nothing on its own. And itemisation is
skipped outright for text made only of characters below `U+02B0`, which is Latin, Common, and no
right-to-left class anywhere: such a string can only ever be one run, so the fast path is not an
approximation of the itemised answer but provably the same answer. Every golden image in this
repository is Latin text drawn with no shaper, so both conditions hold for all of them and the
churn simply never arrived. It will arrive the day a shaper is registered by default, and it will
arrive as correctness rather than as regression.

### What routing them settled

**Measuring and drawing did not agree, and now do — about every character except `\n`.**
`FontHelper.MeasureString` maps a tab to a space, drops every other character below 32, and treats
`\n` as a line break; `DrawString` used to emit a glyph for all of them. So the two had always
disagreed about strings containing control characters. Routing both through one seam made this
visible and **deliberately preserved it** — the filtering stayed at the call site, because changing
it is a behaviour change with nothing to do with shaping and would have moved output the shaper is
supposed to leave alone. It was written down here rather than fixed in passing, and it is now fixed
on its own terms. `src/PdfPinata/Fonts/TextNormalization.cs` holds the one rule both sides read — a
tab becomes a single space, every other character below 32 is dropped, nothing at or above 32 is
touched — and `DrawString` applies it to the string before it measures it for alignment, shapes it,
embeds a glyph for it or encodes it as WinAnsi, so every later read is of the one normalized local.
It runs *before* `ShapeText` rather than inside it because both callers keep using the original
string afterwards, and a shaper filtering underneath them would hand back cluster indices into a
string nobody else has. The half that remains is the half that is a product question rather than a
filtering one:
`MeasureString` still splits on `\n` and reports several lines, `DrawString` still draws one and now
drops the `\n` rather than boxing it, and
`TextStateOperatorTests.ALineFeedIsAbsorbedByDrawStringWhileMeasureStringStillReportsTwoLines`
fails on purpose the day that changes.

**`WordSpacedGlyphRun` was the first thing to break the one-glyph-per-character assumption**, and it
had asserted it outright: `Debug.Assert(text.Length == glyphs.Length, "One glyph per character, or
the split lands in the wrong place.")`. It hand-writes a `TJ` array for Type 0 fonts, splitting the
glyph run after each space, and it found the spaces by indexing the *text*. With a ligature earlier
in the string the indices no longer line up and the extra room lands inside the wrong word. It now
reads `ShapedGlyph.Cluster`, and puts the room after the last glyph of the space's cluster rather
than the first. Two tests pin it.

**Measuring stopped allocating per character and started allocating per call, so it got a fast
path.** The old loop walked the string in place; shaping wants a contiguous run. A string with no
line breaks, tabs or control characters — much the commonest case, and the one the layout engine
measures every single word of — is now shaped where it stands with no copy at all. Only a string
needing the rewriting rents a buffer, from `ArrayPool<char>.Shared`.

### The write path · **built**

`XGraphicsPdfRenderer` already emitted glyph identifiers for Type 0 fonts, so handing it shaped ones
was close to what it did. Three things were not close, and the first of them was a live defect in
the seam as first landed.

**The glyphs a shaper chose were neither embedded nor described.** `DrawString` called
`realizedFont.AddChars(s)`, which looks each character up in the `cmap` and records *that* glyph.
`CMapInfo.GlyphIndices` is what `PdfFont.CreateFontSubSet` subsets to and what `PdfType0Font` builds
the `/W` widths array from, so registering a shaper produced a page drawing a ligature the file did
not carry and gave no width for, while embedding the glyphs of the characters it had swallowed.
`CMapInfo.AddShapedRun` replaces `AddChars` on the Unicode path — replaces, not supplements, because
the characters' own glyphs are not drawn and have no business in the file.

**`/ToUnicode` could not represent a ligature at all.** It was built from
`CMapInfo.CharacterToGlyphIndex`, a `Dictionary<char, int>`: one character in, one glyph out, and no
way to say that one glyph stands for two characters. It is now built from the cluster indices, which
is the only place that fact exists. A glyph meaning one character still goes in a `bfrange` exactly
as before, so nothing about an unshaped document changes; a glyph meaning several needs a `bfchar`,
whose destination is a UTF-16BE string of any length. Where one glyph is reachable meaning two
different things, the **shorter** reading wins — a glyph reachable as one character is better
described by that character — and choosing by length rather than by arrival keeps the written file
the same whichever string was drawn first.

While there: the blocks are now chunked at a hundred entries, which ISO 32000-1 section 9.10.3 has
always required of both `bfrange` and `bfchar` and which was never applied. Every document of any
size has more glyphs than that, so this was not a limit only exotic files reached.

**Per-glyph offsets are emitted.** `ShapedGlyph.OffsetX`/`OffsetY` position attached marks — Arabic
vowel points, Devanagari matras. A glyph wanted `dx` to the right is written as `-dx` before it and
`+dx` after, inside a `TJ` array, so it is displaced without the run growing; the same mechanism
already paid for word spacing, and the two simply add. A vertical displacement has no operand and
needs `Ts`, which is text state rather than an operand, so a run whose glyphs sit at different
heights is shown in one piece per height and the rise is put back to zero afterwards. Nothing else
in this renderer writes `Ts`, so zero is what it is on entry.

**A run with nothing to displace is still a plain `Tj`.** That is worth stating because it is what
keeps every existing document byte-identical, and `TextStateOperatorTests` already held the
principle — "a TJ array of one run would be correct but wasteful". One test there had been pinning
the wasteful form for the case of a word spacing asked for with no space to spend it on; it now
agrees with its neighbour.

### Itemising the string · **built**

A `ShapedRun` is what a shaper answers for *one run*, and a run is by definition one direction and
one script. A string handed to `DrawString` is neither. So `TextShaping.ShapeText` sits above
`Shape`: it asks `TextItemizer` for the runs, shapes each on its own terms, and answers a
`ShapedText` — a list of `ShapedSegment` in visual order, each holding its run and where in the
string it came from. Both `MeasureString` and `DrawString` call it. Four things came out of building
it.

**PDF has no notion of direction, and that is what makes reordering cheap.** A show-text operator
paints glyphs at the pen and moves the pen along; there is no writing mode and nothing to set. So
emitting the segments back to back, leftmost first, *is* the reordering — no positioning arithmetic,
no second text matrix, and a one-segment string produces byte for byte the operator it produced
before segments existed.

**A segment is shaped against its own substring, not against an offset into the whole.** The
alternative — concatenating everything into one `ShapedRun` with clusters shifted — looks tidier and
is wrong: `CMapInfo.CharactersOf` reads `ShapedRun.Direction` to decide which way to look for the
next cluster along the text, and a concatenation of mixed directions has no one direction. Keeping
the runs apart means every existing reader of a `ShapedRun` goes on being handed exactly what it was
built for. `ShapedSegment.TextIn` returns the whole string unsliced when the segment is the whole
string, so the common case still copies nothing.

**The unshaped path had to learn to reverse.** `ShapedRun` promises visual order and the renderer
relies on that promise, so `TextShaping.Unshaped` cannot hand back a right-to-left run in logical
order merely because it has no shaper to ask. It reverses the glyph list and leaves the clusters
alone, which is exactly the shape of what HarfBuzz answers. The consequence is the good one: **the
oldest half of `empira/PDFsharp-1.5#144` is fixed for consumers who take no shaping dependency at
all.** Their Arabic still will not join — that needs the font's `GSUB` — but it is no longer also
backwards, and the two failures were always separate.

**The fast path is a proof, not an optimisation.** Skipping itemisation for strings made only of
characters below `U+02B0` is not "close enough for Latin": Basic Latin through IPA Extensions is
script Latin or Common throughout, and no character in it has a right-to-left or Arabic
bidirectional class, so the paragraph level is 0, no rule can raise anything to an odd level, and
itemisation provably answers the single run the fast path shapes. That is what lets it be taken
without a flag and without a caveat — and it is why the golden images did not move.

### What itemising settled

**An invisible test fixture is one you cannot debug.** The stub shapers in `TextShapingSeamTests`
each answer for one sentinel string, and those sentinels had been made unique by prefixing each with
a private-use character — invisible in the source, undocumented, and perfectly serviceable for as
long as the whole string was shaped in one piece. A private-use character is script Unknown, so
itemisation gives it a run of its own, and the sentinel then reached the shaper in two pieces that
matched nothing. Six tests failed with three different symptoms, none of which pointed anywhere near
the cause. The prefixes are now `seam-`, in ASCII, with a paragraph saying why they exist.

The behaviour that exposed it is correct and is now pinned by a test of its own: an icon glyph from
a private-use area dropped into a sentence stops the words either side of it from being shaped
together. That is the right answer — nothing knows any rules for script Unknown — but it is
surprising enough to be worth stating.

**Script itemisation cannot be done to a paragraph and then applied to its runs**, which is what
this did until a line of Latin followed by Arabic came out with the space in the wrong place. UAX
#24 sweeps a Common character into the run beside it, and *beside* is not a property the paragraph
can settle: asked of the whole of "one من", the space goes with the Latin that precedes it, and
the bidirectional algorithm then puts that space in the middle of the Arabic run. The itemiser had
cut where there was no boundary and left the real one uncut. Sweeping happens inside a
bidirectional run now, where beside means something: **a run is one direction before it is one
script.**

That rule was first obeyed by giving `TextItemizer` a hand-written sweep of its own, so the same
UAX #24 walk existed twice in the same folder — a fix to one could land and miss the other.
`ScriptItemizer.Itemize` now takes a start and a length as well as a string, and `TextItemizer`
calls it once per bidirectional run instead of keeping a copy. **The window is a window, not a
substring**: the walk reads the string between the bounds directly, because the caller is
`TextShaping.ShapeText` and a string plus a list per bidirectional run is not a price that path has
to pay.

**And `ScriptItemizer` is internal now.** The reason it must be asked per run is exactly the reason
nobody outside should be able to ask it at all: a public, documented, obviously-named
`Itemize(string)` does not throw and does not look wrong on a mixed-direction paragraph — it just
disagrees with the bidirectional algorithm about where a boundary falls, which is a worse failure
mode than a wrong answer that looks wrong. `TextItemizer.Itemize` is the supported entry point and
gives the bidi-correct answer for free. A caller who has resolved their own bidi runs and genuinely
wants script boundaries inside one is a small additive change away — the internal range overload is
already the shape that request needs — but no such caller has asked. The deletion test flips as a
result: deleting `ScriptItemizer.cs` used to break nothing but its own tests, and now breaks every
`DrawString` that itemises. The eight script-only tests are reached by reflection, the way
`CharacterScanningTests` reaches `CharacterScanning`; folding them into `TextItemizer`'s tests was
rejected because several of their inputs are mixed-direction too, and a script regression would then
hide behind correct bidi behaviour.

**And the pieces of a right-to-left run have to be turned round among themselves.** A run whose
script changes half way along is still one direction, so the piece written first is the rightmost.
This went unnoticed because the one test covering two right-to-left scripts side by side asserted
`BeEquivalentTo`, which does not look at order. It says `Equal` now, in a test of its own, and the
old one carries a comment saying why it does not.

**`int[].Reverse()` means different things on `net8.0` and `net10.0`.** .NET 10 added an
`Enumerable.Reverse` overload taking an array, which beats the `MemoryExtensions` span overload that
wins on .NET 8 and returns `void`. A test compiled clean on one leg and not the other. Worth
remembering in a repository that multi-targets: write `Enumerable.Reverse(x)` when `x` is an array.

### Joining controls are inside runs, not between them

Rule X9 removes the explicit embedding controls and everything else of class `BN` before the
algorithm resolves anything, and `BidiResult.VisualOrder` therefore does not contain them. `Runs()`
built its runs by walking that order and breaking wherever the indices stopped stepping by one — so
a removed character in the middle of a run both vanished and **cut the run in two**.

U+200C ZERO WIDTH NON-JOINER and U+200D ZERO WIDTH JOINER are class `BN`. They are also the two
characters whose entire purpose is to tell the face how the letters on either side of them join:
`GSUB` reads them, a joiner asks for the joined forms and a non-joiner for the isolated ones. So the
one script that most needs them — Arabic — was the one where they were dropped, *and* where the two
letters they sat between were handed to the shaper as separate runs that could not be joined even by
default. Two failures from one line of run-splitting logic.

A run now reaches over a joining control: `Runs()` steps past one when deciding whether the run
continues, so the control ends up inside the span handed to the shaper. Both halves of that matter,
and the second is what makes it safe:

- **It is in the run**, because it exists to be read and nothing else can read it.
- **It is still not in the visual order**, and `TextShaping.Unshaped` skips it explicitly, so no
  glyph is put on the page for it. Without that second half the no-shaper path would have looked
  `.notdef` up for a character that is zero width by definition and drawn a box where nothing was.

**Only these two, not the whole of `BN`.** The rest of that class is embedding controls, which
change direction and so cannot fall inside one run anyway, and characters like U+00AD SOFT HYPHEN
that this library has always drawn whatever glyph the face maps them to. Widening the exception
would change what existing documents look like, which is a different decision from fixing Arabic.
`UnicodeProperties.IsJoiningControl` is the one place that says which they are.

---

## Item 6c — word order where the layout engine places the words

`DrawString` reorders the string it is given, so a layout engine gets bidirectional text right
exactly to the extent that it hands whole lines over.

### `XTextFormatter` — **built**

Every alignment but one joins the line back into a single string and draws it in one go, so those
needed no change at all. Justifying cannot: each word has to be placed at an x of its own for the
extra room to go between them, so the order the words are placed in is the formatter's own to get
right, and it was placing them in the order they were written.

They are now ordered by **the leftmost position any of the word's characters ends up at**, rather
than by the position of its first character — because the first character of a
right-to-left word is its rightmost. That distinction is what gets the case the obvious
implementation loses: reversing the words of a right-to-left line turns an English phrase inside it
back to front, where ordering by leftmost position keeps *its* two words in their own order and
moves only the phrase. `AnEnglishPhraseInsideAJustifiedRightToLeftLineKeepsItsOwnWordOrder` is that
case.

`XTextFormatter.TextDirection` and `XStringFormat.TextDirection` say which way a paragraph runs
rather than leaving rules P2 and P3 to guess it from the first strong character. Worth setting for
right-to-left text, because the guess is made per *line*: a paragraph of Arabic with one line that
happens to begin with a Latin word or a number would have that line laid out the other way round
from the rest of it.

### MigraDoc — **built**

MigraDoc renders **one show-text operator per leaf**: `RenderLine` walks the line's leaves and
`RenderElement` draws each at `currentXPosition` and advances it. So the words stayed where they
were written and a Hebrew sentence read inside out, even though each of its words was individually
correct.

Reordering them needs every leaf's width before any leaf is placed, and the only thing that knows a
leaf's width is the code that draws it. So the line is walked **twice**: once with `probing` set,
which advances the pen and puts nothing on the page, and then again for real with each leaf placed
where the first walk and the bidirectional algorithm say it belongs.

**The second walk is still in the order the leaves were written. Only the x changes.** That is the
decision the rest of it hangs on. Walking in visual order would have meant reordering the marked
content too - and a structure tree is meant to be in *reading* order, so it would have broken
exactly the thing tagged output exists for. It also keeps the hyperlink and broken-word scopes
nesting as they did, and keeps every stateful thing in the renderer seeing its leaves in the order
it always has. `TheMarksStayInTheOrderTheTextIsRead` asserts both orders at once, and they now
genuinely differ.

The probing walk had to be made harmless rather than merely quiet. Five `gfx.Draw` calls, the
image render, the tagging scopes and the hyperlink annotations are all skipped; the leaf's text is
collected on the way past, from inside `RenderWord` and its two siblings rather than by a second
dispatch that could drift from the first. An image contributes `U+FFFC`, which is neutral, so a
picture in a line takes the direction of the words around it.

Three things needed care:

- **`documentRenderer.NextListNumber` is a counter**, so probing a line that draws a list symbol
  would number every item twice. The symbol is drawn outside the leaf loop, so the probe never
  reaches it.
- **Underline, strikethrough and the hyperlink rectangle run from the first leaf of a stretch to
  the last**, which is one rectangle across a line that is no longer contiguous - backwards, and
  over the words between. While reordering, each leaf gets its own; where a stretch is still
  contiguous the pieces abut and the result is the same line.
- **A line with a tab in it is reordered segment by segment.** The tabs stay where formatting put
  them and the text between two of them is ordered on its own, the tab-width list being replayed for
  the second walk. Where a tab *stop* belongs in a right-to-left paragraph is still not answered;
  [tabbed-bidirectional-lines.md](tabbed-bidirectional-lines.md) says why that is the narrowest
  defensible answer.

A left-to-right paragraph is not measured twice at all: the scan that decides answers from the
characters alone, and nothing below `U+0590` is written right to left.

### Item 7 — `ParagraphFormat.TextDirection` · **built**

Now that the words move, saying which way they run means something. `ParagraphFormat.TextDirection`
carries `BidiParagraphDirection` - the same type `XStringFormat` and `XTextFormatter` take, rather
than a third enum saying the same thing - and it round-trips through MDDDL like any other property.

It was written once before this and **taken out again**, because measuring showed it changed
nothing while every string reaching `DrawString` was a single word. That is worth recording: the
property and the reordering are one feature, and either alone is inert.

## What this deliberately does not cover

- **Vertical writing mode** (CJK `vert`/`vrt2`, `/WMode 1`). A separate and substantial piece of work.
- **Automatic language detection.** The caller says what language a run is, or the shaper is told
  nothing and uses script defaults. Guessing is how you get Turkish `i` wrong.
- **Justification by kashida** (Arabic elongation) rather than by inter-word space. Correct for Arabic
  typography, well beyond a first implementation.
- **OpenType feature control** (`ss01`, `onum`, `smcp` …). The seam can carry a feature list from day
  one; exposing it on `XFont` is later work.
- **Hyphenation.** Related and separate — see the gap analysis; it is 2–3 weeks on its own and blocked
  on pattern-file licensing.
- **CFF subsetting.** Shaping makes CJK OpenType faces *more* attractive, which makes the whole-face
  embed described in `README.md` hurt more. Still a separate 4–6 week item.

## Tests

`src/PdfPinata.Test/Fonts/TextShapingSeamTests.cs` covers the seam, 22 tests: that no shaper is
registered until one is, that one can be taken away again, that a registered shaper decides which
glyphs are drawn and how wide the text measures, that measuring and drawing agree because they ask
the same seam, that a ligature draws as fewer glyphs than there were characters, that a shaper which
declines a run leaves it exactly as it was, that the face handed over carries the bytes and is the
same face every time, that word spacing is paid out where the space really fell rather than where its
character is, and the arithmetic of `ShapedRun` and `ShapedGlyph` themselves.

Every shaper in it is a stub answering a fixed script — the question is whether the library asks the
seam and believes the answer, not whether HarfBuzz works. See the note under item 1 for why they all
answer for one sentinel string and decline everything else.

`src/PdfPinata.Test/Fonts/HarfBuzzShapingTests.cs` covers the shaper itself, 17 tests, against two
faces. **Noto Sans Arabic 2.013 is now in the test assets** — SIL OFL 1.1, the same licence the three
faces already there carry, so `Assets/Fonts/LICENSE.txt` reproduces one licence for all of them. Its
em is 1000 where every Liberation face is 2048, which keeps the design-unit arithmetic honest into
the bargain.

What it proves that nothing else could:

- **A letter has four forms and the shaper picks between them.** Three identical meems in a row come
  back as three *different* glyphs — initial, medial, final — and one meem alone is a fourth, the
  isolated form. The unshaped path asks the `cmap` for each character on its own and draws the
  isolated form three times, which is the defect this whole gap exists for, in one test.
- **An attached mark is a glyph of its own with no advance and an offset in both directions.** The
  dots of `عربي` are separate glyphs that `GPOS` places against the letter they belong to: six glyphs
  for four characters, two of them with `Advance == 0` and a nonzero `OffsetX` *and* `OffsetY`.
  Nothing in a Latin face produces this, and it is the case the offset-emitting code was written for
  and previously could not be tested against.
- **A mark and its letter share one cluster**, which is what lets `/ToUnicode` say the pair means the
  one letter — six glyphs, four clusters, descending.

What Liberation Sans proves, and still does:

- **`GPOS` really ran** — `"AV"` and `"To"` are each narrower shaped together than shaped apart,
  because the face has a kern pair for them. This is the entire difference between a shaped advance
  and a summed per-character width, in one assertion.
- **`GSUB` really ran, many-to-one** — `"e"` followed by a combining acute comes back as the single
  precomposed e-acute glyph, on cluster 0. Two characters, one glyph: structurally the same thing a
  ligature is, and enough to test everything downstream that assumed one glyph per character.
- **Reordering** — a right-to-left run comes back with its glyphs reversed and its clusters
  descending, without needing a right-to-left script to do it with.
- **Surrogate pairs** — U+1F600 is one cluster and one glyph, where the unshaped path looks up each
  UTF-16 code unit separately and draws two.
- **What it will not do** — Arabic against Liberation Sans is four `.notdef` and no warning, *even
  though the Arabic face is sitting beside it in the same directory*. That is the test that says
  fallback is item 5 and is not built.

Also covered: advances are the same at 20 points and at 200 (they are design units); an empty run is
a run and not a null; rubbish where a font should be does not throw; a disposed shaper says so
rather than taking the process; and one shaper serves sixty-four threads at once with the same answer
each time.

`src/PdfPinata.Test/Fonts/ShapedFontEmbeddingTests.cs` covers what the shaped run has to leave behind
in the written file: that a glyph only the shaper knows about is given a width and embedded, that the
glyphs it did *not* choose are not carried along, that a glyph standing for several characters says
all of them in `/ToUnicode`, and — with HarfBuzz — that a composed accent comes out as one glyph
meaning both characters. Each of them failed before the write path was fixed. The last of them draws
`عربي` for real and checks the whole chain at once: six glyphs embedded and given widths, a `TJ`
displacement and a `Ts` rise in the content stream for the marks, `0 Ts` after them, and all four
characters of the word recoverable from the six glyphs.

That last test also found a defect in its own reading rather than in the library. The helper that
parses `/ToUnicode` back was scanning the whole stream for `<code><code><code>`, which matches the
`codespacerange` line above the blocks as readily as an entry inside one — so it reported a control
character as the meaning of a glyph. It reads inside `beginbfrange`/`beginbfchar` blocks only now.
Worth recording because `src/PdfPinata.Charting.Tests/Helpers/ShownText.cs` parses the same CMap the
same loose way and is linked into two test projects; it happens not to be bitten, because nothing
it draws has a glyph identifier that collides.

Its last test is the headline: `"سلام"` drawn with HarfBuzz registered, read back through the
document's own `/ToUnicode`, and asserted to spell the word *backwards along the page* — which is
what drawing it forwards means. The same test checks that the four glyphs are not the four an
isolated lookup would give, so it covers both halves, joining and order, in one place.

`src/PdfPinata.Test/Fonts/ItemizedTextTests.cs` covers itemisation from the outside, 10 tests, and
**registers no shaper at all**, because reordering is not shaping and a consumer who takes no
HarfBuzz dependency should still get it. It reads glyph identifiers back and compares them, so it
uses the Arabic face rather than Liberation Sans — a face with no Arabic in it answers `.notdef` for
every Arabic character alike, which would make a wrong order indistinguishable from a right one.
Beyond the ordering itself: that ordinary Latin is still one run and one plain `Tj`, that digits and
punctuation do not start a run of their own, that a change of script is a boundary even without a
change of direction, that a private-use character is a run of its own, that a string of several runs
measures as its runs add up to, and that each run reaches the shaper with its own script tag and
direction.

`src/PdfPinata.Test/Fonts/FontFallbackTests.cs` covers fallback, 14 tests, and its first two are a
pair worth reading together: `WithoutAFallbackACharacterTheFaceLacksIsDrawnAsNothing` pins the
starting position — four characters, four glyph zero, no complaint from anywhere — and
`WithAFallbackItIsDrawnByTheFaceThatHasIt` measures the fix against it. Then: only the part the face
cannot draw changes face; both faces are selected in the content stream and the caller's is selected
again at the end; the text is *measured* against the face that will draw it, without which every
line break below it lands in the wrong place; a family without the character in it is passed over; a
character nothing can draw is left where it is; spaces do not cut the run they sit in; and a
fallback with nothing to say about the text leaves the page byte for byte what it was.

Its last test is the one that would be worst to get wrong quietly: with HarfBuzz registered as well,
the shaper has to be handed the *fallback* face's bytes. Shaping Arabic against the bytes of a font
with no Arabic in it would answer glyph numbers belonging to the wrong file, and the page would look
plausible and be nonsense.

One branch there is not covered and cannot be: a family that resolves to nothing at all. This
suite's `PinnedFontResolver` answers every family with Liberation Sans on purpose, so that a
document asking for a font that is not shipped lays out the same way everywhere — which means
nothing in it can make the `XFont` constructor throw.

`src/PdfPinata.Test/Drawing/Layout/BidirectionalLayoutTests.cs` covers how far it gets up the layout
engine: a formatter line comes out in visual order across the words in it, and a justified one does
not. The second of those pins a limitation rather than a feature, deliberately, so that item 6c
fails a test when somebody fixes it.

The shaper-installing classes are in one xUnit collection. `GlobalFontSettings.TextShaper` is a
single setting for the whole application domain, so two tests installing shapers at the same time
are one test: whichever clears it first takes the other's away, and the other then draws unshaped
with nothing saying so. That is not hypothetical — it is what happened the first time the three
classes ran together. The collection is not the whole of the defence, either: a stub that answers
only its own sentinel is harmless to install, but a *recorder* that declines everything still sees
every string the rest of the suite draws while it is installed, so it has to name the runs it wants
back exactly rather than matching them loosely.

`src/PdfPinata.Test/Text/` holds the text side: `UnicodePropertyTests` (41, including four on the
`@missing` defaults and one sweeping all 1,114,112 code points through both lookups),
`BidiConformanceTests` (the two suites, 861,948 cases, about two seconds), and `ItemizationTests`
(25, script itemisation and its join with bidi — the part with no conformance suite of its own,
reaching the internal itemiser by reflection).

~~Still to write: a Devanagari face~~ — **since written**: Noto Sans Devanagari is in the assets and
`DevanagariShapingTests` covers the conjuncts and the reordered vowel signs Arabic cannot exercise.
Still to write: a face with deliberately missing coverage for fallback. Per `CLAUDE.md`, a test
needing its own font calls `PinnedFontResolver.Register` rather than swapping the resolver out from
under everything running beside it — though the Arabic and Devanagari faces are both served by the
resolver itself, because a family registered on first use means whatever the first caller made it
mean.

## Where this goes next

Shaping, reordering, fallback and the plumbing between them are all there, through `XGraphics`,
`XTextFormatter` and MigraDoc alike, and a page of Arabic is right even when the face it was asked
for has no Arabic in it. Of the four smaller gaps this section listed, two are closed, one is
half closed, and one is still a question rather than a task.

1. **A Devanagari face in the test assets** — **done**. Noto Sans Devanagari 2.006, OFL like the
   rest, covered by `DevanagariShapingTests`. It is the only face here whose clusters span more than
   one character: a conjunct is three characters and one glyph, and the vowel sign I is written after
   its consonant and drawn to the left of it, which is reordering inside a left-to-right run that no
   bidi algorithm can do and only a shaper can. **The line-breaking half of this item is still
   open**: a line may only be broken at a cluster boundary and nothing in the line breaker knows
   that. It does not split a conjunct at the widths tested, and a test pins that, so a breaker that
   starts cutting into clusters is caught here rather than in somebody's document.
2. **A tabbed line in a right-to-left paragraph** — **done**, by reordering each segment between
   tabs within itself and leaving the tabs where they are. Where a tab *stop* belongs when the text
   runs the other way is the part that wanted deciding, and it is still deliberately undecided; see
   [tabbed-bidirectional-lines.md](tabbed-bidirectional-lines.md).
3. **A `cmap` format 12 reader** — **done**. All three failures went together, because all three go
   through `OpenTypeDescriptor.CharCodeToGlyphIndex`: a surrogate pair drew `.notdef` twice, coverage
   could not answer for an astral character, and so font fallback could not be offered one. An emoji
   was all three at once and now works end to end, including through a fallback.

   Two things about it are load-bearing. **A code point inside the basic multilingual plane is still
   answered out of format 4** even where the face has both subtables — format 12 is a superset in
   principle and the two agree in practice, but "in practice" is not a reason to change which glyph
   every existing document draws. And **`IFontFallback.FamiliesFor` takes a code point rather than a
   `char`**, which is a breaking change to a public interface and the point of the exercise: neither
   surrogate is a character and no `cmap` maps one, so asking about them separately could only ever
   be answered "nobody".
4. **Style simulation per face** — **done**. A family with no bold file has its boldness stroked and
   widened on, which is a property of the face; a string that fell back is drawn out of more than
   one. The rendering mode and the character spacing are written per segment and put back to what
   `PdfGraphicsState` believes before the string ends. Stroking needs a colour and a width, which are
   graphics state rather than text state and cannot be varied from inside the built string, so the
   string is shaped *before* the font is realized and they are set up once for whichever faces turn
   out to need them. `FontHelper.BoldSimulationSpacing` is the one rule the measuring path reads too,
   or a line would be laid out at a width the page does not draw.

## Related

- `docs/specs/font-embedding-gaps.md` — the existing account of what embedding does and does not do.
- `docs/specs/layout-api-decision.md` — a new layout API should not be built on unshaped text.
