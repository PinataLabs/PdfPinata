# Spec — DOM thread safety and value descriptor follow-ups

Migrating the DOM's nullable wrapper structs to `bool?`, `int?`, `double?` and `string`
([#46](https://github.com/jafin/PdfSharpCore/pull/46)) needed a second test class that serializes
DDL. Two tests then began failing at random, alternately, one per run. Disabling parallelization
made them stop.

That was not a test problem. It was a race in `Color.ToString` that had been in the library since
the port, was reachable from any two threads that serialize a document at once, and sat behind
public API.

**All items are now closed.** Items 1, 4 and 6 were fixed here, items 2, 3, 5 and 7 by the value
model work below, and item 8 earlier in
[#46](https://github.com/jafin/PdfSharpCore/pull/46). Each section keeps the evidence it was
written from.

| item | what | severity | status |
|---|---|---|---|
| 1 | `Color.ToString` publishes its colour-name table before filling it | high | **done** |
| 2 | Every `DocumentObject` builds its metadata twice or more under load | low | **done** |
| 3 | `ValueTypeDescriptor.SetNull` casts to `INullableValue` without checking | ~~low~~ **medium** | **done** |
| 4 | `DocumentInfo` decides what to write from emptiness, not from nullness | low | **done** |
| 5 | `Meta.IsNull` decides by testing for each descriptor type by name | low | **done** |
| 6 | `CS8073` is a warning, and it is the only guard against a silent bug | medium | **done** |
| 7 | `NEnum` is the last wrapper struct standing | low | **done** |
| 8 | `Clear()` does nothing unless the object also carries a value | medium | **done** |

Items 2, 3, 5 and 7 all lived in the value descriptor and metadata layer, and were resolved by
[`compile-time-dom-value-model.md`](compile-time-dom-value-model.md), which replaced that layer's
reflection with a Roslyn source generator. Only item 7 was a phase of that work; the other three
fell out of the new design rather than being fixed one at a time, which is why they were moved there
instead of being done here first. Each section below records how.

Items 1 (`Color.ToString`), 4 (`DocumentInfo`) and 6 (`CS8073`) were fixed here as written. Item 1
was the one live bug behind public API, and fixing it is what allowed `DomSerializationCollection`
to be deleted and the DOM tests to run in parallel again.

---

## 1. `Color.ToString` publishes its colour-name table before filling it

`Color.cs:385-397`:

```csharp
public override string ToString()
{
    if (stdColors == null)
    {
        Array colorNames = Enum.GetNames(typeof(ColorName));
        Array colorValues = Enum.GetValues(typeof(ColorName));
        int count = colorNames.GetLength(0);
        stdColors = new Hashtable(count);          // <-- assigned empty
        for (int index = 0; index < count; index++)
        {
            string c = (string)colorNames.GetValue(index);
            uint d = (uint)colorValues.GetValue(index);
            if (!stdColors.ContainsKey(d))
                stdColors.Add(d, c);               // <-- then filled
        }
    }
    ...
}
static Hashtable stdColors;
```

Three separate faults, in one method:

* **The static is assigned before the table has anything in it.** A thread arriving between the
  assignment and the end of the loop sees a non-null `stdColors`, skips construction entirely, and
  reads from a table that is still filling.
* **`ContainsKey` and `Add` are not atomic with respect to each other.** Two threads inside the loop
  can both find a key absent and both add it. `Hashtable.Add` throws on a duplicate key.
* **`Hashtable` supports one writer and many readers, not many writers.** Two threads filling the
  same instance can corrupt it outright.

### Reproduction

`Colors.Black.ToString()` called from 64 threads at once, three runs:

| result | run 1 | run 2 | run 3 |
|---|---|---|---|
| `"Black"` — correct | 31 | 28 | 24 |
| `"RGB(0,0,0)"` — **silently wrong** | 11 | 18 | 18 |
| `ArgumentException: Item has already been added` | 20 | 14 | 21 |
| `""` — empty string | 2 | 4 | 1 |

```csharp
var results = new ConcurrentBag<string>();
Parallel.For(0, 64, _ =>
{
    try { results.Add(Colors.Black.ToString()); }
    catch (Exception ex) { results.Add("THREW " + ex.GetType().Name); }
});
results.Distinct().Should().ContainSingle().And.Contain("Black");
```

The `RGB(0,0,0)` row is the one that matters most. It does not throw and it is not obviously wrong —
a document simply serializes with `RGB(0,0,0)` where it should say `Black`, and the DDL still
parses. The empty string suggests the `Hashtable` itself is being corrupted, not merely read early.

### Fix

Build the table once, off to the side, and publish it complete. A static initializer is the
smallest change and removes the check entirely:

```csharp
static readonly Dictionary<uint, string> stdColors = BuildStandardColors();

static Dictionary<uint, string> BuildStandardColors()
{
    var names = Enum.GetNames(typeof(ColorName));
    var values = (ColorName[])Enum.GetValues(typeof(ColorName));
    var colors = new Dictionary<uint, string>(names.Length);
    for (int index = 0; index < names.Length; index++)
        colors.TryAdd((uint)values[index], names[index]);   // Aqua == Cyan, Fuchsia == Magenta
    return colors;
}
```

The CLR guarantees a static initializer runs once and completes before any thread reads the field,
which is the whole of the problem. `Dictionary` rather than `Hashtable` because the table is never
written after construction, and `TryAdd` says what the existing `ContainsKey` guard meant.

### Acceptance

* The reproduction above returns `"Black"` 64 times out of 64, over repeated runs.
* Keep it as a test — `Color.ToString` is public, and nothing else in the suite calls it
  concurrently.
* `DomSerializationCollection` can then be deleted and the DOM tests allowed to run in parallel
  again, which is the real confirmation.

### Done

Fixed exactly as written above. `ColorToStringTests` carries the reproduction as a regression test,
plus deterministic checks on the table's contents.

`DomSerializationCollection` is **deleted**, and the seven DOM test classes that belonged to it run
in parallel again. The suite was run three times end to end on both `net8.0` and `net10.0` after the
change: 880 tests, no failures, no flakes. That is the confirmation this item was really about — the
`Color` race was the only thing those tests were being serialized to avoid.

Two things worth knowing about the fix:

* **The regression test cannot reproduce the original race.** A static initializer runs once per
  process, so by the time the test executes the table is already built by some earlier test and the
  window no longer exists. It would have caught the defect had it run first, and it still proves
  `ToString` is safe to call concurrently, but the deterministic assertions are what actually pin
  the table. This is recorded in the test's own remarks so nobody mistakes it for stronger evidence
  than it is.
* **`TryAdd` keeps the same name the old `ContainsKey` guard kept** — the first out of
  `Enum.GetNames`, which is not the first declared. See F8 in
  [`dom-value-model-findings.md`](dom-value-model-findings.md): `Colors.Aqua.ToString()` returns
  `"Cyan"`, and did before this change too.

---

## 2. Every `DocumentObject` builds its metadata twice or more under load

Every class in the DOM caches its reflection metadata the same way — `Document.cs:374-382`, and
identically in `Style`, `Styles` and the rest:

```csharp
internal override Meta Meta
{
    get
    {
        if (meta == null)
            meta = new Meta(typeof(Document));
        return meta;
    }
}
static Meta meta;
```

This is **not** the cause of item 1, and it is worth being clear about that: `Meta`'s constructor
fills its descriptor collection before it returns (`Meta.cs:48-51`), so the instance assigned to the
static is always complete. Two threads racing here get two complete `Meta` objects and one wins.

What it costs is the work: under concurrent first use, every DOM type reflects over its own fields
and properties two or more times and throws all but one result away. It is wasteful rather than
wrong, which is why it is low severity — but it is the same shape of mistake as item 1 and should
be corrected while the area is open.

### Fix — done, in the value model spec

`Lazy<Meta>` with the default thread-safety mode, or a static initializer as in item 1. There are
67 of these to change, all mechanical.

Do not change them by hand. [`compile-time-dom-value-model.md`](compile-time-dom-value-model.md)
§4.4 deletes all 67 accessors outright: once each type's descriptor table is generated, the
generator emits the `Meta` override into the class as a `static readonly` field initializer, which
the CLR guarantees runs once and completes before any thread reads it. Same guarantee as item 1's
fix, no lock, no `Lazy<T>`, and no per-type edit to get wrong.

---

## 3. `ValueTypeDescriptor.SetNull` casts to `INullableValue` without checking

`ValueDescriptor.cs:274-292` casts unconditionally:

```csharp
public override void SetNull(DocumentObject dom)
{
    object val = FieldInfo.GetValue(dom);
    INullableValue ival = (INullableValue)val;    // InvalidCastException if it is not one
    ival.SetNull();
    ...
}
```

while `IsNull` twenty lines below tests first and answers `false` for anything that is not an
`INullableValue` — so the two disagree about what this descriptor is for.

~~Every value type that currently routes here does implement `INullableValue`, so nothing fails
today.~~ It became briefly reachable during the `NString` migration, when plain `string` members
started matching the `typeof(String)` branch, and was fixed by routing strings to
`NullableMemberDescriptor` instead. The unguarded cast is still there for the next value type that
does not implement the interface.

### Correction — it is not waiting for a next value type. It is live.

Found while building the parity harness for
[`compile-time-dom-value-model.md`](compile-time-dom-value-model.md). `FormattedText` carries `[DV]`
on nine *properties* that delegate to its `Font` (`FormattedText.cs:485-570`), and five of them are
value types that do not implement `INullableValue`:

| member | type |
|---|---|
| `Bold`, `Italic`, `Superscript`, `Subscript` | `bool` |
| `Underline` | `Underline` (enum) |

`bool` is a `ValueType`, so `CreateValueDescriptor` routes it to `ValueTypeDescriptor`, and
`SetNull` casts it to `INullableValue`. Reproduction, one line through public API:

```csharp
new FormattedText().SetNull();
// System.InvalidCastException: Unable to cast object of type 'System.Boolean'
// to type 'PinataLayout.DocumentObjectModel.Internals.INullableValue'.
```

Nothing had noticed because reading is fine — `IsNull` tests the cast before using it, and
serialization never calls `SetNull` on a whole object. Only the whole-object `SetNull` reaches it.

Pinned as-is by `ValueModelKnownDefectsTests.FormattedTextSetNullThrows`, so that whichever way it
is resolved is a deliberate choice with a failing test to mark it. Severity should be **medium**,
not low: it is a public-API crash, not a latent cast.

### Second defect, found alongside it

`DocumentObjectDescriptor.IsNull` computes `val.IsNull()` on its property branch, discards the
result and returns `true` unconditionally (`ValueDescriptor.cs`, property branch):

```csharp
if (val != null)
    val.IsNull();   // <-- result thrown away
return true;
```

`Style.Font` is the only `[DV]` property in the DOM whose type is a `DocumentObject`, so it is the
only member the branch applies to. The blast radius is small — `Meta.IsNull(dom, name)` does not
use the descriptor for `DocumentObject` members, it calls `GetValue` and asks the object itself, so
`style.IsNull("Font")` answers correctly. Only the whole-object `Meta.IsNull(dom)` sweep calls the
descriptor, and there `Style`'s separately tracked `paragraphFormat` field masks the wrong answer.

Latent rather than live, therefore, but it is a discarded result in a one-line method. Pinned at
the descriptor by `ValueModelKnownDefectsTests.ADocumentObjectPropertyDescriptorAlwaysReportsNull`,
which is the only place it is observable.

### Fix — done, in the value model spec

Match `IsNull`: test the cast, and throw something that names the value if it fails.

[`compile-time-dom-value-model.md`](compile-time-dom-value-model.md) §4.3 goes further and makes the
cast unreachable. Whether a struct member implements `INullableValue` is knowable at compile time,
so the generator emits `ValueKind.NullableValue` only when it does, and raises MDG002 when it does
not. The next value type that does not implement the interface fails the build at its `[DV]`
declaration rather than throwing `InvalidCastException` from `SetNull` at run time.

---

## 4. `DocumentInfo` decides what to write from emptiness, not from nullness

Everywhere else in the DOM, whether an attribute is written is decided by whether the value is
null. `DocumentInfo.Serialize` (`DocumentInfo.cs:131-141`) asks a different question:

```csharp
if (Title != String.Empty)
    serializer.WriteSimpleAttribute("Title", Title);
```

So `document.Info.Title = ""` is set — `IsNull("Title")` is `false` — and is not written, while
`document.FootnoteStartingNumber = 0` is set and *is* written. Round-tripping a document therefore
loses the distinction for that one class.

This predates the migration and is pinned as-is by
`DdlRoundTripTests.AnExplicitlyEmptyStringIsNotWrittenEvenThoughItIsSet`, so the behaviour is at
least recorded rather than accidental.

### Fix

Decide which rule is intended. Changing `DocumentInfo` to test nullness like everything else is the
consistent choice, and would change the DDL emitted for documents that assign `""` — worth a note
in the release notes rather than a silent change.

---

## 5. `Meta.IsNull` decides by testing for each descriptor type by name

`Meta.cs:166`:

```csharp
if (vd is NullableDescriptor || vd is ValueTypeDescriptor || vd is NullableMemberDescriptor)
    return vd.IsNull(dom);
DocumentObject docObj = (DocumentObject)vd.GetValue(dom, GV.ReadOnly);
```

The question being asked is "does this descriptor describe a simple value, or a `DocumentObject`
that the rest of a dotted name is reached through". Asking it by listing types means a new
descriptor is wrong by default: `NullableMemberDescriptor` fell through to the `DocumentObject`
branch and threw `InvalidCastException` on every string until the third name was added by hand.

### Fix — done, in the value model spec

A virtual on `ValueDescriptor` — `IsSimpleValue`, defaulting to `false` and overridden to `true` by
the three — puts the answer next to the type it describes. One call site, so this is about the next
descriptor rather than about this one.

There is no next descriptor. [`compile-time-dom-value-model.md`](compile-time-dom-value-model.md)
§4.3 collapses the hierarchy into one sealed `ValueDescriptor` carrying a `ValueKind`, so the test
becomes `vd.Kind is ValueKind.Leaf or ValueKind.NullableValue` and there is no subclass left to
forget to list. Note that item 7 removes one of the three named here — `NullableDescriptor` is
reachable only through `NEnum` — so fixing this by hand first would mean editing the list twice.

---

## 6. `CS8073` is a warning, and it is the only guard against a silent bug

Comparing a struct that defines `operator ==` against `null` compiles, lifts to `Nullable<T>`, and
is always false. During the `NString` migration a mechanical rewrite turned `style.IsNull` into
`style == null` on four `NEnum` fields. Two failed to compile. The other two compiled, were always
false, and were caught only by `CS8073`.

The same rewrite very nearly caught `LeftPosition.cs:51` and `TopPosition.cs:51`:

```csharp
this.notNull = !value.IsNull;   // value is a Unit parameter, not Point's NDouble field
```

which would have become `value != null` — always **true**, and this time in the opposite direction.

### Fix

```xml
<WarningsAsErrors>$(WarningsAsErrors);CS8073</WarningsAsErrors>
```

in `Directory.Build.props`. The codebase is clean of it today, so this costs nothing now and makes
the next mechanical edit fail loudly instead of quietly.

### Done — and the guard has a hole worth knowing about

Added, and **verified by making it fire**: a deliberate `Color c => c == null` now fails the build
with `error CS8073`. A guard nobody has watched fire is not a guard.

The same probe against `Unit` does **not** fail, and that is not a defect in the setting — it is a
property of `Unit`:

```csharp
public static implicit operator Unit(string value)   // Unit.cs:488
```

`null` converts to `string`, and `string` converts to `Unit`, so `unit == null` binds to the real
`operator ==(Unit, Unit)` against `(Unit)(string)null` rather than lifting to `Unit?`. There is
nothing constant about it, so `CS8073` correctly says nothing.

What happens instead is worse. That conversion opens with `value.Trim()`, so:

```csharp
Unit u = Unit.FromPoint(3);
bool b = u == null;      // compiles clean, throws NullReferenceException
```

So the guard does not reach the most-used struct in the DOM — 52 of the 323 `[DV]` members are
`Unit` — and the failure mode there is a bare `NullReferenceException` rather than a silent `false`.

Checked: **no such comparison exists in the codebase today.** Every `x == null` on a struct-typed
member is either an enum member (now legitimately `TEnum?`), a `Border` (a class), or an `object`
local returned by `GetValue(..., GV.GetNull)`. This is a latent trap, not a live bug.

Recorded as F9 in [`dom-value-model-findings.md`](dom-value-model-findings.md), with the suggested
fix, which is to null-guard the conversion so the failure at least names its cause.

---

## 7. `NEnum` is the last wrapper struct standing — assessed, recommend leaving it

`NBool`, `NInt`, `NDouble` and `NString` are gone. `INullableValue` remains, implemented by `NEnum`,
`Unit`, `Color`, `LeftPosition` and `TopPosition`. Four of those five are domain types that carry
their own state and should stay. `NEnum` was the open question.

**It could go.** `NullableMemberDescriptor` already handles any `Nullable<T>`, so a
`BorderStyle?` field would be read and written by the existing machinery with no new code, and
`Nullable.GetUnderlyingType` gives the descriptor the same `ValueType` that `[DV(Type = ...)]`
supplies today — making that attribute argument redundant as well.

### Assessment — concluded "migrate", and done

The `Type` is redundant, and the answer is better than the one guessed at above: the field does not
become `int?` carrying the attribute, it becomes `TEnum?` carrying nothing. `NullableMemberDescriptor`
already handles `Nullable<T>` by reading the underlying type off the member itself
(`ValueDescriptor.cs:88-90`), so neither `NEnum.type` nor `[DV(Type = ...)]` has anything left to
say. `DVAttribute.Type` goes with `NEnum`.

Two things make the migration safe, both checked rather than assumed:

* **Read semantics already agree.** `NEnum.Value` returns the zero enum value when unset
  (`NEnum.cs:68-70`); `NullableMemberDescriptor.valueWhenNull` is `Activator.CreateInstance(valueType)`
  (`NullableMemberDescriptor.cs:64`), which for an enum is also zero. Both paths produce the same
  answer today, so `TEnum?` read as `?? default` changes nothing observable.
* **`DdlParser` keeps working unchanged.** `ParseAssign` dispatches on `vd.ValueType`
  (`DdlParser.cs:2008-2037`), and for a `TEnum?` member that is the underlying enum type — the same
  value the `[DV(Type = ...)]` branch supplies now. The `typeof(Enum).IsAssignableFrom` test still
  hits `ParseEnumAssignment`.

Two things are not mechanical, and are the reason this is a phase rather than a rewrite pass:

* **The range check has nowhere to live.** `NEnum.Value` throws `ArgumentException` on a value
  `Enum.IsDefined` rejects (`NEnum.cs:84-87`); a `TEnum?` field accepts anything the cast produces.
  It moves to the public property setters via one `EnumGuard.Checked` helper.
* **`Character` must not take that guard.** `Character.Char` writes raw character values through the
  same field `SymbolName` uses and separates them by top nibble (`Character.cs:106-116`) — which is
  exactly why `NEnum` carves `SymbolName` out of its own validation. Applying the guard mechanically
  there would break every `Character` that is not a defined `SymbolName`.

The payoff is larger than removing one struct. `NullableDescriptor` is reachable **only** through
the `type == typeof(NEnum)` branch (`ValueDescriptor.cs:95-100`), so migrating `NEnum` deletes an
entire descriptor kind — five become four — which is what makes item 5's collapse worth doing.
`INullableValue` shrinks to `Unit`, `Color`, `LeftPosition` and `TopPosition`, the four that earn it.

Scheduled as phase 2 of [`compile-time-dom-value-model.md`](compile-time-dom-value-model.md) §3,
where it runs *before* the generator rather than last, because every descriptor kind it removes is
one the generator does not have to model.

---

## 8. `Border.Clear()` does nothing unless the border also carries a value

`Border.Clear()` says what it is for (`Border.cs:65-72`):

```csharp
/// Clears the Border object. Additionally 'Border = null'
/// is written to the DDL stream when serialized.
public void Clear()
{
    fClear = true;
}
```

On its own, it does not. `Borders.Serialize` decides whether to serialize each border by asking the
reflection layer (`Borders.cs:426`):

```csharp
if (!IsNull("Top"))
    top.Serialize(serializer, "Top", null);
```

`IsNull("Top")` resolves to `Border.IsNull()`, which answers from the border's value descriptors -
`Visible`, `Style`, `Width` and `Color`. `fClear` carries no `[DV]` attribute, so it is not among
them and cannot make the border look non-null. A border that has only been cleared is reported
null, skipped, and `Top = null` is never written. The `fClear` check inside `Border.Serialize`
(`Border.cs:148`) is only reached for a border that has some other value set.

### Reproduction

Before the fix, pinned by `BorderClearedTests`:

| what the caller did | `BorderCleared` | DDL contains `Top = null` |
|---|---|---|
| `Top.Clear()` | `true` | **no** |
| `Top.Width = 1; Top.Clear();` | `true` | yes |

### Fix — done

`TabStops` already had the answer, and had had it all along (`TabStops.cs:225-231`):

```csharp
public override bool IsNull()
{
    // Only non empty and not cleared tabstops (TabStops = null) are null.
    if (base.IsNull())
        return !fClearAll;
    return false;
}
```

`Border`, `Borders` and `Shading` now do the same. Putting it in `IsNull` rather than in
`Borders.Serialize` fixes it at one point instead of at every caller — five call sites gate on
`IsNull("Borders")` alone, and `Borders.Serialize` gates on `IsNull("Top")` six times more.

`SetNull` is overridden alongside it in each, because `DocumentObject.SetNull` is documented as
making `IsNull()` true afterwards, and clearing the flag is what keeps that true.

Adding `[DV]` to the flags instead would have been wrong: they are serialization instructions
rather than properties of the object, and `SetNull` would then reset them as a side effect of the
value model rather than deliberately.

**Scope note.** The item as written covered `Border` only. `Borders.ClearAll` and `Shading.Clear`
had the identical defect, take the identical fix, and are handled by the same parser branch
(`DdlParser.cs:2163-2174`), so all three were done together rather than leaving two known-broken
siblings behind.

### Verified

* `Top.Clear()` alone now writes `Top = null`, and so do `Left`, `Bottom` and `Right`.
* No empty `Top { }` block is left behind — `EndContent` rolls back a block nothing committed to.
* A cleared border, cleared borders and cleared shading each survive a DDL write, read and second
  write unchanged. The parser answers `= null` by calling `Clear()`, so the flag comes back.
* `SetNull()` clears the flag and `IsNull()` is true afterwards.

---

## Remaining order

1. **Item 1** on its own, with the concurrency test. It is a live bug behind public API and it is
   the one blocking `DomSerializationCollection` from being deleted.
2. **Item 6**, one line, guards everything after it — including the `NEnum` migration, which is the
   exact kind of mechanical rewrite that produced the `CS8073` near-misses in the first place.
3. **Item 4** on its own, because it changes emitted DDL.
Items 2, 3, 5, 7 and 8 are done.

The original plan had items 2, 3 and 5 batched together as "all mechanical", and item 7 last as an
assessment that might conclude "leave it". The assessment concluded "migrate", and that inverts the
order: item 7 removes a descriptor kind that items 3 and 5 would otherwise have to account for, and
item 2's 67 accessors are deleted rather than edited once the generator emits them. Doing the three
mechanical ones first would mean touching the same code twice and throwing the first pass away.
