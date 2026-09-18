# Spec — A compile-time DOM value model

Every `DocumentObject` exposes a name-addressed view of itself:

```csharp
doc.GetValue("Sections[0].Format.Font.Bold", GV.ReadWrite);
border.SetNull("Width");
```

That view is built at runtime by reflecting over each type's fields and properties and keeping the
ones marked `[DV]` (`Meta.cs:228-251`). The DDL parser is the real consumer — `DdlParser.cs:1907`
looks up `doc.Meta[valueName]` to turn a source line like `Font.Bold = true` into an assignment, and
the descriptor it gets back decides how the right-hand side is parsed.

The marking is fine. The reflection is not. It costs a `FieldInfo.SetValue` and a boxed `object` per
DDL assignment, it forces `[DynamicallyAccessedMembers]` annotations through five files to survive
trimming, it defers every "is this member shape supported?" question to a `Debug.Assert(false)` at
`ValueDescriptor.cs:111`, and it builds each type's table behind an unsynchronised `static Meta meta`
that 67 classes each re-implement.

None of that is necessary. `DocumentObject.Meta` is `internal abstract` (`DocumentObject.cs:220`),
so **no assembly other than `PinataLayout.DocumentObjectModel` can define a `DocumentObject`**. The
type set is closed at compile time, which means the whole table can be. This spec replaces the
reflection with a Roslyn incremental source generator, and does two cleanups first that shrink what
the generator has to handle.

| phase | item | risk | status |
|---|---|---|---|
| 1 | Delete `DVAttribute.ItemType` — declared, set at 9 sites, read nowhere | none | **done** |
| 2 | Migrate `NEnum` to `TEnum?`, deleting `DVAttribute.Type` and `NullableDescriptor` | medium | **done** |
| 3 | Generate the value model; delete `Meta`'s reflection and the 67 hand-written `Meta` properties | medium | **done** |
| 4 | Re-enable `EnableTrimAnalyzer`; delete the `DynamicallyAccessedMembers` annotations | low | **done** |

All four are implemented on `refactor/compile-time-dom-value-model`. 875 tests pass on `net8.0` and
`net10.0`, the solution builds clean for `netstandard2.1` as well, and `EnableTrimAnalyzer` is on
with zero `IL2xxx`. Sections below carry a **Done** note where implementation differed from the
design, and a correction where the design was wrong.

The order matters. Each phase removes work from the next: phase 1 removes a property the generator
would otherwise have to model, phase 2 removes an entire descriptor kind, and only then is phase 3 a
mechanical translation rather than a redesign.

Decisions taken before drafting, recorded here because they set the shape of everything below:

* The three public types in `.Internals` (`Meta`, `ValueDescriptor`, `ValueDescriptorCollection`) may
  be **redesigned freely**. They are public by accident of the 2009 port, not by intent.
* The 67 DOM classes **become `partial`**, so generated code can be emitted into the declaring type.
* `NEnum`'s range validation is **preserved**, moved into the public property setters.
* Generating the 67 `Serialize` methods is **out of scope**; see §8.

---

## 1. What the model is today

Five types, all in `MigraDoc.DocumentObjectModel.Internals`:

| type | role |
|---|---|
| `DVAttribute` | marks a field or property as part of the model; carries `Type`, `RefOnly`, `ItemType` |
| `Meta` | per-DOM-type table; reflects in its constructor, serves `GetValue`/`SetValue`/`IsNull`/`SetNull` |
| `ValueDescriptorCollection` | `ArrayList` for order + `Hashtable` for **case-insensitive** name lookup |
| `ValueDescriptor` | abstract base; five concrete kinds, chosen by member type at `ValueDescriptor.cs:72-113` |
| `NEnum` | wrapper struct making an enum nullable by storing `int.MinValue` as the null sentinel |

The five descriptor kinds and what selects them:

| descriptor | selected when | example |
|---|---|---|
| `NullableMemberDescriptor` | member is `Nullable<T>` or `string` | `bool? visible` |
| `NullableDescriptor` | member is `NEnum` — **and nothing else** | `NEnum style` |
| `ValueTypeDescriptor` | member is any other value type | `Unit width`, `Color color` |
| `DocumentObjectDescriptor` | member derives from `DocumentObject` | `Font font` |
| `DocumentObjectCollectionDescriptor` | member derives from `DocumentObjectCollection` | `Sections sections` |

Two facts that constrain the redesign, both verified rather than assumed:

* `Meta.ValueDescriptors` is public but **has no consumers outside `Meta` itself**. The collection's
  ordering and its `ArrayList`/`Hashtable` internals are therefore free to change.
* Name lookup is case-insensitive (`ValueDescriptorCollection.cs:81`,
  `StringComparer.InvariantCultureIgnoreCase`). DDL relies on this. It must survive.

---

## 2. Phase 1 — delete `DVAttribute.ItemType`

`DVAttribute.ItemType` (`DVAttribute.cs:70-74`) is set at nine declaration sites — `Chart.cs:279`
and `:300`, `Cell.cs:382`, `TextFrame.cs:236`, `TextArea.cs:338`, `Footnote.cs:171`,
`HeaderFooter.cs:267`, `FormattedText.cs:592`, `Hyperlink.cs:496` — and read nowhere. A repo-wide
search for `.ItemType` returns no hits. The `// TODO: Check type in value descriptor` above it is
the unfinished intent; the check was never written.

It is not harmless. It looks like metadata the model depends on, so every reader of a
`[DV(ItemType = typeof(Series))]` declaration has to go and prove to themselves that it isn't.

### Change

Delete the field and the nine usages. `[DV(ItemType = typeof(Series))]` becomes `[DV]`.

No behaviour changes. This is the one phase with no test to write beyond "it still builds".

**Done.** All nine were `ItemType`-only — no site combined it with `RefOnly` or `Type`, so every
one collapsed to a bare `[DV]`. Build clean, 522 tests still passing.

---

## 3. Phase 2 — `NEnum` to `TEnum?`

`NBool`, `NInt`, `NDouble` and `NString` are already gone (PR #46). `NEnum` is the last, and it is
the odd one out: it exists only to make an enum nullable, which `TEnum?` does natively. It stores
the enum's `Type` next to the `int` purely so `ValueDescriptor` can recover it — information
`[DV(Type = ...)]` already carries, which is why both exist.

45 field declarations across 29 files.

### What the migration deletes

* `NEnum` itself.
* `NullableDescriptor` — reachable **only** through the `type == typeof(NEnum)` branch at
  `ValueDescriptor.cs:95-100`. With `NEnum` gone it has no users at all. Five descriptor kinds
  become four.
* `DVAttribute.Type`, and its two `[DynamicallyAccessedMembers]` annotations.

After this the attribute is `[DV]` and `[DV(RefOnly = true)]` — a near-pure marker, which is exactly
the shape phase 3 wants.

### The shape of each edit

```csharp
// before
public BorderStyle Style
{
  get => (BorderStyle)style.Value;
  set => style.Value = (int)value;
}
[DV(Type = typeof(BorderStyle))]
internal NEnum style = NEnum.NullValue(typeof(BorderStyle));

// after
public BorderStyle Style
{
  get => style ?? default;
  set => style = EnumGuard.Checked(value);
}
[DV]
internal BorderStyle? style;
```

`style ?? default` preserves today's read semantics exactly: `NEnum.Value` returns
`val != int.MinValue ? val : 0` (`NEnum.cs:68-70`), so an unset enum has always read back as the
zero value, and `NullableMemberDescriptor` independently agrees — its `valueWhenNull` is
`Activator.CreateInstance(valueType)` (`NullableMemberDescriptor.cs:64`), which for an enum is also
zero. The two paths already produce the same answer, which is what makes this migration safe.

### Preserving the range check

`NEnum.Value`'s setter throws `ArgumentException` on a value `Enum.IsDefined` rejects
(`NEnum.cs:84-87`). A `TEnum?` field cannot, so the check moves to the public setter via one helper
rather than being written out 45 times:

```csharp
namespace PinataLayout.DocumentObjectModel.Internals;

/// <summary>
/// Carries forward the range check NEnum's setter used to apply. A TEnum? field accepts any value
/// the cast produces, so the guard has to sit in the public property that writes it.
/// </summary>
internal static class EnumGuard
{
    internal static T Checked<T>(T value) where T : struct, Enum
    {
        if (!Enum.IsDefined(typeof(T), value))
            throw new ArgumentException("value");
        return value;
    }
}
```

`ArgumentException` rather than `ArgumentOutOfRangeException`, deliberately: the goal is that no
caller can observe the difference. Correcting the exception type is a separate, arguable change.

`Enum.IsDefined(typeof(T), value)` is used rather than the generic `Enum.IsDefined<T>(T)` because
the latter is .NET 5+ and the assembly still targets `netstandard2.1`. If the boxing matters it can
be `#if`'d later; it is one comparison on a property setter, so measure before bothering.

### The `Character` carve-out

`NEnum` skips validation when the type is `SymbolName` (`NEnum.cs:75-81`), and the reason is
visible at `Character.cs:106-116`: `Character.Char` writes a **raw character** through the same
field the `SymbolName` enum uses, and the getter separates them by testing the top nibble.

```csharp
public char Char
{
  get
  {
    if (((uint)symbolName.Value & 0xF0000000) == 0)
      return (char)symbolName.Value;
    return '\0';
  }
  set => symbolName.Value = (int)value;
}
```

So `Character.SymbolName` must **not** get `EnumGuard.Checked`:

```csharp
public SymbolName SymbolName
{
  get => symbolName ?? default;
  // No EnumGuard here. Char writes arbitrary character values through this same field and
  // separates them by their top nibble, so most values it holds are not defined SymbolNames.
  set => symbolName = value;
}
[DV]
internal SymbolName? symbolName;

public char Char
{
  get
  {
    uint raw = (uint)(symbolName ?? default);
    return (raw & 0xF0000000) == 0 ? (char)raw : '\0';
  }
  set => symbolName = (SymbolName)value;
}
```

This is the one place in the migration where a mechanical edit is wrong. Every other `NEnum` field
takes the guard.

### Preserving the int assignment

`NEnum` held the value as an `int` and its setter took one, so a boxed `int` handed to the model
API — `border.SetValue("Style", (int)BorderStyle.DashLargeGap)` — reached an enum member and worked.
The generated setter assigns by cast, and unboxing has to name the boxed type exactly: a boxed
`int` cast to `BorderStyle?` throws `InvalidCastException`.

That is a public API contract, so the emitter converts rather than letting it break. For an enum
member only, the setter tests for an `int` first:

```csharp
setter: static (o, v) => ((Border)o).style = (BorderStyle?)(v is int ? (BorderStyle)(int)v : v),
```

The test lives in `EnumMemberSemanticsTests`. It was `EnumValueTests` in
[#48](https://github.com/jafin/PdfSharpCore/pull/48) that caught this, and only after the rebase —
the test was written against `NEnum` while this branch was already replacing it.

### Done

45 fields across 25 files. The rewrite was scripted for the uniform forms — declaration, the
`get => (Enum)field.Value;` / `set => field.Value = (int)value;` pair, and `field.IsNull` tests
including qualified ones like `font.underline.IsNull` — with `Character`, `Style` and `Styles`
excluded and migrated by hand, since all three use these fields in ways a regex should not touch.

Two things were checked rather than assumed before relying on the rewrite:

* **No `SymbolName` member is 0.** `Character.Serialize` compared `(SymbolName)symbolName.Value`
  against `Tab`, `LineBreak`, `ParaBreak` and `Blank`, and `NEnum` read back 0 when null. Had any of
  those been 0, an unset `Character` would have matched it and the nullable form
  (`symbolName == SymbolName.Tab`, false when null) would have changed behaviour. They start at
  `0xF1000001`, so both forms agree.
* **`SymbolName` is `: uint`.** The bit tests in `Character` (`& 0xF0000000`) needed the cast to stay
  unsigned; the migrated code hoists one `uint raw = (uint)(symbolName ?? default);` and reuses it.

`NEnum.cs` is deleted, `NullableDescriptor` is deleted with it — five descriptor kinds are now four
— and `DVAttribute` is down to `RefOnly` alone. 533 tests pass, up from 522.

---

## 4. Phase 3 — the generator

### 4.1 The marker stays

There is no C# feature that says "these members form a named model" without a marker or a naming
convention, and a convention would be wrong here on the evidence already in the tree:
`Border.fClear` is an `internal` field that deliberately has **no** `[DV]`, which is precisely why
`Border.IsNull()` has to override and special-case it (`Border.cs:138-148`). `DocumentObject.parent`
needs the `RefOnly` opt-out or `IsNull()`/`SetNull()` recurse up the tree forever.

So `[DV]` survives. What changes is that it is consumed at **compile time** and never read at
runtime. While editing it: make it `sealed`, and set `Inherited = false` (attributes on a field are
not inherited anyway, but stating it stops the question).

### 4.2 Member discovery must be replicated exactly

`Meta.AddValueDescriptors` uses `type.GetRuntimeFields()` and
`type.GetProperties(Instance | Public | NonPublic)`, neither with `DeclaredOnly`. The generator has
to produce the same member set, and the rules are subtle enough to write down:

| rule | today | generator |
|---|---|---|
| instance members | included | included |
| static members | scanned, but none carry `[DV]` | **diagnostic** MDG003 rather than silent inclusion |
| inherited members | included — reflection returns inherited non-private members, and no `[DV]` member is private (all are `internal`/`protected`/`protected internal`/`public`) | walk the full base chain |
| private members | excluded by reflection | cannot occur; MDG003 if one appears |
| duplicate names | `Hashtable.Add` throws at first use | **diagnostic** MDG004 at build |
| ordering | fields then properties, order within each group unspecified by the CLR | base-first, then declaration order — deterministic |
| lookup | case-insensitive | case-insensitive |

The inheritance rule is the one that carries real risk, because it decides whether `Image` sees
`Shape`'s `[DV]` fields and whether every type sees `DocumentObject.parent`. **Do not take the table
above on trust** — §7's parity test exists to prove it, and it must be written and passing before
the reflection path is deleted.

**Confirmed.** `ValueModelParityTests` now asserts every row of that table against the live model,
339 assertions over all 67 concrete `DocumentObject` types and their 323 `[DV]` members. What it
establishes, rather than assumes:

* Inherited `[DV]` members *are* included — every type carries `DocumentObject.parent`
  (`protected internal`), and `Image` carries `Shape`'s `internal` fields. The generator must walk
  the full base chain.
* `parent` is the **only** `RefOnly` member in the entire DOM.
* No member falls through to an unsupported shape, so MDG002 starts from a clean tree.
* Name lookup is case-insensitive, and no two members in any one type collide when case is folded.

Ordering is safe to change because nothing outside `Meta` enumerates `ValueDescriptors`, and the two
things that do — `Meta.SetNull(dom)` and `Meta.IsNull(dom)` — are order-insensitive (one sets all,
the other short-circuits on the first non-null). Making it deterministic is a small improvement over
depending on unspecified reflection order.

### 4.3 The runtime types, redesigned

Four descriptor kinds are left after phase 2, and they differ only in behaviour, not in state. That
makes an abstract hierarchy the wrong shape — especially since `Meta.IsNull` currently has to ask
which one it is holding *by type test*:

```csharp
// Meta.cs:168 — item 5 of the thread-safety spec
if (vd is NullableDescriptor || vd is ValueTypeDescriptor || vd is NullableMemberDescriptor)
```

Collapse to one sealed type with a `Kind`:

```csharp
public enum ValueKind
{
    /// <summary>A member that carries its own null: Nullable&lt;T&gt; or string.</summary>
    Leaf,
    /// <summary>A struct implementing INullableValue: Unit, Color, LeftPosition, TopPosition.</summary>
    NullableValue,
    /// <summary>A value type with no null of its own - a plain bool or enum. Added while
    /// implementing; see 4.3.1.</summary>
    PlainValue,
    /// <summary>A nested DocumentObject, created on demand under GV.ReadWrite.</summary>
    DocumentObject,
    /// <summary>A DocumentObjectCollection, created on demand under GV.ReadWrite.</summary>
    Collection,
}

public sealed class ValueDescriptor
{
    public string ValueName { get; }
    public Type ValueType { get; }    // typeof(bool) for a bool?, typeof(BorderStyle) for a BorderStyle?
    public Type MemberType { get; }   // typeof(bool?)
    public ValueKind Kind { get; }
    public bool IsRefOnly { get; }

    readonly Func<DocumentObject, object> getter;
    readonly Action<DocumentObject, object> setter;
    readonly Func<object> factory;      // Kind is DocumentObject or Collection; else null
    readonly object valueWhenNull;      // Kind is Leaf; else null

    public object GetValue(DocumentObject dom, GV flags);
    public void SetValue(DocumentObject dom, object value);
    public void SetNull(DocumentObject dom);
    public bool IsNull(DocumentObject dom);
    public object CreateValue();        // factory(), not Activator
}
```

What this buys, beyond deleting the reflection:

* `Meta.IsNull`'s type test becomes `vd.Kind is ValueKind.Leaf or ValueKind.NullableValue`, closing
  item 5 of the thread-safety spec.
* `ValueTypeDescriptor.SetNull` casts to `INullableValue` without checking
  (`ValueDescriptor.cs:271`), which is item 3 of that spec. The generator knows at compile time
  whether a struct member implements `INullableValue`. ~~It emits `Kind.NullableValue` only when it
  does and MDG002 when it does not, so the unchecked cast becomes unreachable by construction.~~

  **Corrected while implementing.** That plan assumed no such member exists today. Five do —
  `FormattedText.Bold`, `Italic`, `Superscript`, `Subscript` (all `bool`) and `Underline` (an enum),
  all `[DV]` properties delegating to the `Font`. `new FormattedText().SetNull()` throws
  `InvalidCastException` today. So MDG002 cannot be an error over this tree without failing the
  build, and the design needs a fourth answer for a value type that carries no null of its own.
  See §4.3.1.
* `CreateValue` stops calling `ValueType.GetConstructor(Type.EmptyTypes).Invoke(null)`
  (`ValueDescriptor.cs:60-65`) — the last `DynamicallyAccessedMembers` annotation goes with it.
* `FieldInfo`/`PropertyInfo` leave the public surface. Nothing outside used them.

### 4.3.1 `PlainValue`, the kind the design missed

`FormattedText` carries `[DV]` on nine properties that delegate to its `Font`, and five of them —
`Bold`, `Italic`, `Superscript`, `Subscript` (all `bool`) and `Underline` (an enum) — are value
types with no null of their own. The reflection model routed every value type to
`ValueTypeDescriptor`, whose `SetNull` casts to `INullableValue`, so:

```csharp
new FormattedText().SetNull();   // InvalidCastException: Boolean -> INullableValue
```

That is live today, through public API. It went unnoticed because reading is fine — `IsNull` tests
the cast before using it — and serialization never calls `SetNull` on a whole object.

So MDG002 cannot be an error over these members, and the design needed a fourth answer.
`ValueKind.PlainValue` is it: `GetValue`, `SetValue` and `IsNull` behave exactly as before, and
`SetNull` does nothing rather than throwing. **This is the one deliberate behaviour change in the
whole migration.** Everything else preserves what the reflection did, including the answer
`DocumentObjectDescriptor.IsNull` threw away for a property — see §4.3.2.

The no-op costs nothing in practice: `FormattedText.SetNull()` also resets the `font` field these
five properties read through, so they clear anyway. Pinned by
`ValueModelKnownDefectsTests.FormattedTextSetNullNoLongerThrows`.

### 4.3.2 The one bug deliberately carried forward

`DocumentObjectDescriptor.IsNull` computed `val.IsNull()` on its property branch, discarded the
result and returned `true`. The generated descriptor does the same, with a comment saying so.

It is unobservable: `Meta.IsNull(dom, name)` does not use the descriptor for a `DocumentObject`
member — it calls `GetValue` and asks the object — and the only `[DV]` property in the DOM whose
type is a `DocumentObject` is `Style.Font`, whose wrong answer is masked by `Style`'s separately
tracked `paragraphFormat`. Fixing it is a one-line change and a separate decision; making it a side
effect of deleting the reflection would have undermined the parity harness that gates all of this.

### `Meta`

`Meta` becomes a sealed holder rather than a reflector:

```csharp
public sealed class Meta
{
    readonly ValueDescriptor[] descriptors;
    readonly Dictionary<string, ValueDescriptor> byName;

    internal Meta(ValueDescriptor[] descriptors)
    {
        this.descriptors = descriptors;
        byName = new Dictionary<string, ValueDescriptor>(descriptors.Length, StringComparer.OrdinalIgnoreCase);
        foreach (var vd in descriptors)
            byName[vd.ValueName] = vd;
    }
    // GetValue / SetValue / HasValue / IsNull / SetNull unchanged in behaviour
}
```

`OrdinalIgnoreCase` rather than `InvariantCultureIgnoreCase`. Member names are ASCII identifiers, so
the two agree, and ordinal is faster and immune to culture. Given `MigradocTurkishTest.cs` exists in
the repo, add an explicit test that name lookup still resolves under `tr-TR`.

`ValueDescriptorCollection` — `ArrayList` plus `Hashtable`, both non-generic — is deleted. A
`FrozenDictionary` would be marginally faster still, but it is .NET 8+ and the assembly targets
`netstandard2.1`; a `Dictionary` over ~10 entries is not the bottleneck. Skip the `#if`.

### 4.4 Why the classes must be `partial`

Generated code needs to read and write the marked members. Most are `internal`, so a standalone
generated class in the same assembly could reach them — but a handful are plain `protected`, which
is visible only to derived types, and a generated registry class is not derived.

Emitting into the declaring type solves that completely and pays for itself twice over:

```csharp
// <auto-generated/>
#nullable disable
namespace PinataLayout.DocumentObjectModel;

partial class Border
{
    internal static readonly Internals.Meta GeneratedMeta = new Internals.Meta(new[]
    {
        Internals.ValueDescriptor.RefOnlyObject(
            "parent", typeof(DocumentObject), typeof(DocumentObject),
            static o => ((Border)o).parent,
            static (o, v) => ((Border)o).parent = (DocumentObject)v),

        Internals.ValueDescriptor.Leaf(
            "visible", typeof(bool), typeof(bool?),
            static o => ((Border)o).visible,
            static (o, v) => ((Border)o).visible = (bool?)v,
            valueWhenNull: BoxedDefaults.False),

        Internals.ValueDescriptor.Leaf(
            "style", typeof(BorderStyle), typeof(BorderStyle?),
            static o => ((Border)o).style,
            static (o, v) => ((Border)o).style = (BorderStyle?)v,
            valueWhenNull: BoxedDefaults.Of<BorderStyle>()),

        Internals.ValueDescriptor.NullableValue(
            "width", typeof(Unit), typeof(Unit),
            static o => ((Border)o).width,
            static (o, v) => ((Border)o).width = (Unit)v),

        Internals.ValueDescriptor.NullableValue(
            "color", typeof(Color), typeof(Color),
            static o => ((Border)o).color,
            static (o, v) => ((Border)o).color = (Color)v),
    });

    internal override Internals.Meta Meta => GeneratedMeta;
}
```

That generated `Meta` override replaces this, in all 67 classes (`Border.cs:197-206`):

```csharp
internal override Meta Meta
{
    get
    {
        if (meta == null)
            meta = new Meta(typeof(Border));
        return meta;
    }
}
static Meta meta;
```

A `static readonly` field initializer is run by the CLR's type initializer, which is
thread-safe by definition. Item 2 of the thread-safety spec — "every `DocumentObject` builds its
metadata twice or more under load" — is closed as a side effect, in all 67 places at once, with no
lock and no `Lazy<T>`.

The `partial` edit itself is one keyword per class. Mechanical, and the compiler catches any miss.

### 4.5 Generator construction

A generator that returns wrong-but-cached results is worse than reflection, so the pipeline
discipline is not optional.

**Never put Roslyn types in the pipeline model.** `ISymbol`, `Compilation`, `SyntaxNode` and
`ITypeSymbol` all hold the compilation alive and compare by reference, which defeats caching and
leaks memory across IDE keystrokes. Extract to records of strings and enums at the boundary.

**`ImmutableArray<T>` is not value-equatable** — its `Equals` is reference equality on the backing
array. Wrap it:

```csharp
internal readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>> where T : IEquatable<T>
```

and use that in every model record, or caching silently never hits.

**Parser and emitter stay separate.** `Parser` turns symbols into models and is the only file that
references Roslyn types beyond the entry point; `Emitter` turns models into strings and could be
unit-tested with hand-built models and no compilation at all.

The pipeline:

```csharp
[Generator(LanguageNames.CSharp)]
public sealed class DomValueModelGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var members = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "PinataLayout.DocumentObjectModel.Internals.DVAttribute",
                predicate: static (node, _) => node is VariableDeclaratorSyntax or PropertyDeclarationSyntax,
                transform: static (ctx, _) => Parser.ParseMember(ctx))   // -> DomMemberModel, no ISymbol
            .Where(static m => m is not null);

        // Collect() so the base chain can be closed: a derived type's table includes its bases'
        // [DV] members, and those arrive as separate pipeline items.
        context.RegisterSourceOutput(members.Collect(), static (spc, all) =>
        {
            foreach (var type in Parser.GroupByTypeAndCloseInheritance(all, spc))
                spc.AddSource($"{type.HintName}.g.cs", Emitter.Emit(type));
        });
    }
}
```

`Collect()` is a deliberate choice and a real cost: it makes every edit re-run the grouping stage.
The alternative — resolving inheritance per-member — cannot work, because a member does not know
what derives from its declaring type. With 67 types the grouping is microseconds, and because the
models are value-equatable, an edit that does not change any `[DV]` member produces identical
output and the *emit* stage's cache still hits. Take the barrier; it is in the right place.

`ForAttributeWithMetadataName` requires Roslyn 4.3+. Pin `Microsoft.CodeAnalysis.CSharp` to the
**lowest** version that works (4.8.0 is a reasonable floor), not the newest — the referenced version
sets the minimum SDK that can build the repo, and there is no benefit to raising it.

### Done — and it needs two providers, not one

The pipeline above is incomplete as designed. `ForAttributeWithMetadataName` surfaces types that
carry `[DV]`, but **15 DOM types declare no `[DV]` member of their own** — the collections
(`Sections`, `Cells`, `TabStops`, `DocumentElements`, `ParagraphElements`, `SeriesCollection`,
`XValues`, …) and the parameterless field classes (`PageField`, `NumPagesField`, `SectionField`,
`SectionPagesField`, `PageBreak`, `ChartObject`). They inherit only `DocumentObject.parent`, so the
attribute-driven provider never sees them, and they came out of the first build as 15
`CS0534: does not implement inherited abstract member 'DocumentObject.Meta.get'`.

A second provider collects the `DocumentObject` class declarations themselves:

```csharp
IncrementalValuesProvider<ParsedType?> types = context.SyntaxProvider
    .CreateSyntaxProvider(
        predicate: static (node, _) => node is ClassDeclarationSyntax { BaseList: not null },
        transform: static (ctx, _) => Parser.ParseType(ctx))
    .Where(static t => t is not null);

var combined = types.Collect().Combine(members.Collect());
```

`ParsedType` also carries the base chain, which is what lets grouping close inheritance without
going back to the compilation — so the second provider pays for itself twice.

One other thing the first build caught, worth recording because it is the kind of mistake that
compiles in the generator and fails in the generated code: `DocumentObject.parent` is typed as the
**abstract** base, and the factory check only asked whether a parameterless constructor existed, not
whether the type could be instantiated. Every one of the 67 tables emitted
`factory: static () => new DocumentObject()`. The fix is one pattern: `INamedTypeSymbol { IsAbstract: false }`.

### 4.6 Diagnostics

Every one of these is a condition that today either fails at runtime or fails silently.

| id | severity | condition | replaces |
|---|---|---|---|
| MDG001 | error | `[DV]` on a member of a non-`partial` type | — (new requirement) |
| MDG002 | error | `[DV]` on a member whose type matches no `ValueKind` | `Debug.Assert(false, type.FullName)` at `ValueDescriptor.cs:111`, which is a no-op in Release and then returns `null` |
| MDG003 | error | `[DV]` on a static or private member | silent inclusion / silent exclusion |
| MDG004 | error | two `[DV]` members with names equal under `OrdinalIgnoreCase` in one type's closure | `Hashtable.Add` throwing on first use of that type |
| MDG005 | error | `[DV]` on a member of a type not derived from `DocumentObject` | silently ignored — the member simply never appears |
| MDG006 | warning | `[DV(RefOnly = true)]` on a non-reference member | nothing; `RefOnly` on a value type is meaningless |

MDG002 is the one that earns the generator on its own. Today, adding a `[DV]` to a member of an
unhandled type produces a `Debug.Assert` in Debug and a `null` descriptor in Release, which surfaces
much later as a `NullReferenceException` inside the DDL parser.

### 4.7 Project layout and packaging

```
PinataLayout.DocumentObjectModel.Generators/
  PinataLayout.DocumentObjectModel.Generators.csproj
  DomValueModelGenerator.cs      // Initialize only
  Parser.cs                      // ISymbol -> model; the only Roslyn-aware file
  Emitter.cs                     // model -> string; no Roslyn types
  Model/DomTypeModel.cs
  Model/DomMemberModel.cs
  Model/EquatableArray.cs
  Diagnostics.cs
```

```xml
<PropertyGroup>
  <TargetFramework>netstandard2.0</TargetFramework>   <!-- required for analyzers, not negotiable -->
  <IsRoslynComponent>true</IsRoslynComponent>
  <EnforceExtendedAnalyzerRules>true</EnforceExtendedAnalyzerRules>
  <IncludeBuildOutput>false</IncludeBuildOutput>
</PropertyGroup>
<ItemGroup>
  <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.8.0" PrivateAssets="all" />
</ItemGroup>
```

And in `PinataLayout.DocumentObjectModel.csproj`:

```xml
<ProjectReference Include="..\PinataLayout.DocumentObjectModel.Generators\PinataLayout.DocumentObjectModel.Generators.csproj"
                  OutputItemType="Analyzer"
                  ReferenceOutputAssembly="false"
                  PrivateAssets="all" />
```

`PrivateAssets="all"` matters: the DOM project has `GeneratePackageOnBuild=True`, and without it the
generator would appear as a package dependency. **The generator is a build-time tool for this
assembly only.** Consumers of the NuGet package neither need it nor should receive it — they cannot
define a `DocumentObject` anyway (`internal abstract Meta`), so there is nothing for it to generate
on their side.

The generator runs once per TFM leg. It is TFM-agnostic; emitted code must compile on
`netstandard2.1` as well as `net8.0`/`net10.0`, which rules out `FrozenDictionary`, collection
expressions with non-array targets, and `required` members. `file`-scoped types and `static`
lambdas are pure compiler features and are fine on all three.

To debug the emitted output, set `<EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>` on
the DOM project temporarily; files land under `obj/GeneratedFiles/`.

---

## 5. Phase 4 — turn the trim analyzer back on

`PinataLayout.DocumentObjectModel.csproj` currently says:

```xml
<IsTrimmable>true</IsTrimmable>
<EnableTrimAnalyzer>false</EnableTrimAnalyzer>
```

The assembly is *declared* trimmable while the analysis that would check the claim is switched off.
The `[DynamicallyAccessedMembers]` annotations scattered through `DVAttribute.cs` and
`ValueDescriptor.cs` are what that suppression is covering for.

Once phase 3 lands, the reflection those annotations protect no longer exists. So:

1. Delete every `DynamicallyAccessedMembers` annotation in `.Internals`.
2. Set `EnableTrimAnalyzer` to `true`.
3. Expect zero `IL2xxx` warnings. Any that remain are real and are pointing at reflection somewhere
   else in the DOM.

This is the phase that makes the whole exercise verifiable: the compiler, not a reviewer, confirms
the reflection is gone.

Worth adding at the same time: a tiny `PublishAot` console app that builds a document and writes a
PDF, run in CI. It is the only way to catch a reflection regression that the analyzer misses.

**Done.** Both `DynamicallyAccessedMembers` polyfills in
`PinataLayout.DocumentObjectModel/CompileFixes/` are deleted along with every use,
`EnableTrimAnalyzer` is `true`, and the build is clean of `IL2xxx` on all three target frameworks.

`PinataLayout.AotSmokeTest` is the smoke app. It builds a document, exercises the value model the
way the DDL parser does — dotted `GetValue`/`SetValue`, `IsNull`, `SetNull`, `HasValue`,
`CreateValue`, `GV.ReadWrite` auto-creation, case-insensitive lookup — round-trips it through DDL
twice to check the output is stable, and renders it to a PDF. 25 checks; it returns non-zero if any
fails. Deliberately written against the name-addressed paths rather than the typed API, which never
reflected and would pass either way.

Verified: `dotnet publish -c Release -r win-x64` produces a 5.1 MB native binary that passes all 25.
That is the proof the managed build cannot give — the value model needs no metadata at run time.

CI publishes and runs it on `linux-x64` as a separate step.

### 5.1 What the AOT publish found, outside the value model

**Since fixed** — see F5 in [`dom-value-model-findings.md`](dom-value-model-findings.md). Recorded
here as it stood when the smoke app first ran, because it is the only evidence that the app does
what it was built to do: it found something the trim analyzer could not.

The publish was not warning-free, and none of it was the value model:

```
IL3050: ArrayList.ToArray(Type) has RequiresDynamicCode
```

at seven sites — `PdfFlattenVisitor.cs:92` and `Paragraph.cs:608` in the DOM, and
`FormattedCell.cs:172`, `FormattedDocument.cs:254`, `FormattedHeaderFooter.cs:88`,
`FormattedTextArea.cs:134`, `FormattedTextFrame.cs:86` in `PinataLayout.Rendering`.

`ArrayList.ToArray(Type)` constructs an array type at run time, which an AOT compiler cannot always
have generated code for. The smoke test passes today because the array types involved happen to be
rooted, but that is luck, not design — this is exactly the class of failure the smoke app exists to
catch, and it is pointing at real code.

The fix is mechanical per site, e.g.

```csharp
// before
return (RenderInfo[])renderInfos.ToArray(typeof(RenderInfo));
// after
var result = new RenderInfo[renderInfos.Count];
renderInfos.CopyTo(result);
return result;
```

Left undone deliberately: it is a different subsystem from the value model, five of the seven are in
the renderer, and folding an unrelated `ArrayList` cleanup into this work would have made the parity
harness gate something it was not written to gate. Worth its own change.

It was also why the smoke app was initially kept out of `PdfPinata.slnx`: `EnableAotAnalyzer`
reports on code reached through the projects it references, so listing it would have put those seven
warnings on every developer build of everything. With them fixed, the project is in the solution -
it is the only place in the repo where the DOM and the renderer are analysed together for AOT
safety, so a new warning there now means a real hazard somewhere below it.

---

## 6. Sequencing

| PR | contains | reviewable independently |
|---|---|---|
| 1 | Delete `ItemType` (§2) | yes — pure deletion |
| 2 | `EnumGuard` + `NEnum` migration + delete `NullableDescriptor` and `DVAttribute.Type` (§3) | yes — behaviour-preserving, covered by existing tests |
| 3 | Parity test harness (§7), against the **current** reflection implementation | yes — pure addition, proves the harness works before it is load-bearing |
| 4 | Generator project + `partial` on 67 classes + generated `Meta`, reflection still present and still the live path | yes — parity test now compares the two |
| 5 | Switch to generated path, delete `Meta`'s reflection, `ValueDescriptorCollection`, and the 67 hand-written `Meta` properties | yes — the deletion PR |
| 6 | Delete DAM annotations, `EnableTrimAnalyzer=true`, AOT smoke test (§5) | yes |

PR 3 before PR 4 is the point of the whole ordering. The parity harness has to be written against
the implementation it will later be used to replace, or there is nothing to prove the new one equals
the old one.

---

## 7. Verification

### The parity test is the gate

Copy the current reflection implementation into the test project as `ReflectionMeta` — copied, not
referenced, so PR 5 can delete the original — then assert equivalence across the whole type set.
Because `DocumentObject`'s hierarchy is closed, the test can enumerate it exhaustively:

```csharp
[Fact]
public void GeneratedModelMatchesReflection()
{
    var domTypes = typeof(Document).Assembly.GetTypes()
        .Where(t => typeof(DocumentObject).IsAssignableFrom(t) && !t.IsAbstract);

    foreach (var type in domTypes)
    {
        var expected = ReflectionMeta.Build(type);          // the 2009 algorithm
        var actual   = MetaFor(type);                        // the generated table

        Assert.Equal(
            expected.Select(d => d.ValueName).OrderBy(n => n, StringComparer.Ordinal),
            actual.Select(d => d.ValueName).OrderBy(n => n, StringComparer.Ordinal));

        foreach (var e in expected)
        {
            var a = actual[e.ValueName];
            Assert.Equal(e.ValueType,  a.ValueType);
            Assert.Equal(e.MemberType, a.MemberType);
            Assert.Equal(e.IsRefOnly,  a.IsRefOnly);
            Assert.Equal(ExpectedKind(e), a.Kind);
        }
    }
}
```

Names compared as a **set**, since ordering deliberately changes (§4.2). Everything else compared
element by element. This is what proves the inheritance rules in §4.2 rather than assuming them —
if reflection includes an inherited `internal` field the generator missed, or vice versa, the sets
differ and the test says which member.

A second test asserts round-trip behaviour per descriptor: for each type, `SetValue` then `GetValue`
under each `GV` flag, and `SetNull` then `IsNull`, must agree between the two implementations.

### Existing coverage to lean on

`PdfPinata.Test/Dom/` already has the suite built for the PR #46 wrapper-struct migration, and it
covers exactly the semantics phase 2 is at risk of breaking:

| file | guards |
|---|---|
| `DdlRoundTripTests.cs` | DDL out, parse back, compare — the end-to-end check on the whole value model |
| `NullableValueSemanticsTests.cs` | what an unset member reads back as, per `GV` flag |
| `SentinelCollisionTests.cs` | that a legitimate value equal to an old sentinel is not read as null |
| `BorderClearedTests.cs` | the `fClear`-without-`[DV]` interaction |

**Correction, made while implementing.** This section originally called for a new
`SentinelCollisionTests` case on the grounds that `NEnum`'s `int.MinValue` null sentinel collided
with an enum member defined as `int.MinValue`. No DOM enum defines that value, so the collision was
never reachable and such a test would assert nothing.

`PdfPinata.Test/Dom/EnumMemberSemanticsTests.cs` covers what actually changed hands instead —
11 cases over what an unset enum reads back as, that `SetNull` still resets it, that `EnumGuard`
still rejects an out-of-range assignment, that assigning the zero value differs from leaving it
unset, DDL round-trip, and the three `Character` cases that pin the carve-out.

### Performance

`PinataLayout.Benchmarks` is the place to measure, before and after PR 5. The expected direction is
fewer allocations on DDL parse and serialize, because `FieldInfo.GetValue`/`SetValue` boxing on
every member access is replaced by a delegate call, and per-type `Meta` construction stops happening
at all. Do not put predicted numbers in the PR description — measure and report.

---

## 8. Out of scope, but worth recording

**The 67 `Serialize` methods re-list what `[DV]` already declares.** `Border.Serialize`
(`Border.cs:172-192`) writes `visible`, `style`, `width` and `color` — exactly the four `[DV]`
members, each with a hand-written null check and a comparison against a reference object:

```csharp
if (visible != null && (refBorder == null || (Visible != refBorder.Visible)))
    serializer.WriteSimpleAttribute("Visible", Visible);
```

Once the model is generated, generating this too is the obvious next move: a member added to a class
but forgotten in `Serialize` is a bug the compiler could catch instead of a round-trip test.

It is not in this spec because the irregular cases are the majority of the work, not the edge:
`Border` compares against a `refBorder`, `Chart` imposes an ordering, `Character` serializes a
symbol name or a raw character depending on a bit test, and several types emit nothing at all when a
sibling member is set. Any generator would need an escape hatch broad enough that it stops being
obviously safer than the hand-written code. That is a design problem worth its own spec, and it
should be attempted only once phases 1–4 have shown the model itself is trustworthy.

**`DdlParser.ParseAssign`'s type-test chain** (`DdlParser.cs:2008-2037`) dispatches by comparing
`vd.ValueType` against `typeof(string)`, `typeof(int)`, `typeof(Unit)` and so on at runtime. The
generator knows every one of those answers at compile time and could put a `DdlValueKind` on the
descriptor, turning the chain into a switch. Small, safe, and a natural follow-on to PR 5 — left out
here only to keep that PR to one idea.

---

## 9. Relationship to `dom-thread-safety.md`

This spec closes four of the open items in that document, three of them as side effects rather than
as targeted fixes:

| item | how |
|---|---|
| 2 — every `DocumentObject` builds its metadata twice or more under load | §4.4: `static readonly` field initializer, CLR-guaranteed, in all 67 classes |
| 3 — `ValueTypeDescriptor.SetNull` casts to `INullableValue` without checking | §4.3: MDG002 makes the unchecked cast unreachable |
| 5 — `Meta.IsNull` decides by testing for each descriptor type by name | §4.3: `ValueKind` replaces the type test |
| 7 — `NEnum` is the last wrapper struct standing | §3: migrated to `TEnum?` |

Items 1 (`Color.ToString`), 4 (`DocumentInfo` emptiness) and 6 (`CS8073`) are untouched and remain
open there.
