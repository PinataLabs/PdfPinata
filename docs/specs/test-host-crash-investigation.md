# The test host crashed intermittently at the end of a full run

Found and fixed. What follows is what the fault turned out to be, how it was pinned down, and what
was ruled out on the way — the ruling out is most of the value, because three of the four obvious
suspects were wrong.

## The symptom

A full `dotnet test` run ended with

```text
The active test run was aborted. Reason: Test host process crashed

Passed!  - Failed:     0, Passed:  1474, Skipped:     1, Total:  1475, ...
Test Run Aborted.
```

and exit code 1. No test failed, the counts came up short of the total, and the time to the crash
looked like it varied. Roughly a third to a half of full runs died.

## What it was

`Assets/FamilyTree.pdf` is a single page 58 inches wide by 23 — a poster, not a document. Every
other page the tests rasterize is A4. At the 300 dpi `PdfHelper.Rasterize` asked for, that one page
came to **122 megapixels**, took six seconds to draw, and took the process to about **940MB for the
one page**. `PageResizeRenderingTests` drew it three times, `PruneUnusedResourcesRenderingTests`
twice more, and `PdfHelper.Diff` then held two of them at once to compare them.

Ghostscript draws the page, and on Windows it is loaded into the test host rather than run as a
command: `Ghostscript.NativeAssets` ships `gsdll64.dll` and no executable at all. When it cannot
recover from something it does not report an error — it ends the process. So a page it could not
draw took the test host with it: **exit status 1, no dump, nothing on stderr and no failing test.**

On its own the big page draws fine — ten rasterizations in a row in a fresh process all succeed. It
only failed when that 940MB spike landed on a host already carrying the rest of the suite, which is
why it was intermittent and why it always struck at the end.

## The fix

`PdfHelper.Rasterize` now caps a page at 16 megapixels and drops the resolution of a document whose
largest page would exceed it, leaving every other document at 300 dpi exactly. FamilyTree draws at
108 dpi and 15.8 megapixels; every other page in the suite is unchanged, bit for bit, so the
reference images and the tolerances they are compared under all still hold.

The two tests that use the big page compare renderings of it **with each other** — before and after
a resize, before and after a prune — rather than against a checked-in image, so what resolution
they run at was never part of what they assert. They still exercise the same document through the
same code. The rasterizing tests went from 2m19s to 1m17s as a side effect.

## What it was not

Each of these was measured, not argued about:

- **Not memory exhaustion of the machine.** Peak test host working set is 1.09GB, on a 64GB machine
  with 26GB free. The `RADAR_PRE_LEAK_64` event in the Windows event log for `testhost.exe` is a
  red herring.
- **Not a crash at all.** The host exits with status **1**. An access violation would be
  `0xC0000005`, a stack overflow `0xC00000FD`, an unhandled managed exception `0xE0434352` and a
  dump. `DOTNET_DbgEnableMiniDump=1` with a full dump type produced nothing, because there is
  nothing to dump: something called `exit(1)`.
- **Not concurrency around the one in-process Ghostscript.** `RasterizingCollection` does hold: the
  54 rasterizing tests run as one serial block *after* every parallel collection has finished, which
  `Sequence.xml` shows directly. Nothing else is running beside them.
- **Not the volume of rasterization.** The 83 rasterizing tests on their own, which draw more pages
  than the serial block does, ran clean four times over.
- **Not a test leaking global state.** The font and image seams are set only in `TestBackendSetup`,
  and the Turkish-culture test puts the culture back in a `finally`.
- **Not objects reaching a finalizer after the native library went away.** Disposal is already tight
  everywhere — every `RasterizeOutput`, `MagickImage` and pixel collection is inside a `using`.

## How it was pinned down

Worth repeating on the next one of these:

1. **`dotnet test --blame-crash` did produce a `Sequence.xml`** where an earlier attempt had found
   none. It is written under `--results-directory`, and it names every test that started and which
   one never finished. Both captured crashes named exactly one: `FamilyTreeSurvivesBeingResized`.
   That the location was the *same* both times is what turned this from "flaky suite" into one page.
2. **The exit code is the diagnosis.** A watcher holding a handle on `testhost.exe` reports
   `ExitCode` after it dies. Status 1 with no dump says "exited", not "faulted", and that alone
   ruled out the entire class of theories about corruption and finalizers.
3. **Rasterize the assets and look at the sizes.** One page 12 times bigger than every other was
   visible in the output PNGs the whole time.

Two things that cost time and did not pay: `--diag` slows a run so much it is not usable for
something that only happens on a full one, and `procdump` attaching as a debugger risks masking the
very heap behaviour under suspicion — prefer `DOTNET_DbgEnableMiniDump`, which needs no debugger.

## It came back, and the limit was measuring the wrong thing

Same symptom, same class, a different document. `--blame-crash` named
`PageResizeRenderingTests.TestDocumentSurvivesBeingResized` — and named it on **both framework legs
of the same run**, one incomplete test in each `Sequence.xml`, which is the same "the location is
the same both times" signal that settled it the first time.

The limit above was per **page**: `PixelsIn` took the largest page and left the rest out of it. But
`Rasterize` reads *every* page of a document into one collection, so a document of many ordinary
pages was never reduced at all:

| document | pages | per page | held at once | reduced? |
|---|---|---|---|---|
| `FamilyTree.pdf` | 1 | 15.8 MP | 15.8 MP | yes — this is the page the limit was written for |
| `test.pdf` | 4 | 9.7 MP | **38.7 MP** | **no** — every page is comfortably "small" |

So the document that actually asked for the most memory was the one the limit never touched, at two
and a half times the page it was built to catch. `ResizingDoesNotChangeTheDrawing` then renders it
three times over — `before`, `wrapped`, `returned` — and holds the first alongside the others to
compare them, so the one test wanted 80–115 megapixels at once, and with two target frameworks
running it wanted two of those at the same time.

**The limit is now on the whole document**, `MaxPixelsPerDocument`, and `PixelsIn` sums the pages
rather than maxing them. A single ordinary page still draws at 300 dpi, so the reference images and
the tolerances they are compared under are untouched — there is a test that says so, beside one for
the gap this closed.

Before: reproduced on the first run of both legs. After: four consecutive runs of both legs, exit 0,
no crash, and the same count every time.

### What this adds to the lesson

The first fix was right about the cause and too narrow about the measure. "Pages are around 9
megapixels each, so cap the big one" was true of the document in front of it and not of the suite.
Anything that holds a whole document in memory should be sized by the whole document.

## A third time, and this one was not the suite's fault

Same symptom again on Windows, and this time the answer is that there is nothing here to fix. Worth
recording anyway, because most of an hour went into establishing that — and because the signal that
settled the first two says the opposite here.

**`--blame-crash` named a different test almost every time.** Across the runs captured:
`XTextFormatterTest.DrawMultiLineStringWithOverflow`,
`GenericAnnotationRenderingTests.ASquareWithoutAnAppearanceIsNotPaintedAtAll`,
`TextMarkupRenderingTests.AnAnnotationGivenOnlyARectangleIsDrawn`,
`TextMarkupRenderingTests.OpacityThinsTheWash`, `TextMarkupRenderingTests.EveryQuadOfAnAnnotationIsDrawn`.
Above, *the same name twice* is what turned a flaky suite into one page. **A different name every
time is the same evidence pointing the other way**: nothing in the suite is the cause.

It narrowed to `TextMarkupRenderingTests` on its own — eight tests, one collection, **five crashes in
eight runs** — which made everything below cheap to measure.

### What it was not

- **Not memory.** Peak working set across every test host in the run was 144–700MB, sampled once a
  second. The two episodes above were 940MB and over a gigabyte. A ledger written around every call
  to `PdfHelper.Rasterize` showed the process dying on the **tenth of thirteen** one-page documents
  at 300 dpi — an A4 page with a highlight annotation on it.
- **Not concurrency.** That ledger recorded a BEGIN and an END with thread ids; every rasterization
  finished before the next began. An ImageMagick trace of a crashing run agrees: its two threads are
  300ms apart and never overlap. `RasterizingCollection` is doing its job.
- **Not a document.** The thirteen documents that class produces were written to disk and rasterized
  fifteen times each — 195 rasterizations — in a bare console process. Not one failed.
- **Not Ghostscript in a process.** In that same bare process: 60 rasterizations in a row on one
  thread, then 60 more with a fresh thread for each, then 100 rounds of *build the document with
  PdfPinata and Skia, then rasterize it*. All clean, flat memory throughout.
- **Not the temp directory.** `%TEMP%` held 12,924 files, 209 of them ImageMagick leftovers from the
  crashes. Pointing it at an empty directory gave five crashes in eight runs — no better.
- **Not anything in this fork.** `be0b0b7`, the commit whose message records four consecutive clean
  runs, crashes the same way today.

### What is known

The test host exits with **status 1 (`0x00000001`)**, read off the process handle rather than
inferred. `--blame-crash` wrote its `Sequence.xml` every time and a dump not once, because there is
nothing to dump: something calls `exit(1)`, and Ghostscript is the only thing in that process which
does.

### And then it stopped

Same commit, same command, no change to the repository: **thirty-odd consecutive runs without a
crash, including two full suites at 1985 tests, exit 0.** What changed was the machine. During the
crashing hour it was carrying about 28 leftover `dotnet` processes, with the system commit charge at
118GB of a 156GB limit and 8GB of 64GB physically free.

That is consistent with the class of fault above — Ghostscript giving up on an allocation it cannot
make and ending the process — with the pressure coming from **outside** the test host, which is
exactly why the host's own numbers looked innocent.

### What to do when it happens again

1. **Read the exit code, then read the blame names.** Same name twice: look at that document, and
   the two sections above are the playbook. A different name each time: stop looking at the suite.
2. **Re-run before believing it**, and note that a crash that will not reproduce an hour later was
   never about the code.
3. **Look at the machine, not the test host** — system commit charge and free physical memory, not
   `testhost.exe`'s working set. If it returns, capturing free physical memory at the moment of the
   crash is the measurement that would settle it.

## What is still true

CI runs on Linux, where Ghostscript is the system `gs` shelled out to rather than a library loaded
into the test host, so a page it cannot draw fails a test there instead of killing the run. This was
only ever a local Windows problem, and it never blocked the build.

The wider hazard remains: **on Windows every rasterization runs Ghostscript inside the test host,
and its way of giving up is to end the process.** Anything that makes it fail — a much larger page,
a malformed font, a document it will not parse — will read as "test host crashed" with no failing
test. If that happens again, check the exit code first.
