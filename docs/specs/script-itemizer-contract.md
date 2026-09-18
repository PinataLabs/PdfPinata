# Spec — ScriptItemizer put back where it is actually called from (T16)

`PdfPinata/Text/ScriptItemizer.cs` and `TextItemizer.cs` had the same UAX #24 sweep twice: the
public `ScriptItemizer.Itemize(string)` walked a whole string, and `TextItemizer`'s private
`ScriptsOf`/`ScriptOf` did the identical walk by hand — decoding surrogate pairs, sweeping Common and
Inherited characters forward — scoped to one bidirectional run instead. Six lines apart in the file
that ships in the pipeline, so a fix to the rule could land in one and miss the other. `ScriptItemizer
.Itemize(string, int, int)` now answers the question for a window of a string rather than the whole
of it, `TextItemizer` calls that once per bidirectional run and its own copy is gone, and
`ScriptItemizer`/`ScriptRun` are internal.

This is the rare case where what shipped is what the plan described. Every decision the proposal
argued for landed unchanged: the range overload's shape, the deletion of `ScriptsOf`/`ScriptOf`, the
demotion to internal, no `InternalsVisibleTo`, reflection for the script-only tests, a `CHANGELOG.md`
entry naming both removed members. The paragraphs below say what actually changed in the code, since
that is worth pinning down precisely rather than taking the plan's word for it, and close with the one
place the plan's own reasoning is worth re-checking now that the six days it counted have become one.

## The range overload

`ScriptItemizer.cs:63-124` is `Itemize(string text, int start, int length)`. It validates `start` and
`length` the way any bounded API in this codebase does — `ArgumentOutOfRangeException` for a `start`
outside `[0, text.Length]` or a `length` that would run past the end — and then runs exactly the loop
`Itemize(string)` used to run, retargeted from `0..text.Length` to `start..start+length`. Two things
about the retargeting are worth naming precisely rather than taking on faith:

- **The surrogate-pair check is bounded by the window, not by the string.** `char.IsHighSurrogate(
  text[idx]) && idx + 1 < end && char.IsLowSurrogate(text[idx + 1])` reads `end`, not `text.Length` —
  a window that ends between the two halves of a surrogate pair reads what is left of it as a lone
  character rather than reaching past its own bound into whatever the caller put next in the backing
  string. The comment at `ScriptItemizer.cs:82-85` says this in as many words.
- **No substring is taken.** The loop indexes `text` directly between `start` and `end`; nothing in
  the range overload calls `text.Substring`. `Itemize(string text) => Itemize(text, 0, text.Length)`
  is the one-line whole-string convenience the plan asked for, and it costs one extra bounds check
  against a range identical to the string's own — the only place this diverges from a literal
  "add a start/length parameter" reading, and it is not a divergence that matters.

`ScriptRun`'s public getters and its constructor were already `internal` before this commit — only
the enclosing `static class ScriptItemizer` and `readonly struct ScriptRun` themselves moved from
`public` to `internal` (`ScriptItemizer.cs:36`, `:132`). Nothing about the type's shape changed.

## `TextItemizer` calls it instead of keeping a copy

`TextItemizer.cs:39-58`, inside `foreach (var level in bidi.Runs())`, is now:

```csharp
foreach (var script in ScriptItemizer.Itemize(text, level.Start, level.Length))
    runs.Add(new TextRun(script.Start, script.Length, level.Level, script.Script));
```

`ScriptsOf` and `ScriptOf` — the private surrogate-decoding sweep and the "first non-Common script in
a run" lookup, `TextItemizer.cs:84-124` in the pre-T16 file — are gone outright, not kept behind a
flag or left commented. The right-to-left reversal at what is now `TextItemizer.cs:56-57` is
untouched: it still operates on the list of `TextRun`s produced for one bidi run, whichever code
produced them, exactly as the plan said it would.

## What the plan got right about publication, and the one thing worth re-checking

The plan's case for demotion turned on a checked claim: nothing outside `ScriptItemizer`'s own tests
called the public overload, `TextItemizer` never had, and the type had shipped six days earlier in
commit `c7cacc0` with no `dotnet pack`/`nuget push` step in CI to have published it anywhere. All of
that was re-verified rather than assumed for T16 too — `grep -rl "ScriptItemizer"` still finds only
`ScriptItemizer.cs`, `TextItemizer.cs`, and the test file, and `PdfPinata.csproj`'s `<Version>` had
not moved in the interim.

The plan's own "Further Notes" section named the boundary condition for its reasoning explicitly: *"If
this type had shipped a year ago in a released package with confirmed callers, the right call would be
`open-mode-enforcement.md`'s [...] It shipped six days ago."* T16 landed against that same
unpublished state — the six days had become a few more, still short of any release — so the
condition the plan used to justify demotion over `open-mode-enforcement.md`'s "fix behind the same
signature" approach still held at merge time. It is worth flagging as time-sensitive reasoning rather
than a settled fact, precisely because it is the kind of argument that stops being true the day this
package is actually packed and pushed — but it was true on the day this shipped, checked the same way
the plan checked it, not inherited unverified from six days before.

There was no evidence the implementers found something the plan missed. Everything the plan predicted
about the diff — the shape of the range overload, which private members would disappear, which class
would move from `public` to `internal`, that no `InternalsVisibleTo` would be added — is exactly what
`git show 9a0c6cd` contains.

## Testing

`PdfPinata.Test/Text/ItemizationTests.cs` went from 17 tests to 25. The eight pre-existing
script-only tests (`TextOfOneScriptIsOneRun` through `AnAstralCharacterIsOneCharacterAndNotTwo`) keep
calling the now-internal `ScriptItemizer.Itemize` — by reflection, the way
`PdfPinata.Test/IO/CharacterScanningTests.cs` already reaches `CharacterScanning`. The class-level
remark at `ItemizationTests.cs:20-28` explains why: several of those inputs (`"Hi" + Arabic`, for
one) are also mixed-direction, so routing them through `TextItemizer.Itemize` would make the
assertion pass for a bidi-run boundary rather than a script boundary, and a script-itemisation
regression could hide behind correct bidi behaviour.

`ItemizationTests.cs:352-390` is the reflection plumbing: a `ScriptRunView` record read off the
internal `ScriptRun` struct through `PropertyInfo.GetValue`, an `Invoke` helper that resolves
`Itemize` by parameter-type signature so the two overloads (`(string)` and `(string, int, int)`) can
both be reached, and `ItemizeScripts` wrappers that call each. This is more machinery than
`CharacterScanningTests` needed — that type has one method to reach, this one has two overloads to
disambiguate — but the shape is the same convention, not a new one.

Three tests exercise the range overload directly, matching the plan's testing decisions one for one:
`TheWholeStringIsJustTheWidestWindow` asserts `Itemize(text, 0, text.Length)` answers the same runs as
`Itemize(text)`; `AWindowIsItemisedOnItsOwnAndIndexedIntoTheWholeString` itemises a window of `Hebrew +
Arabic` and checks the returned `Start`/`Length` are indices into the whole string rather than the
window; `AWindowSweepsPunctuationIntoItselfAndNotIntoWhatSurroundsIt` itemises `"one " + Arabic`
starting at the space and checks the space sweeps into the Arabic — the direct, isolated version of
what `ASpaceInsideARightToLeftRunDoesNotCutIt` already proved indirectly through `TextItemizer`.

`ItemizationTests`'s existing bidi-and-script tests — `ARunIsBothOneDirectionAndOneScript`,
`ASpaceInsideARightToLeftRunDoesNotCutIt`, `TwoRightToLeftScriptsComeBackInTheOrderTheyAreDrawn`, and
the rest — are unmodified and call `TextItemizer.Itemize`, which is untouched at the API level; they
are the proof that deleting `ScriptsOf`/`ScriptOf` in favour of the range overload produced the
identical answer. `BidiConformanceTests`, `TextShapingSeamTests`, `HarfBuzzShapingTests`,
`ItemizedTextTests` and `FontFallbackTests` all sit downstream of `TextItemizer.Itemize` through
`TextShaping.ShapeText` and none of them know `ScriptItemizer` exists; all pass unmodified, which is
the evidence the refactor is invisible from outside `PdfPinata.Text`.

## What changed outside the two source files

`CHANGELOG.md`'s Removed section gets an entry naming `PdfPinata.Text.ScriptItemizer` and
`PdfPinata.Text.ScriptRun` as breaking, explaining the disagreement with the bidirectional
algorithm on mixed-direction input, and pointing at `TextItemizer.Itemize` as the replacement —
worded almost exactly as the plan's "Implementation Decisions" section proposed it, down to the
`"one من"` example. `docs/specs/text-shaping-and-bidi.md` gets a new paragraph in its shaping section
recording why the entry point went internal and that the deletion test flips as a result, plus its
test-count line for `ItemizationTests` updated from 17 to 25 with a note that the internal itemiser is
now reached by reflection.

## What was deliberately left alone

- **`Script_Extensions` (`scx`).** Still documented as future work at `ScriptItemizer.cs:28-34`,
  independent of this change.
- **Making the range overload public.** No caller has asked for script boundaries within an
  already-known bidi run from outside `PdfPinata.Text`. `docs/specs/text-shaping-and-bidi.md`
  repeats the plan's framing: the internal range overload is already the shape that request would
  need, and adding it is a small, additive change if one shows up.
- **`InternalsVisibleTo`.** Not added anywhere; confirmed by grepping every `.csproj` in the
  repository, which turns up none. The precedent this repository already has — `LineSpans.cs` and
  `VisualOrder.cs` being public because `PinataLayout.Rendering` genuinely needs them across an
  assembly boundary — does not apply here, since nothing outside `PdfPinata` itself ever touched
  `ScriptItemizer`.
- **`BidiAlgorithm` and the joining-control handling in `TextItemizer.cs`.** Untouched; this change is
  scoped to which type owns the UAX #24 sweep and who may call it directly.
- **`UnicodeScript` and `UnicodeProperties.ScriptOf`/`ScriptCode`.** Stay public, unaffected. `TextRun
  .Script` and `TextRun.ScriptCode` — the public surface `TextItemizer.Itemize` actually returns —
  depend on both, and `UnicodePropertyTests` already covers `UnicodeProperties.ScriptOf` directly.
