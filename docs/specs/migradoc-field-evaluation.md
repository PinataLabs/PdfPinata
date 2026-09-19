# Spec — a field's value evaluable without a renderer (T18)

`FieldEvaluator` and `FieldEvaluationContext` landed in `PinataLayout.DocumentObjectModel`, exactly
where the proposal put them, next to the field types under
`Fields/`. `NumberFormatter` moved with them, git recording it as a
72%-similar rename rather than a delete-and-recreate. `ParagraphRenderer.GetFieldValue` is a caller
now, not an owner, and the `IsRenderedField` bug the proposal flagged — `docObj is DocumentInfo`
where every call site passes a paragraph leaf, so the `InfoField` that actually appears there was
never recognised — is fixed, in `FieldEvaluator.IsField`. The shape matches the proposal closely
enough that this is confirmation more than divergence; the one real surprise came from a test for
the fallback path, which turned up an unrelated `OverflowException` and got its own fix in the same
commit.

## What shipped

`FieldEvaluator` (`src/PinataLayout.DocumentObjectModel/Fields/FieldEvaluator.cs`)
is a static class with two members: `IsField(DocumentObject)`, which replaces `IsRenderedField`, and
`Evaluate(DocumentObject field, FieldEvaluationContext context)`, which replaces the three private
methods that used to do this work — `GetFieldValue`, `IsRenderedField`, and `GetDocumentInfo` — inside
`ParagraphRenderer`. `Evaluate` dispatches on the field's own type: `NumericFieldBase` subtypes go
through a private `NumberFor` that reads the right fact off the context and hands the result to
`NumberFormatter.Format`; `DateField` reads `context.PrintDate.ToString(dateField.Format)` directly,
with no fallback branch; `InfoField` reads `field.Document.Info` through a private
`DocumentInformation` helper. Anything else — a `Text`, a `BookmarkField` — throws
`ArgumentException` naming the type and pointing at `IsField`, rather than returning an empty
string.

`FieldEvaluationContext` (`FieldEvaluationContext.cs`, 51 lines) is exactly the plain data carrier the
proposal called for: `DisplayPageNumber` and `SectionNumber` as non-nullable `int`, `NumberOfPages`
and `PagesInSection` as `int?`, `PrintDate` as `DateTime`, and `ResolveBookmarkPage` as a
`Func<string, int?>` rather than exposing `FieldInfos`'s bookmark dictionary. `FieldInfos` gains
`ToEvaluationContext()` (`src/PinataLayout.Rendering/FieldInfos.cs:102-121`), which
does the translation the proposal described — including turning a count of `0` (this class's way of
saying "not known yet") into `null`, which is what lets `FieldEvaluationContext` use `int?` honestly
rather than smuggling a sentinel value across the boundary.

## What `ParagraphRenderer` still owns

`GetFieldValue` is now:

```csharp
string GetFieldValue(DocumentObject field)
{
    string value = FieldEvaluator.Evaluate(field, fieldInfos.ToEvaluationContext());
    if (value != null)
        return value;

    if (field is PageRefField pageRefField && phase == Phase.Rendering)
        return string.Format(AppResources.BookmarkNotDefined, pageRefField.Name);

    return field is NumPagesField ? "XXX" : "XX";
}
```

Five lines of logic, matching the proposal's "five-line wrapper" description almost to the letter.
`Evaluate` never answers a placeholder — the proposal's stated boundary held exactly as planned —
and the placeholder choice (`"XX"`, `"XXX"`, or the localized "bookmark not defined" message) stays
here, keyed off `phase`, which is still `ParagraphRenderer`'s own private `Formatting`/`Rendering`
enum. `GetDocumentInfo` is gone outright, as proposed; `RenderInfoField` and `FormatInfoField`, which
used to bypass `GetFieldValue` and call `GetDocumentInfo` directly, now call `GetFieldValue` like
every other field.

The dead `number <= 0` guards on `PageField` and `SectionField` are gone too — `FieldEvaluationContext`
being non-nullable there made keeping them nonsensical, since `NumberFor` for those two cases can
never produce a value `Evaluate` treats as absent. What's left of the guard logic in the wrapper
above is only for the fields that can genuinely return `null`: `PageRefField`, `NumPagesField`, and
`SectionPagesField`.

## The `InfoField` bug, fixed and pinned

`IsRenderedField`'s `docObj is DocumentInfo` check is gone; `FieldEvaluator.IsField` checks
`NumericFieldBase`, `DateField`, and `InfoField` — the field types themselves, none of which is
`DocumentInfo`. The consequence the proposal predicted is exactly what the changelog and the new
test describe: `GetOutlineTitle` (`ParagraphRenderer.cs`, now calling `FieldEvaluator.IsField` at the
line that used to call `IsRenderedField`) previously dropped an `InfoField`'s text from a heading's
PDF outline entry, so a heading reading "Part One: Annual Report" on the page appeared in the outline
as "Part One: ". `FieldRenderingTests.AHeadingBuiltFromDocumentInformationCarriesThatTextIntoTheOutline`
pins the fix by building exactly that heading and asserting the outline title equals
`"Part One: Annual Report"`. `CHANGELOG.md`'s Fixed section calls the change out explicitly, as the
proposal's User Story 4 asked for — not folded in silently.

`FieldEvaluatorTests.AFieldWithAValueIsToldApartFromOneWithout` is the narrower regression test the
proposal's Testing Decisions section called for: a theory over every type in
`MigraDoc.DocumentObjectModel.Fields` (`PageField`, `PageRefField`, `NumPagesField`, `SectionField`,
`SectionPagesField`, `DateField`, `InfoField` all `true`; `BookmarkField` and `Text` `false`), built
with `Activator.CreateInstance(type, nonPublic: true)` so no field needs a paragraph to sit in to be
asked its own type.

## `PageRefField` and `NumPagesField`/`SectionPagesField` — the trickiest fields, and how they resolve

The proposal flagged these as the fields most likely to need real design work, since their answers
depend on facts that are not always known during formatting. What shipped is a straight
context-lookup with no extra machinery:

- `NumPagesField` and `SectionPagesField` read `context.NumberOfPages` and `context.PagesInSection`
  directly — both `int?`, both `null` until `FormattedDocument.FillNumPagesInfo` /
  `FillSectionPagesInfo` backfill them once the whole document, respectively the whole section,
  finishes formatting. `Evaluate` returns `null` in that window; it never invents a value.
- `PageRefField` calls `context.ResolveBookmarkPage?.Invoke(pageRefField.Name)`, guarding both an
  absent delegate and an absent bookmark the same way — `page > 0 ? page : null` — so "no bookmark of
  that name" and "no way to resolve bookmarks at all" collapse to the same `null` answer rather than
  needing two checks. `FieldEvaluatorTests.APageRefFieldWithNoWayToResolveBookmarksAnswersThatItCannotSayYet`
  pins the null-delegate case specifically, guarding against a null-reference regression there.
  `FieldInfos.ToEvaluationContext()` supplies the delegate as `name => { int shown =
  GetShownPageNumber(name); return shown > 0 ? shown : (int?)null; }` — the same
  `GetShownPageNumber` lookup the renderer always used, now wrapped rather than replaced.

No new resolution logic was needed because none of this was actually hard once the context carried
the right facts as nullable — the proposal's caution here was warranted as a reason to think it
through, but the fields themselves turned out to want nothing beyond a lookup and a null check.

## `NumberFormatter`: what moved, and what it picked up along the way

`NumberFormatter.cs` moved to `src/PinataLayout.DocumentObjectModel/Fields/`
unchanged in its numeral logic, exactly as proposed, and is public for the first time — it always
needed to be, once `FieldEvaluator` calls it from outside `Rendering`, and `FootnoteNumbering.cs` and
the list-symbol path in `ParagraphRenderer.cs` (`symbol = NumberFormatter.Format(...)` at line 1914)
now call it across the assembly boundary rather than within it. Its two overflow-warning strings
moved from `MigraDoc.Rendering.Resources/AppResources.resx` to
`Resources/AppResources.resx`, reached through `DomSR.NumberTooLargeForRoman`
/ `NumberTooLargeForLetters` rather than the renderer's own `AppResources`, matching the proposal's
"same hand-written resource class `InvalidFieldFormat` already uses" plan precisely.

One thing the proposal did not anticipate: writing `NumberFormatterTests` — the direct coverage of
`AsRoman`'s overflow branch and `AsLetters`' wraparound the proposal asked for as User Story 8 — found
a live bug rather than just filling a gap. Both guards read `Math.Abs(number) > 32768` against a
plain `int`, and `Math.Abs(int.MinValue)` throws `OverflowException` because `int.MinValue` has no
positive counterpart. `NumberFormatter.Format(int.MinValue, "ROMAN")` threw where every other
out-of-range value fell back to plain digits. Both guards now widen to `long` before taking the
magnitude — `Math.Abs((long)number) > 32768` — fixed in a second commit on the same PR rather than
folded into the first. `NumberFormatterTests.TheMostNegativeNumberFallsBackToDigitsLikeAnyOtherPastTheCeiling`
pins `int.MinValue` under all four format strings, asserting `"-2147483648"`. Nothing downstream of
the widened guard changes, since a number that passes it is within 32768 of zero and the later
`Math.Abs(number)` calls on the (now-guaranteed-safe) `int` stay exactly as they were. This is only
reachable because `Format` is public and a caller can pass any `int` — pagination itself never
produces a number anywhere near this range.

## Testing

`FieldEvaluatorTests.cs` (`src/PinataLayout.DocumentObjectModel.Tests/`, new, 218 lines) is one function
call per case against a hand-built `FieldEvaluationContext`, no `XGraphics` and no
`PdfDocumentRenderer` — the cost reduction the whole change exists for. It covers each field type's
happy path, the `ROMAN`/`roman`/`ALPHABETIC`/`alphabetic`/plain-digit `Format` variants against a
`PageField`, `InfoField` reading `Document.Info` directly (including a name the document records
nothing under, which reads as `""`, distinct from a field detached from any document entirely, which
throws), the three `null`-answering cases (`NumPagesField`, `SectionPagesField`, and both flavors of
unresolved `PageRefField`), and `IsField` pinned against every type in the `Fields` namespace.

`NumberFormatterTests.cs` (new, 104 lines) covers roman numerals including `0` and negative numbers,
lowercase, the letter sequence including the wrap past `Z` (`27` → `"AA"`, `52` → `"ZZ"`), the
32768 ceiling falling back to digits, `int.MinValue` specifically, and an empty or unrecognized
`Format` string reading as plain digits.

`FieldRenderingTests.cs` stayed as the integration proof, as proposed, and gained exactly the two
cases the proposal's Testing Decisions section called for rather than the render-and-read tests it
already had: `AReferenceToABookmarkThatDoesNotExistSaysSoOnThePage`, asserting the rendered glyphs
carry `AppResources.BookmarkNotDefined`'s message text (matched by excluding the one space with no
counterpart in text the paragraph flattener has been over, since the message is drawn whole rather
than word-by-word), and `AHeadingBuiltFromDocumentInformationCarriesThatTextIntoTheOutline`, the
`InfoField`-in-outline regression test described above.

The demos remained the integration check the proposal named: `DemoSmokeTests.cs` fails the build if
a demo throws or its page count changes, and none did.

## What matches the proposal exactly, and the one place it didn't need to go further

Every Implementation Decision in the proposal landed as written: `FieldEvaluator` in the DOM
assembly rather than `Rendering`; `FieldEvaluationContext` shaped around exactly what `FieldInfos`
already computes; `Evaluate` returning `null` rather than a placeholder; `PageField`/`SectionField`
modeled as always-resolved with their dead guards removed; `IsField` checking field types rather than
`DocumentInfo`; `NumberFormatter` moving with its two warning strings; `InfoField` reaching its
document through `field.Document` rather than through the context; `BookmarkField` staying out of
`IsField`; bookmark navigation (`GetPhysicalPageNumber`, `GetBookmarkTop`, `RealizeHyperlink`)
staying in `Rendering` untouched; and `GetFieldValue` becoming a five-line wrapper rather than a
facade over a dispatch that was still there underneath.

The `PageRefField`/`NumPagesField`/`SectionPagesField` resolution the proposal flagged as the
trickiest part turned out not to need anything beyond the context shape already designed for it —
worth recording because it means the proposal's caution there was about getting the *context's*
shape right (nullable counts, a delegate rather than a dictionary) rather than about needing extra
runtime logic once that shape existed. The `int.MinValue` overflow is the one thing genuinely not in
the original plan: it surfaced only because User Story 8's direct `NumberFormatter` tests existed to
find it, which is itself the argument the proposal made for writing them — untested overflow and
wraparound branches, reachable only indirectly before, cannot be trusted to have been checked at all.

## Out of scope, unchanged

`FieldInfos` and the Format-then-Render pagination architecture were not touched — `FieldEvaluator`
is a pure function of a context, and nothing about when `FieldInfos` fills in its counts changed.
`BookmarkField`'s write side (`AddBookmark`, `FormatBookmarkField`) is untouched. Bookmark navigation
— placing a clickable link rectangle at a target, as opposed to reading a page number as text — stays
in `Rendering`. `PdfPinata.Charting`'s numeric axis and data-label formatting was never in scope
and remains a separate formatter for a separate kind of value. `DdlParser`'s field construction from
`\field(...)` syntax is unchanged; this was always about how a field is asked for its value, not how
one is built.
