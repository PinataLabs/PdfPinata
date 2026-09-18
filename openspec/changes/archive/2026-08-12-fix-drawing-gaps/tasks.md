Ordered cheapest and most certain first. Groups 1, 2 and 3 are independent of each other and of
group 4, and each is worth shipping on its own; group 4 is the large one and is last so that nothing
waits on it.

## 1. The table heading that repeats nothing

- [x] 1.1 Write the failing tests first, in `PdfPinata.Test/Rendering/`: a table whose second row
      alone carries `HeadingFormat` renders today with no repeating heading. Assert what the fix
      will make true — an `InvalidOperationException` naming row 1 — so the test fails for the
      right reason before anything is changed.
- [x] 1.2 In `PinataLayout.Rendering/MigraDoc.Rendering/TableRenderer.cs`, after
      `CalcLastHeaderRow` computes the run, scan rows beyond it for `HeadingFormat` and throw
      `InvalidOperationException` naming the row index and stating that heading rows must form an
      unbroken run from the first row. Confirm this runs during formatting, before any page is
      written — if it does not, move it to where it does, because a half-written document is a
      worse outcome than the silence being replaced.
- [x] 1.3 Cover the scenarios in `specs/repeating-table-headings/spec.md` that must keep working:
      one heading row, two heading rows, no heading rows, and a table that is entirely heading rows
      (which repeats nothing, by the existing `lastHeaderRow == Rows.Count - 1` branch).
- [x] 1.4 Add a line to the release notes. The throw is a behaviour change, however narrow.

## 2. Baseline alignment with a rectangle that has a height

- [x] 2.1 **Answer the open question before writing anything**: read the placement code below
      `XGraphics.cs:1295` and establish whether a `BaseLine` line alignment derives anything from
      the rectangle's height. If it does, that arithmetic is the actual defect and the guard was
      hiding it — fix it and say so in the commit.
- [x] 2.2 Remove the guard at `XGraphics.cs:1295` and the matching one at `XGraphicsPath.cs:391`.
      Both, in the same commit: they are separate code that must agree.
- [x] 2.3 Test that the same string with the same `BaseLine` format lands in the same place for
      rectangles of height 0, 20 and 200 — this is the requirement, and it is what catches a
      height that leaks into the arithmetic.
- [x] 2.4 Test `XStringFormats.Default` into an ordinary rectangle, named for the trap it is:
      the format a caller reaches for when not thinking about formats used to throw on the most
      natural overload.
- [x] 2.5 Test `BaseLineRight` and `BaseLineCenter` with a width, confirming horizontal alignment
      still applies and only the vertical dimension is being ignored.

## 3. Gradients that honour alpha

- [x] 3.1 Pin the current output first: save a document of opaque gradients, keep the bytes, and
      assert they are unchanged at the end of this group. Without this the "opaque output is
      untouched" requirement is an intention rather than a fact.
- [x] 3.2 In `PdfShading`, extract the function-and-coords construction so the same geometry can be
      built twice — once in `DeviceRGB`/`DeviceCMYK` for colour, once in `DeviceGray` for alpha.
      Do not copy it; two copies will drift and the mask will stop matching the gradient.
- [x] 3.3 Build the alpha shading: same `/Coords`, same `/FunctionType 2`, `/ColorSpace /DeviceGray`,
      `/C0` and `/C1` as the source colours' alpha in 0..1.
- [x] 3.4 Build the luminosity group: a `PdfFormXObject` with
      `/Group << /S /Transparency /CS /DeviceGray >>` via the existing
      `PdfTransparencyGroupAttributes`, a `/BBox`, and a content stream painting the alpha shading
      through a pattern. Start with the page box as the BBox and note in the commit that narrowing
      it is a later optimisation.
- [x] 3.5 Wire it together: a `PdfSoftMask` with `/S /Luminosity` and `/G` the form, on a
      `PdfExtGState` via the existing `SoftMask` setter, applied before the colour shading is
      painted. All four types already exist and are unused from this path — reach for them rather
      than adding new ones.
- [x] 3.6 Gate the whole branch on `either colour's A < 255`. Re-run 3.1 and confirm the opaque
      bytes are identical.
- [x] 3.7 Test the appearance: transparent-to-opaque over a filled rectangle, half-alpha over white
      giving mid grey rather than black, and a radial gradient with a transparent outer colour over
      an image. These belong in the rasterizing collection — `[Collection(RasterizingCollection.Name)]`
      and `[GoldenImageFact]` — because only rasterizing shows a blend.
- [x] 3.8 Test the structure as well as the pixels: assert the saved document has an `ExtGState`
      whose `/SMask` is `/S /Luminosity` with a `/G` form carrying the `/DeviceGray` transparency
      group. A reader that renders it correctly today may not tomorrow; the structure is the
      contract.
- [x] 3.9 Test that the graphics state is left as found — an opaque black rectangle drawn after a
      masked gradient is fully opaque, and two gradients with different ramps on one page do not
      mask each other.

## 4. Glyph outlines and `AddString`

- [x] 4.1 Define `IGlyphOutlineProvider` and `XGlyphOutline` in `PdfPinata/Fonts/`, and the
      `GlobalFontSettings.GlyphOutlineProvider` registration property. Match the existing seams:
      the getter throws `InvalidOperationException` naming the property and the packages that
      supply an implementation, exactly as `FontResolver` does when unset.
- [x] 4.2 Test the unregistered case first — `AddString` throws with a message naming the property
      and a package, and adds nothing to the path. This is the behaviour a core-only consumer will
      meet, and it is the one most likely to be got wrong by accident.
- [x] 4.3 Implement `SkiaGlyphOutlineProvider` in `PdfPinata.Skia`: font bytes from the
      registered `IFontResolver` (never resolved independently, or the two seams will disagree
      about which face a family means), `SKTypeface.FromStream`, `GetGlyphs`, `GetGlyphPath`, then
      walk the `SKPath` with its iterator. Convert each quadratic to a cubic exactly — controls at
      `p0 + 2/3(q - p0)` and `p2 + 2/3(q - p2)` — rather than subdividing.
- [x] 4.4 Implement `ImageSharpGlyphOutlineProvider` in `PdfPinata.ImageSharp` against
      SixLabors.Fonts' `IGlyphRenderer`, whose callbacks are already move/line/quadratic/cubic/end.
- [x] 4.5 Implement both `XGraphicsPath.AddString` overloads on top of the seam, feeding
      `CoreGraphicsPath.MoveTo`/`LineTo`/`BezierTo`/`CloseSubpath`. **Call the same alignment
      arithmetic `DrawString` uses** for a rectangle and format — a second copy will drift, and the
      requirement that the path agrees with `DrawString` is what will fail when it does.
- [x] 4.6 Apply the group 2 rule here too: a `BaseLine` format with a rectangle of any height
      places the baseline on the top edge.
- [x] 4.7 Test that the path is non-empty and its bounds approximate `MeasureString`, for a
      TrueType family (Liberation Sans) **and a CFF one (Source Code Pro)** — the CFF case is why
      the seam was chosen over an in-library `glyf` decoder, so it is the case that justifies the
      design. Assert bounds and emptiness, never point counts or coordinates: the two backends will
      subdivide differently and a stricter assertion would pin one backend's arithmetic.
- [x] 4.8 Test the three things a path can do that `DrawString` cannot: filled with a gradient,
      used as a clip with a photograph drawn through it, and an empty string adding nothing and
      throwing nothing.
- [x] 4.9 Answer the open question about glyphs with no outline — bitmap-only glyphs, `.notdef` —
      by checking what `DrawString` does with the same character, then match it and write a test
      recording the choice.
- [x] 4.10 XML-document `AddString` naming `DrawString` with a pen as the way to stroke text
      without a path. Most callers who reach for `AddString` want outlined text and do not need any
      of this.

## 5. Make the demos tell the truth again

- [x] 5.1 `SampleApp/Demos/MagazineDemo.cs` builds its scrim from 140 translucent bands and strokes
      its title with a pen, each with a comment explaining that the direct route does not work.
      Replace the scrim with a real gradient and the title with `AddString`, and delete both
      explanations. This is not tidying: left alone the demo teaches a limitation that no longer
      exists, which is worse than the original gap.
- [x] 5.2 Update `Shows` on `MagazineDemo` — it currently advertises the workarounds.
- [x] 5.3 Update `docs/specs/demonstration-app.md`: the four gaps move from "what the library does
      not do" to a note that they were found here and fixed under this change.
- [x] 5.4 Re-run the demo smoke tests. `Magazine` declares two pages; changing how it draws must
      not change how many pages it draws, and the test will say so if it does.

## 6. Close out

- [x] 6.1 `./ci-build.ps1` clean and `dotnet test` green on both target frameworks.
- [x] 6.2 Rasterize the affected demo pages and look at them. Every one of these four gaps was found
      by looking at a page and none by a test; the same standard applies to the fixes.
- [x] 6.3 Run the demo app on Linux, or check CI does, since gradients with soft masks and glyph
      outlines are both places a backend can differ by platform.
      **CI does.** `.github/workflows/build.yml` runs on `ubuntu-latest`, installs Ghostscript so
      ImageMagick's `gs` delegate is there, and runs the whole suite — which now includes the
      gradient soft masks rasterized through Ghostscript, the demo smoke tests, and
      `TheTwoShippedBackendsAgreeAboutWhereTheGlyphsGo`, which exercises Skia's native outline
      reader and SixLabors' managed one against each other on that machine.
