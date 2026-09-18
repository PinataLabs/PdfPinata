# Spec — giving the sheet a page is printed on a type of its own (T11)

`PdfPage.cs` was 1602 lines carrying six unrelated responsibilities, and the print-production one —
the bleed, the room outside it for printer's marks, the five boxes that say which part of the sheet
is what, and the crop marks — was not even in one place: `TrimMargins` and `MarkMargins` sat between
the geometry cluster's `Resize` methods, and `WriteSheetBoxes`/`DrawCropMarks` sat two hundred lines
from the end beside the page-tree mechanics. A reader chasing the crop-mark trigonometry crossed four
cluster boundaries to reach it. This is the extraction the prior draft of this document proposed, and
it shipped essentially as planned — the shape, the constructor eagerness, the copy-on-assign setter
and the decision to leave the other five clusters alone all landed exactly as written. The one place
worth flagging is small: `TrimMargins`'s doc comment gained a new paragraph rather than the plan's
sketch of a doc-comment edit staying purely additive-in-spirit, and the actual line-count drop
(1602 → 1467, 135 lines) is smaller than the raw diff stat suggests, because the extraction added new
prose to `PdfPage`'s remaining members at the same time it moved code out.

## What moved

`PdfPinata/Pdf/PdfPageSheet.cs` is a new 292-line `internal sealed class`, constructed with a
back-reference to the page that owns it:

```csharp
internal PdfPageSheet(PdfPage page) => _page = page;
readonly PdfPage _page;
```

It holds `TrimMargins`, `MarkMargins`, `Offset` (was `SheetOffset`), `ExtraHeight` (was
`SheetExtraHeight`), `TrimmedSize` (was the private field `_trimmedSize`), `WriteBoxesIfTrimmed` (new
— see below), the private `WriteSheetBoxes`, `SetBox`, `DrawCropMarks`, `_cropMarksDrawn`, and the
static `Mark` helper. Every place the moved code read `Width`, `Height`, `Elements` or `Contents` now
reads `_page.Width`, `_page.Height`, `_page.Elements`, `_page.Contents` — the type holds no copy of
anything the page knows, matching the shape `PdfDocumentOptions`, `PdfSecuritySettings` and
`PdfStructureBuilder` already use toward `PdfDocument`. `Keys.MediaBox` and its four siblings became
`PdfPage.Keys.MediaBox` and so on, compiling unchanged because `PdfPage.Keys` is `internal` and
visible anywhere in the assembly.

`PdfPage.cs:52-91` (all three constructors) each now start with `_sheet = new PdfPageSheet(this);` —
the parameterless constructor, the one taking a `PdfDocument`, and the internal one taking an
existing `PdfDictionary` for an imported page. This is eager, not the lazy `??=` pattern
`PdfDocument.Options`/`Structure` use, exactly as planned: `PrepareForSave` and the public
`DrawCropMarks()` both reach the sheet without going through a getter first, and an imported page
never runs `Initialize()`, so laziness here would just move a null check rather than remove one.

`PdfPage`'s public surface is unchanged and forwards:

```csharp
public TrimMargins TrimMargins { get => _sheet.TrimMargins; set => _sheet.TrimMargins = value; }
public TrimMargins MarkMargins { get => _sheet.MarkMargins; set => _sheet.MarkMargins = value; }
public void DrawCropMarks() => _sheet.DrawCropMarks();
internal XPoint SheetOffset => _sheet.Offset;
internal double SheetExtraHeight => _sheet.ExtraHeight;
```

`SheetOffset` and `SheetExtraHeight` kept their existing names, so `XGraphics.Initialize` and
`XGraphicsPdfRenderer.BeginPage` needed no changes at all.

`_trimmedSize` became `PdfPageSheet.TrimmedSize`, an `internal XSize? TrimmedSize { get; set; }`, and
the two callers outside the cluster reach it exactly as sketched — `PdfPage.Height` and `.Width`
(`PdfPage.cs:523`, `:550`) read `_sheet.TrimmedSize is { } trimmed`, and `MediaBox`'s setter
(`:471`) writes `_sheet.TrimmedSize = null`. `ApplyResizedBox` (`PdfPage.cs:634`) needed no change at
all, as predicted: it assigns through the `MediaBox` property rather than the field it used to be, so
the property's new body already does the right thing underneath it.

`PrepareForSave`'s `if (_trimMargins.AreSet) WriteSheetBoxes();` became one call,
`_sheet.WriteBoxesIfTrimmed();` (`PdfPage.cs:1149`), with the `AreSet` check folded into the new
method on `PdfPageSheet` rather than left reading a private field from outside the type.

## The one divergence from the plan: how the doc comment landed

The plan's Implementation Decisions section described the fix for the copy-on-assign surprise as "a
new paragraph is added to `TrimMargins`'s XML doc, next to the existing five." That is what happened,
almost verbatim — `PdfPage.cs:375-383` now carries:

> **Assigning copies the four values rather than keeping the reference.** The page holds a
> `TrimMargins` of its own from the moment it is constructed, and `page.TrimMargins = other` copies
> `other`'s left, right, top and bottom into it... That is what makes `PdfDocumentSettings.TrimMargins`
> usable as a document-wide default...

and `MarkMargins`'s doc comment got the shorter cross-referencing version the plan also predicted.
Nothing here diverges in substance. What is worth recording is that the same paragraph, near-verbatim,
was also written onto `PdfPageSheet.TrimMargins`'s own doc comment (`PdfPageSheet.cs:60-64`), which
the plan did not call out one way or the other — the extracted type's copy of the property carries its
own short version pointing back at `PdfPage.TrimMargins` for the full explanation, rather than leaving
the internal member undocumented. That is consistent with the rest of the file: every forwarding
member on `PdfPageSheet` carries a `<see cref="PdfPage...">` pointing at the public surface it backs.

## `TrimMargins`'s copy-on-assign setter: kept, not changed

Exactly as decided in the prior draft, the setter was not touched — it still copies `value`'s four
fields into the page's own instance rather than holding the reference:

```csharp
internal TrimMargins TrimMargins
{
    get { if (_trimMargins == null) _trimMargins = new TrimMargins(); return _trimMargins; }
    set
    {
        if (_trimMargins == null) _trimMargins = new TrimMargins();
        if (value != null) { _trimMargins.Left = value.Left; /* ...Right, Top, Bottom */ }
        else _trimMargins.All = 0;
    }
}
```

now living on `PdfPageSheet` verbatim rather than on `PdfPage`. The reasoning recorded in the plan
holds: `PdfPages.cs` hands `Owner.Settings.TrimMargins` — the same instance every time — to every page
added to a document with a document-wide default set, so a reference-holding setter would make every
page in the document alias the default, and mutating one page's margin would silently move every
other page's.

## Testing

`PageBleedTests.cs` needed no edits to its existing 23 facts, which is the point the plan was making:
they are written against `page.TrimMargins`, `page.MarkMargins`, `page.DrawCropMarks()`,
`page.Width`/`Height` and `page.Owner.Save`, none against the fields that moved, so they are the
regression net for the extraction for free. Three new facts were added, all in
`PdfPinata.Test/Drawing/PageBleedTests.cs`, bringing the file to 26:

- `AssigningTrimMarginsCopiesTheValuesRatherThanHoldingTheReference` — assigns a shared `TrimMargins`
  to a page, mutates the shared instance afterward, asserts the page's value did not move and that
  `page.TrimMargins` is not the same instance as `shared`.
- `AssigningMarkMarginsCopiesTheValuesRatherThanHoldingTheReference` — the same shape for
  `MarkMargins`.
- `EveryPageGetsItsOwnCopyOfTheDocumentWideTrimMargins` — sets `document.Settings.TrimMargins.All`,
  adds two pages, moves the first page's `Left` margin, and asserts the second page, the document's
  own default, and a third page added afterward are all unaffected. This is the test that would have
  caught a "make it reference-holding" fix immediately, and it is the one the plan flagged as
  currently unexercised anywhere in the suite (a repo-wide search for `Settings.TrimMargins` in tests
  had found nothing before this).

All three are new coverage, not repaired gaps — the plan's proposed test names were followed exactly.
`PdfDocumentSettings.TrimMargins`'s identical copying setter, called out in the plan as a one-line doc
addition belonging to its own diff, was not touched by this commit and remains undocumented in the
same way.

`PageBleedRenderingTests.cs` and `TrimmedPageRenderingTests.cs` were not touched, and `BleedDemo`
(covered by `DemoSmokeTests.cs`) is unmodified — both are still the integration proof the plan
described, exercising the moved code end to end through `PdfPage`'s public surface without knowing
`PdfPageSheet` exists.

## What was deliberately left alone

The other five responsibilities the original draft catalogued — geometry, content-stream bookkeeping,
annotations and links, resource-name allocation, and page-tree mechanics — are untouched by this
commit, exactly as scoped. `PdfPageSheet` is `internal`, not exposed the way `PdfDocument` exposes
`Options`; `PdfPage`'s public surface for this cluster is still three flat members
(`TrimMargins`, `MarkMargins`, `DrawCropMarks()`), not a grouped `page.Sheet` accessor.
`GetImagePlacements` and `CustomValues`, the two members the plan's six-cluster count had missed,
were not moved either — both stay on `PdfPage`. The resource-naming cluster
(`Resources`, `GetFontName`, `GetImageName`, `GetFormName`, `NoteTransparencyOf`, and the
`IContentStream` implementation) that the plan sketched as the next slice was not started here; it
remains the larger, cross-file change the plan described, touching three other `IContentStream`
implementers elsewhere in the codebase.
