# Spec — spot colours as Separation colour spaces (empira/PDFsharp#201)

Print shops ask for named inks: a Pantone colour, a white underprint on clear film, a varnish. PDF
has a colour space for exactly this, `[/Separation name alternateSpace tintTransform]` (ISO 32000-1
8.6.6.4). Upstream never wrote one. This fork now writes one from `XGraphics`, for fills, strokes
and text. This is a retrospective: it describes the code that shipped.

| item | what | status |
|---|---|---|
| 1 | `XSpotColor` and `XColor.FromSpot`, usable anywhere an `XColor` is | done |
| 2 | One `/Separation` object per colorant name per document, Type 2 tint transform | done |
| 3 | `cs`/`scn` and `CS`/`SCN`, and a process colour reselecting its device space afterwards | done |
| 4 | Conflicting definitions of one name refused | done |
| 5 | PDF/A held to the alternate through the existing device-colour rule; corpus validated | done |
| 6 | Gradients in a Separation space, DeviceN, Lab/ICC alternates, PinataLayout, reading | deliberately not done |

## API

```csharp
var pantone = new XSpotColor("PANTONE 185 C", XColor.FromCmyk(0, 0.93, 0.79, 0));

gfx.DrawRectangle(new XSolidBrush(XColor.FromSpot(pantone)), 50, 50, 200, 100);        // solid
gfx.DrawRectangle(new XSolidBrush(XColor.FromSpot(pantone, 0.4)), 50, 200, 200, 100);  // 40 % tint
gfx.DrawLine(new XPen(XColor.FromSpot(pantone), 2), 50, 350, 250, 350);                // stroke
gfx.DrawString("Spot", font, new XSolidBrush(XColor.FromSpot(0.5, pantone, 1)), 50, 400); // 50 % alpha
```

- `XSpotColor(string name, XColor alternate)` is a sealed class. It holds the colorant name and the
  colour that a device without the ink paints at full tint. The alternate's `ColorSpace` selects the
  alternate space: `DeviceCMYK`, `DeviceRGB` or `DeviceGray`. The alternate's alpha is ignored.
- `XColor.FromSpot(spot, tint = 1)` and `XColor.FromSpot(alpha, spot, tint)` return an ordinary
  `XColor`. That is the whole reason spot colours are carried on `XColor` rather than on a new brush
  type. Anything that takes a colour takes a spot colour: `XSolidBrush`, `XPen`, a pen made from a
  solid brush, text in every rendering mode, and bold simulation.
- `XColor.Spot` and `XColor.Tint` read the spot colour back. For a spot colour, `R`/`G`/`B`,
  `C`/`M`/`Y`/`K` and `GS` hold the alternate at that tint, so code that does not know about spot
  colours paints a sensible process approximation. If you set any of those components, the colour
  becomes a plain process colour again. Setting `A` does not change that. `==` compares the spot
  colour and the tint, so a spot colour is not equal to its own approximation.

## What is written

- One indirect `[/Separation /Name /DeviceXxx << /FunctionType 2 /Domain [0 1] /C0 white /C1 alternate /N 1 >>]`
  per colorant name, per document. It is held in `PdfSpotColorTable` on `PdfDocument`, next to the
  ExtGState table. `C0` is the alternate space's white: `[0 0 0 0]` for CMYK, `[1 1 1]` for RGB and
  `[1]` for grey. So tint 0 is the paper and tint 1 is the alternate.
- A `/ColorSpace` resource named `/CSn` on the page or form being drawn, through the new
  `PdfResources.AddColorSpace`.
- `/CSn cs t scn` for fills and `/CSn CS t SCN` for strokes, from `PdfGraphicsState.RealizeSpotColor`.
  A change of tint alone writes only `t scn`. A process colour after a spot colour always writes its
  `rg`/`k`/`RG`/`K`, even when the numbers match: the operator is what selects the device space
  again. Before this change, the equality check against the last colour would have skipped the
  operator and painted the process colour in the ink. Tests pin both cases.
- Alpha and overprint go through the ExtGState, exactly as they do for any other colour.
- The name is written as UTF-8 bytes, which is what PDF 2.0 says a name means. ASCII names are
  unchanged. The writer already escapes spaces and other characters as `#xx`.

**Two definitions that share a name and disagree about the alternate are refused** with an
`InvalidOperationException` when the second one is drawn. A RIP separates by name, so both would
print from one plate while every screen showed two colours. PDF/A-2 clause 6.2.4.4 forbids it as
well. Equal definitions in separate `XSpotColor` instances share the one colour-space object.

## The document's colour mode does not convert a spot colour

`PdfDocumentOptions.ColorMode` controls how *process* colour is written. A spot colour keeps the
alternate the caller gave it, whatever the mode. A CMYK alternate in an RGB document stays CMYK.

## PDF/A

No new rule was needed, and none was weakened. `PdfResourceConformanceRules.CollectDeviceColorFamilies`
already followed a Separation down to its alternate. Its reasoning: a reader without the ink paints
the alternate for real, so the alternate is held to the output intent in the same way that painting
it outright would be. So:

- A CMYK alternate in an RGB PDF/A document (default sRGB intent) is refused at `Save` with the usual
  "3-component … 4-component" message. It is the same refusal as for `k`.
- An RGB alternate in an RGB PDF/A document conforms. The conformance corpus now paints one (fill
  and stroke) on `pdfa-1b`, `pdfa-2b` and `pdfa-3b`, and veraPDF passes all nine documents.
- A CMYK alternate conforms when the output intent is a CMYK profile. As before, a CMYK document
  claiming PDF/A has to supply that profile itself.

`SpotColorTests` covers all three cases.

## Deliberately left out

- **Gradients.** A gradient brush between spot colours paints the process approximation (the
  alternate at each tint), not a shading in the Separation space. A shading in a Separation space
  (`/ColorSpace [/Separation …]` with a 1-in, 1-out function over tint) is the natural next step.
- **DeviceN** (several colorants in one colour space, e.g. duotones) is not written.
- **ICC-based or Lab alternates.** Only device alternates are available. A Lab alternate is what a
  colour-managed Pantone book would give. It would also free a CMYK spot from needing a CMYK output
  intent under PDF/A.
- **PinataLayout.** The DOM `Color` type has no spot colour, and adding one touches the value model
  and MDDDL serialization. Not trivial, so not done.
- **Reading.** Nothing maps a Separation in an opened document back to an `XSpotColor`. A document
  that already has separations of its own is not deduplicated against them.
- **`/SeparationInfo`** on the page (an optional hint for a separation workflow) is not written.
- **`XColor.RgbCmykG`** (the XmlSerializer property) carries process components only. A spot colour
  serialized through it comes back as its approximation.
- **`/All` and `/None`** are accepted as names without special handling. ISO 32000 gives them their
  own meaning, and a caller naming them is asking for that meaning.

## Tests

- `src/PdfPinata.Test/Drawing/SpotColorTests.cs`: the written array, the function and the operators,
  read back from a saved and reopened file. Also covers sharing across pages and instances, the
  conflict refusal, reselecting a process colour, alpha, text, forms, the value semantics of the
  colour, and the three PDF/A cases.
- `src/PdfPinata.Test/Drawing/SpotColorRenderingTests.cs`: rasterizes through Ghostscript to show
  that the alternate is what is painted (CMYK at full and half tint, filled and stroked; RGB with the
  painted pixels counted). ImageMagick returns a page with CMYK in it as a CMYK raster, so the test
  converts to sRGB before it reads channels.
