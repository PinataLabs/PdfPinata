# Spec — the demonstration app

Unlike its neighbours this spec is tied to no upstream issue. It records the scope of the fork's own
tooling: what `SampleApp` becomes, and what it deliberately does not try to be. It was written
before the work rather than after it, so the status column tracks progress.

| item | what | status |
|---|---|---|
| 1 | A command line that runs one demo, several, or all of them | done |
| 2 | Fonts that are the same on every machine | done |
| 3 | The source of each demo, printed from the file that ran | done |
| 4 | Thirteen demos, one PDF each, covering the drawing surface | done |
| 5 | A smoke test so a broken demo fails the build | done, 74 tests |
| 6 | Three more, covering the interactive layer: forms, annotations, outlines | done |
| 7 | Six more, covering what landed after the drawing surface was demonstrated | done |

---

## The defect

`SampleApp` is the only runnable sample in the repository and it demonstrates almost nothing.

It is one 145-line `Program.cs`. `Main` draws a string, then three `XTextFormatter` boxes with
translucent rectangles over them, and saves `test1.pdf`. Then it calls
`ResizeAnA4DocumentWithLinksToA5`, which is a careful and well-commented piece of work about page
resizing that has no relationship to the text formatting above it, and saves `resized-a5.pdf`. There
is no way to run one without the other, nothing that says what either produces, and the output goes
to `"."` — the current directory, which under `dotnet run` is the project folder and under a
published binary is wherever the user happened to be standing.

Neither `README.md` nor `CLAUDE.md` mentions the project exists.

Meanwhile the capability with no demonstration has been piling up:

```text
  built                                       demonstrated by
  ────────────────────────────────────────    ───────────────
  complete ISO/DIN page sizes                 nothing
  PdfPage.Resize, PdfDocument.ResizePages     SampleApp, by accident
  word and character spacing, Tz, text rise   nothing
  stroke / fill / stroke+fill text            nothing
  underline and strikeout decorations         nothing
  XTextFormatter columns, ellipsis, rotation  nothing
  named destinations, the link helpers        SampleApp, one call
  text markup annotations                     nothing
```

`docs/PdfPinata/samples/` describes 21 samples in prose with no code behind any of them, and
`TextLayout.md` documents an `XTextFormatter.DrawString` overload that does not exist. Prose that
nothing compiles is prose that rots, and it has.

---

## Item 1 — the command line

`System.CommandLine` 2.0.11, which is the stable post-GA package and ships `lib/net8.0`, the target
this project builds for. `Spectre.Console` renders the output; it is not `Spectre.Console.Cli`,
because one argument parser is enough.

The app targets `net8.0` alone rather than the library's set, so that `dotnet run` needs no `-f`.
Not `net10.0` alone: `PdfPinata.Test` references this project to run the demos and builds a
`net8.0` leg, which could not reference a `net10.0` one.

```text
SampleApp list                              what each demo shows
SampleApp run                               all of them
SampleApp run --example Fonts Text          two of them
SampleApp run -e fonts                      names are matched case-insensitively
SampleApp run --output C:\somewhere         somewhere other than ./output
SampleApp run --no-code                     PDFs only, no source printed
```

Each demo writes exactly one file, `<Name>.pdf`, into the output directory, and an existing file of
that name is deleted before the demo runs rather than written over. Writing over it would leave a
stale file behind if the demo threw halfway, and a stale PDF that opens is worse than no PDF.

An unknown name is rejected by an option validator that names the value and lists the valid ones. A
demo that throws is reported, the rest still run, and the process exits non-zero — a demo suite that
stops at the first failure tells you about one break per run.

## Item 2 — fonts that do not depend on the machine

`SkiaFontResolver` scans the platform's font directories, which is right for the library and wrong
for a demonstration. Arial is resolved on Windows, something else on macOS, and on a bare Linux
container frequently nothing at all. A demo of *fonts* that renders whatever the machine has is not
a demo of anything.

So the app carries its own, as `EmbeddedResource`:

```text
Liberation Sans    Regular Bold Italic BoldItalic    sans, Arial metrics
Liberation Serif   Regular Bold Italic BoldItalic    serif, Times metrics
Source Code Pro    Regular                           mono, and PostScript (CFF) outlines
```

All three are SIL OFL, and one `LICENSE.txt` covers them.

Source Code Pro is there twice over. It is the mono face the newspaper and magazine layouts want,
and it is the only font here whose outlines are CFF rather than TrueType — a different embedding
path, and one that cannot be subsetted. Shipping only its regular face is also deliberate: a request
for bold or italic comes back with `XStyleSimulations`, so the Fonts demo can show simulated weight
and slant beside real ones and label which is which.

`BundledFontResolver` differs from the test suite's `PinnedFontResolver` in the one way that matters
here. `PinnedFontResolver` answers *every* family with Liberation Sans, because a test wants the
same metrics whatever it asks for. A demo wants the opposite: three families that look like three
families. So the resolver maps each name to its own file, and falls back to the sans for anything it
does not recognise.

### Where the registration lives, and why it matters

`GlobalFontSettings.FontResolver` refuses to change once a font has been used:

```csharp
if (FontFactory.HasFontSources)
    throw new InvalidOperationException("Must not change font resolver after is was once used.");
```

The test assembly installs `PinnedFontResolver` from a `[ModuleInitializer]` for everything in it,
including — once item 5 lands — these demos. If a demo registered a resolver of its own it would
either throw outright, or win the race and quietly move every golden-image test in the suite onto
different font metrics.

So **no demo touches a static seam.** Registration happens once, in `Backends.EnsureRegistered`,
called from `DemoRunner.Run` and from nowhere else — a path only `Program.Main` reaches, and one
`list` and `--help` never take. A demo is handed a document and fills it in. Under the test host it
draws with whatever resolver is already installed, which is what makes it safe to run there.

## Item 3 — printing the source

The app exists to show code as much as output, and code shown beside code that ran is code that will
drift apart from it. So nothing is transcribed. Each demo marks its example:

```csharp
#region example
// ... the drawing, exactly as it runs
#endregion
```

and the printer reads the demo's own `.cs` file, takes what lies between the markers, dedents it and
hands it to Spectre. Region markers rather than parsing out a method body: a scan for two literal
strings cannot be defeated by a brace inside a string literal, and the region means something in the
IDE too.

The file is found by capturing `[CallerFilePath]` in the base constructor, which the compiler fills
in at the derived class's `: base()` call and so records each demo's own file. That path is the
build machine's, so it is used only when it still exists — which makes editing a demo and re-running
show the edit — and otherwise the file's leaf name indexes an embedded copy.

### Why everything is embedded

Fonts, images and sources are all `EmbeddedResource`, read through one loader. The alternative,
`CopyToOutputDirectory`, has to work in three places:

```text
  src/SampleApp/bin/…/            files land here                  ✓
  publish output              files land here                  ✓
  src/PdfPinata.Test/bin/…/    they do NOT — a referenced        ✗
                              project's content items do not
                              flow to the referencing project
```

The third is where the smoke test runs. Embedding removes the question: there is no path to resolve,
nothing to copy, no dependence on the working directory, and the test gets the assets for free. It
costs about 3MB inside a binary that is never packaged.

## Item 4 — the demos

Thirteen, one PDF each.

| name | shows |
|---|---|
| `HelloWorld` | the minimum document, and every field of `PdfDocument.Info` |
| `Fonts` | three families × four styles, a size ramp, the six `XTextDecoration` line styles, simulated versus real bold and italic |
| `Orientation` | portrait and landscape, A3/A4/A6/Letter/Legal captioned with their size in millimetres and points, and a page with `Rotate` set |
| `Images` | one JPEG at natural size, half, double, stretched out of proportion, letterboxed to fit, centre-cropped, and rotated |
| `Text` | colour through RGB, CMYK and grey; rotation; word, character and horizontal spacing; fill against stroke against both; text rise; baseline alignment; web and named links |
| `Layout` | `XTextFormatter` — wrapping, the four alignments including justified, columns, indents and gaps, ellipsis truncation, rotation, and `GetLayout` measuring a box before it is drawn |
| `Tables` | MigraDoc — repeated header rows, merged cells, shading, borders, a totals row |
| `PageResize` | an A4 document with a link, shrunk to A5, content and link and destination moving together |
| `Bleed` | `PdfPage.TrimMargins` — a photograph drawn from negative coordinates onto a sheet larger than the page, with the trim edge marked and the five page boxes listed |
| `Invoice` | MigraDoc — styles, header and footer page fields, tab stops, line items, totals |
| `Newspaper` | masthead, a headline across the measure, five columns of body, a sidebar, a captioned image |
| `Magazine` | a full-bleed image under a gradient scrim, a title built with `AddString`, a drop cap from `XTextFormatter.DropCap`, and a slanted pull-quote straddling the gutter with the copy flowing down both sides of it from `XTextFormatter.Obstacles` |
| `SideWrap` | MigraDoc — `WrapFormat.Style` on a text frame, one page for each of the four side-wrapping styles |

`Layout` and `PageResize` are where the two existing samples end up. Neither is deleted; both are
given a name, a description and a file of their own.

The split between the last three is not arbitrary. `Invoice` is MigraDoc's kind of document — flowed
content, styles, a table that breaks across pages. `Newspaper` and `Magazine` are not: **MigraDoc
has no multi-column page setup**, so a newspaper laid out through it would have to be hand-positioned
text frames. `XTextFormatter.Columns` does the job directly, which is why those two are drawn on the
PdfSharp side. The three together are also the honest answer to "which API do I reach for" — the
demos disagree with each other on purpose.

`SideWrap` is the fourth in that argument. It is `Magazine`'s pull quote done a level up: one
paragraph, one text frame, one property, and the renderer breaking the lines.

Both engines flow text beside things now, and the pair is still worth reading side by side — what
differs is no longer *whether* but *what you hand it*. MigraDoc is given a **shape in a document
tree** and works out where it lands; `XTextFormatter` is given a **rectangle in the block's own
coordinates** by a caller who drew it and therefore knows. Neither is the other's implementation, and
the choice is the same one it always was: whether the page is laid out or drawn.

Item 6 adds three more, listed under it below.

### Where the drawing surface is thinner than it looks

Worth knowing before writing a demo that promises more than exists:

- There is no fit-to-box or cover helper for images. `Images` computes those rectangles itself, and
  that computation *is* the demonstration.
- There is no list support outside MigraDoc's `ListInfo`. `Layout` draws its bullets by hand;
  `Invoice` has the real one.
- There is no kerning and no OpenType feature support at all — see `pdfkit-text-parity.md`. No demo
  should imply otherwise.
- `XGraphics.DrawString` does not wrap, and draws `\n` literally. Wrapping is `XTextFormatter`'s job
  and the demos should not blur the two.
- `XTextFormatter` flows text around a **rectangle**, not around a contour. `RectangleObstacle` is
  the only `IFlowObstacle` that ships; an ellipse, a polygon and an `XGraphicsPath` are new
  implementations of that interface rather than a redesign, but they are not written. A demo should
  not imply text follows a silhouette.
- A line is given **one** run of free space, the widest. An obstacle standing in the middle of a
  measure leaves a run either side and the narrower one stays empty, which is what MigraDoc does too.

A drop cap used to be on this list. `XTextFormatter.DropCap` is now a property: it takes the first
character of the text, scales it so its foot rests on the last reserved line's baseline, reserves the
room and shortens the lines beside it. `Magazine` used to spend thirty lines drawing the letter and
then adding one word at a time to a probe rectangle until the answer stopped fitting — a workaround
the demo was teaching as though it were a technique.

### Four gaps this app found, and closed

Four more turned up while building, each of which had a demo claiming something the library did not
do. Three of them failed **silently** — no exception, no warning, just a page missing the thing that
was asked for. They were found because something drew a page and a human looked at it; nothing in
the test suite would have caught any of them.

All four were fixed under the `fix-drawing-gaps` change, which this app is what prompted. They are
recorded here because *finding* them is the clearest argument the app makes for its own existence:

- **A gradient carried no transparency.** `PdfShading` wrote `/C0` and `/C1` and no soft mask, so a
  gradient from a transparent colour to an opaque one rendered as a flat opaque band. `Magazine`
  built its scrim from a hundred and forty translucent solid fills. It now draws one gradient.
- **`XGraphicsPath.AddString` was not implemented.** It did not throw — it reported through
  `DiagnosticsHelper` and drew nothing, so a title built that way silently disappeared. `Magazine`
  stroked its title with a pen instead. It now builds a path and fills it with a gradient, which is
  the thing a path can do that no `DrawString` overload can.
- **`XLineAlignment.BaseLine` demanded a zero-height rectangle.** Passing a height threw rather than
  being ignored — including for `XStringFormats.Default`, which *is* `BaseLineLeft`. The height is
  now unread rather than refused.
- **A repeated table heading had to start at row zero and said nothing when it did not.**
  `TableRenderer.CalcLastHeaderRow` walks from the first row and stops at the first without
  `HeadingFormat`. A title band above the column names ended the run before it started and nothing
  repeated. The rule is unchanged; a row marked outside the run is now refused rather than discarded.

A fifth turned up while fixing the first: **no gradient the library wrote was visible in a
conformant reader at all.** An RGB shading's ramp carried a fourth value, the colour's alpha, which
is not a colour component — and Ghostscript answers a function wider than its colour space by
painting nothing. That is why the scrim above was built from solid fills rather than from a gradient
that merely looked wrong.

### And one that worked all along

`Magazine`'s photograph is described above as "bled off three edges". It is not, in the sense a
printer means: it runs to the edge of an ordinary page, so a cut landing a fraction inside it leaves
a white line. A real bleed needs a sheet larger than the page and artwork drawn past the page's edge
onto it.

`PdfPage.TrimMargins` does exactly that, and always has. It moves the drawing origin to the trimmed
page's corner, grows the sheet by the margins, and writes all five page boxes on save. It had **no
test of any kind** and no demo, and nothing in the documentation said it existed — so a caller
wanting a bleed had no way to find out that the library already did it.

This is the same problem as the five above, arrived at from the other side. There, a feature
silently did nothing. Here, a feature silently did the right thing and nobody knew. Both are one
refactor away from being a broken feature nobody notices, and the demo is what makes the difference
in each case: `Bleed` exists so that the feature is discoverable, and the tests behind it exist so
that it stays working.

Writing those tests found three defects in it, all sharing one cause — `PdfPage.PrepareForSave`
derived the sheet from `Width`, and `Width` reads the media box it then overwrote. A trimmed page
therefore grew every time it was saved, stopped reporting its own size once it had been, and put
`/TrimBox` on the wrong edges whenever the top and bottom margins differed. All three are fixed, and
the tests that recorded them now assert the fix. A fourth thing the tests made plain was that
`/BleedBox` was written equal to `/MediaBox`, leaving nowhere on the sheet for a crop mark; the boxes
now nest properly and the marks are drawn. See `openspec/specs/page-bleed/spec.md`, which is where
that change's delta spec was promoted when it was archived.

## Item 5 — the smoke test

One theory in `PdfPinata.Test`, over the demo registry, running each demo into the test
assembly's output directory and asserting the PDF opens and has the page count the demo declares.
Adding a demo enrols it; there is no list to keep in step. A second test asserts each demo's source
region was found and is not empty, so a demo whose markers went missing fails rather than printing
nothing.

That is a test that a demo still *runs*, not that it still demonstrates what it claims. Nothing
automatic can check the second thing, which is why item 4's table is written in terms of what a
reader should look for.

## Item 6 — the interactive layer

The thirteen demos above are all about ink. Nothing among them touched the part of PDF that a reader
*does* something with: form fields, annotations, bookmarks. Three more, on the same terms — one PDF
each, enrolled in the smoke test by being added to the registry.

| name | shows |
|---|---|
| `Forms` | an AcroForm: five text fields (plain, required, password, comb, multiline), a check box, a three-widget radio group, a combo box, a list box, a push button carrying a URI action |
| `Annotations` | the four text markup subtypes over the words they mark, a markup spanning a line break as two quads in one annotation, the seven note icons, web/page/named links, a file attachment carrying its bytes, four rubber stamps, and a parity table against PDFKit |
| `Outline` | a three-level bookmark tree, entries landing on the heading rather than the page, the entry styles and colours, and all eight destination types |

The three were chosen against [PDFKit's documentation](https://pdfkit.org/docs/annotations.html)
rather than against this library's own surface, because a parity list only means something measured
from outside. That is also what makes the gaps below findable.

### `Forms` is the odd one out, and says so on the page

**The typed AcroForm API could not author a form.** `PdfAcroForm`, all eight `PdfAcroField`
subclasses and `PdfWidgetAnnotation` had `internal` constructors; `PdfAcroFieldCollection` had no
`Add`; and `PdfDocument.Catalog` is still `internal`. The API read and filled a form somebody else
wrote, which is a real and useful thing, and was not the thing anybody asking "how do I add a text
box" wants.

So the demo assembled ISO 32000-1 §12.7 out of `PdfDictionary` — possible from outside the assembly
through `PdfInternals.AddObject` and `PdfInternals.Catalog` — and page two of the PDF was the table
of what the typed API *did* offer, so the demo was a workaround and the documentation of why one was
needed at the same time.

**That gap is closed**, and the demo is now what it always should have been: ten fields made with
`new`, named, flagged, added to `document.GetOrCreateAcroForm()` and placed with
`PdfAcroField.AddWidget`, with every appearance stream drawn through `XGraphics.FromForm`. Page two
is still a table, but of where each capability lives rather than of what is missing. Two entries are
still written by name because nothing wraps them: `/MK` and the push button's `/A` action.
`interactive-layer-gaps.md` item 1 has the design and the four defects that closing it uncovered.

It round-trips: reopening the file gives `PdfTextField`, `PdfCheckBoxField`, `PdfRadioButtonField`,
`PdfComboBoxField`, `PdfListBoxField` and `PdfPushButtonField`, each with the right flags and, where
the field type has one, the right value — a push button has none, since it exists for its action.

### Four things these three found

Two are defects in the library, and two are traps that caught this app's own first draft. The full
inventory — ten items, including the ones the demos worked around rather than tripped over — is
`interactive-layer-gaps.md`.

- **`PdfOutline.Opened` was never written.** A reader takes an entry's expanded state from
  `/Count`, and `PrepareForSave` wrote that key only `if (OpenCount > 0)` — a field assigned in
  one place, `PdfOutlineCollection.Add`, which credited the new entry's *ancestors* and ran once.
  So the `opened` argument on four constructors and four `Outlines.Add` overloads was accepted and
  dropped, no outline item carried `/Count` at all, and every tree arrived collapsed. `Style` and
  `TextColor` beside it both wrote correctly, which is what made it look as though `Opened` should
  have. **Fixed**, with 7 tests — see `bookmarks-and-outlines.md` item 5. `Outline`'s chapter 2 is
  now the branch that arrives shut, and is a regression test a human can see.
- **`PdfInternals.CreateIndirectObject<T>()` cannot work.** Its body declares
  `ConstructorInfo ctorInfo = null; // TODO`, tests it for null, and so always falls through to
  `return result` with `result` still null — behind a `Debug.Assert` that fires in a debug build and
  says nothing in a release one. `AddObject` is the working route, and is what `Forms` uses.
- **`/DA` with a zero font size is not portable.** Zero means auto-size. On a single-line field
  every viewer does something sensible; on a multiline one Ghostscript scales the first line to the
  height of the whole box, so a two-line value fills the page. Every field in `Forms` names its
  size. This is the demo working as intended — it was found by rasterizing the page and looking.
- **A content stream has no `arc` operator.** That is PostScript. The first draft of the radio group
  drew its rings with `arc`, a viewer handed one draws *nothing* rather than complaining, and the
  result was two invisible radio buttons and one that showed only because its dot is a ZapfDingbats
  glyph. The rings are four Béziers now.

The first two are the same shape as the four gaps item 4 found: no exception, no warning, a
property that reads back exactly as it was set, and a file that quietly does not contain it.

## Item 7 — the features that landed after the app did

Items 4 and 6 demonstrated the drawing surface and the interactive layer. Everything built since —
text shaping and bidi, tagged output and PDF/UA, PDF/A, signing, text extraction, incremental
update — had no demonstration at all, which is the state item 4 was written to end. Six more, on the
same terms: one PDF each, enrolled in the smoke test by being added to the registry.

| name | shows |
|---|---|
| `International` | Hebrew and Arabic reordered with no shaper needed for it, an English word inside a right-to-left sentence, `TextDirection` on `XStringFormat` and `XTextFormatter`, Arabic joined through `PdfPinata.HarfBuzz`, U+200C asking it not to, and `FontFallback` drawing Arabic in a document that asked for a Latin face |
| `Accessibility` | `TagContent`, headings becoming `/H1`…`/H6` from `OutlineLevel`, a heading row becoming `/TH` with `/Scope /Column`, `Table.Summary`, `Image.AlternativeText` deciding between a described `/Figure` and an artifact, and the four PDF/UA-1 refusals |
| `Archive` | `PdfAConformance` across all three parts, the XMP packet read back out of a probe's own bytes, `CustomizeMetadata` and `AdditionalDescriptions`, the output intent, and the five PDF/A refusals |
| `Signing` | `PdfSigner`, `Pkcs7Signer`, a caller-drawn appearance, `PdfSignatures.InDocument`, and `PdfSignatureVerifier` answering `IsIntact` and `CoversWholeDocument` separately |
| `Extract` | `PdfTextExtractor.ExtractText` and `ExtractRuns` over a document the demo wrote, saved and opened again, including a two-column page coming back interleaved |
| `Revise` | `SaveIncremental` against `Save`, `PdfDocumentOpenMode.Append`, the `/Prev` chain counted in the bytes, and the trap of appending into the file it was read from |
| `FacturX` | `FacturXInvoice.AttachTo` — the file name, the `/Data` relationship and the media type the standard fixes; that attaching is what claims PDF/A-3; the XMP extension schema declaring the `fx:` properties; `EInvoiceProfile`'s exact spellings; and `FindIn` / `ReadFrom` as the receiving half |

`Compress` gained the setting its own summary already promised — `CrossReferenceFormat`, measured
twice on a third page of its own, because one page of drawing is the shape an object stream has
least to offer and a table showing only that number teaches the opposite of what is true.

### Three demos now report what the library said, rather than quoting it

`Accessibility`, `Archive` and `Signing` all build throwaway documents, ask them to save, and print
the exception that came back. Nine refusal messages reach the page that way, and none of them is a
string literal in the demo. A quotation goes stale the day a message is reworded and nothing says so;
a caught message cannot. Where a rule stops being enforced the page prints *"This document saved. The
rule is no longer enforced"* in place of the message — said rather than asserted, because a demo is
not a test and a silent stale quotation is the failure worth avoiding.

### The e-invoice demo, an ICC profile that may be shipped, and a defect in `Archive`

`FacturX` is the seventh of these: a page a person reads, the same invoice attached as UN/CEFACT CII
XML, and a second page reporting what the attachment had to satisfy — the file name, the
`/AFRelationship`, the media type and the conformance nobody set, all read back out of the document
rather than described, then `FacturXInvoice.FindIn` and `ReadFrom` answering from a reopened file the
way a receiving system would. It costs the seventh project reference, `PdfPinata.EInvoice`, which
carries no dependency of its own.

**It needed an ICC profile, and that is what unblocked it.** A demo claiming PDF/A-3 has to embed an
output intent, none shipped, and `Archive` had been building a minimal one in code — its own remark
said a binary in a sample app "raises a licence question the sample does not need". The question has
an answer: `src/assets/icc/sRGB-v2-micro.icc`, 456 bytes released to the public domain under CC0, with
its provenance in `src/assets/icc/LICENSE.txt` and its own `cprt` tag reading `CC0`. 250 lines of ICC
byte-writing left `ArchiveDemo` with it.

The library then took the profile over, so **neither demo carries one**: the core package embeds it,
and an RGB document claiming PDF/A that names no profile is given it. The two demos divide the
subject between them, which is better than both showing the same thing — `Archive` sets
`PdfOutputIntents.SrgbProfile` explicitly, because its page is about the output intent and about the
CMYK refusal that remains; `FacturX` sets nothing about colour at all and still conforms, which is
what a caller now gets for free.

**Then veraPDF was pointed at both outputs, and `Archive` failed.** It writes a property in a
namespace of its own — `sample:demo`, through `CustomizeMetadata` — and PDF/A clause 6.6.2.3.1 admits
no property whose schema the file has not either predefined or described. There was no extension
schema, so the file carried a PDF/A-3b claim a validator rejects, in the demo whose whole subject is
that a claim is checked rather than stamped on. It now declares the namespace in a
`pdfaExtension:schemas` block before writing the property, passes, and says on the page that this is
what a namespace of one's own costs. The rule reached the demo the only way it could have: from
outside.

### `Save` is now overridable, for the two demos saving would break

`PdfDemo.Run` built a document and called `document.Save(path)`. That is right for thirty-four demos
and destroys the output of two. `Save` writes a file afresh from the object model: it renumbers the
objects and drops the bytes the document was read from, which invalidates every signature on the file
and discards every earlier revision. Signing and incremental update are precisely the two features
whose output cannot survive it, so `PdfDemo.Save(PdfDocument, string)` is `virtual` and those two
write their own bytes — `PdfSigner.Sign` into the file for one, `SaveIncremental` into it for the
other.

Both outputs are the real thing rather than a picture of it. `Signing.pdf` verifies: one signature,
`/ETSI.CAdES.detached`, intact and covering the whole document. `Revise.pdf` carries three
`startxref` markers, three `%%EOF`s and two `/Prev` entries.

### Two more project references, and two more seams registered

`PdfPinata.HarfBuzz` and `PdfPinata.Signing`, both for one demo each. Neither is a dependency
the library forces on anyone — that is the point of both being packages of their own — and writing
them into the project file is how a reader can see what the two demos actually cost.

`Backends.EnsureRegistered` now fills all five static seams rather than three. The two new ones are
the two whose unset state is not an error, so they are registered with `??=` and a demo that finds
them null is not broken. They are registered there for the same reason as the other three and it
matters more here: `TextShaper` and `FontFallback` are process-wide, the smoke test runs demos inside
a host shared with every other test in the assembly, and nothing calls `EnsureRegistered` from there.
So under test `International` draws its Arabic unshaped — which is exactly what a caller who takes no
shaper gets — and its page count does not depend on either seam. Its pages read the seams and say
which way they were drawn.

A fourth font came with it: Noto Sans Arabic, because none of the other three has a single Arabic
glyph, and a fallback demo needs a face to fall back *to*. SIL OFL like the rest, and covered by the
same `LICENSE.txt`.

### The right-to-left source problem, and what it cost

`International` needs Hebrew and Arabic in a C# file, and a source file mixing right-to-left text
with left-to-right code is one no editor renders the way anybody means — the quotation marks appear
on the wrong side of the string and a reader cannot see where the text ends and the code begins.
Escapes are the usual answer. This file instead builds every string from code points:

```csharp
string hebrew = From(0x05E9, 0x05DC, 0x05D5, 0x05DD);
```

so the file is provably ASCII throughout, which no review of escapes can establish by eye. It is
also, unlike escapes, robust against a tool that writes the literal character back.

---

## Deliberately not done

- **No golden images.** The machinery exists, and using it here would make every deliberate
  improvement to a demo's appearance a reference-image update. Demos are meant to be edited.
- **No reflection over the assembly to discover demos.** An explicit registry is one line longer per
  demo and gives the smoke test a real data source instead of an implicit one.
- **No `Spectre.Console.Cli`.** It is a fine argument parser and this project already chose one.
- **The prose samples under `docs/` are not deleted or rewritten.** They cover ground the demos do
  not — splitting, concatenating, protecting, exporting images — and that is a separate piece of
  work. `TextLayout.md`, which documents an overload that no longer exists, is worth fixing on its
  own account.
- ~~**No charting demo.**~~ This held while the brief was the drawing surface. `Charts` exists now,
  and the fourth project reference it was going to cost turned out to be one MigraDoc already
  carried — the reference is written out in the project file so a reader can see it rather than
  inherit it.
- **No demo of the A levels of PDF/A.** `PdfAConformance` carries only the B levels, because A
  additionally requires a full tagged structure tree; `Archive` claims PDF/A-3b and `Accessibility`
  claims PDF/UA-1, and a document claiming both is a third thing neither demo builds.
- **No timestamped signature.** PAdES B-T needs a token from a time-stamping authority, which is not
  implemented and would need a network call from a sample app in any case. `Signing` says on the page
  that its claimed time is the producer's own clock and proves nothing.
- ~~**No veraPDF in the loop.**~~ Still true of the build — the demos are not validated by CI, and
  `ConformanceCorpus` is what is. But veraPDF was pointed at the two conformance demos' output by
  hand once it existed, and it found something; see below.
