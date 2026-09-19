# Spec — closing the demonstration app's coverage gaps

This continues `demonstration-app.md`, which built `SampleApp` and stopped at sixteen demos. Like
that one it is tied to no upstream issue: it is the fork's own tooling. It was written before the
work, so the status column tracks progress.

The sixteen demos cover the drawing surface and the interactive layer. Measured against the
library's actual public surface they leave whole assemblies untouched — charting, bar codes,
encryption, document assembly, form XObjects — and leave most of MigraDoc's structural features and
most of `XGraphics`' vector methods with nothing a reader can look at.

| item | what | status |
|---|---|---|
| 1 | `Charts` — the charting assembly, both routes into it | done, 4 pages |
| 2 | `Assemble` — merge, split, reorder, import, prune, consolidate | done, 7 pages |
| 3 | `Imposition` — `XForm`, `XPdfForm`, watermark, 2-up, booklet | done, 5 pages |
| 4 | `Vectors` — the eleven `Draw*` methods no demo calls | done, 4 pages |
| 5 | `Barcodes` — the three linear codes and DataMatrix | done, 3 pages |
| 6 | `Protect` — passwords, permissions, and reading one back | done, 2 pages |
| 7 | `Navigation` — page labels, viewer preferences, custom values | done, 6 pages |
| 8 | `Compress` — `PdfDocumentOptions`, measured in bytes | done, 2 pages |
| 9 | `Inspect` — reading back the content stream that was written | done, 3 pages |
| 10 | `Unicode` — `PdfFontEncoding`, CID fonts, CFF versus TrueType | done, 2 pages |
| 11 | `Structure` — MigraDoc's TOC, bookmarks, sections, lists | done, 5 pages |
| 12 | `Ddl` — MigraDoc's own serialisation format, round-tripped | done, 4 pages |
| 13 | `Images` extended — alpha and interpolation; see the note | mostly done |
| 14 | L1 — MigraDoc drops a `Footnote` silently | superseded: footnotes render, item 34 |
| 15 | L2 — MigraDoc drops a `Barcode` shape silently | done, 3 tests |
| 16 | L3 — `BarCode.FromType` reaches two of the four code types | done, 7 tests |
| 17 | Infrastructure — an optional password, so item 6 can be real | done |
| 18 | L4 — a chart draws its plot area at `NaN` unless an axis was read | fixed on `main` as C1; tests kept |
| 19 | L5 — a combination chart prints its legend on top of itself | done, 2 tests |
| 20 | L6 — a pie signs and scales its percentages twice | done, 7 tests |
| 21 | L7 — three `XGraphicsPath` members collect geometry and drop it | done, 10 tests |
| 22 | L8 — a pen made from a brush strokes with no alpha | done, 4 tests |
| 23 | L9 — the miter limit is guarded by the line cap, and truncated | done, 4 tests |
| 24 | L10 — interleaved 2 of 5 checks nothing and fails at drawing time | done, 9 tests |
| 25 | L11 — `HasOwnerPermissions` is a constant `true` | done, 7 tests |
| 26 | L12 — `CSequence` throws for every interface member it declares | done, 5 tests |
| 27 | L13 — `CompressContentStreams` defaults differently per build | done |
| 28 | `ImageFailures` — an image that will not load, and the event that says why | done, 2 pages |
| 29 | The renderer refuses to write `NaN` or infinity into a content stream | done, 12 tests |
| 30 | L14 — a `CodeDataMatrix` given no size draws at negative infinity | done, 1 test |
| 31 | L15 — an image of no pixels measures to `NaN` and reports nothing | done, 8 tests |
| 32 | The prose samples under `docs/` point at the demos | done, 24 pages |
| 33 | A spec for MigraDoc footnote rendering | done, `migradoc-footnotes.md` |
| 34 | MigraDoc footnote rendering, built to that spec | done, 24 tests |
| 35 | `Footnotes` — the demo for it | done, 5 pages |

Items 14 to 16 are defects found while surveying for the rest. Items 18 to 20 were found by
*building* item 1 and looking at the page it drew, items 21 to 23 the same way from item 4, item 30
by turning item 29's guard on, and item 31 by building item 28 — which is the demonstration app
working exactly as `demonstration-app.md` argued it would. They are in this spec rather than in ones
of their own because finding them is what the app is for, and the same argument applies to all
eleven: no exception, no warning, a property that reads back exactly as it was set, and a file that
quietly does not contain what was asked for.

Items 28 to 33 were done in a second pass, after the first was reviewed. Four of them — 27, 28, 29
and 32 — were open questions at the end of the first pass rather than gaps, because each was a
decision about the library's behaviour or its documentation rather than about its demonstrations.
They were put to whoever owns that call and are recorded here as taken.

Items 34 and 35 are the third pass, and they close item 14 rather than adding to it. Footnote
layout was written to the spec item 33 produced, with its three open choices settled first: a note
is never split across a page break, a note outside a section's own paragraphs is refused, and both
`FootnoteLocation` values are implemented. `migradoc-footnotes.md` records each decision beside the
item it settles.

Item 18 is the worst thing in this list. It is not a demonstration gap at all — **every column, bar,
line and area chart this library has ever drawn came out empty** unless the caller happened to touch
`chart.XAxis` before rendering, and touching it means *reading* the property, not setting anything on
it. It affects both routes into the engine, it has been there since the fork, and nothing in the
test suite could have caught it because nothing rendered a chart.

It is also the one item here whose fix is not this branch's. `main` found it independently while this
work was in flight, by writing `PdfPinata.Charting.Tests`, and fixed it as C1 of
`charting-renderer-findings.md` — a better fix than this branch's, for the reason given under item 18
below. Two routes to the same defect within a week of each other, neither of which existed a month
ago, is the argument for both pieces of work rather than against either.

---

## What the survey measured against

Not the demos' own list. Each of the following is a public, implemented, shipped capability with no
demonstration of any kind, found by walking the assemblies rather than the documentation:

```text
  assembly / namespace                         demonstrated by
  ─────────────────────────────────────────    ───────────────
  PdfPinata.Charting, 8 chart types         nothing
  MigraDoc Chart + ChartMapper                 nothing
  Drawing.BarCodes, 3 linear + DataMatrix      nothing
  Pdf.Security, RC4 40/128, 8 permissions      nothing
  ImportPage/DuplicatePage/MovePage/PlacePage  nothing
  PruneUnusedResources, ConsolidateImages      nothing
  XForm, XPdfForm                              nothing
  DrawEllipse/Arc/Pie/Polygon/Curve/Bezier/…   nothing
  XRadialGradientBrush, pen caps, joins, dash  nothing
  XGraphicsContainer, Scale/Translate/Multiply nothing
  PageLabels, ViewerPreferences, CustomValues  nothing
  PdfDocumentOptions, all five                 nothing
  Pdf.Content.ContentReader, the CObject model nothing
  XPdfFontOptions.Unicode vs WinAnsi           nothing
  MigraDoc TOC, OutlineLevel, ListInfo, DDL    nothing
  DocumentRenderer.ImageFailed                 nothing
```

`docs/PdfPinata/samples/` describes six of these in prose with no code behind any of them —
`CombineDocuments`, `ConcatenateDocuments`, `SplitDocument`, `Booklet`, `TwoPagesOnOne`,
`Watermark`. `demonstration-app.md` left those alone on the grounds that they were a separate piece
of work. Items 2 and 3 are that piece of work.

## Two rules carried forward, and one relaxed

The two load-bearing rules from `demonstration-app.md` are unchanged. **No demo registers a
backend** — registration stays in `Backends.EnsureRegistered`, reached only from `Program.Main`,
because the smoke test runs these inside a host that has already installed `PinnedFontResolver`.
**Assets are embedded resources, not content files**, because a referenced project's content items
do not reach the referencing project's output directory.

The rule that bends is *one file per demo*, and it bends only far enough for item 6. Every demo here
still writes exactly one PDF. Where a demo needs a second document — two files to merge, a source to
impose, a document to split — it builds that document **in memory** and never writes it. That is a
better demonstration than a folder of intermediates anyway: the reader sees the whole transaction in
one file, and the smoke test's contract is untouched.

Item 6 is the exception, and it is not really about file count. `Protect` must produce a genuinely
encrypted PDF or it demonstrates nothing, and the smoke test opens every declared output with
`PdfReader.Open(path, Import)` and no password. Item 17 gives `PdfDemo` an optional password for the
test to use.

---

## Item 1 — `Charts`

`PdfPinata.Charting` is a complete charting engine: eight `ChartType` values — line, clustered and
stacked columns, clustered and stacked bars, area, pie, exploded pie — plus combination charts, which
are not a ninth value but a series whose own `ChartType` disagrees with its chart's. Around them sit
`Axis`, `Gridlines`, `Legend`, `DataLabel`, `TickLabels`, `MarkerStyle` and `FillFormat`, and a
renderer for each in `PdfSharp.Charting.Renderers`. `PinataLayout.Rendering` renders a MigraDoc
`Chart` too:
`Renderer.Create` dispatches it, and `PinataLayout.Rendering.ChartMapper` maps the DOM's chart onto the
charting engine's.

`demonstration-app.md` declined a charting demo on the grounds that it "would mean a fourth project
reference for one page". That was wrong when it was written and is checkable: `PinataLayout.Rendering
.csproj` already carries `<ProjectReference Include="..\PdfPinata.Charting\…" />` with no
`PrivateAssets`, and `SampleApp` already references `PinataLayout.Rendering`. The charting assembly
is on the app's reference graph today and in its output directory today. An explicit reference is
worth adding for legibility, but it adds no dependency that is not already there.

The demo shows both routes, because they are different tools:

- **The MigraDoc route** — `section.AddChart(ChartType.Column2D)`, series added to the DOM, the
  document renderer laying the chart out in the flow beside the text. This is what a report wants.
- **The PdfSharp route** — a `PdfSharp.Charting.Chart` drawn straight onto an `XGraphics` at a
  rectangle the caller chose. This is what a dashboard wants, and it is the only route that lets a
  chart sit inside a page the caller is otherwise drawing by hand.

Four pages: a page per chart family (column and bar, line and area, pie), and a last page setting
the two routes side by side with the same data so the difference is the API rather than the picture.

## Item 2 — `Assemble`

`PdfDocument` exposes a complete page-assembly API that nothing demonstrates:

```text
  AddPage(PdfPage)        take a page from another document
  InsertPage(int, PdfPage)  the same, at a position
  ImportPage              the explicit import, with AnnotationCopyingType
  PlacePage               replace rather than insert
  DuplicatePage           copy a page within a document
  MovePage                reorder
  PruneUnusedResources    drop what an imported page brought and does not use
  ConsolidateImages       one XObject where several were identical
```

`import-size-and-annotations.md` records four defects fixed in exactly this area — unused resources
travelling with an imported page, `InsertRange` throwing on a page carrying a resolvable link,
destinations arriving standing for nothing. All four are fixed, none is visible to a reader, and
`AnnotationCopyingType` is a parameter on four methods with no example of why it exists.

The demo builds two source documents in memory — one with links, one with a shared image drawn on
three pages — then merges, reorders, duplicates and prunes, and reports on a final page what each
step did to the page count and to the byte count. `PruneUnusedResources` and `ConsolidateImages`
are the two that pay for themselves in bytes, so the demo measures them rather than asserting them.

Splitting is the same operation read backwards: pages are extracted into in-memory documents whose
sizes are reported. Nothing is written to disk, per the rule above.

## Item 3 — `Imposition`

`XForm` and `XPdfForm` are the two `XImage` subclasses no demo touches, and they are the mechanism
behind three of the six orphaned prose samples.

- `XForm` is a **form XObject the caller draws into**: a piece of content drawn once and stamped
  many times, at different scales and rotations, costing one copy in the file however often it is
  placed. That is the thing to demonstrate — draw a complicated device once, place it twenty times,
  and show the file is not twenty times bigger.
- `XPdfForm` is **a page of an existing PDF as something drawable**. Watermarking, 2-up and booklet
  imposition are all the same call with different transforms.

Four pages: the stamp, a watermark under and over existing content (the order matters and the demo
says why), two source pages imposed 2-up, and a four-page booklet sheet.

## Item 4 — `Vectors`

`XGraphics` has sixteen `Draw*` methods for shapes and paths. The demos before this one called five
of them. Untouched:
`DrawEllipse`, `DrawArc`, `DrawPie`, `DrawPolygon`, `DrawCurve`, `DrawClosedCurve`, `DrawBezier`,
`DrawBeziers`, `DrawRoundedRectangle`, `DrawLines`, `DrawRectangles`. Also untouched:
`XRadialGradientBrush`, `XPen`'s caps, joins, miter limit and custom dash patterns, `XFillMode`,
`XGraphicsContainer`, and every transform except `RotateAtTransform`.

Three pages: the primitives with their parameters labelled (an arc's angles are measured in a
direction people get wrong, so the demo draws the angles); pens and brushes, including the two fill
modes on the same self-intersecting path; and transforms, including `BeginContainer`/`EndContainer`
against `Save`/`Restore` — they are not the same thing and the difference is worth a page.

One thing the demo must not do is set a clip inside a nested graphics state. `XGraphicsPdfRenderer`
throws `NotImplementedException("Cannot set new clip region in an inner graphic state level.")`, and
a demo should teach that boundary rather than trip over it.

## Item 5 — `Barcodes`

`Drawing.BarCodes` implements Code 2 of 5 Interleaved, Code 3 of 9, OMR and a complete ECC200
DataMatrix — encoder, Reed-Solomon, symbol sizing, the lot — reachable through
`XGraphics.DrawBarCode` and `XGraphics.DrawMatrixCode`. Only the matrix code has tests
(`Drawing/MatrixCodeTests.cs`); the three linear codes have none and no demo.

One page: each code type at a usable size, with its text, direction and anchor varied, plus the
`CodeDirection` and `TextLocation` options that a caller would otherwise have to read the enum to
find. See item 16 for the API defect this turned up.

## Item 24 — L10, interleaved 2 of 5 checks nothing and fails at drawing time

```csharp
protected override void CheckCode(string text)
{
}
```

`CodeBase.Text`'s setter calls `CheckCode`, and every other code type uses it to refuse input its
symbology cannot carry. This one accepted anything. Interleaved 2 of 5 encodes two digits per five
bars, so a code has to be digits and there has to be an even number of them; give it neither and
`RenderNextPair` fails at *drawing* time with an `IndexOutOfRangeException` for the odd length or a
`FormatException` for the non-digit — neither of which names the code, the rule, or the line that
set it.

The message it should have been raising already existed. `BcgSR.Invalid2Of5Code` reads *"'{0}' is not
a valid code for an interleave 2 of 5 bar code. It can only represent an even number of digits."*, is
written out in full beside `Invalid3Of9Code`, and was called from nowhere at all. The check is now
written and calls it, matching `Code3of9Standard` exactly.

An empty code stays legal: it is what the parameterless constructor sets, and the renderer already
refuses it as an unset code rather than an invalid one.

## Item 25 — L11, `HasOwnerPermissions` is a constant `true`

```csharp
public bool HasOwnerPermissions => _hasOwnerPermissions;
internal bool _hasOwnerPermissions = true;
```

Those two lines are the whole of it. `_hasOwnerPermissions` is initialised to `true` and assigned
**nowhere in the library**, so the property answered "yes, this caller has owner rights" for every
document however it had been opened — including one opened with the user password, which is the only
case anybody would ever ask about.

The answer was already being computed. `PdfStandardSecurityHandler.ValidatePassword` returns
`PasswordValidity.OwnerPassword` or `PasswordValidity.UserPassword`, and `PdfReader` used that
result to refuse a `Modify` open and then dropped it on the floor. Recording it is one line.

A caller reading this to decide whether they may lift a restriction was told yes every time. Nothing
in the library acts on the property itself, so nothing misbehaved — which is exactly why it survived:
the only way to notice is to print it, and the `Protect` demo's second page prints it under both
passwords side by side.

Two cases stay `true` and are pinned by tests: a document being created, whose creator is its owner,
and a document that was never encrypted, where there is nothing to be shut out of.

## Item 6 — `Protect`

`PdfSecuritySettings` offers user and owner passwords, `PdfDocumentSecurityLevel` selecting RC4 40
or RC4 128, and eight independent permission flags — print, modify, extract, annotate, fill
forms, accessibility extract, assemble, full-quality print. `PdfReader.Open` takes a password and
`XPdfForm.FromStream` takes one too. There is no demonstration of any of it, and `Security/` in the
test project is the only place a caller can see the API used.

The demo's own output **is** encrypted, with both passwords set and the passwords printed on the
page and in `Shows`. It sets a deliberately mixed permission set — printing allowed, extraction
refused — so that a reader opening it in a viewer can see the permissions dialog reflect what the
code asked for. A second page reports the settings back after a round trip through
`PdfReader.Open(…, password)`, which is the half of the API a caller reaches for when *reading* a
protected file.

This is the only item that needs item 17.

## Item 26 — L12, `CSequence` throws for every interface member it declares

`CSequence` — the list a content stream comes back as — declares `IList<CObject>` and implements
every member of it **twice**:

```csharp
public int IndexOf(CObject value) { … }            // works
int IList<CObject>.IndexOf(CObject item)           // throws
{
    throw new NotImplementedException();
}
```

Thirteen members in that shape, `IEnumerable<CObject>.GetEnumerator` among them. C# binds an
interface to the explicit implementation wherever there is one, so everything reached *through*
`IList<CObject>` or `IEnumerable<CObject>` threw while the identical public method beside it worked.

LINQ reaches a collection through `IEnumerable<T>`. So `sequence.Select(…)`, `.Where(…)`, `.Count()`
— the first thing anybody does with a parsed content stream — threw `NotImplementedException` from a
class that visibly supports all of it. `OfType<T>()` happened to work, because it takes the
non-generic `IEnumerable`, which made the failure look arbitrary.

Every stub is deleted rather than implemented: each already had a public counterpart with a matching
signature, so C# now binds the interfaces to those. `IsReadOnly` was the one member with no public
counterpart and is added, returning false. `CArray` derives from `CSequence` and inherited all of it,
so it is fixed by the same change.

## Item 27 — L13, `CompressContentStreams` defaults differently per build

```csharp
#if DEBUG
    bool _compressContentStreams = false;
#else
        bool _compressContentStreams = true;
#endif
```

The same calling code writes a materially larger PDF from a debug build than from a release one —
about 16% larger on the `Compress` demo's own page. Nothing said so: the property's documentation
was one line and mentioned neither the default nor that it moves.

The intent is clear enough and defensible for the library's own development, since an uncompressed
content stream can be read straight out of the file. What is not defensible is that it was
undocumented, because the symptom is a consumer comparing two files from two builds and concluding
something is wrong with their code.

**Documented first, then changed.** The conditional is gone: the default is `true` in every build,
and the remarks say what it used to be and that setting it to `false` is how to get the readable
content stream back. That was a decision about the library's behaviour rather than about its
documentation, so it was put to whoever owns that call and made when they took it.

The `Compress` demo still sets the option explicitly on every row rather than reporting a default,
because a table of defaults goes quietly wrong the day one changes — as this one just did.

## Item 28 — `ImageFailures`

`DocumentRenderer.ImageFailed` was added for issue 366 (see `image-failure-reporting.md`) and
nothing showed it working. The demo provokes all four failure kinds a caller can actually reach,
collects the events, and prints what the handler was told beside the placeholders the same four
images drew.

Each failing image is an `IImageSource` written by hand, and that is worth seeing for its own sake:
the interface is six members, and implementing it is how an application supplies images from a
database or an HTTP response rather than from a file. Here the same six members are arranged to fail
at four different points — while `XImage` is built, while the image is measured, while its bytes are
asked for, and not at all, by measuring to nothing.

`ImageFailure` has a fifth value, `FileNotFound`, which nothing in this fork assigns: images arrive
through `IImageSource` rather than by path, so there is no file for the renderer to fail to find. It
has a placeholder string of its own and is unreachable. The demo says so on the page rather than
leaving a reader to wonder why only four of five appear.

This was listed under "deliberately not done" in the first pass, on the grounds that it needed a
page of scaffolding for one panel. It needed about eighty lines, and it found item 31.

## Item 29 — refusing to write `NaN`

PDF has no syntax for NaN or for infinity. Writing one produces an operand a reader cannot parse,
and a reader's answer to that is to stop drawing — silently, and usually for the rest of the content
stream rather than for the one operator. The page arrives blank with nothing to say why, and the
drawing call that caused it returned happily, possibly hours earlier.

Items 18 and 30 are both exactly this and nothing else, and neither could be caught by a test,
because a content stream full of `NaN` is a valid file that a test can open and read back.

Every coordinate now goes through one of `XGraphicsPdfRenderer`'s `Append*` methods, and each of
those refuses a number it cannot write. The message quotes the operator it was about to emit —
`NaN NaN m` names the operator, and where the `NaN` falls among the operands says which coordinate
went wrong — and names the two ways one gets there: an argument that was already not a number, or a
transform that made it one.

The custom dash pattern in `PdfGraphicsState` is assembled as text a piece at a time rather than
formatted in one go, so it reaches the stream by a route of its own and is checked separately. It is
the only such route.

This was listed under "deliberately not done" in the first pass, on the grounds that it touches
every coordinate the library writes. It was then put to whoever owns that call, taken, and found
item 30 within ninety seconds of the first test run.

## Item 30 — L14, a `CodeDataMatrix` given no size draws at negative infinity

`XSize.Empty` is not a zero size. It is `(-Infinity, -Infinity)`, which is how the type tells "no
size given" apart from "no area". Five of `CodeDataMatrix`'s eight constructors default `Size` to
it, and nothing downstream checked, so `Render` divided that by the module count and drew every dark
module as

```text
NaN NaN -Infinity -Infinity re
```

The barcode came out invisible — from the constructor taking nothing but text and a module count,
which is the one the upstream issue and the documentation both use. The test covering it asserted
only that drawing did not throw, which it never did.

An unset size now becomes two points per module, quiet zone included, putting a 26 by 26 symbol a
little under 18mm square. `BarCode.InitRendering` answers the same question by refusing an empty
size, and the difference is deliberate: a linear code's width depends on how many bars its text
turns into, so there is nothing to fall back on, where a matrix code is square and measured in
modules and both are known by the time it is drawn.

## Item 31 — L15, an image of no pixels measures to `NaN` and reports nothing

An image reporting no pixels divides zero by zero in `CalculateImageDimensions`' aspect-ratio
arithmetic, so its extent arrived as `NaN`. The guard meant to catch a size of nothing read

```csharp
if (resultHeight <= 0 || resultWidth <= 0)
```

and every comparison against `NaN` is false, so a size that was not a number counted as a good one.
What followed was an element of no known height: the page broke around it, the next one came out
blank, no placeholder was drawn, and no `ImageFailed` event was raised. The caller got a blank page
and no way at all to find out why.

The check is now written as "greater than zero" rather than "not less than or equal to zero", which
are the same for every number and not the same for `NaN`, and it refuses an infinite extent too.

Two things about this are worth recording. It is a second hole in the same path
`image-failure-reporting.md` item 5 patched — that one stopped `EmptySize` being assigned to a field
nothing read, and this one stopped it being assigned at all. And item 29's guard did **not** catch
it: the `NaN` went into layout rather than into a coordinate, so nothing was ever drawn for the
writer to refuse. A guard at the writer is the last line, not the only one.

The placeholder's message also drew at a fixed eight points whatever the box measured, so a
placeholder narrower than its message — which is what an image of a tall aspect ratio gets — wrote
the message out through both sides of the box and across whatever was beside it. It now takes the
largest of the usual sizes that fits.

## Item 32 — the prose samples

The twenty-three pages under `docs/PdfPinata/samples/` came from upstream PDFsharp, and nothing
builds them, so they say what was true when they were written.

Every page now names the demo that covers it, with the command to run it, and says plainly which of
the two has protection. The index gained the setup this fork needs and upstream does not — the core
package carries no font or imaging dependency, so both seams must be filled in before anything
draws, and no sample code shows it being done — along with what the samples assume and this
repository does not provide: `PdfSharp.*` namespaces, and upstream's tree of sample PDFs behind
every `../../../../../PDFs/` path.

Three pages were wrong rather than merely stale. `HelloWorld` was the one page still written against
`PdfSharp.*` and would not compile. `FontResolver` described a default font resolver twice and
linked to it twice, and there is no default and the file has not existed for some time; its
`IFontResolver` also has three members rather than the two described. `ProtectDocument` said nothing
about AES being read-only here, which is the first thing somebody protecting a document wants to
know.

Twelve demos have no page here at all, because upstream never wrote one. The index says which.

## Item 33 — the footnote spec

`migradoc-footnotes.md`. Item 14 made the gap audible; the spec is the plan for closing it. The two
things worth knowing before starting are in there: implementing it adds no DOM surface at all, and
the layout is a fixed point rather than a straight pass, because the height available to the body
text is the page less the notes whose marks land on it, and which notes those are depends on how the
body text broke.

A MigraDoc barcode renderer — item 15's gap — is deliberately **not** in that spec. The two are
unrelated in everything but symptom.

## Item 7 — `Navigation`

Everything a document says about how it should be *presented*, which is entirely undemonstrated
apart from one `PageMode` in `Outline`:

- `PdfPageLabels` — front matter in roman numerals and the body in arabic, which is why a reader's
  page box shows "iv" on the fourth page and "1" on the tenth. Fully implemented here, including
  `GetLabel`, and completely invisible.
- `PdfViewerPreferences` — hide the toolbar, centre the window, fit the window to the first page.
- `PageLayout` — single page, one column, two columns left or right.
- `Language`, for a screen reader.
- `CustomValues` — private data in the catalog, which survives a round trip.
- `NamedDestinations` — the table, as against the individual named destination `Text` already
  creates.

Three pages, laid out as front matter, body and a report page, so the page labels have something to
label.

## Item 8 — `Compress`

`PdfDocumentOptions` has five properties and no demonstration: `ColorMode` (RGB against CMYK),
`CompressContentStreams`, `NoCompression`, `FlateEncodeMode` and `UseFlateDecoderForJpegImages`.
Every one of them is invisible in the rendered page and visible only in the byte count, which is
exactly the kind of feature a demo can make real.

The demo builds one page of representative content — text, a path, a photograph — saves it to a
`MemoryStream` under each combination of options, and prints the resulting byte counts as a table on
the page it then writes. The reader sees the settings, the sizes, and the trade.

## Item 9 — `Inspect`

`Pdf.Content.ContentReader` and the `CObject` model are public: a caller can read back the operators
a page was drawn with. The test project has four content-stream readers under `Helpers` — linked
into `PinataLayout.Rendering.Tests` rather than copied — so the technique is well understood inside
the repository and has no example outside it.

The demo draws a page, saves it to a `MemoryStream`, reopens it, and prints its own content stream —
the operators, their operands, and a count by operator name. It is the only demo whose subject is
the file rather than the page, and it is the fastest way for someone debugging output to learn that
they can look.

## Item 10 — `Unicode`

`XPdfFontOptions` selects `PdfFontEncoding.WinAnsi` or `PdfFontEncoding.Unicode`, which decides
whether a simple font or a CID font is written, and therefore which characters survive.
`PdfDocumentRenderer(true)` — the `unicode:` argument three MigraDoc demos pass without comment — is
the same switch. `font-embedding-gaps.md` records six gaps closed around this, including CFF
embedding whole where TrueType is subsetted, and none of it is visible.

The demo shows the same string in both encodings, the CID font path, and the difference in what
lands in the file. Liberation Sans carries Latin, Latin Extended, Greek and Cyrillic, so the demo
covers those without a new asset; **CJK is deliberately out of scope** and the page says so, because
a CJK face is a multi-megabyte asset for one page and this app already carries 3MB.

The CFF-against-TrueType difference gets a panel of its own: Source Code Pro is already embedded and
is already the app's only CFF face, so the demo can report that one of the two embedded faces was
subsetted and the other could not be, and name the reason.

## Item 11 — `Structure`

The four MigraDoc demos between them call `AddParagraph`, `AddTable`, `AddImage`, `AddTextFrame`,
`AddStyle`, `AddTabStop`, `AddFormattedText` and two page fields. What they leave out is most of
what makes MigraDoc worth using for a long document:

- **A table of contents** — `PageRefField` against a `BookmarkField`, with `TabLeader.Dots`. The
  classic MigraDoc TOC, and the reason `TabStops` has a leader property at all.
- **Bookmarks for free** — `ParagraphFormat.OutlineLevel` drives `DocumentRenderer.AddOutline`, so
  headings become a PDF outline with no manual `Outlines.Add` anywhere. `Outline` builds its tree by
  hand, which is right for PdfSharp and misleading as the only example.
- **Sections** with different page setups, and the full `HeadersFooters` set — primary, first page,
  even page — which is how a report gets a title page without a header.
- **Lists** — `ListInfo`, the only list support in the library. `demonstration-app.md` notes that
  `Layout` draws its bullets by hand; the real one still has no demo.
- **`Hyperlink`** in its web, local, bookmark and file forms.
- The predefined styles in `StyleNames` and how style inheritance resolves.

Four pages: a title page with its own header treatment, a TOC, and two pages of headed, numbered,
cross-referenced body — which is also the only demo in the app whose PDF has a working outline it
never asked for.

## Item 12 — `Ddl`

`DdlWriter` and `DdlReader` serialise a MigraDoc document to MigraDoc's own text format and read it
back. It is a real feature with a hand-written scanner and parser (`DdlScanner`, `DdlParser`,
`DdlReaderErrors`) and it has no example anywhere outside the test project.

The demo builds a small document in the object model, writes it to DDL with
`DdlWriter.WriteToString`, reads that string back with `DdlReader`, renders the *re-read* document,
and prints the DDL beside the result. If the round trip loses anything the page shows it, which
makes this the one demo that is also a regression test a human can read.

## Item 13 — `Images` extended

`Images` draws one JPEG seven ways, and the computation of those rectangles is the demonstration.
What it cannot show with one opaque JPEG:

- **An alpha channel.** A PNG with transparency over a coloured ground, which is the case people
  actually ask about, and the one where a backend difference would show.
- **`XImage.Interpolate`**, which decides whether an upscaled image is smoothed or blocky.
- **`DocumentRenderer.ImageFailed`**, the event `image-failure-reporting.md` was built for. Without
  a handler a failed image is a grey box and the exception is dropped; the demo subscribes and
  prints the reason. This is a MigraDoc-side event, so the panel showing it renders a tiny MigraDoc
  document rather than drawing directly.

This means one new asset: a PNG with an alpha channel, generated rather than found so its licence is
not a question, embedded like the rest.

## Item 14 — L1, MigraDoc drops a `Footnote` silently

`ParagraphElements.AddFootnote` exists in two overloads, `Footnote.cs` is in the DOM,
`StyleNames.Footnote` is a predefined style — and `PinataLayout.Rendering` contains the string
"Footnote" nowhere at all. `ParagraphRenderer.FormatElement` switches on the element's type name and
ends `default: return FormatResult.Continue;`, so a footnote is skipped without a word.

A caller writes `paragraph.AddFootnote("…")`, reads the property back, and gets a PDF with no
footnote and no error.

Implementing footnote layout — reserving the area, numbering, splitting the note across a page break
— was a feature rather than a fix and was out of scope here. What was in scope was that it stopped
being silent: `FormatElement` got an explicit `case "Footnote":` that threw a
`NotSupportedException` naming the gap.

**That throw is gone, because footnotes render now.** See item 34. What remains of this item is the
argument it made: making the gap audible is what turned a silent wrong answer into a decision
somebody could take, and the decision taken was to close it. The three tests that pinned the refusal
became the twenty-four that pin the feature.

The refusal it introduced still exists for the one case footnotes are *not* implemented for - a note
in a table cell, a text frame or a header - which is item 8 of `migradoc-footnotes.md`.

## Item 15 — L2, MigraDoc drops a `Barcode` shape silently

The same shape of defect one level up. `Shapes/Barcode.cs` is in the DOM with `Type`, `Text`,
`BearerBars` and the rest; `Renderer.Create` dispatches `Paragraph`, `Table`, `PageBreak`,
`TextFrame`, `Chart` and `Image` and nothing else. A `Barcode` added to a section returns a null
renderer and is dropped.

`Renderer.Create` returning null is *legitimate* for two element kinds and the code says so — a
legend is rendered as part of its chart, and a bookmark draws nothing. So the fix cannot be a blanket
throw at the call site; it is an explicit branch for `Barcode` that throws and names
`XGraphics.DrawBarCode` as the working route, which item 5 demonstrates.

While in that method: its `RenderInfo` overload tests `is Chart` twice, at `Renderer.cs:195` and
`:197`, so the second branch is unreachable. Harmless, inherited from upstream, and worth deleting
while the file is open.

## Item 16 — L3, `BarCode.FromType` reaches two of the four code types

`CodeType` has four values. `BarCode.FromType` handles `Code2of5Interleaved` and `Code3of9Standard`
and falls to `default: throw new InvalidEnumArgumentException(...)` — so `CodeType.Omr` and
`CodeType.DataMatrix`, both of them implemented and both of them reachable by constructing the class
directly, are reported as though they were not values of the enum.

`CodeOmr : BarCode`, so it belongs in the factory and is a two-line addition.
`CodeDataMatrix : MatrixCode`, which is not a `BarCode` and cannot be returned from a method typed
that way — so that case gets a message saying what to construct instead and which draw method takes
it, rather than a message implying the caller passed a bad enum value.

## Item 18 — L4, a chart draws its plot area at `NaN` unless an axis was read

Four lines, one in each axis renderer:

```csharp
xari.axis = chart.xAxis;      // HorizontalXAxisRenderer.Init, and three more like it
if (xari.axis != null)
{
    CalculateXAxisValues(xari);   // ← the scale is worked out here
    …
}
```

`chart.xAxis` is the **field**. `chart.XAxis` is the property, and the property is what creates an
axis the first time anything asks for one:

```csharp
public Axis XAxis
{
    get
    {
        if (this.xAxis == null)
            this.xAxis = new Axis(this);
        return this.xAxis;
    }
}
```

So a caller who never mentioned either axis left both fields null, the renderer skipped everything
inside that `if`, and `AxisRendererInfo.MinimumScale` and `MaximumScale` stayed at the zero they were
constructed with. Plotting a point then divides by `MaximumScale - MinimumScale`, which is zero over
zero, and the entire plot area is written to the content stream as

```text
NaN NaN m
NaN NaN NaN NaN NaN NaN c
```

Nothing complains at any point. The document saves, opens, and rasterizes, because a reader handed an
operand it cannot parse abandons the path and paints nothing — so the chart arrives with its frame,
its axes' labels and its legend all correct and **the data missing**. The workaround, had anyone
known they needed one, was to read a property and discard the result.

This is not confined to the drawn route. `ChartMapper.Map` maps the axes only
`if (!domChart.IsNull("XAxis"))`, so a MigraDoc chart whose caller never touched `XAxis` reaches the
same renderers in the same state. Both routes, every axis-bearing chart type, since the fork.

**The fix in the tree is not this branch's.** While this work was in flight, `main` found the same
defect independently by writing `PdfPinata.Charting.Tests`, and fixed it as C1 of
`charting-renderer-findings.md`. That fix is the better one and is what survived the rebase.

This branch read the property in all four renderers. `main` keeps reading the field and moves the
scale calculation *outside* the null check, which is what the two Y axis renderers had always done —
one line apart, and the reason a chart with no `YAxis` had always drawn correctly and merely gone
unlabelled. The difference matters: reading the property creates an axis as a side effect of
rendering, so a chart nobody configured would silently acquire tick labels it never asked for.
Calculating the scale outside the check leaves the labelling to depend on the axis, where it belongs,
and the data to plot regardless.

The tests written here are kept. They come at it from the other direction — a chart drawn as a caller
draws one, asked only whether its data reached the page — and one of them was rewritten during the
rebase, because it asserted that touching the axis changed nothing at all. Under `main`'s fix it
changes the labelling and must, so it now asserts that it changes *only* that.

**The `NaN` writer guard has since been done**, as item 29. It was listed here as deliberately not
done, on the grounds that it touches every coordinate the library writes. It found a fourteenth
defect within ninety seconds of being switched on.

## Item 19 — L5, a combination chart prints its legend on top of itself

`LegendRenderer.Format` totalled every entry's width, added the padding, and *then* widened each
entry's marker to the widest marker in the legend:

```csharp
foreach (LegendEntryRendererInfo leri in lri.Entries)
    leri.MarkerArea = maxMarkerArea;      // the last thing Format did
```

`Draw` lays the entries out by stepping along `leri.Width` and paints each marker at
`leri.MarkerArea`. Those two disagreed by exactly the widening, so every entry was measured narrow
and drawn wide, and each one was painted over the end of the one before it.

It only shows when the entries disagree about their marker width, and one thing makes them disagree:
`LegendEntryRenderer.Format` gives a line series three times the marker of a column. That is a
combination chart, which is why nothing had ever seen it.

Equalising before measuring rather than after fixes it, and makes the legend's own arithmetic
honest — with equal markers the step from one label to the next depends only on the text before it,
which is what the test compares a combination chart against a chart of nothing but lines to check.

## Item 20 — L6, a pie signs and scales its percentages twice

```csharp
double percent = 100 / (sumValues / Math.Abs(sector.point.value));
dleri.Text = percent.ToString(sri.dataLabelRendererInfo.Format) + "%";
```

`percent` is already out of a hundred and the sign is appended unconditionally, so the format string
the caller supplies must be a plain numeric one. Nothing says so, and the natural thing to write is
the .NET percent format — which multiplies by a hundred and appends a sign of its own. A share of
`0.1875` asked for as `"0%"` came out as **`1875%%`**.

The format now decides which of the two it is. One carrying `%` is a .NET percent format, is handed
the fraction, and its result is used as it stands; anything else is handed the number out of a
hundred and has the sign appended, which is what the property always meant and still does. Every
format a caller might reasonably write is now right, and the rule is one sentence long.

Worth knowing while in there, and pinned by a test: leaving `Format` alone is not the same as
leaving it empty. `DataLabelRenderer` substitutes `"0"` for an unset format, so an untouched pie
labels its slices in whole percents rather than to full precision.

## Item 21 — L7, three `XGraphicsPath` members collect geometry and drop it

```csharp
public void AddPie(double x, double y, double width, double height, double startAngle, double sweepAngle)
{
    DiagnosticsHelper.HandleNotImplemented("XGraphicsPath.AddPie");
}
```

`AddClosedCurve` and `AddPath` were the same, and `DiagnosticsHelper`'s default behaviour is
`DoNothing`. So a caller collected geometry into a path, read every property back exactly as it had
been set, and drew a page with the shape missing.

This is the identical defect `demonstration-app.md` records under `XGraphicsPath.AddString` — *"It
did not throw — it reported through `DiagnosticsHelper` and drew nothing, so a title built that way
silently disappeared"* — which that spec closed by implementing it. These three are closed the same
way, and for the same reason: the alternative is a demo that has to avoid three members of a public
class without saying why.

All three had reference implementations sitting in the same repository. `XGraphicsPdfRenderer` draws
a pie and a closed curve directly to the page, so `CoreGraphicsPath.AddPie` and `AddClosedCurve` are
written to match those segment for segment — a shape collected into a path and the same shape drawn
straight out have to agree, and a test pins that they do. `AddPath` appends the other path's points
and types, turning its opening move into a line when the caller asks for the figures to be joined.

Three tests in `XGraphicsPathTests` had to be rewritten rather than added:
`APieIsNotImplementedAndQuietlyAddsNothing` and its two neighbours pinned the *broken* behaviour,
with a note saying *"pinned so that the day it grows an implementation the change is visible here"*.
That day is this one. The gap was known and written down; what was missing was somebody drawing a
pie and noticing the page was blank.

## Item 22 — L8, a pen made from a brush strokes with no alpha

`XPen(XBrush, double)` sets `_brush` and never sets `_color`, so `pen.Color` is `XColor.Empty` —
whose alpha is **zero**. `RealizePen` writes the pattern correctly and then reaches its last block:

```csharp
PdfExtGState extGState = _renderer.Owner.ExtGStateTable.GetExtGStateStroke(color.A, overPrint);
```

which sets the stroking alpha to nothing. The gradient was built, the pattern was named, the stroke
was painted, and none of it could be seen.

A solid brush had a second problem on the way in. `RealizeBrush` is called with a rendering mode of
0, which means *fill*, so `XPen(new XSolidBrush(red))` set the **fill** colour and left the stroke at
whatever the page had used last. A solid brush is a colour, so it is now turned into one and takes
the ordinary stroke path; only a gradient goes to the pattern, and its transparency travels through
the soft mask `RealizeBrush` installs rather than through a colour the pen never had.

## Item 23 — L9, the miter limit is guarded by the line cap, and truncated

```csharp
if (_realizedLineCap == (int)XLineJoin.Miter)
```

The **cap** tested against a value of the **join** enum. It agreed with itself only because
`XLineCap.Flat` and `XLineJoin.Miter` are both zero, so a pen that mitred its joins and rounded its
ends never wrote its miter limit at all, and one with flat ends wrote it whatever its join was.

Beside it, `(int)pen._miterLimit` truncated the limit on the way out and `!= 0` discarded anything
below one. A limit is a ratio of the mitre's length to the pen's width; 1.5 is an ordinary value for
it and became 1, which bevels every join that is not perfectly straight. It is written as a real now.

Worth recording because it cost a first draft of `Vectors` an hour: the panel demonstrating this
originally drew a chevron whose corner was about 146°, whose mitre therefore reached 1.05 times the
pen width, and which no limit anyone would set could ever cut. The panel looked like a rendering
defect and was a badly chosen angle. The corner is a narrow spike now, and the two limits beside it
differ visibly.

## Item 17 — infrastructure, an optional password

`PdfDemo` gains `public virtual string? OpenPassword => null;`, and `DemoSmokeTests` passes it to
`PdfReader.Open` when it is set. Fifteen demos are unaffected. `Protect` overrides it, which is what
lets the demo's declared output be genuinely encrypted rather than an unencrypted page describing
encryption.

The runner prints it beside the output path when it is set, so somebody running `SampleApp run -e
protect` is told the password rather than having to read the source to find it.

---

## The order the demos are read in

`DemoRegistry` is a curriculum, not an alphabetical list. The fourteen new demos slot in by subject:

```text
   1 HelloWorld         11 Tables            21 Annotations
   2 Fonts              12 PageResize        22 Outline
   3 Unicode       ★    13 Bleed             23 Invoice
   4 Orientation        14 Assemble     ★    24 Structure   ★
   5 Images        ★    15 Imposition   ★    25 Footnotes   ★
   6 ImageFailures ★    16 Protect      ★    26 Ddl         ★
   7 Text               17 Navigation   ★    27 Charts      ★
   8 Vectors       ★    18 Compress     ★    28 Newspaper
   9 Barcodes      ★    19 Inspect      ★    29 Magazine
  10 Layout             20 Forms             30 SideWrap
```

Thirty demos, fourteen of them new and one — `Images` — extended. ★ marks what this spec added.

Two of the placements are the point rather than the alphabet. `ImageFailures` sits immediately after
`Images` because it is the same subject seen from the other side: what happens when the picture does
not arrive. `Footnotes` sits immediately after `Structure` for the same reason — both are MigraDoc
deciding where something goes rather than a caller saying where to put it.

Fonts then Unicode; the drawing surface then the vector methods then the codes drawn with them; the
page-level demos then the document-level ones; the interactive layer then MigraDoc, ending as it
does today with the three combined layouts.

## Deliberately not done

- **No MigraDoc barcode renderer.** Item 15 makes it audible rather than silent; drawing one is a
  feature with its own spec. Footnote rendering was on this list for the same reason and came off
  it: items 33 to 35 built it.
- **No CJK font.** Item 10 says so on the page. A face that covers CJK is megabytes for one panel.
- **No `.ttc` demonstration.** `FontResolverBase` supports the `file.ttc#1` form and the app carries
  no collection file; manufacturing one to demonstrate it would be demonstrating the asset.
- **No golden images**, unchanged from `demonstration-app.md`, and for the same reason: demos are
  meant to be edited.
- **No second output file from any demo**, item 6's encryption aside. In-memory documents are a
  better demonstration and leave the smoke test's contract alone.
- **No MigraDoc barcode renderer.** Item 15 makes the gap audible and item 33 deliberately leaves it
  out of the footnote spec. It wants a spec of its own, and is much the smaller of the two: a shape
  renderer mapping the DOM `Barcode` onto `PdfPinata.Drawing.BarCodes`, with no layout problem in
  it at all. It is now the only element the DOM can hold and this renderer refuses.
- **Footnotes are not split across a page break, and are refused outside a section's own
  paragraphs.** Items 6 and 8 of `migradoc-footnotes.md`, both deferred by decision rather than by
  oversight, both recorded there with what it costs.
- **`ImageFailure.FileNotFound` is left in the enum, unreachable.** Nothing assigns it. Removing a
  public enum value would break callers switching on it, for no gain; item 28 says so on the page
  instead.
- **The `Preview` and `Clock` samples have no demo and will not get one.** One draws to a live
  surface this fork does not ship, the other is an ASP.NET handler. Item 32 says so on both pages.

Two entries that stood here in the first pass are gone, because both were done in the second:
`DocumentRenderer.ImageFailed` is item 28, and the prose samples are item 32. The reasons given for
deferring them were sound and both turned out to be cheaper than estimated — and the first of them
found item 31.
