# Findings — the font path under concurrency, issue #381

[empira/PDFsharp#381](https://github.com/empira/PDFsharp/issues/381) reports that MigraDoc's
`FontHandler.FontToXFont` memoises the last `(Font, XFont)` pair in two unsynchronised static
fields, so that under concurrent rendering a thread can be handed **an `XFont` belonging to a
different `Font`** — text measured or drawn at another document's font size. Nothing throws; the
document is produced and is silently typeset wrong.

**It is not a defect here.** This note records how that was established, and what was added to keep
it true.

| item | what | status |
|---|---|---|
| 1 | The racy memo in the DOM→`XFont` conversion | not present |
| 2 | The caches underneath it | already locked |
| 3 | A guard against the memo being introduced as an optimisation | done |

---

## Why it is absent

Two layers, and the report turns on the one in between them.

**The conversion has no cache.** `FontHandler.FontToXFont` here builds an `XFont` and returns it —
the shape this fork was ported with, before upstream added the memo in 6.2.0 and turned its fields
into `WeakReference<T>` in 6.2.1. There is no state to tear.

**The caches below it are held under a lock.** `FontFactory.ResolveTypeface`,
`FontFactory.CacheFontSource`, `FontFamilyCache`, `FontDescriptorCache` and the `GlobalFontSettings`
seams all take `Lock.EnterFontFactory` in a `try`/`finally`. That is this fork's counterpart of the
`Locks.EnterFontManagement` the report credits upstream with, and it is why the report describes the
step above it as "the one unguarded step in an otherwise guarded font path". Here there is no step
above it.

## How that was checked rather than assumed

Reading the code says the memo is absent. It does not say that nothing *else* in the path tears, and
a passing concurrency test proves very little on its own — it may simply be blind.

So the tests were written first and then run against a deliberately broken build: upstream's cache,
grafted into this fork's `FontHandler` exactly as the report quotes it, and reverted afterwards.

| | grafted | as it stands |
|---|---|---|
| wrong `XFont` in 200,000 conversions, 8 threads | 38,745 | 0 |
| distinct pages in 300 concurrent renders | 211 | 1 |
| distinct pages in 300 sequential renders | 1 | 1 |

The first row is the report's own probe, which upstream measures at 8 tears in 200,000; it is far
louder here because this fork's conversion allocates rather than hitting a warm cache, so the window
between publishing the two fields is a larger share of the call. The second is the report's
end-to-end reproduction, and it says the same thing the report does: the sequential column is the
evidence that the page *is* reproducible, so a difference in the concurrent column is disagreement
between threads and not noise.

The committed tests use a fraction of those volumes — 60,000 conversions and 120 renders — which
still fails the grafted build on every run with room to spare.

## What was added

`src/PinataLayout.Rendering.Tests/ConcurrentRenderingTests.cs`, three tests: the same document laid
out repeatedly draws the same page, laying it out on eight threads at once draws that same page, and
the font a paragraph is drawn with is the font it asks for.

They are worth their second of runtime because what they pin is easy to lose. A memo in front of
`FontToXFont` is exactly the optimisation a profiler leads someone to, it is correct on one thread,
and no assertion about a single document can see it go wrong. The third test also fixes the shape
the probe needs: **two stable `Font` instances**. A memo keyed on the object never hits for a font
allocated per call, so a test that builds a fresh `Font` each time would hide the race rather than
look for it.

## Later: the caches were not wholly locked (#329)

The second row of the table above overstated it. `FontFactory` held its caches in plain
`Dictionary` objects, and `XGlyphTypeface.GetOrCreateFrom` took the lock around each call in turn
and looked a font source up with no lock at all, while another thread could be writing the same
dictionary. The caches are `ConcurrentDictionary` now, written with `TryAdd`, and `GetOrCreateFrom`
holds the lock from its first lookup to its last. `SameFullNameFontTests` creates fonts for forty
families on eight threads at once and checks each measures with its own bytes. The same issue
stopped caching a font source under the full name inside the file, which refused a second file
sharing that name: it is cached by its bytes and by the resolver's face name alone, and
`PdfFontTable` keys a document's fonts by the bytes as well, so both files are embedded.

Holding the lock across the whole of `GetOrCreateFrom` means it is held while the resolver is asked
for a typeface and for its bytes. That is not new: `FontFactory.ResolveTypeface` already held it
across `IFontResolver.ResolveTypeface` and `GetFont`, here and upstream, and there is one font
lock, a monitor, so no second lock can be taken in the other order. A resolver that waits on
another thread which itself creates a font would deadlock now as it did before.

## Not concluded here

The tests say that two *separate* documents lay out correctly at the same time. They say nothing
about sharing one `Document`, one `PdfDocument` or one `XGraphics` between threads, which no part of
this library promises and which these do not attempt. The DOM's own races are a separate note —
[dom-thread-safety.md](dom-thread-safety.md), all of it closed.
