# Spec — the CRAP backlog, and the order to work it

`charting-renderer-findings.md` closed the last measurement of this kind: `PdfPinata.Charting`
held the ten highest-CRAP methods in the fork, and covering them turned up seven defects. This one
picks up where that left off, across the whole tree rather than one assembly. Like that spec it is
tied to no upstream issue, and it was written before the work, so the status column tracks progress.

Measured on `dev/migradoc-render-coverage` at `d0d36e5`, over the six shipped assemblies:

```powershell
dotnet test src/PdfPinata.slnx -f net10.0 --settings coverage.runsettings `
  --collect:"XPlat Code Coverage" --results-directory ./TestResults
```

3,510 tests, exit 0, no test-host crash. **75.0% of lines and 69.2% of branches.** Of 7,287
methods, 237 score above the CRAP threshold of 30 and 109 of those have never been executed at all.

## Where it stands now

Re-measured the same way after each batch. Every batch is done.

| after | tests | lines | branches |
|---|---|---|---|
| baseline (`d0d36e5`) | 3,510 | 75.0% | 69.2% |
| batches 0 and 1 | 3,519 | 75.2% | 69.7% |
| batch 2 | 3,569 | 75.4% | 70.2% |
| batch 3 | 3,641 | 75.9% | 70.8% |
| batch 4 | 3,679 | 76.5% | 71.5% |
| batch 5 | 3,715 | 77.0% | 72.1% |
| batches 6 and 7 | 3,781 | 77.3% | 72.5% |
| batches 8, 9 and 10 | 3,818 | 77.6% | 73.2% |
| batches 11 to 14 | 3,890 | **78.3%** | **73.9%** |
| the pull request review | 3,897 | **78.3%** | **73.9%** |

Every run exit 0, and every total checked against `dotnet test --list-tests` rather than read off
the word `Passed`.

## Re-measured at `1d46b2a`, 2026-08-17

Every batch above being done, the list was re-measured to find the next one. **4,049 tests, exit 0,
no test-host crash** — 152 more than the last row above.

**The percentages from this run are not comparable to the table above, and are deliberately not
added to it.** The run had to be made in Release: the Roslyn analysis server holds
`src/PinataLayout.DocumentObjectModel.Generators/bin/Debug/netstandard2.0/…Generators.dll` open, so the
Debug build cannot complete on a machine running it. Release reports 70.4% of lines and 54.6% of
branches over eight assemblies, against the 78.3%/73.9% over six above — a different configuration
over a different denominator, so the difference says nothing about whether coverage moved. Anyone
re-measuring for the table must stop the analysis server first and build Debug.

Two things about the run *are* comparable, because they count methods rather than lines, and both
say the backlog worked:

| | baseline `d0d36e5` | now `1d46b2a` |
|---|---|---|
| methods above the CRAP threshold of 30 | 237 | **172** |
| of those, never executed at all | 109 | **71** |
| methods measured | 7,287 | 7,527 |

Retirable points now stand at about **8,700**, against the 17,900 the original list opened with.

A note for whoever measures next, because it cost an hour here. ReportGenerator emits no risk
hotspots for these reports, and coverlet writes `crapScore="0"` on every method, so the score has to
be computed — `cc² × (1 − cov)³ + cc` — from each method's `cyclomaticComplexity` and
`sequenceCoverage` attributes in the OpenCover XML. Use `sequenceCoverage`; do **not** compute
coverage by unioning sequence points by hand. A multi-line `||` chain short-circuits, so its later
sequence points never execute even when the method is fully exercised, and a hand-rolled union
reports `AnsiEncoding.IsAnsi1252Char` at 57% when it is at 100%. The four methods in the
"Out of scope — already covered" table below are the check: any method that reads them at less than
100% has a broken measurement, not a coverage gap.

## Batch 15 — one method, two copies, again

`PinataLayout.DocumentObjectModel.Tests`. The highest-value shape in this list, and the third time
it has come up.

| # | target | CC | cov | retires | status |
|---|---|---|---|---|---|
| 15.1 | `LeftPosition.Parse(string)` — `Shapes/LeftPosition.cs:207` | 10 | 0% | 100 | **done**, finding F21 |
| 15.2 | `TopPosition.Parse(string)` — `Shapes/TopPosition.cs:203` | 10 | 0% | 100 | **done**, finding F21 |

`public static`, and the two bodies are identical character for character apart from the type name:
trim, look at the first character, and either hand the string to `Unit.Parse` or to
`Enum.Parse(typeof(ShapePosition), …)`. Neither has ever run.

Write one table of strings and assert both against it, the way `ExtractPageNumberParityTests` holds
the two `ExtractPageNumber` copies to one answer — that is the arrangement that makes them agree
rather than merely testing them separately. F6, F12 and F18 were all one half of a near-copy pair
having a guard its twin lacked, so a divergence here is a finding, not a test to bend around.

One thing to settle rather than pin blindly. Both open with

```csharp
if (value == null || value.Length == 0)
    throw new ArgumentNullException("value");
```

which answers `ArgumentNullException` for `""`. That is the wrong exception for an empty string and
the argument name is a literal rather than `nameof`. Decide whether the empty case becomes
`ArgumentException` — it is a public API change, so it wants saying out loud — and pin whichever
answer is chosen.

Cover: a signed number, an unsigned number, a unit with a suffix, each `ShapePosition` member,
wrong case, surrounding whitespace, a name that is not a member, `""` and null.

**They agree, and they were wrong together** — see F21, which is the same shape as F12. 58 tests in
`src/PinataLayout.DocumentObjectModel.Tests/LeftAndTopPositionParityTests`, of which 24 hold both
implementations to one table.

Two things learned rather than assumed, and pinned as such:

- **Nothing in the repository calls either method.** `git grep` finds no caller outside the new
  tests; the DDL parser and the DOM set `Left` and `Top` through `INullableValue.SetValue`, which is
  a different path with its own copy of the enum-or-unit decision. That is why both sat at 0%. They
  are `public static` and this is a library, so unlike batch 12.1 they are not unreachable — they
  are public API with no internal caller, and the defect below was consumer-facing.
- **The asymmetry is deliberate and had to be pinned separately.** The two share one
  `ShapePosition` enum and admit different members of it — `Left`, `Right`, `Center`, `Inside`,
  `Outside` against `Top`, `Bottom`, `Center` — so each must *refuse* the names the other takes.
  The refusal is not in `Parse` at all: it happens in the private constructor the implicit
  conversion runs, which is why reading `Parse` alone makes the two look interchangeable.
  `Undefined` is the one name that parses to an empty position rather than throwing, because both
  constructors admit it by name.

The `ArgumentNullException`-for-`""` question the batch raised was **settled by leaving it**. It is
the wrong exception by BCL convention, and the argument name is a literal rather than `nameof` — but
`""` already answered `ArgumentNullException` before this work, changing it would break a caller
catching that type specifically, and it buys nothing a caller can use. The fix below therefore
changes the behaviour of no input that previously worked: the only inputs whose answer moved are the
ones that previously crashed.

### F21 — a position of nothing but whitespace read off the end of the string

Both copies opened by testing the string they were given, and trimming it afterwards:

```csharp
if (value == null || value.Length == 0)
    throw new ArgumentNullException("value");

value = value.Trim();
char ch = value[0];
```

`"   "` has a length of three, so it passed the guard; `Trim()` then left nothing, and `value[0]`
threw `IndexOutOfRangeException` — out of a public API, with nothing in the message to say what was
wrong. Demonstrated against both before fixing, over `"   "`, `"\t"`, `" \t "` and `"\r\n"`:

```text
LeftPosition.Parse("   ")  ->  IndexOutOfRangeException
TopPosition.Parse("   ")   ->  IndexOutOfRangeException
```

The fix is the order of the two steps, in both:

```csharp
if (value == null)
    throw new ArgumentNullException("value");

value = value.Trim();
if (value.Length == 0)
    throw new ArgumentNullException("value");

char ch = value[0];
```

Pinned by `WhitespaceAloneIsRefusedTheSameWayAsNothingAtAll`, which ran red with
`IndexOutOfRangeException` against the unfixed code before it ran green — the four whitespace cases
failed and the other 54 tests passed, so the pin is known to be capable of failing.

This is F12's shape exactly: a guard testing the wrong side of a bound, copied identically into both
halves of a near-copy pair, so neither had the guard the other lacked and the parity test found it
by agreeing rather than by disagreeing.

## Batch 16 — the generator's cache key

`PinataLayout.DocumentObjectModel.Generators.Tests`. Highest-scoring method in the tree that has
never run.

| # | target | CC | cov | retires | status |
|---|---|---|---|---|---|
| 16.1 | `EquatableArray<T>.Equals(EquatableArray<T>)` — `Model/EquatableArray.cs:29` | 14 | 0% | 196 | **done**, 0% → 62.5%, below the threshold |
| 16.2 | `EquatableArray<T>.GetHashCode()` — `Model/EquatableArray.cs:45` | — | 0% | — | **left**, never called |

This is what makes the incremental generator's models comparable, so it decides whether a pipeline
step is re-run or served from cache. Wrong `Equals` is not a cosmetic bug: too eager and the
generator emits stale source, too shy and incremental caching stops working.

**There is no direct route to it.** `EquatableArray`, `DomMemberModel`, `ParsedMember`, `ParsedType`
and `DiagnosticInfo` are all `internal`, and this repository has no `InternalsVisibleTo`. What is
observable is the consequence, through the public `DomValueModelGenerator` and Roslyn's own step
tracking: run the generator over one compilation, run it again over a second parsed separately from
the same or different text, and ask the driver why the source-output step ran. The two compilations
share no syntax tree and no symbol, so a driver answering `Cached` can only have got there by
comparing the models by value.

10 tests in `IncrementalCachingTests`, with the two-run mechanics in `IncrementalCachingProbe`.
They are worth more than the score says: **nothing had ever tested that the caching works at all**,
and it is the whole reason the models are shaped the way they are.

Three things learned, and the last two are why 16.1 stops at 62.5% rather than going further.

**Only one `EquatableArray` is ever compared.** `DomTypeModel` holds an
`EquatableArray<DomMemberModel>` and declares `IEquatable<DomTypeModel>`, but it is built inside the
`RegisterSourceOutput` callback — downstream of every cache — so nothing compares it, ever. The one
that travels through the pipeline is `DiagnosticInfo.MessageArgs`, an `EquatableArray<string>`, and
that is the only route to `Equals`. Not a defect, but it means the value equality on `DomTypeModel`
is inert, and it is why the method sat at 0% despite the type being central to the design.

**The remaining branches have no public route**, checked rather than assumed:

- The null-array branch needs `default(EquatableArray<T>)`. The only constructor assigns from
  `ToArray()`, and nothing in the generator creates a default one.
- The length-mismatch branch needs two `DiagnosticInfo`s with the same descriptor and different
  argument counts. A record compares its members in declaration order, so `Descriptor` is compared
  first and a differing one returns before `MessageArgs` is reached — and every call site uses a
  fixed number of arguments per descriptor (`NotPartial` one, `NotADocumentObject` and
  `RefOnlyOnValueType` two, `NotAnInstanceMember` and `UnsupportedMemberType` three). So the two
  conditions cannot both hold.
- `Equals(object)`, `GetHashCode()` and the non-generic `GetEnumerator()` are never called: the
  pipeline compares through `IEquatable<T>` and never hashes a model.

**A latent hazard, recorded rather than fixed.** `Equals` compares elements with
`array[i].Equals(other.array[i])` and `GetHashCode` calls `item.GetHashCode()`, both of which throw
`NullReferenceException` on a null element of a reference type — and `T` is `string` or
`DomMemberModel`, both reference types. It is unreachable today: every `DiagnosticInfo.Create` call
site passes symbol names, and `DomMemberModel`s come from a parse that cannot yield null.
`EqualityComparer<T>.Default` would handle it on both sides. Left alone because nothing can reach
it, and a change to a comparison this central wants a demonstrated failure behind it rather than a
guess — the same reasoning as `Table.SetEdge` under F4.

One more thing pinned that surprised the batch: **an edit anywhere above a declaration invalidates
the cache**, comment or blank line included. `ParsedMember.DeclarationOrder` is
`TargetNode.GetLocation().SourceSpan.Start` and `LocationInfo` carries the spans, so inserting
anything higher up the file moves both and the whole emit re-runs. `DomModels.cs` already says
Location "costs nothing in cache terms that DeclarationOrder does not already cost", which is true —
the cost was already there. Recorded rather than fixed: the obvious cheaper key, an ordinal index
within the type, is not available to `ForAttributeWithMetadataName`, which sees one member at a time
and never its siblings. `AnEditAboveADeclarationIsNotServedFromTheCacheEvenWhenItChangesNothing`
pins it, with `AnEditBelowEveryDeclarationIsServedFromTheCache` beside it to show the cause is
position rather than trivia.

## Batch 17 — text encoders and the DDL scanner's readers

`PinataLayout.DocumentObjectModel.Tests` for the DDL items, `PdfPinata.Test` for the encoder.

| # | target | CC | cov | retires | status |
|---|---|---|---|---|---|
| 17.1 | `DdlEncoder.StringToLiteral(string)` — `DdlEncoder.cs:102` | 10 | 0% | 100 | **done**, 0% → 100% |
| 17.2 | `DdlEncoder.StringToText(string)` — `DdlEncoder.cs:55` | 18 | 37% | 82 | **done**, 37% → 100%, findings F22 and F23 |
| 17.3 | `PdfEncoders.ToHexStringLiteral(string, PdfStringEncoding, PdfStandardSecurityHandler)` — `Pdf.Internal/PdfEncoders.cs:139` | 10 | 0% | 100 | **left**, wants a fixture |
| 17.4 | `DdlScanner.MoveToNextParagraphContentLine(…)` — `DdlScanner.cs:628` | 18 | 35% | 89 | **left**, its own batch |
| 17.5 | `DdlScanner.ReadText(…)` — `DdlScanner.cs:385` | 30 | 59% | 62 | **left**, its own batch |

17.1 and 17.2 are the two halves of one job — escaping a string for DDL — and share a fixture, which
is why they are together rather than one per rank. The round trip is the assertion worth making:
encode, read back through `DdlReader`, and check the string survives. Escapes, braces, backslashes
and a string that needs no escaping at all are where the branches are.

45 tests in `src/PinataLayout.DocumentObjectModel.Tests/DdlEncoderTests`, taking both encoders to 100%
and both below the threshold. **Two findings, F22 and F23**, and only the first is fixed.

The two methods escape different things, which is deliberate and is now pinned as such: paragraph
text escapes the backslash, both braces and the comment marker, because all of those end the text
early; a quoted literal escapes the backslash and the quote and nothing else, because inside quotes
nothing else means anything. They also disagree about null — `StringToText` hands it straight back
and `StringToLiteral` answers `""` — which is pinned rather than reconciled, since either could
otherwise look like the mistake.

**17.3 was left.** `PdfEncoders` is `internal` and so is `PdfStringFlags`, so there is no public way
to construct a `PdfString` that carries the `HexLiteral` flag: the flag only ever arrives from the
parser having read a `<…>` literal out of a file. Covering it therefore needs a PDF fixture that
already contains a hex string, and the byte-level surgery to make one out of a document this library
wrote shifts every xref offset. It is a fixture item rather than a test item, and it belongs with
whatever batch next needs a hand-built PDF. Note that `LexerHexStringTests` already covers the
reading side.

**17.4 and 17.5 were left**, and were not moved by this batch — `ReadText` is still at 59.1% and
`MoveToNextParagraphContentLine` at 35.0% after all 45 tests. That is the right answer rather than a
disappointment: both are about a paragraph *continuing across lines*, and every test here is a
single-line paragraph. They want a batch of their own, built from multi-line paragraphs, blank-line
separation and the indentation rules — which is a fixture and a subject, not a fixture these
happened to share.

### F22 — three slashes in a row came out as a comment

`StringToText` escapes `//` because it would otherwise begin a comment. It escaped only the first
slash of each pair, and consumed the second:

```csharp
case '/':
  if (index < length - 1 && str[index + 1] == '/')
  {
    strb.Append("\\//");
    ++index;
  }
  else
    strb.Append("/");
  break;
```

For exactly two slashes that is right: `\//` reads back as an escaped slash and then a plain one.
For three it is not. `"///"` came out as `\///`, which the scanner reads as one escaped slash
followed by `//` — the start of a comment — so the rest of the line, including the brace closing the
paragraph, was swallowed and the document would not read back at all:

```text
"///"  ->  \///  ->  DdlParserException: End of file expected.
```

Any odd run of three or more slashes does it, and a Windows UNC path or a URL in a paragraph is
enough to produce one. The fix escapes every slash that begins a pair and lets the loop reach the
next one, which is also simpler than what it replaces — no index to skip:

```csharp
case '/':
  if (index < length - 1 && str[index + 1] == '/')
    strb.Append("\\/");
  else
    strb.Append("/");
  break;
```

`"//"` still encodes to `\//`, so nothing that worked changes. `"///"` now encodes to `\/\//` and
`"////"` to `\/\/\//`, both of which read back as themselves. Pinned by the escaping table and, end
to end, by `TextSurvivesBeingWrittenAndReadAgain` over `a///b` and `a////b`.

### F23 — text beginning with an escaped character cannot be read back

Found while fixing F22, and left recorded rather than fixed because the repair is in the serializer
rather than the encoder.

A paragraph carrying no style or format of its own is written as bare text inside its section,
without a `\paragraph` keyword around it. The escapes in that text are only honoured once the
scanner is reading paragraph content, and what gets it there is the first plain character. So text
whose *first* character needs escaping is encoded correctly and then read at section level, where
`\{` is a keyword rather than an escaped brace: the brace nesting goes wrong and the reader gives up.

```text
"{}"   ->  \{\}   ->  DdlParserException: End of file expected.
"//"   ->  \//    ->  DdlParserException: End of file expected.
"a{b}c" ->  a\{b\}c  ->  reads back as itself
```

A single letter in front of it is the difference. Both halves are pinned — the round-trip theory
asserts the working case, and `TextBeginningWithAnEscapedCharacterCannotBeReadBack` asserts the
broken one — so a fix shows up as a failing test rather than silently.

Not fixed here because the encoder is not what is wrong: it produced exactly the right escape. The
repair is for the paragraph serializer to emit `\paragraph{…}` when its text begins with a character
it had to escape, and that changes the shape of documents this library writes, which is a decision
of its own rather than a coverage item.

## Batch 18 — never executed, one apiece

No shared fixture, so take these in rank order and stop when the return drops. All are `CC 10` at
0%, worth 100 retirable points each, unless noted.

| # | target | suite | status |
|---|---|---|---|
| 18.1 | `PdfTextExtractor.Walker.Execute(COperator)` — `Pdf.Extraction/PdfTextExtractor.cs:132` — CC 44, 60%, **124 points** | Core | **done**, 60% → 95.5% |
| 18.2 | `PdfPages.FindPage(PdfObjectID)` — `Pdf/PdfPages.cs:84` | Core | **deleted** |
| 18.3 | `PdfCrossReferenceTable.CheckConsistence()` — `Pdf.Advanced/PdfCrossReferenceTable.cs:249` | Core | **deleted**, replaced by tests |
| 18.4 | `DictionaryElements.CreateValue(Type, PdfDictionary)` — `Pdf/PdfDictionary.cs:1077` | Core | **deleted** |
| 18.5 | `XTextSegmentFormatter.CalculateTextSize(…)` — `Drawing.Layout/XTextSegmentFormatter.cs:149` | Core | **done**, all four overloads |
| 18.6 | `VerticalMetricsTable.Read()` — `Fonts.OpenType/OpenTypeFontTables.cs:536` | Core | **left**, wants a font with `vmtx` |
| 18.7 | `Table.DeepCopy()` — `Tables/Table.cs:73` | DOM | **done** |
| 18.8 | `LineFormat.Serialize(Serializer)` — `Shapes/LineFormat.cs:127` | DOM | **done** |
| 18.9 | `TextMeasurement.MeasureString(string, UnitType)` — `TextMeasurement.cs:58` | Rendering | **done** |
| 18.10 | `DocumentRenderer.RenderObject(…)` — `DocumentRenderer.cs:261` | Rendering | **done** |

18.1 is the largest single return left that is not already recorded as left — the text extractor's
operator walker, at 60% over forty-four branches. Every content operator it does not handle is an
uncovered branch, and `text-extraction.md` is the spec to read first.

18.7 wants the assertion `Chart.DeepCopy` got in batch 5.2: mutate the original after copying and
check the copy did not move. A deep copy that returns non-null proves nothing.

18.9 is in `PinataLayout.Rendering.Tests` and needs a real font, so it cannot go in the DOM suite —
`NamedFontsOnly.cs` serves a name and throws if asked for a face.

**Three of the first six turned out to be unreachable**, which is batch 12's lesson again and is why
the check comes before the test. A method at 0% is not necessarily untested; it is sometimes uncalled.

- **18.2 has no caller anywhere.** `FindPage` is `internal`, and its own declaration is the only
  occurrence of the name in the repository — it carries a `// TODO: public?` comment and has been
  waiting for an answer since the initial import. Not deleted, because making it public is a
  plausible answer and the decision is not a coverage item's to take.
- **18.3 is compiled out of every build.** `CheckConsistence` carries `[Conditional("DEBUG_")]` —
  with a trailing underscore, which is this codebase's way of writing a symbol that is never
  defined, as `#define VERBOSE_` in `OpenTypeFontTables.cs` does. So the two calls in
  `PdfReader.Open` and the two inside the class are all removed by the compiler in Debug and Release
  alike, and no test can reach it in any configuration. The four further call sites at
  `PdfCrossReferenceTable.cs:199` to `:241` are commented out on top of that.
- **18.4 is private with no caller.** `CreateValue` is declared without an access modifier inside
  `DictionaryElements`, so it is private, and nothing calls it.

**All three were then deleted**, which is batch 0 and 1's answer rather than batch 12.1's: none of
them implements a named algorithm worth keeping as a basis for future work, and all three are
`internal` or `private` in a repository with no `InternalsVisibleTo`, so removing them is invisible
to every consumer.

18.2's `// TODO: public?` was answered "no" before it was deleted, and the answer is worth keeping
because the question will occur to the next reader too. A caller can already do the whole job through
public API — `page.Reference.ObjectID` reads an id, and `PdfPages` is publicly `IEnumerable<PdfPage>`:

```csharp
((IEnumerable<PdfPage>)document.Pages).FirstOrDefault(page => page.Reference.ObjectID == wanted)
```

Checked rather than assumed, with a throwaway test that was then removed. The cast is needed because
`PdfPages` declares `new GetEnumerator()` over `PdfDictionary`'s, so overload resolution otherwise
fails. Publishing `FindPage` would also have exported a wart: it scans with `item as PdfReference`
and a null check, so a page held as a direct dictionary rather than an indirect reference is silently
skipped — it answers null where the public indexer answers the page.

18.3's invariants were **moved into tests rather than dropped**, which is the point of removing a
check that never ran. `src/PdfPinata.Test/Pdfs/CrossReferenceConsistencyTests` asserts them from
outside, against the bytes the writer produced: every object numbered once, no object numbered zero,
and the count the xref declares matching the objects the file defines. The renumbering path the old
checks bracketed is covered directly — importing pages from a second document, where both number
their objects from one and a failure to renumber would collide. The two assertions cross-check each
other, so a regular expression that matched nothing would fail the test rather than pass it quietly.

**18.5 was done and is the one straightforward item in the six.** All four `CalculateTextSize`
overloads are public and had never run. 7 tests added to `XTextSegmentFormatterTests`, asserting
that the two string overloads and the two segment overloads agree with each other — the string ones
build a single segment and delegate, so a disagreement means one has grown a step the other has not —
and that narrowing the width makes the same text taller, which is what says it lays the text out
rather than measuring one line.

One quirk pinned there rather than assumed: **measuring an empty string reports no height and the
whole width the caller offered.** With no blocks to measure the width falls back to the incoming
`width` rather than to zero, so a caller sizing a box to its content gets back the box it started
with.

**18.6 was left.** `VerticalMetricsTable` reads `vmtx`, which is a vertical-writing table that the
Latin faces in `Assets/Fonts` do not carry, so covering it needs a CJK or vertical-metrics font
added to the repository — a fixture decision rather than a test.

Three that look like this batch and are **not** on it, checked before listing: `DataMatrixSymbol
.SmallestSizeFor` (barcodes are a fork-inherited corner with no other coverage and no spec — its own
decision), `OpenTypeDescriptor..ctor` (reached only through font loading, so it is covered
incidentally the moment 18.6 is), and `LineFormatRenderer..ctor(XGraphics, LineFormat, double)`,
which batch 13.3 records as done and which now reads 0% — worth a look on its own account, because
either the batch-13 test stopped reaching it or the two-argument overload no longer chains to it.

### What 18.1 needed, and why the walker was only half covered

`XGraphics` emits four of the operators the walker understands — `Tf`, `Tm`, `Tj` and `TJ` — so a
page this library drew leaves most of the switch untouched however many drawing tests are written.
The rest are what another producer's page contains, and reaching them means writing the content
stream by hand. `TextExtractionTests` already had the mechanism for that in `WithTwoShows`; it is now
a general `WithContentReplaced`, which draws a word to get a font resource and its `/ToUnicode` map
and then replaces the content stream outright.

9 tests, covering `q`, `Q`, `cm`, `TD`, `T*`, `TL`, `Tz`, `Ts`, `Tr` and `'`. Two are worth naming:

- **Text in render mode 3 is skipped, and that is a decision rather than an oversight.** It is how
  the OCR layer under a scanned page is drawn, and reporting it would hand the caller the page's
  text twice. Pinned in both directions, because render mode is text state that persists past `ET`:
  one test says the invisible run is not reported, the next says a run after it *is*, which is what
  would fail if the mode were ever treated as lapsing at `ET`.
- **`TD` sets the leading as a side effect**, and the test asserts a third run positioned by a
  following `T*` rather than the second run's position — otherwise it would be testing the movement
  and not the side effect.

### 18.7 to 18.10 — the last four, and one lesson about which reader to use

7 tests in `src/PinataLayout.DocumentObjectModel.Tests/TableCloneAndLineFormatTests` and 15 in
`src/PinataLayout.Rendering.Tests/TextMeasurementAndRenderObjectTests`.

`Table.DeepCopy` is `protected override` and reached through the public `Clone`. It clones five
children by hand — columns, rows, format, borders and shading — so the assertion that matters is
independence, the way `Chart.DeepCopy` got it in batch 5.2: mutate the original afterwards and check
the copy did not follow, in both directions. The reparenting half is not directly observable, so it
is checked by putting the copy in a document of its own and writing it.

`LineFormat.Serialize` has five guarded attributes and is reached by writing a document containing a
shape. Both sides are pinned: everything set is written, and an untouched line format contributes no
attributes rather than a row of defaults. Note in passing that `LineStyle` has exactly one member,
`Single`, so the `Style` attribute can only ever say one thing.

`TextMeasurement.MeasureString` and `DocumentRenderer.RenderObject` are both public, both need a real
font, and so both go in `PinataLayout.Rendering.Tests` rather than the DOM suite. Each unit
conversion is asserted against the measurement in points rather than against a number typed in, so
the tests say what the unit means and stay true whatever the font measures.

**The lesson, which cost a run:** `Glyphs` is the right reader for the rest of that suite and the
wrong one here. It reads glyph identifiers because `PdfDocumentRenderer` embeds its fonts as
Identity-H — but `RenderObject` draws onto an `XGraphics` the caller made, which is the whole point
of it, and that path writes the string as characters. The first version of these tests read "Framed"
as `{18034, 24941, 25956}`, which is the characters two at a time. `TextOperators.ShownStrings` is
the reader for a page that was not written by the document renderer.

The last row is the review of the pull request the rest of this was raised in. It moves neither
number, which is the point of recording it: three more defects (F18–F20) and four tests that were
not asserting what they said they were, none of it visible in a coverage percentage. The lines were
already covered. What was wrong was what the tests claimed about them.

Batches 0 and 1 were settled by deleting the target rather than covering it, so their movement is
the denominator shrinking — six unreachable methods carrying about 4,800 points between them are
gone. Batch 2 is the first that is tests. Seventeen findings, fifteen of them fixed; F2, F7 and the
F9–F11 group are each worth more than the batch that turned them up.

CRAP is `complexity² × (1 − coverage)³ + complexity`, so a fully covered method scores its own
complexity and no less. The **retires** figure below is what full coverage would remove —
`complexity² × (1 − coverage)³` — which is the part a test can actually win. The whole backlog is
worth about **17,900 points of a possible 29,150**, a little over three fifths of everything the
codebase has available to lose.

## Rules that decide where a test can go

These are from `CLAUDE.md` and they constrain the batches below more than the code does.

- **No `InternalsVisibleTo` anywhere in the repository.** An `internal` target is reached either
  through public API or by reflection, the way `AreaProbe`, `ParagraphIteratorProbe` and
  `MappedChartProbe` already do. Prefer public API; reach for reflection only when there is no
  route, and say so in the test's own comment.
- **`PinataLayout.DocumentObjectModel.Tests` references the DOM and nothing else** — no renderer,
  no backend, no Ghostscript, no font files. `NamedFontsOnly.cs` serves a font *name* and throws if
  asked for a face. A target needing a real font belongs in `PinataLayout.Rendering.Tests` instead.
- **`PinataLayout.Rendering.Tests` rasterizes nothing**, and neither does
  `PdfPinata.Charting.Tests`. Keep it that way; assert against the content stream through the
  linked readers under `src/PdfPinata.Test/Helpers`.
- **`PdfPinata.Test` is the broad one and the default** for anything in the core package.
- Assertions use **AwesomeAssertions** — same API as FluentAssertions, different `using`.
- **Judge a run by its exit code, not by the word `Passed`.** A total below what
  `dotnet test --list-tests` finds did not pass, it stopped.

## Batch 0 — settle rank 1 before writing anything

| # | target | where | retires | status |
|---|---|---|---|---|
| 0.1 | `Font.ApplyFont(Font, Font)` | `src/PinataLayout.DocumentObjectModel/Font.cs:85` | 3,136 | **deleted** |

The highest CRAP score in the tree, and **nothing calls it**. The overload is `internal`, and the
one call to `ApplyFont` anywhere in the repository is `ParagraphElements.cs:219`, which calls the
public one-argument overload. Twenty-two lines and fifty-six branches that no caller can reach.

Deleting it is worth every one of those points and closes the question. Testing it needs reflection
and pins behaviour nothing depends on. Check the git history for why the `refFont` overload was
added and whether a caller was lost, then decide — but decide first, because it is a third of the
`Font.cs` cluster on its own.

**Settled by deleting it.** The history says no caller was lost: the overload arrives at the initial
import and `git log -S` over the whole history finds no call to it, ever. Nor was one possible from
outside — it is `internal` and the repository has no `InternalsVisibleTo`. The idea it implements is
alive elsewhere and in use: `VisitorBase.FlattenFont(Font, Font)` copies a reference font's values
into the ones a font leaves null, and both flatten visitors call it.

## Batch 1 — three switch statements

~~`PinataLayout.DocumentObjectModel.Tests`. Best return in the backlog by a wide margin.~~
**Deleted rather than tested — see finding F1.** They were unreachable, not untested.

| # | target | CC | cov | retires | status |
|---|---|---|---|---|---|
| 1.1 | `DdlScanner.IsParagraphElement(Symbol)` — `DdlScanner.cs:1049` | 32 | 0% | 1,024 | **deleted** |
| 1.2 | `DdlScanner.IsSectionElement(Symbol)` — `DdlScanner.cs:1022` | 23 | 0% | 529 | **deleted** |
| 1.3 | `DdlScanner.IsDocumentElement(Symbol)` — `DdlScanner.cs:1002` | 10 | 0% | 100 | **deleted** |
| 1.4 | `DdlScanner.IsHeaderFooterElement(Symbol)` — `DdlScanner.cs:1078` | — | 0% | — | **deleted** |
| 1.5 | `DdlScanner.IsFootnoteElement(Symbol)` — `DdlScanner.cs:1098` | — | 0% | — | **deleted** |

The batch was written expecting three untested predicates and found five unreachable ones. 1.4 and
1.5 were not on the list because they are small; they are here because they were the only callers
the other three had, so deleting them is what makes the other three dead in turn.

## Findings

Recorded the way `charting-renderer-findings.md` records them: the code, what is wrong with it, and
what pins it now. Numbered in the order they were found, which is the order of the batches.

| # | what was wrong | fixed |
|---|---|---|
| F1 | Five scanner predicates naming which keyword may appear where were unreachable. Of the two rules they state, the footnote one is already enforced elsewhere by decision; only `\pagebreak` in a header goes unchecked, and it is a silent no-op | deleted; see below |
| F2 | **Every error message in the DOM** read `<<<error: message not found>>>` — the lookup asked for public instance properties and the generated resources are internal statics | yes |
| F3 | A complete, balanced `.mdddl` file with one bad enum value or a missing bracket hangs the reader for good | no — pinned |
| F4 | `Table.SetShading` read its counts from null backing fields, so an untouched table threw `NullReferenceException` before any of its four range checks could run | yes |
| F5 | Redefining a style handed back a detached clone; writing to it reached nothing | yes |
| F6 | The punctuator lookahead read one character past the end of the document for a trailing `+` or `-` | yes |
| F7 | `DdlReader.ObjectFromString(ddl, errors)` built its reader without the error list, so every non-fatal complaint went nowhere | yes |
| F8 | `AxisTitle.Serialize` compared a `Unit` against null, which throws — **no chart with an axis title could be written** | yes |
| F9–F11 | An image could not survive an MDDDL round trip, for three separate reasons: the writer wrote a field nothing fills, the reader asked the backend for an image at `""` before reading the path, and an image with only a source counted as empty and was dropped | yes |
| F12 | `ExtractPageNumber` walked its index off the front of a path that was all digits — in both copies, identically | yes |
| F13 | Ticking a check box drawn in two places could tick both halves, because a local leaked between them | yes |
| F14 | No scalar accessor on `PdfArray` followed an indirect reference; every one on `PdfDictionary` did | yes |
| F15 | The content lexer could not read `d0` or `d1`, so **every Type 3 glyph was misread** and every operator after the first got an extra operand | yes |
| F16 | An unterminated string ending in a backslash put U+FFFF into the text | yes |
| F17 | A repeated symbol was drawn `Count` squared times, into the width reserved for `Count` | yes |
| F18 | The UTF-16 half of the literal string scanner had F16's fault and did not get F16's fix | yes |
| F19 | `GetMatrix` refused the literal that `SetMatrix` and its own create branch write, so **no matrix this library wrote could be read back** | yes |
| F20 | A check box whose single widget is a child of its own ignored `Checked = true` entirely | yes |
| F21 | `LeftPosition.Parse` and `TopPosition.Parse` tested for emptiness before trimming, so a string of nothing but whitespace read `value[0]` off the end — `IndexOutOfRangeException` from a public API, identically in both copies | yes |
| F22 | `DdlEncoder.StringToText` escaped only the first slash of each `//` pair and consumed the second, so `"///"` came out as `\///` — an escaped slash followed by the start of a comment, which swallowed the rest of the line and the brace ending the paragraph | yes |
| F23 | A paragraph with no style of its own is written without a `\paragraph` keyword, so text whose first character needs escaping is read at section level where the escape is not honoured, and the document will not read back | no — pinned |

Eighteen of the twenty are fixed. F3 is recorded rather than fixed because fixing it changes what
the reader accepts, which is a decision rather than a repair. F1 is half fixed: the unreachable
predicates are deleted, and the one rule they held that is not enforced anywhere else —
`\pagebreak` in a header — is deliberately left as the silent no-op it is. See the correction
under it for why the fix does not belong in the parser.

F18, F19 and F20 came out of the review of the pull request this spec was raised in, which is worth
saying: three of them are the same shape as findings already in this list — one half of a pair of
near-copies getting a fix its twin did not (F18, and F6 before it), and a value that cannot be read
back by the method that wrote it (F19, and F9–F11 before it).

### F1 — the scanner's five element predicates were unreachable, and the rules they held are unenforced

`DdlScanner` carried five `internal static` predicates saying which keyword may appear where.
`IsSectionElement`, `IsHeaderFooterElement` and `IsFootnoteElement` had no caller anywhere;
`IsDocumentElement` and `IsParagraphElement` were called only from the two dead ones. None has been
called since the initial import, and `internal` with no `InternalsVisibleTo` means none could be.

The parser answers the same question a different way, and does not ask them:

```csharp
private DocumentElements ParseDocumentElements(DocumentElements elements, Symbol context)
{
    while (TokenType == TokenType.KeyWord)
    {
        switch (Symbol)
        {
            case Symbol.Paragraph: ParseParagraph(elements); break;
            // … one arm per element kind …
            default: ThrowParserException(DomMsgID.UnexpectedSymbol, scanner.Token); break;
        }
    }
    return elements;
}
```

`context` is taken and never read. That is where the predicates belonged, and the two rules they
exist to state are the two the parser does not enforce — verified before deleting them:

- `IsHeaderFooterElement` accepts every paragraph element and every document element **except**
  `PageBreak`. `\document{\section{\header{\pagebreak}}}` parses.
- `IsFootnoteElement` accepts every paragraph element **except** `Footnote`.
  `\paragraph{a\footnote{b\footnote{c}}}` parses, nesting a footnote in a footnote.

**Correction, made when this was revisited.** The two rules are not equally unenforced, and the
first draft of this finding overstated the case by looking only at the parser.

`\footnote` inside a footnote **is** refused, one layer down and on purpose.
`TopDownFormatter.ReserveFootnotes` throws when a note is attached to anything that does not own a
page, and `migradoc-footnotes.md` item 8 records it as "refused, by decision", covering "cells, text
frames, headers, footers and notes nested inside notes, in one place and by construction". The
message names the case outright:

> A footnote can only be attached to a paragraph that is laid out on a page. This one is inside a
> table cell, a text frame, a header or footer, or another footnote, none of which owns the page its
> note would have to appear at the foot of.

So nothing is missing there. Moving the refusal to parse time would only change *when* the caller
hears about it, and would make it worse in one way: the parser can only see the DDL, while the
formatter's message names all four contexts and says what to do instead.

`\pagebreak` in a header is genuinely unchecked, and is a silent no-op rather than a fault. The
renderer ignores it — a section with a break in its header renders to one page, the same section
with the break in its body renders to two. Refusing it would need more than the parser, because the
parser is not the only thing that produces one:

- `HeaderFooter.Elements.AddPageBreak()` succeeds, so the model holds one.
- `DdlWriter` writes it back out as `\pagebreak` inside `\primaryheader`.

A check in `ParseDocumentElements` alone is four lines — the `context` argument is already in hand,
already says `Symbol.HeaderOrFooter`, and the existing `catch (DdlParserException)` in
`ParseParagraph` would report it and carry on. But it would leave the writer emitting a document its
own reader rejects. Doing it properly means refusing it in `DocumentElements.AddPageBreak` when the
parent is a `HeaderFooter` — which is reachable, the collection being constructed as
`new DocumentElements(this)` — and letting the parser and writer follow from that.

That is a public API that succeeds today turning into one that throws, to forbid something that
currently does nothing. Left alone deliberately; recorded here so the choice is visible rather than
forgotten.

Deleted, because a rule no caller consults is not a rule. Restoring the checks would be a parser
behaviour change — it would refuse documents that read today — so it is its own decision and its
own spec, not a coverage item. This note is the record that the intent existed.

### F2 — every error message in the DOM said only that it could not find itself

`DomSR.GetString` is how a `DomMsgID` becomes text, and it looked the message up like this:

```csharp
internal static string GetString(DomMsgID id)
{
    return (string)typeof(AppResources).GetProperties()
        .Where(x => x.Name == id.ToString() && x.PropertyType == typeof(string))
        .FirstOrDefault()?.GetValue(null);
}
```

`GetProperties()` with no arguments means `Public | Instance | Static`. `AppResources` is an
internal generated class whose properties are `internal static`, so the query matched nothing for
every one of the fifty-eight ids, `GetString` returned null every time, and `FormatMessage` fell
through to its placeholder:

```csharp
else
    message = "<<<error: message not found>>>";
```

So every parser error and every validation error raised by identifier — which is all of them that
do not go through one of `DomSR`'s half-dozen hand-written helpers — told the caller
`<<<error: message not found>>>`. The messages were in `AppResources.resx` throughout. Reading a
malformed `.mdddl` file reported nothing about what was malformed.

The fix is the binding flags:

```csharp
const BindingFlags ResourceProperties =
    BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;

internal static string GetString(DomMsgID id)
{
    return (string)typeof(AppResources).GetProperties(ResourceProperties)
        .FirstOrDefault(x => x.Name == id.ToString() && x.PropertyType == typeof(string))
        ?.GetValue(null);
}
```

This is the same fault one layer up from the one `ErrorMessageResourceTests` already describes —
that class was written because the assembly rename broke the resource lookup, and it tested the
generated properties directly, which is exactly the path that worked. Now pinned from the other
side by `EveryMessageIsReachableByTheIdentifierThatNamesIt` and
`AMessageIsFormattedWithWhatWentWrongRatherThanAPlaceholder` in that same class, and end to end by
`DdlReadingTests.AParserErrorSaysWhatIsWrongRatherThanThatItHasNothingToSay`.

`DomMsgID.Success` is excluded from the sweep: it is the no-error value, has no message, and
nothing raises it.

### F3 — a complete file with one thing wrong in it hangs the reader too

`DdlReadingTests.AFileThatStopsInsideASectionIsNeverFinishedWith` already records that a truncated
file never comes back. Found while looking for a parser error to assert against: truncation is not
needed. Both of these are complete and balanced, and both hang for good:

```text
\document{\section[PageSetup{PageFormat = NoSuchFormat}]{\paragraph{t}}}
\document[Info{Title = "x"]{\section{\paragraph{t}}}
```

An enum value that is not one of the enum's, and a missing right bracket. That lowers the cost of
the defect a long way — a hand-written file with a spelling mistake in it is enough, and the reader
has the whole file in front of it. Pinned by
`AWholeFileWithOneThingWrongInItIsNeverFinishedWithEither`, alongside the truncation cases.

Worth noting for whoever fixes it: the DDL reader swallows a great deal else without complaint. An
unknown enum member (`Alignment = Sideways`), an unknown colour name (`Color = Puce`), an unknown
symbol (`\symbol(NoSuchSymbol)`) and an empty attribute value (`Width = `) all read without error.
Only some malformed input hangs; most of it is simply accepted.

### F4 — `Table.SetShading` could not reach its own range checks on an untouched table

The method opens by reading the two counts it validates against:

```csharp
var rowsCount = this.rows.Count;
var clmsCount = columns.Count;
```

Both are the backing fields, and both are null until something has asked for the collection. A
table with no row or column added to it therefore threw `NullReferenceException` on the first line
and never reached the four `ArgumentOutOfRangeException` checks below it — the checks that exist
precisely to say which argument is out of range. Reading them through the `Rows` and `Columns`
properties, which build the collection on demand, is the fix:

```csharp
var rowsCount = Rows.Count;
var clmsCount = Columns.Count;
```

Pinned by `AnEmptyTableHasNoRangeToColour` and `ATableWithColumnsButNoRowsHasNoRangeToColourEither`.

Worth noting while nearby, and deliberately not fixed here: `Table.SetEdge` a few lines down has no
range check at all and indexes straight into `this.rows`. It is not on the backlog and giving it
checks would change what it throws, so it wants its own decision.

### F5 — redefining a style handed back an object the document was not holding

`Styles.Add` replaces a style of a name it already has, and it stores a *clone* of the one it was
given:

```csharp
int index = GetIndex(style.Name);
if (index >= 0)
{
    style = style.Clone();
    style.parent = this;
    ((IList)this)[index] = style;
}
else
    base.Add(value);
```

`Styles.AddStyle` then returned the style it built rather than the one that ended up in the
collection, so redefining an existing style — which `Document.AddStyle` and the MDDDL reader both
do — handed the caller a detached object with no parent. Writing to it reached nothing:

```csharp
var redefined = document.AddStyle("Quiet", "Normal");
redefined.Font.Bold = true;              // silently affects nothing
document.Styles["Quiet"].Font.Bold;      // false
```

Fixed by returning what the collection actually holds, which is the same object on the ordinary
path and the clone on the replacement path:

```csharp
Add(style);
return this[name];
```

Pinned by `TheStyleHandedBackIsTheOneTheDocumentIsHolding`, with
`AddingANameThatIsAlreadyThereReplacesItRatherThanAddingASecond` alongside it to say that replacing
is the intended behaviour and only the return value was wrong.

### F6 — the punctuator lookahead read one character past the end of the document

`CLAUDE.md` says of the two PDF lexers that "a change to one usually belongs in the other", and
that "the document lexer usually has the guard the content lexer is missing". The MDDDL scanner has
a pair of the same shape: `ScanPunctuator` reads a punctuator while consuming it, `PeekPunctuator`
reads one at a given index without moving. They are near-copies, arm for arm, and they differed in
exactly one place — the two-character lookahead for `+=` and `-=`:

```csharp
// PeekPunctuator
case '+':
    if (this.ddlLength >= index + 1 && m_strDocument[index + 1] == '=')

// ScanPunctuator
case '+':
    if (nextChar == '=')
```

`ddlLength >= index + 1` is satisfied when `index + 1 == ddlLength`, so the very next line indexes
`m_strDocument[ddlLength]` and throws `IndexOutOfRangeException`. The bound wanted is "there is a
character after this one", which is `>`. `ScanPunctuator` never had the fault because `nextChar` is
maintained as null at the end of the buffer rather than read from past it.

Reached by a document whose last character is `+` or `-`. Demonstrated directly against the scanner
before fixing:

```text
'x+' index 1 -> IndexOutOfRangeException
'x-' index 1 -> IndexOutOfRangeException
```

Fixed to `>` in both arms.

Pinned by `ASignAtTheVeryEndOfTheDocumentIsNotReadPastTheEndOfIt`, which took a second attempt. The
first version ended the document with a sign after its closing brace — `\document{…}}}+` — and that
input never reaches the lookahead at all: the parser has finished by then and says "End of file
expected". It also called the reader through a helper that catches every exception, so it could not
have seen the throw even had one happened. The test passed with the defect deliberately put back,
which is how it was found; the review of the pull request this spec was raised in caught the second
half of that.

The lookahead is reached from three places, and all three are a keyword asking whether an attribute
block or an argument list follows it. So the sign has to arrive immediately after such a keyword and
be the last character of the document. Measured across a spread of candidates, with the fix out and
then in:

```text
\document{\section{\paragraph{a\space+       IndexOutOfRangeException   ->  does not come back
\document{\section{\paragraph{aield(Page)+ IndexOutOfRangeException   ->  does not come back
\document{\section{\paragraph{t}}}+          "End of file expected."    ->  "End of file expected."
```

The fixed reader does not come back from those two, because they are also truncated and that is
F3 — a different defect, separately pinned. So the assertion is that the fault is not an
`IndexOutOfRangeException`, read on a thread of its own, with not coming back an acceptable answer
and reading off the end of the buffer not.

### F7 — `DdlReader.ObjectFromString` threw away the error list it was handed

```csharp
public static DocumentObject ObjectFromString(string ddl, DdlReaderErrors errors)
{
    using (var stringReader = new StringReader(ddl))
    {
        using (var reader = new DdlReader(stringReader))   // errors is not passed
            return reader.ReadObject();
    }
}
```

The overload exists to give the caller the reader's complaints, and it built the reader with the
one-argument constructor, which sets the error manager to null. Every non-fatal complaint the
parser collected went nowhere and the list came back empty however wrong the DDL was.
`ObjectFromFile` has always passed its own along, so the two overloads disagreed.

This matters more than it looks, because the parser reports far more than it throws. Every
attribute assignment is wrapped in `catch (Exception ex) { ReportParserException(ex,
InvalidAssignment, …) }`, so a colour out of range, a colour space that is not implemented, a
misspelt attribute name and a malformed number are all *reported* rather than thrown — and none of
those reports could reach a caller reading from a string. What the caller saw was a document that
had read cleanly with some of its properties quietly unset.

Fixed by passing `errors` to the constructor. It turned an existing test round:
`AnAttributeThatNamesNothingIsDiscardedWithoutAWord` asserted that a misspelt attribute produces no
complaint, which was true of what it could observe and false of what the parser did. It is now
`AnAttributeThatNamesNothingIsDiscardedAndTheReaderSaysSo` and asserts the dropped name is in the
message.

The blanket `catch (Exception)` is left as it is and pinned by `DdlColourTests` rather than argued
with — a reader that carries on past a bad attribute is a defensible choice, and now that the
complaints reach the caller it is a reasonable one.

### F8 — an axis title could never be written, because a `Unit` was compared to null

```csharp
if (this.orientation != null)
  serializer.WriteSimpleAttribute("Orientation", this.Orientation);
```

`orientation` is a `Unit`, which is a value type, so `!= null` cannot mean what it looks like. It
compiles only because `Unit` has an implicit conversion from `string`: the compiler converts the
`null` literal to a `Unit` and compares against that. The conversion throws on null — and this
fork's `Unit` throws with a message that says so in as many words, having been made to explain
exactly this mistake:

> A null string cannot be converted to a Unit. If this came from writing 'unit == null', that
> comparison is always meaningless: Unit is a value type, and the implicit string conversion is the
> only reason it compiles at all. Test unit.IsEmpty instead.

So `AxisTitle.Serialize` threw `ArgumentNullException` every time it ran, unconditionally, and a
chart whose axis has a title could not be written to MDDDL at all. It is the only `Unit`-against-null
comparison left in the tree; the eleven other `Unit` fields in the charting DOM all use `.IsNull`.
Fixed to match them.

### F9, F10, F11 — an image could not survive a round trip, for three separate reasons

All three had to be fixed before a single image could be written and read back, which is why they
had gone unnoticed together: each hid the next.

**F9 — the writer wrote a field nothing ever fills.**

```csharp
serializer.WriteLine("\\image(\"" + (this.name ?? "")… + "\")");
```

`Image.name` is declared `internal string name = null` and is assigned nowhere in the repository.
Neither route to an image fills it: `AddImage` takes an `IImageSource` and sets `Source`, and the
parser also puts `\image("path")` on `Source`. Every image in every document was therefore written
as `\image("")`, losing the path. Now falls back to `Source?.Name`.

**F10 — the reader asked the backend for an image at `""` before reading the path.**

```csharp
case Symbol.Image:
    ParseImage(elements.AddImage(ImageSource.FromFile("")), false);
```

The placeholder is built first and `ParseImage` sets the real source afterwards — but
`FromFile("")` throws immediately, on every backend, so *no `\image` in any .mdddl file could be
read at all*. Twice, once for a section element and once for paragraph content. Both now pass
`null`, which `AddImage` accepts and `ParseImage` overwrites.

**F11 — an image with nothing but a source counted as empty.**

`Source` is a plain auto-property rather than part of the `[DV]` value model, so the generated
`IsNull` cannot see it. An image built with `AddImage` and given no size was "null", and the
serializer skips null objects — so writing such a document dropped the image and the section around
it, silently. `Image` now overrides `IsNull()` to say that an image with a source is not empty.

Pinned by `ImageSerializationTests`, which writes an image, reads it back, and checks the path it
gets is the path it gave.

### F12 — a path of nothing but digits walked the index off the front of the string

`file.pdf#3` means page three, and the same sixteen-branch parse of it exists twice: in
`XPdfForm.ExtractPageNumber` and in MigraDoc's `ImageHelper.ExtractPageNumber`, whose copy carries
the comment "duplicated from class XPdfForm". The good news is that they had not drifted — the two
are behaviourally identical, which is not what `CLAUDE.md` leads one to expect of a copied pair.

They were identically wrong. The loop that walks back over the trailing digits tests the character
before the bound:

```csharp
while (Char.IsDigit(path, length) && length >= 0)
    length--;
```

When every character is a digit, `length` reaches -1 and `Char.IsDigit(path, -1)` throws
`ArgumentOutOfRangeException` rather than the loop ending. So `ExtractPageNumber("123", out _)`
threw — a file named for a number, with no extension, is enough. Reordered to `length >= 0 &&
Char.IsDigit(path, length)` in both, after which the loop leaves `length` at -1, the `#` test fails,
and the path comes back unchanged with page 0, which is right.

Pinned by `ExtractPageNumberParityTests`, which runs both implementations over one table so that a
future divergence fails rather than passing quietly in one assembly.

### F13 — ticking a twin check box could tick both halves of it

The `HasKids` branch of `PdfCheckBoxField.Checked` deals with the two children in turn, and shares
one local between them:

```csharp
string name1 = "";
// … search child 0's /AP /N for a name that is not /Off, assign it to name1 …
if (name1.Length != 0) { /* write name1 to child 0 */ }

// … search child 1's /AP /N for /Off, assign it to name1 …
if (name1.Length != 0) { /* write name1 to child 1 */ }
```

The second search only ever *assigns* `name1` when it finds what it is looking for. If child 1 has
no `/AP`, or an `/AP` with no `/Off` state in it, `name1` still holds the on state found for child
0 — and child 1 is written with it. Both children end up ticked, which is precisely the state the
whole branch exists to prevent, and the comment above it records as two days of work.

Fixed by clearing `name1` between the halves, in both the ticking and the unticking direction, so
that finding nothing means writing nothing. Pinned by `AChildWithNoAppearanceIsLeftAsItWas`.

### F14 — an array would not follow an indirect reference; the identical dictionary entry would

`PdfDictionary.DictionaryElements` and `PdfArray.ArrayElements` offer the same five scalar
accessors — `GetBoolean`, `GetInteger`, `GetReal`, `GetString`, `GetName`. Each has the same shape:
return a default if the entry is absent or null, unwrap a direct value, unwrap an indirect one,
otherwise refuse.

Every one of the dictionary's did the third step:

```csharp
if (obj is PdfReference)
    obj = ((PdfReference)obj).Value;
```

Not one of the array's did. So `/Foo << /Bar 3 0 R >>` read back its boolean and `/Foo [ 3 0 R ]`
threw `InvalidCastException` from the same call on the same value. An indirect scalar inside an
array is uncommon but perfectly legal, and a writer that reuses one value across several arrays
produces exactly this.

Fixed in all five rather than in the two on the backlog: repairing `GetBoolean` and `GetString`
alone would have left three siblings in the same class behaving differently from their two
neighbours, which is a worse state than the one it started in. Pinned by
`EveryScalarAccessorOnAnArrayFollowsAnIndirectReference`, which holds all five to it at once.

### F15 — the content lexer could not read `d0` or `d1`, so every Type 3 glyph was misread

Found while writing a fixture for batch 10 and much larger than the batch. `ScanOperator` ends an
operator at the first character `IsOperatorChar` rejects, and that accepts letters, `*`, `'` and
`"` — no digits. `d0` and `d1` are the only content operators with a digit in them, and the PDF
specification requires a Type 3 glyph description to **begin** with one of the two.

So the scanner read `d`, the setdash operator, and left the digit behind as an operand of whatever
came next. Reading the first line of any Type 3 glyph:

```text
1000 0 0 0 200 200 d1 /Im1 Do   ->   d(6) Do(2)
```

`Do` is handed two operands, `1` and `/Im1`, and everything that reads a content stream by operand
position reads the wrong one. `PdfResourcePruner` asks for operand 0, gets the number, finds no
name to keep, and prunes away every resource the glyph draws with. That is how it was noticed; the
misreading is not the pruner's and affects every consumer of `PdfPinata.Pdf.Content`, and it
shifts *all* the operators after the first, not only the first.

The fix takes the pair by name rather than opening `IsOperatorChar` to digits, which would let an
operator swallow the operand of whatever followed it:

```csharp
while (IsOperatorChar(ch))
    ch = AppendAndScanNextChar();

if (_token.Length == 1 && _token[0] == 'd' && (ch == '0' || ch == '1'))
    AppendAndScanNextChar();
```

A digit only joins a `d` when it follows immediately, which is never how setdash and its successor's
operand are written. Pinned by three tests in `CLexerTests`: the two operators read as one token
each, setdash still reads as itself, and no other digit joins an operator.

`CLAUDE.md` names the content lexer as "the older and rougher of the two"; `Pdf.IO/Lexer.cs` has no
equivalent fault because the document body has no operators.

### F16 — an unterminated string ending in a backslash put the end-of-file marker in the text

The 8-bit branch of `CLexer.ScanLiteralString` guards the top of its loop against the end of the
content, and the escape handler reads a character *after* that guard has been passed. A content
stream ending `(a\` therefore set `ch` to `Chars.EOF`, fell through the escape switch to its
default arm, and appended the marker itself — so the token came back with U+FFFF in it.

A caller reading the text of a truncated content stream got a replacement-looking character that
was in no PDF. Fixed by returning at that point instead:

```csharp
if (ch == Chars.EOF)
    return _symbol = CSymbol.String;

_token.Append(ch);
```

Found while covering 11.1, whose note says a backslash before end-of-input is one of the places the
uncovered branches are. `CLAUDE.md` says to check each uncovered branch against `Pdf.IO/Lexer.cs` —
that comparison came out clean otherwise, including the line continuations, which the content lexer
handles for CR, LF and CRLF alike because it normalises them before the escape is looked at.

### F17 — a repeated symbol was drawn the square of its count

`ParagraphRenderer.GetSymbol` answers the character a `Character` stands for, repeated as many
times as the character says it repeats:

```csharp
string returnString = "";
returnString += ch;
int count = character.Count;
while (--count > 0)
    returnString += ch;
```

`RenderSymbol` then repeated *that* the same number of times again:

```csharp
string sym = GetSymbol(character);
string completeWord = sym;
for (int idx = 1; idx < character.Count; ++idx)
    completeWord += sym;
```

So `AddCharacter(SymbolName.Bullet, 3)` drew nine bullets, and four drew sixteen — measured
directly before fixing:

```text
count 1 -> 1 glyph    count 2 -> 4 glyphs    count 3 -> 9 glyphs    count 4 -> 16 glyphs
```

Worse than a wrong count: `FormatSymbol` measures `GetSymbol`'s answer, so the formatter reserved
room for `Count` and the renderer drew `Count` squared into it, over whatever came next.

`RenderSymbol` is the one at fault. `GetSymbol`'s other two callers — the bookmark title builder
and `FormatSymbol` — both want the repeated form, so the repetition belongs where it is and the
second one had to go:

```csharp
void RenderSymbol(Character character)
{
    RenderWord(GetSymbol(character));
}
```

Pinned by `ARepeatedSymbolIsDrawnAsManyTimesAsItSaysItIs` over counts of one, two, three and five,
and by `ARepeatedSymbolTakesTheWidthTheFormatterReservedForIt`, which checks that what follows the
symbols begins where they end.

### F18 — the UTF-16 half of the string scanner had F16's fault and did not get F16's fix

`ScanLiteralString` is two loops, not one. A literal opening with the UTF-16 byte order mark is
read two bytes at a time by the first; everything else is read a byte at a time by the second. The
two are near-copies of one another, down to the comment `// TODO: not sure that this is correct...`
in both, and F16 fixed only the second.

Both compose a character and then append it, and in both the escape arm reads the next character
before the guard at the top of the loop can be reached again:

```csharp
case '\':
{
    ch = ScanNextChar();   // content ends here, so ch is Chars.EOF
    ...
}
break;
```

So content ending in a lone backslash put U+FFFF into the UTF-16 string exactly as it had into the
8-bit one. The fix is F16's, in the other loop:

```csharp
if (ch == Chars.EOF)
    return _symbol = CSymbol.String;

_token.Append(ch);
```

`CLAUDE.md` names this shape — *"a change to one nearly always belongs in the other"* — for the two
lexers and for the category axis renderers. It holds inside a single method too, and F6 was the
same thing: `PeekPunctuator` and `ScanPunctuator` are copies, and only one had the bound wrong.

Pinned by `ScanLiteralString_endsAUnicodeStringAtTheEndOfTheContentWithoutInventingCharacters`,
which feeds the scanner raw bytes because the branch is chosen by a byte order mark. The tests
beside it — `ScanLiteralString_readsAUnicodeStringTwoBytesAtATime` and
`ScanLiteralString_carriesTheHighByteOfAUnicodeCharacter` — say the ordinary case still reads, and
were what made the omission findable: the loop was covered, and the one line the 8-bit copy had
gained was the line missing from it.

### F19 — no matrix this library wrote could be read back

`PdfDictionary.Elements.GetMatrix` accepted a matrix written as an array of six numbers and refused
one written as a literal:

```csharp
else if (obj is PdfLiteral)
{
    throw new NotImplementedException("Parsing matrix from literal.");
}
```

The literal is the shape it meets. `SetMatrix` writes one:

```csharp
public void SetMatrix(string key, XMatrix matrix)
{
    _elements[key] = PdfLiteral.FromMatrix(matrix);
}
```

and so did `GetMatrix`'s own create branch, which wrote the identity as the hard-coded string
`"[1 0 0 1 0 0]"` under a comment reading `// cannot be parsed, implement a PdfMatrix...`. Nothing
in the library writes a matrix any other way: `PdfFormXObject`, `PdfShadingPattern` and
`PdfGradientSoftMask` all set `/Matrix` through `SetMatrix`. So every matrix the library produced
was one it could not read, and a caller asking a form XObject for its own `/Matrix` got
`NotImplementedException`.

Nothing had to change about what is written — six numbers between brackets is what a PDF array
looks like on the page anyway. Only the reading:

```csharp
else if (obj is PdfLiteral literal)
{
    value = MatrixFromLiteral(literal);
}
```

`MatrixFromLiteral` strips the brackets, splits on whitespace and parses six invariant-culture
numbers, throwing `InvalidCastException` for anything that is not six of them — the same exception
the array path throws for an array of the wrong length, so the two shapes now fail alike as well as
succeed alike. The create branch now writes `PdfLiteral.FromMatrix` rather than its own copy of the
identity, so there is one spelling of a written matrix instead of two.

Pinned by `AMatrixTheCreateOverloadWroteIsReadBackAsTheIdentity`,
`AMatrixSetAsALiteralComesBackWithTheNumbersItWasGiven` and `ALiteralThatIsNotSixNumbersIsNotAMatrix`.
The first of those was in the branch already, asserting the `NotImplementedException` under the
name `AMatrixTheCreateOverloadWroteCannotBeReadBack` — a test that recorded the defect accurately
and should have been a finding rather than a pin.

### F20 — a check box whose widget is a child of its own ignored being ticked

`PdfCheckBoxField.Checked` has two paths: the field has no children, or it has exactly two. The
two-child path is upstream's answer to a document where the same field is drawn in two places, and
its comment records that finding it took two working days. Every other number of children fell off
the end of both the getter and the setter:

```csharp
if (Fields.Elements.Items.Length == 2)
{
    ...
}
// and nothing otherwise
```

One child is not an unusual shape. A field whose widget annotation is a separate object rather than
merged into the field dictionary is ordinary, and it is a tick box like any other: it has one value
and one appearance state to show. Under the old code `Checked = true` did nothing at all to it, and
the getter then answered `false` — a requested state change lost with no error.

The single-child case is now handled where it belongs, beside the no-children one:

```csharp
else if (Fields.Elements.Items.Length == 1)
{
    PdfDictionary child = ChildAt(0);
    string name = value ? OnStateOf(child) : OffStateOf(child);
    if (child != null && name.Length != 0)
    {
        child.Elements.SetName(Keys.V, name);
        child.Elements.SetName(PdfAnnotation.Keys.AS, name);
        Elements.SetName(Keys.V, name);
    }
}
```

The getter reads the first child whatever the number of children, which it can now do because the
`== 2` restriction on it was arbitrary: the pair path already answered from child 0 alone.

Three or more children is still left alone, deliberately. The pair scheme is not "all the widgets
show the state" — it is child 0 on and child 1 off, and unticking swaps them, so the two children
are a pair rather than a set. That says nothing about what a third widget should be, and inventing
an answer would change what real forms are written with. `AFieldWithThreeChildrenIsStillLeftAlone`
records it, and says why in the words above rather than leaving the reader to work it out.

`ChildAt` also fixes a smaller thing on the way past: the existing code casts each child to
`PdfReference` unconditionally, so a child written as a direct dictionary throws
`InvalidCastException`. The helper follows a reference when there is one and takes the dictionary
when there is not.

Pinned by `AFieldWithOneChildTakesTheStateItIsAskedFor` and
`AFieldWithOneChildCanBeClearedAgain`.

## Batch 2 — public DOM members with no test at all

~~`PinataLayout.DocumentObjectModel.Tests`.~~ **Written in `src/PdfPinata.Test/Dom/` instead** — see
the note below. Direct calls with plain arguments; no document to build.

| # | target | CC | cov | retires | status |
|---|---|---|---|---|---|
| 2.1 | `Style.BaseStyle` setter — `Style.cs:180` | 28 | 0% | 784 | **done**, 13 tests |
| 2.2 | `Table.SetShading(int, int, int, int, Color)` — `Table.cs:142` | 20 | 0% | 400 | **done**, 17 tests, finding F4 |
| 2.3 | `Font.ApplyFont(Font)` — `Font.cs:120` | 20 | 0% | 400 | **done**, 9 tests |
| 2.4 | `Document.AddStyle(string, string)` — `Document.cs:123` | 12 | 0% | 144 | **done**, 11 tests, finding F5 |

`Style.BaseStyle` carries cycle detection over twenty-eight branches and is the one to write first —
a style set as its own base, or a pair set as each other's, is the case worth pinning. `SetShading`
is range checks over a cell block; assert the cells outside the block are untouched, not only that
the ones inside changed.

**Where these went.** The batch says `PinataLayout.DocumentObjectModel.Tests` and they are in
`src/PdfPinata.Test/Dom/`, because every one of these four classes already has tests there —
`StyleLookupTests`, `ReadOnlyStyleTests`, `TableRowCellsTests`, `FormattedTextFontRoundTripTests` —
and `CLAUDE.md` says the two suites do not overlap. Splitting `Style` across both projects to
follow the batch heading would have cost more than it saved. Four new files:
`StyleBaseStyleTests`, `TableSetShadingTests`, `FontApplyFontTests`, `DocumentAddStyleTests`.

Two things were learned rather than assumed, and are pinned as such. A `Font` cannot state both
subscript and superscript — each setter unstates the other — so the `else` in `ApplyFont` never has
to choose. And `Style.BaseStyle` reads its parent collection to resolve the name, so a `Style` built
but not yet added to a document throws `NullReferenceException` from a public setter rather than
saying what is wrong; recorded in the test rather than fixed, because `Styles.Add` validates the
same thing properly a moment later.

## Batch 3 — the MDDDL scanner and parser, reading

`PinataLayout.DocumentObjectModel.Tests`. Reached through `DdlReader` — DDL text in, `Document` out.
Thirty methods across these two files sit above the threshold; these six are the ones in the top 50.

| # | target | CC | cov | retires | status |
|---|---|---|---|---|---|
| 3.1 | `DdlScanner.PeekPunctuator(int)` — `DdlScanner.cs:231` | 55 | 17% | 1,717 | **done**, finding F6 |
| 3.2 | `DdlParser.ParseCMYK()` — `DdlParser.cs:2328` | 24 | 0% | 576 | **done** |
| 3.3 | `DdlScanner.ScanPunctuator()` — `DdlScanner.cs:1225` | 51 | 48% | 370 | **done** |
| 3.4 | `DdlParser.ParseArea(TextArea)` — `DdlParser.cs:1528` | 22 | 24% | 209 | **done** |
| 3.5 | `DdlParser.ParseChr(ParagraphElements)` — `DdlParser.cs:827` | 12 | 0% | 144 | **done** |
| 3.6 | `DdlParser.ParseRGB()` — `DdlParser.cs:2287` | 12 | 0% | 144 | **done** |

72 tests in three new files: `DdlColourTests` (3.2, 3.6), `DdlCharacterAndPunctuationTests` (3.1,
3.3, 3.5) and `DdlChartAreaTests` (3.4). Two findings, F6 and F7, both fixed.

Note that 3.4 names `ParseArea(TextArea)` and there is a second `ParseArea(PlotArea)` overload
beside it. The plot area is a `ChartObject` rather than a `TextArea`, and its overload reads
attributes and then discards everything between the braces — the source marks the spot with an
unanswered "ignore everything? warn?". Left alone; it is not the item and giving it meaning is a
feature rather than a test.

Build one DDL corpus rather than one string per test: colour literals in both `RGB(…)` and
`CMYK(…)` forms drive 3.2 and 3.6, a `\chr(…)` run drives 3.5, and punctuation-dense input drives
3.1 and 3.3 together. Malformed input matters as much as valid input here — half the uncovered
branches are the error paths.

Two of these scan malformed text, so carry `[Fact(Timeout = …)]` with the `Task.Run` wrapper that
`CLexerTests` uses: xUnit honours a timeout only on an `async` test, and a scanner change that loops
hangs the host rather than failing it.

## Batch 4 — the MDDDL serializers, writing

`PinataLayout.DocumentObjectModel.Tests`. The same corpus written back out through `DdlWriter`.
Round-tripping covers these on the way past, which is why they follow batch 3 rather than lead it.

| # | target | CC | cov | retires | status |
|---|---|---|---|---|---|
| 4.1 | `Barcode.Serialize(Serializer)` — `Shapes/Barcode.cs:161` | 18 | 0% | 324 | **done** |
| 4.2 | `Character.Serialize(Serializer)` — `Character.cs:135` | 18 | 0% | 324 | **done** |
| 4.3 | `Image.Serialize(Serializer)` — `Shapes/Image.cs:160` | 12 | 0% | 144 | **done**, findings F9–F11 |
| 4.4 | `Footnote.Serialize(Serializer)` — `Footnote.cs:223` | 12 | 0% | 144 | **done** |
| 4.5 | `AxisTitle.Serialize(Serializer)` — `Shapes.Charts/AxisTitle.cs:162` | 12 | 0% | 144 | **done**, finding F8 |
| 4.6 | `Serializer.WriteComment(string)` — `Serializer.cs:209` | 14 | 12% | 134 | **done** |
| 4.7 | `Font.Serialize(Serializer, Font)` — `Font.cs:324` | 124 | 91% | 12 | **left**, as advised |

4.7 is on the list for its complexity, not its gap — it is already at 91% and only twelve points are
available. Take it last, or leave it; splitting the method would be worth more than covering it.
Left: `DdlSerializationTests` already round-trips every property a font has, and the twelve points
are not worth a test written to reach a branch rather than to say something.

`WriteComment` is the interesting one: a comment carrying a newline, and one long enough to wrap,
are what the uncovered branches are for.

33 tests in `src/PinataLayout.DocumentObjectModel.Tests/DdlElementSerializationTests`, and five more in
`src/PdfPinata.Test/Dom/ImageSerializationTests` — 4.3 is the one item of the batch that cannot go in
the backend-free suite, because an image is made by handing `AddImage` an `IImageSource`.

Worth carrying forward: **the DOM treats an object with nothing set as null and skips it when
writing**, and that is the reason several of these look untestable at first. A barcode with no
properties, and a paragraph holding nothing but an empty footnote, are both dropped along with the
section around them — so `Barcode.Serialize`'s "a barcode must have a code" guard is only reachable
once something *else* on the barcode is set. Pinned both ways rather than worked around.

## Batch 5 — the flattening visitors and the chart DOM

`PinataLayout.DocumentObjectModel.Tests`.

| # | target | CC | cov | retires | status |
|---|---|---|---|---|---|
| 5.1 | `Chart.CheckTextArea(TextArea)` — `Shapes.Charts/Chart.cs:524` | 24 | 27% | 227 | **done** |
| 5.2 | `Chart.DeepCopy()` — `Shapes.Charts/Chart.cs:77` | 28 | 35% | 214 | **done** |
| 5.3 | `RtfFlattenVisitor.VisitFormattedText(FormattedText)` — `Visitors/RtfFlattenVisitor.cs:45` | 14 | 0% | 196 | **done** |
| 5.4 | `VisitorBase.FlattenTabStops(TabStops, TabStops)` — `Visitors/VisitorBase.cs:280` | 12 | 0% | 144 | **done** |
| 5.5 | `VisitorBase.VisitTable(Table)` — `Visitors/VisitorBase.cs:699` | 90 | 81% | 57 | **done** |

`DeepCopy` wants the assertion to be that the copy is *independent* — mutate the original after
copying and check the copy did not move — not merely that a copy came back non-null. That is the
shape of bug a deep copy actually has.

36 tests in two new files: `ChartDomTests` (5.1, 5.2) and `FlatteningTabStopsAndTablesTests` (5.3,
5.4, 5.5). No findings — every one of these behaved as it should.

Two things worth carrying. `CheckTextArea` compares by reference and has no public caller, so it is
reached by writing a chart: an area asks its chart which of the six it is in order to know the
keyword to write itself under, which makes serializing the six-areas-at-once case the test that
would catch a mixed-up comparison. And the reparenting half of `DeepCopy` is not directly
observable — `parent` is `protected internal` — so it is checked by putting the copy in a second
document and writing it, which is what a mis-parented child would break.

## Batch 6 — one method, two copies

| # | target | CC | cov | retires | suite | status |
|---|---|---|---|---|---|---|
| 6.1 | `ImageHelper.ExtractPageNumber(string, out int)` — `ImageHelper.cs:99` | 16 | 0% | 256 | DOM | **done**, finding F12 |
| 6.2 | `XPdfForm.ExtractPageNumber(string, out int)` — `Drawing/XPdfForm.cs:392` | 16 | 46% | 41 | Core | **done**, finding F12 |

The same sixteen-branch parse of a `file.pdf#page=3` path, once per assembly, and nothing makes them
agree. Write one table of path strings — no fragment, an empty fragment, a non-numeric page, a
negative page, a page of zero, whitespace — and assert both against it. If they disagree, that is a
finding, not a test to bend around.

This is the pattern `CLAUDE.md` warns about for the two lexers and the category-axis renderers: a
change to one nearly always belongs in the other, and the copy usually has one guard the twin lacks.

**They agree, and they were wrong together** — see F12. 24 tests in one file,
`src/PdfPinata.Test/Dom/ExtractPageNumberParityTests`, asserting both implementations against one
table of twenty paths. Both go in `PdfPinata.Test` rather than one each: it references both
assemblies, so a single theory can hold the two to the same answer, which is the only arrangement
that actually makes them agree rather than merely testing them separately.

## Batch 7 — the AcroForms check box

`PdfPinata.Test`. The largest single-method line debt in the report.

| # | target | CC | cov | retires | status |
|---|---|---|---|---|---|
| 7.1 | `PdfCheckBoxField.Checked` setter — `Pdf.AcroForms/PdfCheckBoxField.cs:84` | 48 | 7% | 1,853 | **done**, finding F13 |

One setter, 106 uncovered lines. The simple path is already covered; the whole cost is the `HasKids`
branch that handles two fields sharing a name, which the code's own comment records as two days of
work. Reach it through public API: open a form PDF with a kids-bearing check box, set the value,
write it, reopen and read `/V` and `/AS` back. Needs an asset — check `src/PdfPinata.Test/Assets`
before making one.

No asset needed in the end: `src/PdfPinata.Test/Pdfs/AcroForms/AcroFormBuilder` already builds a
form from nothing and reads it back, which is how the existing field tests get a
`PdfCheckBoxField` at all. It gained one method, `WithTypedParent`, for a field that has both a
type of its own and children of its own. 9 tests in `AcroFormTwinCheckBoxTests`.

The scheme turned out not to be what the name suggests, and is worth stating: **both children
always carry a value, and the state of the box is which of the two holds the on state.** Ticked
means child 0 is on and child 1 is `/Off`; unticked means child 1 is on and child 0 is `/Off`. The
getter reads child 0 alone. A field with children but not exactly two of them is left untouched by
both halves.

## Batch 8 — the typed element accessors

`PdfPinata.Test`. Small, in-memory, no file and no round trip. Their siblings on the same classes
are covered; these three were simply missed.

| # | target | CC | cov | retires | status |
|---|---|---|---|---|---|
| 8.1 | `PdfDictionary.DictionaryElements.GetMatrix(string, bool)` — `Pdf/PdfDictionary.cs:706` | 12 | 0% | 144 | **done** |
| 8.2 | `PdfArray.ArrayElements.GetBoolean(int)` — `Pdf/PdfArray.cs:208` | 12 | 0% | 144 | **done**, finding F14 |
| 8.3 | `PdfArray.ArrayElements.GetString(int)` — `Pdf/PdfArray.cs:294` | 12 | 0% | 144 | **done**, finding F14 |

Each reads an item that may be a `PdfReference`, a null, absent, or the wrong type outright. Cover
all four, and note that the `create` overloads behave differently from the plain ones.

19 tests in `src/PdfPinata.Test/Pdfs/TypedElementAccessorTests`. The `PdfReference` case is the one
that mattered — see F14.

Two things noted in passing. `GetMatrix`'s create overload writes `[1 0 0 1 0 0]` as a
`PdfLiteral`, and the read path throws `NotImplementedException` on a literal — so **a matrix the
create overload wrote cannot be read back by the method that wrote it**. Pinned as it stands; the
source's own comment ("cannot be parsed, implement a PdfMatrix...") says the author knew. And this
test assembly has a test *class* called `PdfInteger` in `PdfPinata.Test`, which shadows
`PdfPinata.Pdf.PdfInteger` for everything in a nested namespace — the same trap the
`AcroFormBuilder` comment records for `PdfReader`.

## Batch 9 — document plumbing

`PdfPinata.Test`.

| # | target | CC | cov | retires | status |
|---|---|---|---|---|---|
| 9.1 | `PdfCatalog.Version` setter — `Pdf.Advanced/PdfCatalog.cs:63` | 32 | 0% | 1,024 | **done** |
| 9.2 | `PdfFormXObjectTable.DetachDocument(DocumentHandle)` — `Pdf.Advanced/PdfFormXObjectTable.cs:136` | 14 | 0% | 196 | **done** |
| 9.3 | `KeysMeta.GetValueType()` — `Pdf/KeysMeta.cs:104` | 27 | 42% | 144 | **done** |
| 9.4 | `Parser.ReadObject(PdfObject, PdfObjectID, bool, bool)` — `Pdf.IO/Parser.cs:111` | 68 | 74% | 82 | **left**, 82 points |

9.1 validates a version string across thirty-two branches and has never run — every malformed
version is an uncovered branch. 9.2 is the detach half of the `XPdfForm` import path; reaching it
means importing a form from one document and disposing the source, which is worth a test on its own
account regardless of the score.

39 tests in two new files, `DocumentPlumbingTests` (9.1, 9.2) and `KeyValueTypeTests` (9.3). No
findings, but three things are worth carrying:

- **The catalog accepts two versions and no others.** 1.0 to 1.2 and 1.5 to 1.6 are refused as
  unsupported, and anything else — including 1.7 and 2.0 — is refused as unreadable. The default
  is 1.4 rather than the 1.3 the field is declared with, under a comment reading "HACK in
  PdfCatalog". Pinned as it stands: raising the ceiling would change what every document declares.
- **9.2 has no public route.** Its only caller is `PdfDocument.OnExternalDocumentFinalized`, which
  runs when an imported document is finalized, and a test cannot ask for that. Reached by
  reflection with the reason stated in the test, which is what the rules at the top allow.
- **A key declared as a scalar cannot be created.** `GetValue(key, VCF.Create)` builds dictionaries
  and arrays and throws `NotImplementedException` for the other eight types `KeysMeta` can name, so
  eight of its ten arms resolve a type correctly and then have it refused. So does a key with no
  declaration at all, which makes `VCF.Create` unsafe against an extension's own keys.

**9.4 was left**: 82 points at 74%, the lowest-value item of the batch, and its uncovered branches
are malformed-object recovery paths that want a corrupt-document fixture apiece.

## Batch 10 — the resource pruner

`PdfPinata.Test`.

| # | target | CC | cov | retires | status |
|---|---|---|---|---|---|
| 10.1 | `PdfResourcePruner.ReadCharProcs(PdfDictionary, PdfDictionary, int)` — `Pdf.Advanced/PdfResourcePruner.cs:299` | 12 | 0% | 144 | **done**, finding F15 |
| 10.2 | `PdfResourcePruner.ReadAppearances(PdfPage)` — `Pdf.Advanced/PdfResourcePruner.cs:328` | 24 | 38% | 136 | **done** |

A Type 3 font's `/CharProcs` and an annotation's `/AP` stream are the two inputs. Both are cases
where pruning too much is silent — assert what survived, not only that pruning ran.

7 tests in `src/PdfPinata.Test/IO/PruneCharProcsAndAppearancesTests`, with three fixtures added to
`SharedResourceFixtures`. Writing the Type 3 one the way a real Type 3 font is written is what
turned up F15, which is the largest finding of the backlog so far and is not in the pruner at all.

## Batch 11 — the content lexer and text layout

`PdfPinata.Test`.

| # | target | CC | cov | retires | status |
|---|---|---|---|---|---|
| 11.1 | `CLexer.ScanLiteralString()` — `Pdf.Content/CLexer.cs:330` | 88 | 67% | 281 | **done**, finding F16 |
| 11.2 | `XTextSegmentFormatter.AlignLine(IList<Block>, int, int, double)` — `Drawing.Layout/XTextSegmentFormatter.cs:512` | 38 | 48% | 207 | **done** |
| 11.3 | `XTextSegmentFormatter.CreateLayout(List<List<Block>>, XRect)` — `Drawing.Layout/XTextSegmentFormatter.cs:354` | 40 | 62% | 88 | **done** |

11.1 is the content lexer, which `CLAUDE.md` names as the rougher of the two — so check each
uncovered branch against `Pdf.IO/Lexer.cs`'s literal-string scanner as you go. Where the document
lexer has a guard this one lacks, that is a finding to record rather than a branch to cover.

The comparison came out clean: the content lexer handles the line continuations the document lexer
does, because it normalises CR, LF and CRLF before the escape is looked at. Its own fault was
elsewhere — see F16. 21 new tests in `CLexerTests`, and 8 in `XTextSegmentFormatterTests` covering
all four alignments and the wrapping that survives them.

Escapes, nested unbalanced parentheses, octal runs of one to three digits, and a backslash before
end-of-input are where the uncovered branches are.

## Batch 12 — fonts and encryption

`PdfPinata.Test`.

| # | target | CC | cov | retires | status |
|---|---|---|---|---|---|
| 12.1 | `RC4Encryptor.CreateOwnerKey(string)` — `Pdf.Security/RC4Encryptor.cs:112` | 12 | 0% | 144 | **left**, unreachable |
| 12.2 | `FontResolverBase.GetPlatformFontFiles()` — `Utils/FontResolverBase.cs:157` | 12 | 0% | 144 | **left**, per-platform |
| 12.3 | `OpenTypeFontface.AddTable(OpenTypeFontTable)` — `Fonts.OpenType/OpenTypeFontface.cs:191` | 72 | 77% | 61 | **left**, 61 points |

12.2 walks the machine's font directories and branches per platform, so most of it cannot run on any
one host — cover what is reachable and leave the rest, rather than contorting the test. It is on
the list because it is untested, not because it can reach 100%.

**This batch was left, and it is the only one that was.** Each item turned out to be a poor target
on inspection rather than on principle, and the reasons are worth writing down so that a later
reading does not simply try again:

- **12.1 is unreachable.** `RC4Encryptor` is internal, `CreateOwnerKey` is not on `IEncryptor`, and
  nothing in the repository calls it or its sibling `CreateUserKey`. This fork reads encrypted
  documents and does not write encryption, so both are the unused half of the algorithm. Unlike
  batches 0 and 1 it was **not** deleted: it implements PDF Reference algorithm #3 by name and is
  the obvious basis for write support, so removing it would throw away a spec implementation to buy
  144 points. Left in place and recorded here instead.
- **12.2 is `private static` and reads the host's real font directories.** One of its three
  platform arms can run on any given machine, and which one is not the test's to choose. Covering
  the reachable arm means asserting that this machine has fonts, which is a fact about the machine.
- **12.3 is 61 points at 77%.** The remaining branches want a writable font image, which means
  building one — a large fixture for the smallest return on the list.

Batch 12 is 349 points of the backlog's 17,900, and buying them would have meant a reflection
harness for an unreachable method, a host-dependent assertion, and a font-image builder.

## Batch 13 — charting

`PdfPinata.Charting.Tests`. The three helpers under `Helpers/` are what make these legible —
`Drawn`, `PaintedRectangles`, `ShownText`.

| # | target | CC | cov | retires | status |
|---|---|---|---|---|---|
| 13.1 | `Chart.DeepCopy()` — `PdfSharp.Charting/Chart.cs:70` | 14 | 0% | 196 | **done** |
| 13.2 | `Chart.CheckAxis(Axis)` — `PdfSharp.Charting/Chart.cs:114` | 12 | 0% | 144 | **left**, unreachable |
| 13.3 | `LineFormatRenderer..ctor(XGraphics, LineFormat, double)` — `Renderers/LineFormatRenderer.cs:44` | 12 | 0% | 144 | **done** |
| 13.4 | `BarDataLabelRenderer.CalcPositions()` — `Renderers/BarDataLabelRenderer.cs:115` | 17 | 26% | 118 | **done** |

13.4 is a data-label renderer, and `charting-renderer-findings.md` records that C7 was exactly this
shape on the pie. Read a blank point's value through `PointRendererInfo.Value`, which answers `NaN`,
rather than `point.value`, which throws.

16 tests, 8 in a new `ChartCloneAndLineFormatTests` and 8 added to `DataLabelTests`. No findings —
the bar label renderer behaves, including with a blank in the series.

**13.2 was left.** `Chart.CheckAxis` is the charting package's counterpart to the DOM's
`Chart.CheckTextArea`, and the DOM's is called by `TextArea.Serialize` — but this package has no
serializer, so nothing calls `CheckAxis` and nothing can: it is `internal` and there is no
`InternalsVisibleTo`. The same shape as batch 1, and left rather than deleted only because it is
four lines mirroring a live method next door.

13.3's constructor is reached through the gridlines of an axis, which is where every one of its
call sites builds one. Its `defaultWidth` parameter is always passed as zero: the two-argument
overload chains to it with zero and no other caller uses the three-argument form.

## Batch 14 — MigraDoc rendering

`PinataLayout.Rendering.Tests`. Rasterizes nothing; assert against the content stream.

| # | target | CC | cov | retires | status |
|---|---|---|---|---|---|
| 14.1 | `ParagraphRenderer.ProbeAfterDecimalAlignedTab(XUnit, out bool)` — `ParagraphRenderer.cs:507` | 12 | 0% | 144 | **done** |
| 14.2 | `ParagraphRenderer.GetSymbol(Character)` — `ParagraphRenderer.cs:2003` | 11 | 0% | 121 | **done**, finding F17 |
| 14.3 | `ImageRenderer.CalculateImageDimensions()` — `ImageRenderer.cs:189` | 56 | 71% | 80 | **left**, 80 points |

14.2 maps a `Character` to the text it renders as and has never run — every symbol in the enum is an
uncovered branch, so it takes the same theory shape as batch 1. 14.1 is the decimal-aligned tab
path, which needs a paragraph with a decimal tab stop and content on both sides of it.

21 tests in `SymbolAndDecimalTabTests`. 14.2 turned up F17, the second-largest finding of the
backlog. The symbols are asserted by comparing glyph sequences against the same character written
as text, which is what `Glyphs` exists for — MigraDoc embeds Identity-H, so a page cannot be read
back as characters.

**14.3 was left**: 80 points at 71%, and the branches that remain are combinations of explicit
width, explicit height and scaling that want a fixture apiece.

## Out of scope — complex, and already covered

These clear the threshold on complexity alone. They are at or near full coverage, so their score
*is* their complexity and no test can lower it. Listed so that a later reading of the hotspot report
does not mistake them for work.

| target | CC | cov |
|---|---|---|
| `PageSizeConverter.ToSize(PageSize)` — `root/PageSizeConverter.cs:44` | 123 | 100% |
| `AnsiEncoding.IsAnsi1252Char(char)` — `Pdf.Internal/AnsiEncoding.cs:104` | 78 | 100% |
| `AnsiEncoding.UnicodeToAnsi(char)` — `Pdf.Internal/AnsiEncoding.cs:151` | 78 | 100% |
| `Filtering.GetFilter(string)` — `Pdf.Filters/Filtering.cs:44` | 78 | 100% |
| `PageSetup.GetPageSize(PageFormat, out Unit, out Unit)` — `PageSetup.cs:74` | 70 | 100% |
| `Ascii85Decode.Decode(byte[], FilterParms)` — `Pdf.Filters/Ascii85Decode.cs:138` | 56 | 99% |
| `Lexer.ScanKeyword()` — `Pdf.IO/Lexer.cs:372` | 52 | 100% |
| `PdfAnnotationTransformer.TransformOne(PdfDictionary, XMatrix)` — `Pdf.Advanced/PdfAnnotationTransformer.cs:77` | 50 | 100% |

If any of these matters later, the answer is to split the method, and that is a refactor with its
own spec — not an item here.

## What the review of the pull request found

The work above was raised as one pull request, and reviewing it turned up three more defects in the
code — F18, F19 and F20 — and four tests that did not assert what their names claimed. The second
group is worth recording separately, because a test that cannot fail is worse than no test: it
occupies the place a real one would have gone and reports the opposite of the truth.

| the test | what it claimed | what it did |
|---|---|---|
| `ASignAtTheVeryEndOfTheDocumentIsNotReadPastTheEndOfIt` | the lookahead does not read off the end | called the reader through a helper that catches every exception, on an input that never reaches the lookahead. Passed with the defect deliberately put back |
| `AFormattedTextNamingAStyleThatDoesNotExistFallsBackToTheInvalidOne` | the fallback style arrives | asserted only that `Font` is not null, and `FormattedText.Font` makes one on being asked |
| `AGridlineFormatThatStatesNothingAtAllDrawsNoGridline` | no gridline is drawn | measured that no line is drawn *at the stated width*. A silent format does draw a gridline, at a hairline default — the name was the wrong half, not the assertion |
| `ABarChartWithABlankInItIsStillLabelled` | the chart is still labelled | asserted only that drawing does not throw |

Three shapes to watch for, from those four:

- **A helper that catches broadens what the test tolerates.** `ComplaintsAbout` exists to collect
  what the reader complains about, and collecting requires catching. That makes it exactly the
  wrong route for a test whose claim is that something is *not* thrown. The three copies of it are
  now one, in `ReaderDiagnostics`, whose summary says so.
- **An assertion that a lazily-built thing is not null asserts nothing.** Ask for a value only that
  thing would have.
- **"Does not throw" is half a test.** It says the code survived; it does not say it did the work.
  Both of the charting ones read that way, and both had an observable answer available.

The other two review points were declined, with reasons:

- A check box with three or more widget children is still a silent no-op. See F20 for why the pair
  scheme does not generalise to three.
- The two hang pins for F3 now run on background threads of their own rather than on `Task.Run`.
  This was raised as thread-pool starvation and it is real - a reader that does not return keeps
  its thread for the life of the process - but the fix is the thread, not the pin. The pins stay.

## Working the list

Take the batches in order. Within a batch the items share a fixture, which is the point of the
grouping — writing them one at a time in rank order costs more than writing the batch.

For each batch:

1. Write the tests. Where a target's uncovered branches turn out to be a defect rather than an
   untested path, record it here as a finding with its code and its fix, the way
   `charting-renderer-findings.md` does, and turn the test round to assert the fixed behaviour.
2. Run the batch's own project, then the whole suite. **Check the exit code and the test total**,
   not the word `Passed`.
3. Re-measure and update the status column with what the score actually became — the retires figure
   above is what full coverage would win, and partial coverage wins less.

Re-measuring is the same command as at the top, then:

```powershell
reportgenerator -reports:"TestResults/**/coverage.opencover.xml" -targetdir:"coverage" `
  -reporttypes:"Html;TextSummary"
```

`coverage.runsettings` sets `UseSourceLink` to `false`. Do not turn it on: ReportGenerator then
fetches every source file from GitHub, which takes minutes and rewrites local paths into raw URLs
that no editor can open.

Note that the five test projects each produce their own OpenCover report covering overlapping
assemblies, so a per-file reading understates coverage badly. Merge them per sequence point —
ReportGenerator does this correctly, and any hand analysis must do the same or it will report a
method as uncovered because a different suite is the one that covers it.
