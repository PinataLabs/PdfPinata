# Spec — the dead graphics-state stack, and the sync contract between the two live ones (T15)

Three types in `XGraphics`'s save/restore machinery carry the word "state", and the original framing
of this task was that all three should be unified into one. Reading them says otherwise: one of the
three is exactly the dead, malformed thing it was described as and removing it is unambiguous; the
other two are not a redundant pair but the two sides of a real seam, and fusing them would undo a
separation the codebase has a reason to keep. So this is a confirm-and-remove, not a redesign.

## What was removed

`PdfGraphicsState.PushState()` and `PopState()` are gone. They read:

```csharp
public void PushState()
{
    // BeginGraphic
    _renderer.Append("q/n");
}
```

That is the literal text `"q/n"` — a forward slash and the letter `n`, not a newline — so a reader
would see `q`, `/`, `n` rather than the operator `q` followed by whitespace. Nothing ever wrote that
into a document: `grep -rn "PushState\|PopState"` over the whole repository returned exactly the two
declarations and no call site, and `PdfGraphicsState` is `internal sealed`, so nothing outside the
assembly could have called them either. The two facts are related — nothing ran them, so nothing
noticed they were wrong.

They were deleted rather than corrected. Fixing the string in place would leave dead code that now
*looks* right, which invites a future reader to wire it back up on the strength of a repaired typo
instead of asking why nothing calls it. The more durable defect was never the string anyway: it was
that `PushState`/`PopState` sit next to the fields they appear to operate on and are named the same
shape as the real pair, `SaveState`/`RestoreState`, which is private and 2,500 lines further down
`XGraphicsPdfRenderer.cs`. Every `q` and `Q` in every document this library has produced came from
that private pair, which is correct.

## What the two live stacks each do, and why they stay two

- **`GraphicsStateStack` / `InternalGraphicsState`**, owned by `XGraphics` through `_gsStack`, track
  one real value — `Transform`, the user-space matrix `XGraphics.Transform` answers from — plus the
  `Invalid` flag that refuses restoring a handle twice or one no longer on the stack. Nothing about
  PDF. `XGraphicsState` and `XGraphicsContainer` are pure handles over these, one field and no logic,
  and stay that way.
- **`PdfGraphicsState`**, owned by `XGraphicsPdfRenderer` through `_gfxStateStack`, tracks realized
  content-stream state: four CTM matrices, the clip `Level`, and every realized stroke, fill and text
  value that decides which operator gets written.

`IXGraphicsRenderer` is public and deliberately gives a caller no way to read realized state back —
its `Transform` property is commented out of the interface — which is a second, independent sign that
the split is intentional. Merging the two stacks means either leaking PDF-specific realized state
above that seam or growing the interface to read it back through, and neither is wanted.

The header comment on `InternalGraphicsState.cs` describes an automaton tracking every clipping path
and transformation across arbitrary `Save`/`Restore`/`BeginContainer`/`EndContainer` combinations, and
then says plainly that it was designed and deliberately not implemented. It stays unimplemented; the
"lay down some rules for using `XGraphics`" replacement is what shipped and has worked since.

## The synchronization point, now written down

The two stacks are paired by a single mutable field write, done in exactly two places —
`_gfxState.InternalState = state.InternalState;` in `XGraphicsPdfRenderer.Save`, and the same for
`container.InternalState` in `BeginContainer`. Both now carry a comment saying so, and
`PdfGraphicsState.InternalState` carries one saying what it is for. It happens once per save,
immediately before `SaveState()` pushes, and it is sound only because of an ordering established
above the seam: `XGraphics.Save`/`Restore`/`BeginContainer`/`EndContainer` all push onto or validate
against `_gsStack` **before** calling into the renderer, so a bad caller handle is refused by
`GraphicsStateStack` and never reaches the renderer's stack at all. That ordering is unchanged. The
rule for anyone extending the renderer is short: a push onto one stack needs a push onto the other.

`RestoreState(InternalGraphicsState)` searches `_gfxStateStack` by reference identity for the frame
carrying that cross-link, popping and writing `Q` for each state saved inside it. It now checks the
stack before each pop and throws naming the real condition — the two graphics state stacks are out of
sync — rather than letting `Stack<T>.Pop()` throw its own `"Stack empty."` from a frame that explains
nothing. No caller can reach this today, because `GraphicsStateStack.Restore` refuses an unknown
handle first; it is a diagnostic against a future change that pushes one stack without the other, not
a new contract for any caller.

## Testing

`XGraphicsSurfaceTests` gained `StateOf`, which isolates the literal `q`/`Q` operators out of a
content stream the way `ShapeOf` already isolates `re`/`m`/`l`/`c`/`v`/`y`/`h`, and `DepthsOf`, which
reports the nesting depth each shape is drawn at. Three tests use them:
`SavingAndRestoringWriteTheLiteralQAndQOperators`, `ASaveNeverRestoredIsClosedWhenThePageEnds`, and
`RestoringAnOuterStateClosesEveryStateSavedInsideIt`.

This is new coverage rather than a repaired gap. Every existing save/restore test reads
`GraphicsStateLevel` and `Transform`, both of which live on the `XGraphics` side and would say the
same thing whatever bytes the renderer wrote — which is exactly why `"q/n"` shipped unnoticed and why
no golden image would ever have caught it. Two of the four `q`/`Q` pairs on a page belong to the page
itself: the renderer opens page space and then world space before the first thing is drawn, and
`EndPage` closes both, along with anything a caller saved and never restored.

The existing nesting tests, the golden images, `PdfPinata.Charting.Tests`,
`PinataLayout.Rendering.Tests` and the veraPDF corpus are the regression net for the deep nesting the
charting plot-area renderers, MigraDoc's text-frame and border renderers, and the barcode renderers
drive. All pass unmodified, and all six corpus documents still conform.
