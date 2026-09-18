# Health check — action list

Generated from the 8-dimension assessment of `fix/list-box-multiple-selection` at `1d46b2a`,
2026-08-17. **GPA 3.88 (A).** Seven dimensions grade A, one grades B.

This file is a work list for an agent. Every task names the files, the change, and the command that
proves it. Read [the "Do not do" section](#do-not-do) **before** starting — most of what a detector
flags in this repository is a false positive, and "fixing" it would be a regression.

Ground rules that constrain every task below, from `CLAUDE.md`:

- **Judge a test run by its exit code, not by the word `Passed`.** A total below what
  `dotnet test --list-tests` finds did not pass, it stopped.
- Assertions use **AwesomeAssertions** (a FluentAssertions fork — same API, different `using`).
- No `InternalsVisibleTo` anywhere. An `internal` target is reached through public API or reflection.
- Commit subjects are plain prose, imperative, no conventional-commit prefix. Bodies explain what was
  wrong and why the new way is right.
- `.editorconfig` enforces a final newline. Do not strip it.

---

## Severity 0 — Critical

**None.** No critical defects, no vulnerable packages, no cycles, no dead code, no build failures, no
undocumented public API.

Nothing here is urgent, and the graded task below raises a B to an A rather than fixing a bug. Do not
manufacture urgency that the assessment did not find. Two things are deliberately open all the same,
and neither is a reason to stop the world:

| Open | What it is | Where |
|---|---|---|
| **F23** | A paragraph with no style is written without a `\paragraph` keyword, so text whose first character needs escaping is read back at section level, where the escape is not honoured. Pinned by a test that asserts the failure; the repair belongs in the serializer rather than the encoder. | `docs/specs/crap-coverage-backlog.md` |
| **Generator cache granularity** | An edit *anywhere above* a declaration invalidates the cache, because the model holds absolute source positions. Recorded and pinned, not fixed. | `PinataLayout.DocumentObjectModel.Generators.Tests/IncrementalCachingTests.cs` |

---

## Severity 1 — High

### 1.1 ~~Document the backend registration surfaces~~ — VOID, no work required

**Closed without action.** The first draft of this list called for documenting 31 members in
`PdfPinata.ImageSharp` and `PdfPinata.Skia`. That was measurement error, not a gap.

Every one of those 31 members sits inside a type that is not publicly reachable:

| Type | Accessibility | Members |
|---|---|---|
| `SkiaImageSource.SkiaImageSourceImpl` | `private sealed` | 8 |
| `ImageSharpImageSource<T>.ImageSharpImageSourceImpl<T2>` | `private` | 10 |
| `ImageSharpGlyphOutlineProvider.OutlineCollector` | nested, no modifier → `private` | 12 |
| `ImageSharpVersion` | `internal static` | 2 |
| `OpenTypeFontMetadata` | `internal static` | 1 |

A `public` member of a `private` nested class is not public API. The compiler agrees: **all seven
shipped packages already set `GenerateDocumentationFile=true` in their own `.csproj`, and a forced
clean rebuild of each emits zero CS1591 warnings.** Public API documentation is complete.

The original 87.1% figure came from a scan that matched `public` on member declarations without
checking whether the containing type was reachable. The compiler-verified figure is **100%**.

### 1.2 Re-measure CRAP and open the next coverage batch — `Tests B → A`

**The only graded gap left in the repository.**

Coverage is **78.3% lines / 73.9% branches** over 3,897 tests. An A needs 90%.

**Measured, and the batches are worked.** `docs/specs/crap-coverage-backlog.md` now carries a
"Re-measured at `1d46b2a`" section and **batches 15 to 18**. Every target that could be reached from
public API has been written; five are deferred on a missing fixture and are listed below.

- [x] Re-measure — done 2026-08-17. 4,049 tests, exit 0, no crash. Methods above the CRAP threshold
      fell from 237 to **172**, and those never executed at all from 109 to **71**.
- [x] Append the new batch tables to the spec — batches 15 (the `Left/TopPosition.Parse` pair),
      16 (the generator's `EquatableArray` cache key), 17 (DDL and PDF text encoders) and
      18 (ten methods that have never run).
- [x] **Batch 15 — done.** `LeftAndTopPositionParityTests`, 58 tests, 24 of them holding both
      implementations to one table. Turned up **finding F21**: both tested for emptiness *before*
      trimming, so `"   "` read `value[0]` off the end and threw `IndexOutOfRangeException` out of a
      public API — F12's shape exactly, identical in both copies. Fixed in both. The
      `ArgumentNullException`-for-`""` question was settled by leaving it: changing it would break a
      caller catching that type and buys nothing, and the fix moved the behaviour of no input that
      previously worked.
- [x] **Batch 16 — done.** `IncrementalCachingTests`, 10 tests, taking `EquatableArray.Equals` from
      0% to 62.5% and below the CRAP threshold. No direct route existed — every model type is
      `internal` — so they assert the consequence through the public generator and Roslyn's step
      tracking: two separately parsed compilations, and whether the driver says `Cached`.
      **Nothing had tested that incremental caching works at all**, which is worth more than the
      score. Three things recorded rather than fixed: the `EquatableArray` in `DomTypeModel` sits
      downstream of every cache and is never compared; the remaining branches of `Equals` have no
      public route (checked, not assumed); and an edit *anywhere above* a declaration invalidates
      the cache, because the model holds absolute source positions.
- [x] **Batch 17 — 17.1 and 17.2 done**, both encoders 0%/37% → **100%**. `DdlEncoderTests`,
      45 tests. Two findings: **F22** (fixed) — `StringToText` escaped only the first slash of each
      `//` pair, so `"///"` came out as `\///`, an escaped slash followed by a comment that swallowed
      the rest of the line; and **F23** (pinned, not fixed) — a paragraph with no style is written
      without a `\paragraph` keyword, so text whose first character needs escaping is read at section
      level where the escape is not honoured. 17.3 left (needs a hex-string PDF fixture; `PdfEncoders`
      and `PdfStringFlags` are both internal). 17.4 and 17.5 left and deliberately unmoved — they are
      about paragraphs continuing across lines and want a batch built from multi-line fixtures.
- [x] **Batch 18 — first six worked, 18.1 and 18.5 done.** 16 tests. `PdfTextExtractor.Walker
      .Execute` **60% → 95.5%** (CRAP 168 → 44) by writing content streams by hand, since `XGraphics`
      only ever emits four of the operators it understands. All four `CalculateTextSize` overloads
      covered. **Three of the six were unreachable** — batch 12's lesson again: `PdfPages.FindPage`
      has no caller anywhere, `DictionaryElements.CreateValue` is private with no caller, and
      `PdfCrossReferenceTable.CheckConsistence` carries `[Conditional("DEBUG_")]` — a symbol never
      defined — so it is compiled out of Debug and Release alike. 18.6 needs a font with `vmtx`.
- [x] **Batch 18.7 to 18.10 — done.** 22 more tests. `Table.DeepCopy` asserted for independence in
      both directions rather than for coming back non-null; `LineFormat.Serialize` pinned both ways;
      `TextMeasurement.MeasureString` with every unit asserted against the measurement in points; and
      `DocumentRenderer.RenderObject` over all three renderable kinds and its three refusals.

**Every batch has been worked; five targets inside them are deferred, each on a fixture that does not
exist yet.** None is blocked on judgement, so any of them can be picked up cold:

| Target | What it needs |
|---|---|
| 17.3 `PdfEncoders` hex strings | A PDF fixture carrying a hex string. `PdfEncoders` and `PdfStringFlags` are both `internal`, so the route in is a document, not a call. |
| 17.4, 17.5 | Paragraphs continuing across lines. Wants a batch built from multi-line MDDDL fixtures rather than one test each. |
| 18.6 vertical metrics | A font with a `vmtx` table. `Assets/Fonts` has none. |
| Debug re-measure (§3.2) | The Roslyn analysis server stopped, so a Debug run can be compared against the spec's baseline table. |

**Standing requirement, not a one-off task:**
- [ ] Any test that scans malformed input carries `[Fact(Timeout = …)]` **with** the `Task.Run`
      wrapper — xUnit honours a timeout only on an `async` test, and a lexer change hangs the host
      rather than failing it.

**Expected yield:** the previous 14 batches turned up 20 real defects, three of them severe enough
that a whole feature could not round-trip. Treat findings as the point, not the percentage.

**Effort:** ~1 day to measure and scope; the batch itself depends on what it finds.

---

## Severity 2 — Medium

### 2.1 Record the detector triage so it is not re-litigated

Two separate assessments of this repository have now spent significant effort establishing that the
largest detector clusters are correct by design. Nothing in the tree records the conclusion, so the
next health check repeats the work — or worse, "fixes" them.

- [ ] Add a short section to `docs/specs/` (or extend `crap-coverage-backlog.md`) covering:
  - The `catch (Exception ex) when (!Unrecoverable.Is(ex))` idiom, why it exists, and that
    `PdfPinata/Internal/Unrecoverable.cs` and its MigraDoc twin are the two copies of it.
  - The three sibling filter forms that are equally deliberate:
    `ImageSharpVersion.IsBindingFailure(ex)` in the ImageSharp backend, `!IsUnrecoverable(ex)` in
    `ImageRenderer`, and the explicit type-list filter in `PdfSignatureVerifier.cs:92`.
  - That the backends' `*Impl` classes are private nested types by design, so their members are not
    public API however many `public` keywords a text scan finds.

**Effort:** ~30 minutes.

### 2.2 ~~Enable `GenerateDocumentationFile`~~ — VOID, already enabled

All seven shipped packages set it in their own `.csproj`. It is deliberately placed there rather
than in `Directory.Build.props`, and each carries a comment explaining why: `Directory.Build.props`
is imported *after* the SDK has already derived `DocumentationFile` from the property, so setting it
there would not take effect.

CS1591 is already effectively a gate — the Release build runs at zero warnings, so any new
undocumented public member would show up immediately. Nothing to add.

### 2.3 ~~Close the core documentation backlog~~ — VOID

Same measurement error as 1.1. `PdfPinata` is not at 82.6% and `PinataLayout.Rendering` is not at
83.9%; both are at 100% of publicly-reachable members, compiler-verified.

The members the scan flagged in `PinataLayout.Rendering` — `ObstructedArea.X/Y/Width/Height`,
the four `RenderInfo.DocumentObject` overrides, `FormattedDocument.Equals`/`GetHashCode`,
`FootnoteRegistry`'s comparer, and three renderer constructors — are all on `internal` types.

---

## Severity 3 — Low / decisions

### 3.1 Decide on the `ImageSource` namespace mismatch

`PdfPinata/Drawing/ImageSource.cs` ships in the **PdfPinata** assembly but declares the
namespace `PinataLayout.DocumentObjectModel.Shapes`. Registering an image backend therefore needs a MigraDoc `using` from code with no other MigraDoc involvement.
`CLAUDE.md` already flags it as a trap for the eye.

- [ ] Decide: keep it (and leave the CLAUDE.md note as the mitigation), or move the type to a
      `PdfPinata.Drawing` namespace with a `[Obsolete]` type-forwarding shim at the old name.
- [ ] This is a **public API break** if moved. It is a decision for the maintainer, not a cleanup —
      do not action it without agreement.

### 3.2 Note the Debug-build file lock in developer docs

The Debug build fails on a machine running the Roslyn MCP navigator: it holds
`PinataLayout.DocumentObjectModel.Generators/bin/Debug/netstandard2.0/…Generators.dll` open, and the
copy step gives up with MSB3027/MSB3021 after ten retries. Release builds to a different path and is
unaffected. This is environmental, not a project defect.

- [ ] Add one line to `CLAUDE.md` under Commands: if the Debug build fails on a locked
      `Generators.dll`, stop the analysis server or build `-c Release`.

This is not only a build annoyance: it makes a **spec-comparable coverage measurement impossible**
while the server runs, because the baseline table in `crap-coverage-backlog.md` is Debug and the
workaround is Release. Do not try to dodge it with `-p:BaseOutputPath`: that flattens all five test
projects into one output directory, and they then lock each other's `xunit.abstractions.dll`, which
is a worse failure than the one it works around. Stop the analysis server instead.

---

## Do not do

Each of these looks like a finding and is not. Changing any of them is a regression.

| Do not | Why |
|---|---|
| Add XML docs to the backends' `*Impl` classes, `OutlineCollector`, `ImageSharpVersion`, or `OpenTypeFontMetadata` | All are `private` nested or `internal` types. Their members are not public API, CS1591 does not fire on them, and consumers cannot see them. `<inheritdoc/>` there is noise. |
| Trust a text scan for "undocumented public members" | Matching `^\s*public\s+` counts members of private and internal types. Ask the compiler instead: build with `GenerateDocumentationFile=true` and count CS1591. |
| "Fix" the 56 `catch (Exception)` sites in production | 51 carry a deliberate filter — `when (!Unrecoverable.Is(ex))`, `when (ImageSharpVersion.IsBindingFailure(ex))`, `when (!IsUnrecoverable(ex))`, or an explicit type list. The remaining 5 are log-and-rethrow (`Debug.WriteLine(ex.Message); throw;`) and a cleanup-and-rethrow, none of which swallows anything. All 56 were read. Zero are defects. |
| Add `CancellationToken` for the ~160 AP009 findings | Every one is an xUnit `[Fact]`/`[Theory]` method. Test methods take no cancellation token. |
| Fill the 7 "empty catch" AP007 sites in production | All 7 were opened and read. `PdfReader.TestPdfFile` (×2) and `ScanFileVersion` are contractually documented as *"never throws an exception"*; `Color.cs:286` is an `Enum.Parse` probe with a comment saying so; `PdfDictionary.cs:811` falls back to a default date. Each already carries the explanatory comment the detector asks for. |
| Narrow the blanket `catch` in `DdlParser.cs` | Recorded as a deliberate decision in `crap-coverage-backlog.md` finding F7 — *"a reader that carries on past a bad attribute is a defensible choice"* — and pinned by `DdlColourTests`. |
| Move `GenerateDocumentationFile` into `Directory.Build.props` | It is in each `.csproj` on purpose. `Directory.Build.props` is imported after the SDK derives `DocumentationFile`, so it would silently stop working. Each project carries a comment saying so. |
| Delete `Polyfills/` or the PolySharp reference | They look like dead code on a modern-target glance. They are the `netstandard2.1` polyfills, compiled into that leg alone: PolySharp generates the missing types and `Polyfills/` holds the missing members. |
| Drop the `netstandard2.1` target leg | It exists for Unity, whose scripting runtime cannot consume a `net8.0` assembly. Check with the maintainer first. |
| Chase the 4% structural test-coverage number | The metric is invalid for this suite — it is behaviour-driven, not one test class per production type. The real figure is 78.3% line coverage (§1.2). |
| Add `InternalsVisibleTo` to make testing easier | Explicitly forbidden. Reach `internal` targets through public API, or by reflection the way `AreaProbe`, `ParagraphIteratorProbe` and `MappedChartProbe` already do. |

---

## Verification

Run after each task. A task is done when its own check passes **and** these stay green.

```powershell
# Build — must stay 0 errors, 0 warnings
dotnet build PdfPinata.slnx -c Release

# Full suite — judge by exit code, and check the total against --list-tests
dotnet test
dotnet test --list-tests

# Vulnerable packages — must stay empty
dotnet list PdfPinata.slnx package --vulnerable --include-transitive
```

Public API documentation — the compiler is the only trustworthy measure. Must stay at zero for every
shipped package:

```powershell
foreach ($p in @('PdfPinata','PdfPinata.Skia','PdfPinata.ImageSharp','PdfPinata.Signing',
                 'PdfPinata.Charting','PinataLayout.DocumentObjectModel','PinataLayout.Rendering')) {
  $out = dotnet build "$p/$p.csproj" -c Release --no-incremental 2>&1
  "{0,-34} CS1591={1}" -f $p, ($out | Select-String 'warning CS1591').Count
}
```

### Baseline to beat

| Dimension | Grade | Measure at `1d46b2a` |
|---|---|---|
| Build Health | A | 0 errors, 0 warnings |
| Code Quality | A | 0 high-confidence findings in 125,568 production lines; all 56 mediums read and cleared |
| Architecture | A | 0 project cycles, 0 type cycles |
| Test Coverage | **B** | **78.3% lines, 73.9% branches, 3,897 tests** |
| Dead Code | A | 0 unused symbols |
| API Surface | A | 69 renderer types internal, no `InternalsVisibleTo` |
| Security | A | 0 vulnerable packages incl. transitive |
| Documentation | A | 0 CS1591 across all 7 shipped packages, compiler-verified |
