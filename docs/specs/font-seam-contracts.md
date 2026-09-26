# Spec — what the static seams promise

What stating the lifecycle of the global seams covers, and what it deliberately leaves out.
Constrained by the reasoning in CLAUDE.md that keeps capability arriving as a seam of its own.

| item | what | status |
|---|---|---|
| 1 | A way to ask whether a seam is set, without catching | proposed |
| 2 | One stated lifecycle contract across the six seams | proposed |
| 3 | The pinned test font shared by link rather than copied three times | proposed |
| 4 | Cache invalidation consistent across the seams that need it | proposed |

## Problem Statement

The core package deliberately carries no imaging or font dependency, and capability arrives through
global static seams. There are six, not five: `GlobalFontSettings.FontResolver`,
`GlyphOutlineProvider`, `TextShaper`, `FontFallback` and `DefaultFontEncoding`, plus
`ImageSource.ImageSourceImpl`. All six have the same shape — `public static T { get; set; }` — and
six different sets of facts a caller must know, none of which are in the signature:

| seam | read unset | write after use | side effect |
|---|---|---|---|
| `FontResolver` | throws | throws once a font exists | — |
| `ImageSourceImpl` | returns null, throws at first use | allowed | — |
| `GlyphOutlineProvider` | throws | allowed | — |
| `TextShaper` | null, and null is correct | allowed | — |
| `FontFallback` | falls back to the resolver | allowed | clears two caches |
| `DefaultFontEncoding` | has a default | throws once set | — |

The asymmetry is intentional and well argued — CLAUDE.md explains that `TextShaper` and
`FontFallback` treat unset as a working default so the common path stays free, and that
`FontFallback` answers the registered resolver when that resolver implements `IFontFallback` too.
None of that reasoning is expressible in `public static T { get; set; }`, so all of it lives in prose
and every consumer re-learns it from documentation.

Three consequences bite.

**The seam cannot be asked.** `src/SampleApp/Infrastructure/Backends.cs` used to find out whether a
resolver was registered by reading the property inside a `try` and catching
`InvalidOperationException`, and its own comment called this *"Ugly"*. Two seams can now be asked —
`GlobalFontSettings.IsFontResolverSet` and `IsGlyphOutlineProviderSet` — and `Backends` asks them
(#188). They are the two whose getter throws when unset; item 1's wider contract is still open.

**One configuration per assembly.** `FontResolver` refuses to change once a font has been created,
and xUnit gives no ordering guarantee that would let a fixture get there first — so four test
assemblies each carry a `[ModuleInitializer]`: `PdfPinata.Test`, `PinataLayout.Rendering.Tests`,
`PdfPinata.Charting.Tests` and `PinataLayout.DocumentObjectModel.Tests`. The last of these ships a
whole `IFontResolver` implementation, `NamedFontsOnly`, to supply a single *string* — the default
font name, which building a `Document` asks for — and throws if asked to resolve a face. No assembly
can exercise two resolver configurations, so `FontResolverParityTest` reaches the Skia and ImageSharp
resolvers by subclassing them rather than registering either. And a demo may not register a backend
at all, because the smoke-test host has already claimed the seam.

**The knowledge that decides every measurement is copied three times.** `PinnedFontResolver` serves
Liberation Sans in place of Arial because glyph widths decide where a line wraps and therefore what a
layout assertion sees. It exists at 148 lines in `PdfPinata.Test`, 61 in
`PinataLayout.Rendering.Tests` and 62 in `PdfPinata.Charting.Tests`, each with its own doc comment
explaining why this copy is smaller than the others. The repository already has the mechanism for
sharing test modules across assemblies and uses it for four content-stream readers, linked by
`<Compile Include="..\PdfPinata.Test\Helpers\...">`. The one piece of knowledge that decides every
measurement in three suites is not among them.

Cache invalidation is inconsistent for the same reason. Setting `FontFallback` calls
`FontFallbackResolution.Forget()`; setting `FontResolver` or `TextShaper` does not, and
`FontDescriptorCache` has no invalidation at all.

## Solution

Two separable moves, neither of which changes how many seams there are.

**State the contract.** Give the seams a way to be asked whether they are set, and make the lifecycle
— set-once, set-anytime, unset-is-valid — a stated property of each rather than something learned by
reading. This is about making the existing design legible, not about changing it.

**Link the pinned font module.** Share `PinnedFontResolver` and its registration the way the four
content readers are already shared, so that "which font do the tests use" has one answer.

## User Stories

1. As a developer, I want to ask whether a font resolver is registered, so that I do not have to
   catch an exception to find out.
2. As a developer, I want the same question answerable for the image source, so that composition
   roots can be written once.
3. As a developer, I want each seam to say whether it may be set again, so that I learn the rule from
   the type rather than from a doc comment.
4. As a developer, I want a seam whose unset state is valid to be distinguishable from one whose
   unset state is an error, so that I know which are optional.
5. As a developer writing a composition root, I want registering twice to be safe or clearly
   refused, so that repeated initialisation is predictable.
6. As a developer, I want setting a resolver to invalidate what depends on it, so that caches cannot
   answer for a resolver that is gone.
7. As a maintainer, I want the pinned font in one file, so that a change to the test metrics is one
   edit.
8. As a maintainer, I want the registration shared too, so that three near-identical module
   initializers become one.
9. As a maintainer, I want the DOM test project to keep needing no font file, so that its
   independence from every backend is preserved.
10. As a maintainer, I want `FontResolverParityTest` to keep comparing the two backends, so that the
    three implementations of "family name from a font file" stay in agreement.
11. As a maintainer, I want a test that changes a seam not to affect tests running beside it, so that
    the suite stays honest.
12. As a maintainer, I want the demo app to keep not registering a backend, so that the smoke test
    keeps working.
13. As a consumer on Unity, I want `netstandard2.1` to keep working, so that the target the core
    exists to keep is kept.
14. As a consumer who has written an `IFontResolver`, I want my implementation to keep compiling, so
    that this costs me nothing.
15. As a consumer, I want existing registration code to keep working unchanged.

## Implementation Decisions

**`IFontResolver` does not change.** This is the constraint the whole area is built around and it is
not reopened. CLAUDE.md sets it out: a new member breaks every consumer who has written one, and
netstandard2.1 rules out a default interface method Unity's runtime would accept. That is why
capability keeps arriving as a seam of its own, and it stays that way.

**The number of seams does not change.** Merging them would be the same mistake in a different shape.

**Asking is additive.** A way to query whether a seam is set is a new member on a static class, which
breaks nobody. This is the cheapest half of the work and could ship alone.

**The lifecycle becomes stated, not enforced differently.** `FontResolver` keeps refusing a change
after first use — that rule exists because glyph metrics are cached and changing the resolver
underneath them produces documents whose measurements disagree with their glyphs. What changes is
that the rule is discoverable before it is violated.

**Cache invalidation is a correctness question and should be settled on its own merits.**
`FontFallback` forgets its caches; the other seams do not. Either the others need it, in which case
the absence is a defect, or `FontFallback`'s call is defensive. That should be decided rather than
copied in either direction, and it is small enough to land separately from everything else here.

**`PinnedFontResolver` is linked, not packaged.** The precedent is explicit and the reasoning is
already written down in `PdfPinata.Charting.Tests.csproj`: the linked files keep their own
namespace, compiling the source needs no project reference, and this *"couples the content of the two
projects and not their builds"*. The pinned resolver should be shared the same way. The three copies
differ, so reconciling them is part of the work rather than a side effect of it — the 148-line version
is the fullest and the two 61-line versions share 47 identical lines.

**`NamedFontsOnly` stays.** The DOM test project references the DOM and nothing else — no renderer,
no backend, no Ghostscript, no font files — and that boundary is load-bearing. It resolves no face and
throws if asked to, which is the line saying a test needing a real font belongs in
`PinataLayout.Rendering.Tests`. Sharing the *pinned* resolver into it would break exactly what makes
it useful.

**Three implementations of "family name and style from a font file" are noted and not addressed.**
The core parses the `name` table in `OpenTypeFontTables`; `PdfPinata.Skia` re-parses it in
`OpenTypeFontMetadata`; `PdfPinata.ImageSharp` gets a third answer from SixLabors.
`FontResolverParityTest` holds two of the three in agreement by walking the machine's font directory
and does not reach the third. Real duplication, and a different proposal.

## Testing Decisions

**A good test here asserts on what the seam does, not on how it stores it.** The observable behaviour
is: what does reading answer, what does writing accept, and what happens to work already done.

**Modules under test.** `GlobalFontSettings` and `ImageSource` directly, and every suite indirectly,
because the pinned-font change touches what three assemblies measure against.

**The awkward part, stated plainly: these are process-global and the tests share a process.** A test
that registers a resolver changes the world for everything running beside it. That is why the current
arrangement uses module initializers, and it is why the querying half of this work is testable and
the lifecycle half is harder. Tests for set-once behaviour need either their own assembly or an
arrangement that does not disturb the registered resolver — and `PinnedFontResolver.Register`, which
adds a font rather than swapping the resolver, is the existing pattern for that.

**Prior art to follow rather than reinvent.** `src/PdfPinata.Test/TestBackendSetup.cs` is the
registration model. `PinnedFontResolver.Register` is how a test adds a font of its own without
swapping the resolver out from under everything else. `HarfBuzzShapingTests` and `FontFallbackTests`
show the established way to narrow a global seam: an adapter that declines every run but one
sentinel, because the seam has no scope. `FontResolverParityTest` shows how the backends are compared
without registering either.

**The behaviours worth pinning.** That asking answers false before registration and true after, for
each seam. That a set-once seam refuses the second write with a message naming the rule. That a seam
whose unset state is valid answers null rather than throwing. That whatever is decided about cache
invalidation holds for every seam it was decided for.

**The pinned-font change is a refactor and must move nothing.** Every layout assertion in three
suites depends on those metrics. If a golden image or a wrap position moves, the reconciliation of
the three copies changed behaviour and is wrong. This is the entire acceptance criterion for that
half.

**Judge the run by its exit code.** Two of the three affected suites rasterize, and a Ghostscript
failure reads as a crashed host with `Passed!` printed anyway. A total below what `--list-tests`
finds did not pass.

## Out of Scope

- **Changing `IFontResolver`.** Settled, for reasons that still hold.
- **Reducing the number of seams.** Same.
- **Making the seams instance-scoped rather than global.** The largest possible version of this
  change, breaking every consumer, and not proposed.
- **The three implementations of reading a font's name table.** Real; separate.
- **`SkiaFontResolver` as a 39-line pass-through** to a class in its own assembly that contains no
  Skia. It gives the backend split a name to register; whether that is worth its own type is a small
  question of its own.
- **`ScriptItemizer`** — a public module implementing UAX #24 whose only callers are tests, while the
  production path re-derives the same walk in `TextItemizer`. Noted in passing; unrelated to the
  seams.
- **Parallelising the test suites.** The global seams are only one of the reasons that is hard;
  ImageMagick driving one in-process Ghostscript is another.

## Further Notes

Two halves, and they are worth separating. The linking of `PinnedFontResolver` is a contained,
low-risk change with a clear precedent already in the build files, and it removes a three-way copy of
the one fact that decides what every layout test sees. It could be done this week and would be worth
doing even if nothing else here happens.

Stating the contract is the more valuable half and the one that touches public API, additively. It
does not fix the deeper awkwardness — a process-global mutable seam is still a process-global mutable
seam, and one configuration per assembly is still one configuration per assembly. It makes the
existing design say out loud what it currently expects to be read.

What it must not become is an argument for widening `IFontResolver`. That question is settled, the
reasoning is recorded, and this proposal is written to be compatible with it rather than to reopen
it.
