# Spec — What replacing the DOM value model found, and what is left

[`compile-time-dom-value-model.md`](compile-time-dom-value-model.md) replaced the DOM's
reflection-built value model with a Roslyn source generator, and migrated `NEnum` to `TEnum?` on the
way. That work is done: 875 tests pass on `net8.0` and `net10.0`, `EnableTrimAnalyzer` is on with
zero `IL2xxx`, and a natively compiled binary exercises the whole model end to end.

Doing it, and then working through the items it unblocked, turned up nine things that were not the
point of the exercise. Two were live defects, one of which crashes through public API. The rest are
latent, or are shapes in the code that only became visible once the reflection stopped hiding them.

Several were found by *verifying* rather than by reading — F8 by asserting the obvious thing about
colour aliasing and watching it fail, F9 by checking that a newly added compiler guard actually
fires. That is the pattern worth carrying forward: the findings came from probing claims, not from
inspecting code.

This document records those, and what is worth doing next. Nothing here is required by the value
model work — it is all standalone, and can be picked up in any order.

| # | finding | severity | status |
|---|---|---|---|
| F1 | `FormattedText.SetNull()` threw `InvalidCastException` | medium | **fixed** |
| F2 | `DocumentObjectDescriptor.IsNull` discards the answer it computes | low | **done** |
| F3 | `FormattedText.IsNull()` can never return true | low | **done** |
| F4 | Writing through a read-only style silently does nothing | medium | **done** |
| F5 | `ArrayList.ToArray(Type)` is AOT-unsafe, at seven sites | medium | **done** |
| F6 | Reflection's member order was never specified | — | resolved as a side effect |
| F7 | `FormattedText`'s nine delegating `[DV]` properties are the odd shape in the DOM | ~~low~~ **medium** | **done** |
| F8 | Aliased colours serialize under the name that was not declared first | low | **won't fix** |
| F9 | `unit == null` compiles clean and throws at run time; `CS8073` cannot catch it | medium | **done** |
| F10 | Serializing a style based on `DefaultParagraphFont` throws `NullReferenceException` | **high** | **done** |

---

## F1. `FormattedText.SetNull()` threw — fixed

`FormattedText` carries `[DV]` on nine properties that delegate to its `Font`. Five are value types
with no null of their own — `Bold`, `Italic`, `Superscript`, `Subscript` (`bool`) and `Underline`
(an enum). The reflection model routed every value type to `ValueTypeDescriptor`, whose `SetNull`
cast to `INullableValue` without checking:

```csharp
new FormattedText().SetNull();
// System.InvalidCastException: Unable to cast object of type 'System.Boolean'
// to type 'PinataLayout.DocumentObjectModel.Internals.INullableValue'.
```

This was item 3 of [`dom-thread-safety.md`](dom-thread-safety.md), which described the unguarded
cast as reachable only by "the next value type that does not implement the interface". It was
reachable already.

Fixed by `ValueKind.PlainValue`, whose `SetNull` does nothing. Pinned by
`ValueModelKnownDefectsTests.FormattedTextSetNullNoLongerThrows`.

**Why nothing had noticed:** reading is fine — `IsNull` tests the cast before using it — and
serialization never calls `SetNull` on a whole object. Only the whole-object sweep reaches it.

---

## F2. `DocumentObjectDescriptor.IsNull` discards the answer it computes

The property branch computed `val.IsNull()`, threw the result away, and returned `true`:

```csharp
if (val != null)
    val.IsNull();   // <-- result discarded
return true;
```

The generated descriptor carried it forward deliberately, with a comment saying so. Changing it
during the migration would have meant the parity harness was gating a behaviour change rather than
a behaviour-preserving replacement. Fixed afterwards — see **Done** below.

**Blast radius is small.** `Style.Font` is the only `[DV]` property in the DOM whose type is a
`DocumentObject`, and `Meta.IsNull(dom, name)` does not use the descriptor for a `DocumentObject`
member — it calls `GetValue` and asks the object — so `style.IsNull("Font")` answers correctly. Only
the whole-object `Meta.IsNull(dom)` sweep calls it, and there `Style`'s separately tracked
`paragraphFormat` field masks the wrong answer.

### Suggested fix

One line, in `ValueDescriptor.IsNull`'s default branch: drop the `if (!isField) return true;` guard
and let the property fall through to the same `value == null || value.IsNull()` the field branch
uses. Then update `ValueModelKnownDefectsTests.ADocumentObjectPropertyDescriptorAlwaysReportsNull`,
which currently pins the wrong answer.

Verify by checking that `Style.IsNull()` is unchanged for a style with a font — it should be,
because `paragraphFormat` already answers correctly, which is the whole reason this is latent.

### Done

Fixed exactly as described: the `isField` guard is gone and properties answer for the object they
hold, like fields always did.

Nothing a caller can see changed, which was the expectation and is now asserted rather than assumed.
`ValueModelKnownDefectsTests` grew from one test pinning the wrong answer to three: the descriptor
now agrees with the object it describes, an unassigned property still reports null, and both routes
callers actually take — `Meta.IsNull(dom, name)` and the whole-object `Meta.IsNull(dom)` — answer
exactly as before.

882 tests pass, and the whole existing suite is untouched by the change.

---

## F3. `FormattedText.IsNull()` can never return true

A consequence of F1's shape, and visible only once the model had a `Kind` to inspect. Of
`FormattedText`'s thirteen descriptors, **seven can never report null**:

| member | kind | why it is never null |
|---|---|---|
| `Bold`, `Italic`, `Underline`, `Superscript`, `Subscript` | `PlainValue` | a value type with no null; `IsNull` is hardcoded false |
| `FontName`, `Name` | `Leaf` | they read `Font.Name`, whose getter returns `name ?? ""` — never null |

`Meta.IsNull(dom)` returns false on the first non-null member, so:

```csharp
new FormattedText().IsNull();   // false, always, for every FormattedText ever constructed
```

This is pre-existing and preserved — `ValueTypeDescriptor.IsNull` returned false for a non-
`INullableValue` too, and `Font.Name` has always coalesced to `""`. It is recorded here because
`IsNull()` is public, is documented as "determines whether this instance is null (not set)", and for
this one type is a constant.

### Suggested work

Decide what `IsNull()` should mean for a type whose model contains members that cannot be unset.
Two defensible answers:

* **Exclude members that cannot be null** from the whole-object sweep, the way `RefOnly` members are
  already excluded. `PlainValue` is exactly the set that can never contribute a `true`.
* **Leave it**, and document that `FormattedText.IsNull()` is meaningless.

The first is a behaviour change for `FormattedText` and nothing else — no other DOM type has a
`PlainValue` member. Worth a test either way; there is none today.

### Done — at the source, by F7

Neither option was taken. F7 removed the nine delegating `[DV]` properties outright, and with them
went five of the seven members that could never be null. The remaining two, `FontName` and `Name`,
were among the nine. `FormattedText`'s model is now `parent`, `font`, `style`, `elements` — every one
of which can be null — so `IsNull()` means what it says for the first time:

```csharp
new FormattedText().IsNull();   // true
```

Asserted by `ValueModelKnownDefectsTests.AnEmptyFormattedTextIsNull`. **Zero `PlainValue` members
remain anywhere in the DOM**, which is asserted too, because it changes what F1's regression test is
really proving.

---

## F4. Writing through a read-only style silently does nothing

Found while writing a test, not while reading code, which is why it is worth recording.

```csharp
var document = new Document();
var builtIn = (Style)document.Styles[0];      // DefaultParagraphFont, IsReadOnly == true

builtIn.Font.Bold = true;
builtIn.Font.Bold;                             // false
```

`Style.ParagraphFormat`'s getter returns `paragraphFormat.Clone()` when `readOnly` is set, and
`Style.Font` is `ParagraphFormat.Font`. So the assignment lands on a clone that is discarded the
moment the expression ends. No exception, no diagnostic, no effect.

The intent — that built-in styles cannot be mutated — is right. Enforcing it by handing back a
throwaway and letting the caller write to it is not: the caller has no way to tell success from
silence, and the natural reading of `document.Styles[0].Font.Bold = true` is that it worked.

### Suggested fix

Throw from the setters of a read-only style rather than cloning on read, or at minimum throw from
`ParagraphFormat`'s and `Font`'s setters when their owning style is read-only. Either changes
behaviour for anyone currently relying on the silence, so it wants a release note.

Cheapest useful step is a test that pins the current behaviour, so that whichever way it is decided
is deliberate.

### Done — it throws

The clone is still handed out, because reading a built-in style is legitimate and making the getter
throw would have broken that. What changed is that the clone now carries its `Style` as its parent.
`Clone()` nulls the parent, which is precisely why a write to it could not tell it was pointless —
the object had no way back to the style that owned it.

`DocumentObject.ThrowIfReadOnly` walks the parent chain for a read-only `Style`, and every setter on
`ParagraphFormat` and `Font` calls it — 27 of them, 20 expression-bodied and 7 block-bodied. The
message names the style and says to use `Styles.AddStyle` instead.

```csharp
document.Styles[0].Font.Bold = true;
// InvalidOperationException: The style 'DefaultParagraphFont' is read-only and cannot be
// modified. It is one of the built-in styles. Add a style of your own with Styles.AddStyle...
```

**This is a breaking change** for anyone relying on the silence, and wants a release note. Nothing in
the library relied on it — the whole suite passed before the new tests were added, which is the
evidence that no internal path writes to a built-in style.

`ReadOnlyStyleTests` covers the throw, that reading still works, that a user-defined style is
unaffected, that a style *based on* the read-only one is still writable (the guard walks the object's
parent chain, not the style inheritance chain), and that serialization still works.

**Found on the way:** `Styles[Style.DefaultParagraphFontName]` returns `null`, while `Styles[0]`
returns the same style fine. Not investigated — the tests use the index and say why.

---

## F5. `ArrayList.ToArray(Type)` is AOT-unsafe, at seven sites

Found by the native publish, not by the trim analyzer. `ArrayList.ToArray(Type)` carries
`RequiresDynamicCode` because it constructs an array type at run time, which an AOT compiler cannot
always have generated code for.

| file | line |
|---|---|
| `MigraDoc.DocumentObjectModel/Paragraph.cs` | 608 |
| `MigraDoc.DocumentObjectModel.Visitors/PdfFlattenVisitor.cs` | 92 |
| `MigraDoc.Rendering/FormattedCell.cs` | 172 |
| `MigraDoc.Rendering/FormattedDocument.cs` | 254 |
| `MigraDoc.Rendering/FormattedHeaderFooter.cs` | 88 |
| `MigraDoc.Rendering/FormattedTextArea.cs` | 134 |
| `MigraDoc.Rendering/FormattedTextFrame.cs` | 86 |

`PinataLayout.AotSmokeTest` passes today because the array types involved happen to be rooted. That
is luck, not design, and it is exactly the class of failure the smoke test exists to catch.

### Suggested fix

Mechanical, per site:

```csharp
// before
return (RenderInfo[])renderInfos.ToArray(typeof(RenderInfo));
// after
var result = new RenderInfo[renderInfos.Count];
renderInfos.CopyTo(result);
return result;
```

Seven edits, no behaviour change, and it takes the AOT publish to warning-free. This is the highest
value-for-effort item in this document.

### Done

All seven fixed. Six use `CopyTo` into a statically typed array; `PdfFlattenVisitor`'s is the one
that differs — its `ArrayList` holds boxed `int`, so it unboxes one element at a time rather than
relying on `Array.Copy`'s unboxing rules, which is a detail better written down than inferred.

`FormattedDocument` also moved from `ContainsKey` followed by an indexer to a single `TryGetValue`,
since the fix needed the value in a local anyway.

The AOT publish is now **clean of both `IL2xxx` and `IL3050`**, and the native binary still passes
all 25 checks. All 880 tests pass — the rendering tests exercise every one of these paths.

**Consequence worth noting:** `PinataLayout.AotSmokeTest` is now in `PdfPinata.slnx`. It was kept
out precisely because these seven warnings would have appeared on every developer build; with them
gone, having the project in the solution is a benefit rather than a cost. It is the only place in
the repo where the DOM and the renderer are analysed together for AOT safety, so a new warning there
now means a real hazard somewhere below it. Verified the full solution build is warning-free with it
included.

**The wider version.** `ArrayList` and `Hashtable` account for ~50 uses across the DOM and the
renderer. The value model shed its two (`ValueDescriptorCollection`); the rest are untouched.
Migrating them to `List<T>` and `Dictionary<K,V>` would remove this whole class of problem, along
with the boxing and the absent type safety, but it is a large mechanical change across code with no
dedicated tests — the renderer especially. Worth doing as its own tracked piece of work, not folded
into anything.

Now specified in [`legacy-collections-migration.md`](legacy-collections-migration.md), which counts
the sites exactly — 46 `ArrayList` references across 17 files, 12 `Hashtable` across 8 — and finds
three that are public API and four places where the obvious edit changes behaviour.

---

## F6. Reflection's member order was never specified — resolved as a side effect

`Meta` built its table from `Type.GetRuntimeFields()` then `Type.GetProperties(...)`. The CLR
specifies no order within either call, so the descriptor order was whatever the runtime happened to
produce. Nothing depended on it — `Meta.SetNull(dom)` sets all, `Meta.IsNull(dom)` short-circuits on
the first non-null, and nothing outside `Meta` enumerated `ValueDescriptors` — but it was not
reproducible build to build.

The generated model orders members base-class first, then source position within each class. No
action needed; recorded because "the order changed" is a true statement about this migration and
someone will eventually notice.

---

## F7. `FormattedText`'s delegating `[DV]` properties are the odd shape in the DOM

Every other `[DV]` member in the DOM is a field holding state. `FormattedText` has nine that are
properties holding none — they read and write the corresponding member of its `Font`:

```csharp
[DV]
public bool Bold
{
    get => Font.Bold;
    set => Font.Bold = value;
}
```

They exist so that DDL can write `Bold = true` directly inside a `FormattedText` block rather than
inside a nested `Font` block. That is a real requirement and the properties are a reasonable way to
meet it — but they are the direct cause of F1 and F3, and they are why `MDG002` could not be an
error over this tree as the value model spec originally designed it.

### Evaluated, and done — it was a one-line parser change hiding a data-loss bug

The evaluation was: could the parser resolve `Bold` against the nested `Font` instead, letting the
nine properties lose their `[DV]`? The answer is yes, and finding it out turned up something worse
than the shape.

`DdlParser.ParseFont` called `ParseAttributes(formattedText)` — resolving the names against the
**FormattedText**. But `Font.Serialize` *writes* those names out of **Font's** model. The two models
had to agree, and they did not: **`Font` has `Strikethrough` and `FormattedText` never did.**

That is a live, silent data-loss bug, in both directions at once:

| what was set | DDL written | read back |
|---|---|---|
| `Strikethrough` alone | `ont[Strikethrough = Single]` | **lost** — no `FormattedText.Strikethrough` to resolve against, reported as an invalid value name and discarded |
| `Strikethrough` + `Bold` | `old` | **lost at write time** — `CheckWhatIsNotNull` never inspected `strikethrough`, so the font looked like "only Bold is set" and took the shortcut |

`FontProperties` had no `Strikethrough` member at all, which is why the second one was possible.

**The fix is what the evaluation proposed**, plus the writer half:

* `ParseFont` resolves against `formattedText.Font`. The names come from `Font`'s model, so that is
  where they should be read back into — and every delegating property becomes unnecessary.
* `FontProperties.Strikethrough` added, and `CheckWhatIsNotNull` inspects it.
* The nine `[DV]` attributes deleted.

Behaviour is otherwise unchanged, because the delegating setters wrote straight through to `Font`
anyway — resolving against `Font` directly reaches the same field. `FormattedTextFontRoundTripTests`
covers every property the writer emits, that the `old` and `\italic` shortcuts are still taken
when a single property really is the only one set, and that a styled `ont("Name")[...]` still
round-trips.

Knock-on effects, both good: **F3 is fixed at the source**, and `ValueKind.PlainValue` now has no
members anywhere in the DOM.

---

## F8. Aliased colours serialize under the name that was not declared first

Found while fixing `dom-thread-safety.md` item 1, by writing a test that asserted the obvious thing
and watching it fail.

```csharp
Colors.Aqua.ToString();       // "Cyan"
Colors.Fuchsia.ToString();    // "Magenta"
```

`ColorName` declares `Aqua = 0xFF00FFFF` at line 42 and `Cyan = 0xFF00FFFF` at line 60, so the
table can only hold one of them and the guard that builds it keeps the first one it meets. The
natural assumption is that this means the first declared. It does not: `Enum.GetNames` orders by
**value**, not by declaration, and for two names sharing a value the tie-break is unspecified. In
practice `Cyan` and `Magenta` win.

**This is not a regression.** The old `ContainsKey`/`Add` guard and the new `TryAdd` both keep the
first entry in the same iteration order, so the fix is faithful. It is recorded because a document
that assigns `Colors.Aqua` serializes as `Cyan`, round-trips back as `Cyan`, and nothing in the
library says so.

### Decided: won't fix

Called as not mattering. The colours are genuinely equal, the DDL is correct, and a round trip is
lossless in value if not in spelling. Recorded here so the next person to notice does not have to
re-derive it.

The original write-up follows.

### Suggested work

Decide whether it matters. Two positions, both defensible:

* **It does not** — the colours are genuinely equal, the DDL is correct, and a round trip is
  lossless in value if not in spelling. Document it and move on.
* **It does** — a caller who writes `Colors.Aqua` and reads back `Cyan` has been surprised, and the
  choice is currently made by an unspecified sort. If so, drive the table from an explicit ordered
  list rather than from `Enum.GetNames`, so the winner is chosen rather than inherited.

`ColorToStringTests.AliasedColoursAgreeOnOneName` asserts only that both alias to the *same* name
and that it is one of the pair, deliberately, so that this does not become a test of the runtime's
enum ordering.

---

## F9. `unit == null` compiles clean and throws at run time

Found while verifying that item 6 of [`dom-thread-safety.md`](dom-thread-safety.md) — turning
`CS8073` into an error — actually guards anything.

It does, for most structs: a deliberate `Color c => c == null` now fails the build. It does **not**
for `Unit`, and `Unit` is the most-used struct in the DOM — 52 of the 323 `[DV]` members.

`Unit` declares `implicit operator Unit(string)` (`Unit.cs:488`). `null` converts to `string`, and
`string` converts to `Unit`, so `unit == null` binds to the real `operator ==(Unit, Unit)` against
`(Unit)(string)null` rather than lifting to `Unit?`. Nothing about it is constant, so `CS8073`
correctly stays silent. And that conversion opens with `value.Trim()`:

```csharp
Unit u = Unit.FromPoint(3);
bool b = u == null;      // compiles with no warning, throws NullReferenceException
```

So the guard item 6 adds does not reach the type it would most need to, and the failure mode there
is a bare `NullReferenceException` with no indication of why.

**Not a live bug.** Every `x == null` on a struct-typed member in the tree today is an enum member
(legitimately `TEnum?` since the migration), a `Border` (a class), or an `object` local returned by
`GetValue(..., GV.GetNull)`. Checked across the DOM and the renderer. This is a trap waiting for the
next mechanical `IsNull` → `== null` rewrite, which is exactly the edit item 6 exists to guard and
exactly the edit this stack of work has done a lot of.

### Suggested fix

Null-guard the conversion, so the failure names its cause:

```csharp
public static implicit operator Unit(string value)
{
    if (value == null)
        throw new ArgumentNullException(nameof(value));
    ...
}
```

That does not make `unit == null` an error — nothing can, short of removing the string conversion —
but it turns an unexplained `NullReferenceException` into something that says what happened. Whether
to go further and drop the implicit `string` conversion for an explicit `Unit.Parse` is a public API
decision worth its own discussion; the conversion is convenient and widely used.

### Done

Guarded, and the exception message names the mistake rather than just the symptom: it says the
comparison is meaningless, why it compiled, and to test `IsEmpty` instead. The `<exception>` doc on
the conversion explains the `CS8073` interaction, so the next person to wonder why the guard did not
catch it has the answer in place.

`UnitNullConversionTests` pins both the guard and that real string conversions still work. No
existing test changed behaviour, which confirms nothing in the tree was converting null to a `Unit`.

**Still not an error at compile time**, and cannot be while the implicit `string` conversion exists.
This downgrades a silent trap to a loud one; it does not remove it. Dropping the conversion in
favour of an explicit `Unit.Parse` would, and remains a public API decision for someone else.

---

## F10. Serializing a style based on `DefaultParagraphFont` throws

Found by chasing a one-line aside from F4's tests: `Styles[Style.DefaultParagraphFontName]` returned
`null` while `Styles[0]` returned that very style.

It was deliberate. `Styles[string]` started its search at index **1**:

```csharp
int count = Count;
// index starts from 1; DefaultParagraphFont cannot be modified.
for (int index = 1; index < count; ++index)
```

Protection by being unreachable — and both `GetIndex` and the integer indexer see straight through
it, because they start at 0. So the collection disagreed with itself about whether a style existed.

`Style.Serialize` then looked the base style up **both ways**:

```csharp
Style refStyle0 = Document.Styles[Document.Styles.GetIndex(baseStyle ?? "")];  // found, never read
refStyle        = Document.Styles[baseStyle ?? ""];                            // null
refFormat = refStyle != null ? refStyle.ParagraphFormat : null;                // guarded
refFont   = refStyle.Font;                                                     // not guarded
```

`refStyle0` is assigned and never read — someone half-fixing this and stopping. `refFont` is
assigned in all five branches and read in none. So the line that crashed was populating a variable
nothing used:

```csharp
var document = new Document();
document.Styles.AddStyle("Derived", Style.DefaultParagraphFontName);
DdlWriter.WriteToString(document);   // NullReferenceException
```

`AddStyle` validates the base style with `GetIndex`, which finds it — so the library lets you build
exactly the document it then cannot serialize. **High severity: a crash through public API on a
supported operation.**

### Done

Three things, and the ordering matters:

* `Styles[string]` starts at 0, so it agrees with `GetIndex`.
* `refStyle0` and `refFont` deleted — both unread, in every branch.
* The skip's stated purpose is now met properly rather than abandoned: F4 made every setter on a
  read-only style's `ParagraphFormat` and `Font` throw. Being findable and being writable were
  conflated, which is how the skip came to exist; they are separate now, and
  `StyleLookupTests.FindingItByNameDoesNotMakeItWritable` pins that.

This is why F4 had to land first. Removing the skip on its own would have made a genuinely
modifiable style reachable by name.

`StyleLookupTests` also asserts that the two lookups agree for **every** style in the collection,
not just this one.

---

## Also closed in `dom-thread-safety.md`

Three items from that document were not touched by the value model work itself:

| item | what | severity | status |
|---|---|---|---|
| 1 | `Color.ToString` publishes its colour-name table before filling it | high | **done** |
| 4 | `DocumentInfo` decides what to write from emptiness, not from nullness | low | **done** |
| 6 | `CS8073` is a warning, and it is the only guard against a silent bug | medium | **done** |

**Item 1 is done.** The table is now a `static readonly Dictionary` built by a static initializer,
`DomSerializationCollection` is deleted, and the seven DOM test classes it serialized run in
parallel again — 880 tests, three consecutive clean runs on both target frameworks. That was the
real confirmation the item asked for: the `Color` race was the only thing those tests were being
serialized to avoid. It also produced F8 above.

**Item 6 is done**, and verified by making it fire rather than by assuming it would. It also
produced F9 above: the guard cannot reach `Unit`, which is the struct it would most need to.

**Item 4 is done too**, and was done on `master` in [#48](https://github.com/jafin/PdfSharpCore/pull/48)
rather than here — this stack branched before that landed, which is why earlier drafts of this
document listed it as open. `DocumentInfo.Serialize` now decides from nullness like the rest of the
DOM, so a document that assigns `""` to `Title`, `Subject`, `Author` or `Keywords` gains a
`Title = ""` line it did not emit before. **That document is closed; every item in it is fixed.**

---

## Gaps in what was built

Honest list of what the value model work left thin.

**`Emitter` still has no isolated unit tests.** It was deliberately written free of Roslyn types so
it could be exercised with hand-built `DomTypeModel`s and no compilation, and that was never done.
The generator tests above cover its output through a real compilation, and the 339 parity assertions
cover it against the live model, so this is now a speed and precision gap rather than a coverage one:
a snapshot test over `Emitter.Emit` would catch a formatting or escaping regression faster and point
at it more directly. `Verify` is already used elsewhere in the repo.

**~~The diagnostics are untested.~~ Done.** `PinataLayout.DocumentObjectModel.Generators.Tests`
now drives the generator through `CSharpGeneratorDriver` and asserts that each of MDG001–MDG006
fires, that a valid type produces none, and that the emitted source actually binds rather than
merely being produced. 12 tests.

Two things it settled that were previously only reasoned about:

* Every `ValueKind` is classified as the model expects — including `PlainValue` for a plain `bool`
  and `NullableValue` for a struct implementing `INullableValue`.
* A type declaring no `[DV]` member of its own still gets a table, and an abstract type gets none
  while its members are still inherited by concrete types below it. Those are the two rules the
  first build of the generator got wrong.

Worth knowing about the harness: the test snippets compile against **stand-ins** for the DOM types
declared in the test source, not against the real assembly. That is not a shortcut —
`DocumentObject.Meta` is `internal abstract`, so a test compilation referencing the real assembly
could not declare a `DocumentObject` at all, because the generated `internal override` cannot
override an internal member from another assembly. The same property that makes the whole
compile-time model possible makes referencing it from a test compilation impossible.

**The AOT smoke test's Linux leg is unverified.** It was written and confirmed on `win-x64` — a
5.1 MB native binary passing all 25 checks. The CI step publishes `linux-x64` with `clang` and
`zlib1g-dev`, and has not run yet. If it fails, it will fail on toolchain setup rather than on
anything in this work.

**Incremental caching is unmeasured.** The generator is built to the usual discipline — value-
equatable models, `EquatableArray`, no Roslyn types in the pipeline — but nobody has attached a
profiler or checked that an edit to an unrelated file produces a cache hit. The `Collect()` barrier
is a deliberate cost and its size is unknown.

---

## Suggested order

1. ~~**`dom-thread-safety.md` item 1** — `Color.ToString`.~~ **Done.**
2. ~~**F5, the seven `ToArray(Type)` sites**~~ **Done.** — mechanical, no behaviour change, takes the AOT publish
   to warning-free.
3. ~~**`dom-thread-safety.md` item 6** — `CS8073` as an error.~~ **Done.**
4. ~~**F2** — one line plus a test update.~~ **Done.**
5. ~~**Generator diagnostic tests**~~ **Done.**
6. ~~**F7's evaluation**, then **F3** and **F4**~~ **All done.**
7. ~~**`dom-thread-safety.md` item 4**~~ **Done**, on `master` rather than here — see above.
8. **MDG007**, the first step of [`generated-serialization.md`](generated-serialization.md): a
   diagnostic for a `[DV]` member that no `Serialize` method writes. It deletes nothing, depends on
   nothing, and is the only item in either new spec that finds bugs rather than removing code.
9. **The `ArrayList`/`Hashtable` migration**, specified in
   [`legacy-collections-migration.md`](legacy-collections-migration.md) — six pieces, sequenced so
   the untested renderer comes last.
10. **The rest of `generated-serialization.md`**, which needs the byte-comparison harness in place
    before any type is switched over.

Both of the remaining items were promised a spec rather than a line on a list, and now have one.
The serialization spec's answer to §8 of
[`compile-time-dom-value-model.md`](compile-time-dom-value-model.md) is *don't generate the method* —
measured across all 74 `Serialize` methods, only 15 are pure value-model output, and three are so
bespoke that generating them would mean writing a second DDL writer. What it generates instead is
the flat run of attribute writes inside them, which half the methods are within two statements of
being.

Still not scheduled: isolated snapshot tests for `Emitter`, and measuring the generator's incremental
caching. Both are recorded under **Gaps in what was built** above.
