# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), with changes grouped by product area before change type, and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [Unreleased]

### PDF Reader & Writer

#### Fixed

- **A literal string continued onto the next line with a backslash before CR LF no longer keeps the LF.** A CR LF is one end-of-line marker (ISO 32000-1 7.2.3), and the backslash is ignored together with the whole of it (7.3.4.2). Both the document lexer and the content-stream lexer dropped the CR and kept the LF as the first character of the next line. A backslash before a lone CR or a lone LF was already read correctly. (#131)
- **A stream whose `/Length` refers back to itself no longer overflows the stack in `PdfReader.Open`.** An indirect `/Length` is resolved by reading the object it names. When that object was the stream itself, or another stream whose `/Length` named the first, reading it resolved the same length again, until the stack overflowed and the process ended with no exception to catch. Such a length is now unknown, as a missing one is, and the stream is read up to its `endstream` keyword. (#128)
- **Text extraction no longer hangs on a font whose `/W` widths run up to the last `int` code.** A `cFirst cLast w` run was filled in with an `int` counter, which wraps at `int.MaxValue` rather than passing it, so a run ending there never finished and filled a dictionary until memory ran out. The guard against a run claiming more than 65,536 codes computed the span as an `int` too, so a run such as `-1 2147483647` overflowed past it. Both are now `long`, and such a run is either skipped by the guard or filled in and finished. (#137)
- **`PdfReader.Open` in `PdfDocumentOpenMode.Append` opens a file with padding after `%%EOF`.** Append mode looked for the last `startxref` a second time, in the last 2 KB only, and parsed the number in the current culture. So a file whose producer pads the end (SAP, for example; empira/PDFsharp#390) opened in every mode except Append, which threw "The document has no startxref". Append now uses the offset the parser already read. (#164)
- **PDF dates are written in the Gregorian calendar whatever the current culture.** `/CreationDate`, `/ModDate` and an annotation's `/M` were formatted in the current culture, so under th-TH, for example, they carried the Buddhist year 2569. (#160)
- **An XMP date of `DateTimeKind.Unspecified` carries the local offset, so it describes the same instant as `/Info`.** `/Info` wrote the local offset for such a date and XMP wrote none, and PDF/A requires the two to agree. Both now take the offset from one computation. (#160)

### Pages & Documents

#### Breaking

- **`PageSize.Post`, `PageSize.Elephant` and `PageSize.RA5` have the dimensions of their sheets.** Post is now 1116 × 1386 points rather than 1126 wide, and Elephant 1656 × 2016 rather than 1565 wide (the digits were transposed). RA5 is 434 points wide rather than 433, because 153 mm had been truncated rather than rounded. All three now match the DOM's `PageFormat`, and a test keeps the two tables in step. Documents using these sizes get a different page size. (#161)

#### Added

- **`PdfDocument.CanSave()` returns a `PdfSaveCheck`.** Its `CanSave` says whether the document can be saved and its `Reason` says why not, or is null when it can. It replaces `CanSave(ref string message)`, which is now deprecated.

#### Fixed

- **LINQ over a page's `PdfContents` now yields the `PdfContent` streams instead of their `PdfReference`s.** `foreach` already gave `PdfContent`, but enumeration as a `PdfArray`, through `IEnumerable<PdfItem>` (what LINQ sees) or through plain `IEnumerable` gave the references underneath, so `page.Contents.OfType<PdfContent>()` was empty and `Cast<PdfContent>()` threw. Every way of enumerating the array now yields the same content streams, as `PdfAnnotations` has since #81.
- **`PdfPage.Resize` no longer hangs on a malformed `/Dests` name tree or outline that leads back to itself.** The resize walked the name tree with a depth cap but no record of the nodes it had visited, so a `/Kids` naming its own node took 2^33 steps. It now uses the guarded `PdfNameTree` walk. The outline walk beside it hung the same way on an item that is its own `/First` and `/Next`, and now enters each item once. (#165)

### Drawing & Graphics

#### Breaking

- **`XColor.GS` means how light a colour is, 0 for black and 1 for white, whichever way the colour was built.** A colour built from RGB held its darkness there, while one built from CMYK or grey held its lightness, so `XColor.FromArgb(255, 255, 255).GS` was 0 and `XColor.FromGrayScale(1).GS` was 1. Code reading `GS` from an RGB colour now gets the other end of the scale. An RGB black declared grey was written to a form field's `/MK` background or border as white, and is now written as black. Black, white and pure red now compare equal whether they were built from RGB, CMYK or grey. (#187)

#### Fixed

- **An arc with a sweep of 0 is drawn as a single curve that stays at its start, and always returns.** `XGraphics.DrawArc` and `XGraphicsPath.AddArc` never returned for a zero sweep starting at exactly 360 or -360: the quadrant the arc ends in came out as 4, and the walk through quadrants 0 to 3 kept adding curves until the process ran out of memory. Off a quadrant edge, such as a start of 45, both control points were 0/0 and the content-stream writer refused the NaN, so `DrawArc` threw at once and a path holding the arc threw when it was drawn. On any other quadrant edge the arc was cut as though it crossed that edge, so a start of 90 drew the whole ellipse. A zero sweep, or one too small to move the start angle (such as float cancellation leaves), is now one piece from its start to its start, whose control points lie at that point. Arcs with a non-zero sweep are unchanged. (#129, #130)
- **`Code3of9Standard` refuses an apostrophe and the `*` start/stop character when the code is set.** The check accepted `'` and the lookup did not know it, so drawing the barcode threw `IndexOutOfRangeException`; the apostrophe is not a Code 39 character. A `*` inside the data, including the `"*ABC*"` convention of Code 39 fonts, drew a second delimiter that stops a scanner reading, because the barcode draws its own start and stop characters. Both now throw `ArgumentException` when the code is set. Pass `"ABC"` rather than `"*ABC*"`. (#158)
- **`quality: null` on `ImageSource.FromFile`, `FromBinary`, `FromStream`, `SkiaImageSource.FromSkiaBitmap` and `ImageSharpImageSource.FromImageSharpImage` means the default of 75.** The parameter is `int?`, but both backends cast it to `int`, so passing null threw "Nullable object must have a value". (#176)

### Annotations & Forms

#### Breaking

- **`PdfAcroField.PdfAcroFieldCollection` is an `IReadOnlyList<PdfAcroField>` and no longer a `PdfArray`.** A form's `Fields` and a field's `Fields` are views of the array underneath, so LINQ over them is typed: `form.Fields.Single(f => f.Name == "x")` is a `PdfAcroField` with no `Cast`. The collection has no `Elements`; the array as the file has it is `form.Elements.GetArray(PdfAcroForm.Keys.Fields)` or `field.Elements.GetArray(PdfAcroField.Keys.Kids)`. Reading a field's `Fields` no longer writes an empty `/Kids` into the field; the array is made when the first field or widget is added. (#146)
- **A check box's state is its field's `/V`, and every widget shows it.** `PdfCheckBoxField.Checked` used to read the first widget's own `/V` whenever the field had widgets under `/Kids`. A field with exactly two widgets was ticked by turning the first on and the second off, unticked the other way round, and given no value of its own; a field with three was not changed at all. A reader goes by the field's value, so what it showed and what `Checked` read could disagree. Now `Checked` reads the field's `/V`, inherited if need be, and the setter writes it and sets each widget's `/AS` to that state where the widget's appearances offer it and to `/Off` where they do not (ISO 32000-1 12.7.4.2.3). A box drawn twice is ticked in both places. A file with no `/V` anywhere is read by its widgets' `/AS`, and a `/V` left on a widget is removed when the state is set. (#146)
- **`PdfCheckBoxField.CheckedName` is read-only and `UncheckedName` is gone.** Both were properties nothing read. `CheckedName` now answers the on state the widgets' appearances name, which is what `Checked = true` writes; the off state is always `/Off`. (#146)
- **`BackColor` and `BorderColor` moved from `PdfTextField` to `PdfAcroField`.** Source that sets them compiles unchanged, but an assembly compiled against the old properties throws `MissingMethodException` until it is recompiled. (#153)
- **A field's `Fields` lists the fields nested under it and no longer its widget annotations.** A field's `/Kids` holds both kinds (ISO 32000-1 12.7.3.1), and the collection returned every kid as a field, so a widget came back as a nameless `PdfTextField` or `PdfCheckBoxField`. A kid is now a widget when it has `/Subtype /Widget`, a `/Parent` and neither `/T` nor `/Kids`, and `Fields`, its `Count`, its indexer and its enumerator skip it. A position given to the indexer therefore counts fields only, and is not the position in the array underneath (`field.Elements.GetArray(PdfAcroField.Keys.Kids)`) when a field has widgets. The widgets are `PdfAcroField.Widgets`. Code that reached a field's widget as `field.Fields[0]` reads `field.Widgets[0]` now. (#146)

#### Added

- **Every field has `BackColor` and `BorderColor`, and they are written to each widget's `/MK` as well as drawn.** They were properties of `PdfTextField` alone, drawn into the appearance stream and nowhere else. `/MK` is what a viewer builds a field from when it does not show that stream. pdf.js, in Firefox, lays its own input over every text and choice field and styles it from `/MK`; Acrobat does while a field has the focus; and every viewer honouring `/NeedAppearances` rebuilds each field from it. The colours therefore disappeared in all of them, and fields showed without a box. Setting a colour now writes `/MK /BG` or `/MK /BC` (with a solid one-point `/BS` for a border) on every widget, including widgets added later, and removes the entry when set back to `XColor.Empty`. **`PdfPushButtonField.Caption`** writes `/MK /CA`. (#153)
- **`PdfComboBoxField` and `PdfListBoxField` draw their own appearance.** A combo box shows its chosen option, or the text typed into an editable one. A list box shows its options from the new `TopIndex` (`/TI`) down, with the chosen rows highlighted. Both draw in the size and colour `DefaultAppearance` names, and have `Font`, `ForeColor`, `BackColor` and `BorderColor` as a text field does; left unset, the background and border come from each widget's `/MK`. Before, both left the drawing to the reader through `/NeedAppearances`, which Ghostscript, print pipelines and most previewers ignore, so the value was set and not shown. PDF 2.0 deprecates that flag and PDF/A forbids it. A field read from a file is redrawn only when it is changed, not on save. (#151)
- **`PdfAcroField.Widgets` lists the widget annotations a field is drawn as**, typed as `PdfWidgetAnnotation`: the widgets under its `/Kids`, or, for a field merged with its only widget in one dictionary, that widget. **`PdfWidgetAnnotation.Field`** goes the other way, and **`PdfAcroField.IsTerminal`** says whether a field has no fields nested under it. (#146)
- **`PdfAcroField.PdfAcroFieldCollection` has a `Count` and a typed enumerator.** A form's `Fields` and a field's `Fields` can now be counted without LINQ, and `foreach (var field in form.Fields)` is typed as `PdfAcroField`, yielding exactly what the indexer returns.

#### Fixed

- **Reading a form's fields no longer takes their widgets away from the page.** Typing a widget as a field pointed the dictionary's reference at the new field object, so `page.Annotations[i]` stopped being the widget `AddWidget` had returned, and reading the fields and the annotations in turn made a new object each time. A value written through the widget-as-field went into the widget's `/V`, where no reader looks, and the field kept none. A field and an annotation can no longer be made out of one another; attempting it throws `InvalidOperationException`. (#146)
- **A field merged with its widget in one dictionary is one field object and one widget object, whichever is read first.** Such a dictionary is canonically the field, and `page.Annotations` and `field.Widgets` give a view of it that shares its entries and takes nothing over. Before, reading the page after the field retyped the dictionary as an annotation, and the field object made first then threw `NotImplementedException` for `/Kids` when asked for its `Fields`. (#146)
- **`AddWidget` on a field merged with its widget separates the two first.** A field read from a file can be one dictionary with its only widget, and adding a second widget beside it wrote a field that was an annotation with widget kids, which is not valid PDF. The first widget's entries now move into a dictionary of their own under `/Kids`, which takes the field's place in the page's `/Annots`, and the widget object a caller already held is that dictionary from then on. (#146)
- **A text field's value no longer jumps when the field is clicked into.** A viewer edits a field in the font size and colour its `/DA` names, and centres one line vertically; the appearance the library drew used a fixed 10 points, black, pinned to the top left, so the text moved and changed size as the field gained and lost the focus. `Font` and `ForeColor` now default to what `/DA` names (the field's own, an ancestor's or the form's), and one line is centred vertically two points in from the side. The same drawing now wraps a `MultiLine` value instead of running it off the side, puts a `Comb` field's characters one to each of `MaxLength` cells, draws a `Password` field's value as asterisks instead of in the clear, and aligns to `/Q`. (#155)
- **Setting `Value` on a text field or a list box redraws it.** `Text`, `SelectedIndex` and `SelectedIndices` redrew the field and `Value` did not, so a form filled through `AcroForm.Fields[name].Value` kept showing what it showed before in any viewer that draws the appearance stream. A list box also drops the `/I` that named the rows chosen before, and writes a new one for a multiple-choice list. (#151)
- **A malformed colour in a field's `/DA` no longer makes a choice field throw.** `/Helv 9 Tf 1 rg` names too few numbers for `rg`, and was parsed as though it did not, so every setter that redraws the field threw `FormatException`. An operator given the wrong number of operands is read as black. (#151)
- **A choice field with no font resolver registered no longer throws from its setters.** It draws its own appearance since #151, which needs a font; with none set on the field and no resolver, the value is written and the appearance left to the reader, as before. (#151)
- **`PdfTextField` no longer draws its value into the fields nested under it.** It drew into every kid with a `/Rect`, which included nested fields merged with their widgets, over their own values. It draws into its widgets alone. (#146)
- **LINQ over a form's fields, or a field's kids, now yields the typed `PdfAcroField` objects instead of `PdfReference`s.** The collection's indexer returned `PdfTextField`, `PdfCheckBoxField` and the rest, but every enumeration yielded the references underneath, so `OfType<PdfAcroField>()` was empty and `Cast<PdfAcroField>()` threw. Every enumeration now yields the same objects the indexer does, typing each field on the way as the indexer always has.
- **`PdfDocument.MakeAcroFormsReadOnly` walks the form's fields once.** It counted them with LINQ's `Count()` in its loop condition, enumerating the whole collection again on every iteration.
- **`PdfOutlineCollection.Insert` accepts an index equal to `Count` and appends the outline.** `IList<T>.Insert` requires this, but the collection threw `ArgumentOutOfRangeException`, including for `Insert(0, outline)` on an empty collection. The outline is now placed in the tree exactly as `Add` places it, and is saved at the end of its list.
- **An outline's colour no longer loses a level when the document is saved and reopened.** `/C` was read by truncating each component, so 127, written as `0.498`, came back as 126. It is now rounded, as an annotation's colour already was. A component outside 0 to 1 in an outline or annotation colour now reads as the nearest valid value, where it used to throw. (#159)
- **A highlight, underline, squiggly or strike-out annotation stamps its modification date when its quadrilaterals change.** `AddQuad` and `ClearQuads` redrew the annotation without touching `/M`, although a redaction's `AddQuad` already stamped it. (#197)
- **A text markup annotation left with nothing to mark removes its appearance.** With no quadrilaterals and an empty rectangle it kept showing the last appearance it drew; every other annotation that draws itself already removed its `/AP` when asked for nothing. (#197)
- **A negative `BorderWidth` is refused the same way on every annotation that draws itself.** Line, Square, Circle, FreeText, Ink, Polygon and PolyLine now all throw `ArgumentOutOfRangeException` with the message "A border cannot be narrower than nothing." and `ParamName` `value`. Ink, Polygon and PolyLine used to name `width`, and Line said "A line cannot…". (#197)

### Signatures & Metadata

#### Fixed

- **`Rfc3161TimestampProvider` no longer reads a response of any size, and its own client no longer follows redirects.** It read the whole body into memory with no limit and followed a 3xx to wherever it pointed. It now reads through the same guarded exchange as `OcspRevocationDataProvider`: headers first, then at most 1 MiB of body, with the client's `Timeout` over the whole exchange, body included. A larger or slower response fails the signing with an `InvalidOperationException` naming the authority. The client the provider makes for itself follows no redirect, because the caller named the authority it trusts; if your authority has moved, pass its new URI. A caller that supplies its own `HttpClient` keeps its own redirect policy. The OCSP provider also gains the timeout over the body, which it lacked before. (#166)

### Charts

#### Breaking

- **A chart line format that is not `Visible` is no line, on every axis and every element.** `Converter.ToXPen` turns such a format into a pen of width 0, which PDF draws as the thinnest line the device can. Only a column chart's value axis checked the width, so setting just `XAxis.LineFormat.Width` drew a hairline on the category axis and on a bar chart's value axis and nothing on a column chart's; tick marks, gridlines, the zero baseline and the legend border checked nothing. A pen of width 0 or less is now dropped wherever it is drawn, and a hidden legend border no longer takes up padding. A gridline format naming only a colour is therefore no longer drawn: set `Visible = true`, as a series already needed. (#173)

#### Fixed

- **The charting `XSeries` implements `IEnumerable`, so LINQ can be used over it.** It had a public `GetEnumerator` and no interface, so `foreach` compiled and every LINQ operator, `Cast` included, did not. It is non-generic like the package's other collections: `Cast<XValue>()` yields a blank as null and `OfType<XValue>()` leaves it out.
- **A data point's own `LineFormat` is applied the same way on column, bar and pie charts, and only once the caller has set it.** Each renderer turned it into a pen differently. A bar or pie point that sets only a width is now drawn at that width, a pie point is outlined at its own width rather than 1, and a pie point set not to be `Visible` is no longer stroked. Merely reading `Point.LineFormat` no longer takes the point's border away, which it always did on a column chart. An area chart is outlined once per series and takes no notice of a point's line format. (#171)
- **Stacked column and stacked bar charts with an explicit `MinimumScale` or `MaximumScale` agree on which segments to draw.** A segment is drawn only when the whole of it lies on the scale, from where it starts on its pile to where the pile reaches with it, and blanks are skipped. Stacked columns used to be drawn past the plot area, and stacked bars were shown or hidden by the segment's own value rather than by where its pile reached. Scales worked out from the data are unaffected. (#168)
- **A column or bar left out of a chart for being off the scale no longer has its data label drawn**, whether the chart is stacked or clustered. The label was written wherever the missing column would have ended, often outside the plot area. (#168)
- **On a scale entirely below zero, a clustered bar starts at the scale's minimum even when the bar before it is off the scale.** The start was carried from one bar to the next, so a bar after one that fell off the scale started from that bar's value, as a clustered column never did. (#170)
- **An exploded pie leaves a gap between its wedges.** The 2° gap only turned the whole pie, because `Math.Max` always returned the full sweep. Each wedge now gives up the gap, half from each side. A share narrower than two gaps keeps half of itself, and a single share is drawn whole. (#169)
- **A chart too small for its axes no longer strokes a border around a plot area that has no room**, just as it draws no wall there. (#172)
- **A null value-axis title caption is treated as no caption.** It threw `ArgumentNullException` while a column or bar chart was drawn; the category axis already skipped it. (#174)
- **A pie chart's legend no longer throws on an empty category collection or a blank category.** The first now numbers the entries and the second gives that entry no text, as the category axis does. (#175)
- **A bar chart labels a zero on the positive side of the axis at `InsideEnd` and `OutsideEnd`**, as a column chart does. It put the label on the negative side. (#196)
- **A clustered column or bar chart no longer throws `ArgumentException` for a value between zero and a minimum the caller set above zero.** Such a value, 1 on a scale from 2 to 5 for example, is left undrawn, as any value off the scale is. (#196)
- **A stacked column chart whose value axis has its minimum above its maximum draws nothing rather than throwing**, as a stacked bar chart already did. (#196)

### PinataLayout & DDL

#### Breaking

- **`Unit` implements `IEquatable<Unit>` (source).** Comparing units no longer boxes or uses reflection, and `GetHashCode` now hashes all three things `Equals` compares. Because `Unit` converts implicitly from `string`, `int` and `double`, `unit.Equals("3")` now parses the string and answers true for 3pt, as `==` always did. `unit.Equals(null)` now throws `ArgumentNullException`, as `unit == null` already did. Cast to `object` to keep the old answer, or test `IsEmpty`. `==` and `Equals` now always agree, so a unit holding NaN equals itself.

#### Fixed

- **A PinataLayout image whose source fails to open with anything but an `InvalidOperationException` gets a placeholder.** The exception used to escape formatting and end the whole render. It is now reported through `ImageFailed` as `ImageFailure.NotRead` and a placeholder is drawn, as it already was for the same exception thrown while the image was drawn. An `InvalidOperationException` is still reported as `InvalidType`. (#131)
- **A list that starts a new section is tagged in that section.** When a section ended with a list and the next began with one of the same kind and level, the tagger saw that the list's parent had changed, closed the old list and opened a new one, but under the previous section's `/Sect`. So the new section's list appeared in the structure tree inside the section before it. It is now opened under its own section. (#137)
- **An object added to a PinataLayout collection through its non-generic `IList` belongs to that collection.** `IList.Add`, `Insert`, `Remove` and the indexer setter on `DocumentObjectCollection` went straight to the list underneath. They set no parent, reset no cached values, and skipped what a collection does on `Add` or `InsertObject`. So `((IList)table.Rows).Add(row)` gave the row no cells, and `Styles` accepted a paragraph. They now go through the typed members, as the charting collections already did. One thing a caller can see: an argument that is not a `DocumentObject` now throws `ArgumentException` from `Add`, `Insert` and the indexer. It used to be stored and throw later, on read. `Contains`, `IndexOf` and `Remove` still answer false, -1 and nothing for one. A null is still accepted, as the typed members accept it.
- **An underline or strikethrough whose dash style changes between adjacent runs is drawn in each run's own style.** The two pen comparisons that decide where a rule ends compared colour and width only, so a dotted run followed by a dashed one was drawn as one dotted line. (#162)
- **An upward text frame restores every graphics state it saves.** It saved the state twice and restored it once, so everything drawn after it on the page ran one `q` deeper. (#163)
- **A PinataLayout chart no longer gives every point an empty line format.** The mapper wrote one for every point, always with a solid dash, so a dashed series' columns, bars and sectors were outlined solid. A point's line format is now mapped only when the document set one. (#171)
- **A CMYK colour in a PinataLayout document drawn in RGB has the RGB that `XColor.FromCmyk` gives.** The DOM's `Color` used its own copy of the conversion, which rounded black up by half a level, so about half of all K values came out one level darker. K = 0 is one of them, so CMYK white was 254, 254, 254. (#186)

### API & Packaging

#### Deprecated

- **`PdfDocument.CanSave(ref string message)`.** Use `CanSave()`, which returns a `PdfSaveCheck` carrying the reason as well as the answer. It behaves as before and will be removed.

#### Removed

- **`PdfDocument(string filename)`.** It built a document and then threw `NotImplementedException`, so no caller ever got one from it. Use `new PdfDocument()` and `Save(path)`.

#### Fixed

- **Every package is built with `Microsoft.Sbom.Targets` 4.1.12.** Each project's override to 4.1.12 was evaluated before the shared reference it meant to change, so it never applied and every package was built with 4.1.5. The version is now set once in `Directory.Build.targets`. (#167)

## [0.3.0] - 2026-09-22

### PDF Reader & Writer

#### Added

- **RunLengthDecode streams are now decoded and encoded.** `/RunLengthDecode` and its `RL` abbreviation are supported through `Filtering.GetFilter` and `Filtering.RunLengthDecode`, following ISO 32000-1 7.4.5. Truncated streams return the data decoded so far rather than throwing.

- **TIFF Predictor 2 streams are now decoded.** Flate and LZW streams using `/Predictor 2` are supported for 1, 2, 4, 8 and 16 bits per component, with any number of colours. Sixteen-bit samples are treated as big-endian and each row restarts prediction. (#79)

- **Imported pages preserve more page-level PDF state.** Page import now keeps `/Group`, `/UserUnit`, `/Tabs`, `/Trans` and `/Dur` in addition to the resources, contents, boxes, rotation and annotations already imported. `/UserUnit` raises the output to PDF 1.6 and `/Tabs` to PDF 1.5 where required. `/Tabs /S` is deliberately dropped because page import does not bring the structure tree with it. Entries tied to source-document structures, including `/StructParents`, `/B`, `/AA`, `/Metadata`, `/PieceInfo` and `/SeparationInfo`, are also deliberately left behind. (#86)

- **`ContentReader` preserves inline images when content is written back.** Inline images are now represented as `CInlineImage` and emitted as `BI … ID … EI`, preserving their dictionary entries and raw data. The end marker is still detected by scanning for `EI`, so binary data containing those bytes can still be misidentified. Truncated inline images no longer make the parser loop indefinitely. (#84)

- **Font embedding restrictions can optionally be enforced.** Set `PdfDocumentOptions.RespectFontEmbeddingRestrictions` to honour the font OS/2 `fsType` embedding permissions. Restricted License and Bitmap Embedding Only faces are refused; No Subsetting faces are embedded whole. Preview & Print, Editable and Installable faces remain embeddable. The option is off by default. (#78)

- **Resource deduplication is available at save time.** `PdfDocumentOptions.DeduplicateResources` can write a single structurally identical copy of fonts, images, forms, colour spaces, graphics states, patterns, shadings and content streams. Pages, annotations, fields, structure elements, optional-content groups and signatures are never merged. Append-mode documents refuse the option. See `docs/specs/resource-deduplication.md`. (#68, empira/PDFsharp#275)

#### Changed

- **Malformed PDF dates no longer rely on exceptions for normal fallback.** Invalid dates still fall back as before, but parsing no longer throws and catches internally or trips Debug assertions. (#83)

- **Composite-font `/W` arrays are written more compactly.** Consecutive glyph widths are emitted as `c [w1 w2 …]` runs rather than one `c [w]` entry per glyph. Glyph metrics are unchanged. (#81)

- **Names are escaped consistently when written.** Delimiters, `#`, and bytes outside `!` through `~` are written as `#xx`. The underlying name bytes do not change. (#59)

- **`/Producer`, default `/Creator`, and XMP `pdf:Producer` use the actual PdfPinata version.** The value now resembles `PdfPinata 0.2.1 (https://github.com/PinataLabs/PdfPinata)` instead of the old inherited PDFsharp version string. `ProductVersionInfo.Producer` is now a static property and `Producer2` has been removed.

#### Fixed

- **PDF names containing characters beyond Latin-1 are written as UTF-8 bytes.** Previously such names could be corrupted into ambiguous `#xxxx` text. Names read from an existing PDF continue to round-trip byte-for-byte. (#87)

- **Names in content streams are escaped when written back.** A name such as `/A#20B` no longer becomes separate tokens after a read/write cycle, and delimiters inside names are escaped consistently. (#87)

- **Malformed `#` sequences in names no longer abort parsing.** A `#` is treated as an escape only when followed by two hexadecimal digits; otherwise it is kept literally, both in document objects and content streams. (#83, #84)

- **Cross-reference stream offsets beyond 2 GiB are read without truncation.** Offsets now use the full width declared by `/W`, including five-byte fields. (#83)

- **PDFDocEncoding now decodes as well as encodes according to ISO 32000-1 Annex D.** Undefined codes map to U+FFFD and several previously incorrect encoder mappings are corrected. (#82)

- **ASCIIHexDecode honours `>` wherever it appears and rejects non-hex data.** Odd trailing digits are padded with zero as required, and the caller's buffer is no longer modified. (#79)

- **ASCII85Decode rejects `z` inside a partial group.** Such data previously decoded silently out of step. (#79)

- **Indirect `/Filter` and `/DecodeParms` objects are followed correctly.** Indirect parameter arrays and indirect/null elements are also handled. (empira/PDFsharp#323, #59)

- **`CSequence.Add(CArray)` adds the array as one operand.** The array is no longer spread into the surrounding sequence. (empira/PDFsharp#300, #59)

- **Incremental updates involving cross-reference streams are read from the newest revision.** Newer full objects now correctly override older compressed objects, and object-number reuse by cross-reference streams no longer causes stale objects to win. (empira/PDFsharp#353, empira/PDFsharp#213)

- **Hybrid-reference files now load objects described by `/XRefStm`.** The supplemental cross-reference stream is processed alongside the classic table, so compressed objects are no longer silently lost. Under `Strict`, an unreadable `/XRefStm` raises `PdfReaderException`; under `Moderate`, the supplemental stream is ignored and the document opens using only the classic table. See `docs/specs/hybrid-reference-files.md`. (empira/PDFsharp#388)

- **A looping `/Prev` chain no longer hangs `PdfReader.Open`.** `Strict` throws; `Moderate` reads each section once. (#66)

- **Incremental saves preserve valid `/Size` values and do not reuse freed object numbers.** New numbering begins from the largest `/Size` declared by any revision. (#63)

- **Incremental saves to files indexed by cross-reference streams now write a valid appended revision.** The appended revision uses its own cross-reference stream with `/Index` and `/Prev`; classic-table files continue to append in the classic format. (#55)

- **An unencrypted document stays unencrypted when `SecuritySettings` was merely read before `SaveIncremental` or signing.** (#65)

- **Streams assigned through `PdfDictionary.Stream` get a correct `/Length`.** Shared or cloned stream data is rechecked when written, and assigning `Value` on a cloned stream no longer throws `NullReferenceException`. (#93)

- **`PdfInteger` and `PdfUInteger` no longer convert to `DateTime.MinValue`.** `IConvertible.ToDateTime` now throws `InvalidCastException`, matching the underlying integer types. `PdfLong` continues to interpret its value as ticks. (#87)

- **A UTF-16 literal string in a content stream resolves escapes before decoding.** Line continuations no longer shift subsequent UTF-16 bytes or cause the parser to run past the closing delimiter. (#84)

- **Content-stream strings preserve their type on round-trip.** `CString` can now write hex strings, Unicode literal strings and Unicode hex strings, and `CParser` records the original string type instead of treating all strings as plain literals.

### Pages & Documents

#### Added

- **Page box presence and effective values are exposed.** `PdfPage.HasMediaBox`, `HasCropBox`, `HasBleedBox`, `HasTrimBox` and `HasArtBox` report whether each box exists. `EffectiveCropBox`, `EffectiveBleedBox`, `EffectiveTrimBox` and `EffectiveArtBox` apply ISO 32000-1 defaults and clip to the media box. See `docs/specs/page-boxes.md`. (#58)

- **Page lifecycle events are available.** `PdfDocument.PageAdded`, `PageRemoved` and `PageGraphicsCreated` allow consumers to react to page changes or draw recurring page content such as headers and watermarks. A `PageGraphicsCreated` handler runs inside a saved graphics state. (#60)

#### Fixed

- **Reading a missing page box no longer creates it.** Missing boxes still read as empty rectangles, but simply accessing them no longer writes `[0 0 0 0]` into the page dictionary. Invalid box values are treated as missing rather than throwing `InvalidCastException`. (#58)

- **A removed page can be reinserted after a save.** Reinserted pages and their dependent objects receive valid object numbers instead of colliding with numbers reassigned during the earlier save. (#93)

- **Page outline style and colour entries respect the output PDF version.** `/F` and `/C` are written only when the saved version supports them, and `Regular` removes `/F` on the next save.

- **PDF/A-1 now rejects unsupported page-level features.** A page transparency `/Group`, `/UserUnit` or `/Tabs` is refused under PDF/A-1 where those features exceed the profile's PDF 1.4 limits. (#86)

### Drawing & Graphics

#### Added

- **Gradient extension can be controlled.** `XBaseGradientBrush.ExtendLeft` and `ExtendRight` control whether a gradient continues beyond its start and end using the endpoint colour, mapping to a shading dictionary's `/Extend`. Both default to `false`. (empira/PDFsharp#321)

- **Spot colours are supported.** `XSpotColor` defines a named colourant with a DeviceCMYK, DeviceRGB or DeviceGray alternate, and `XColor.FromSpot(spot, tint)` can be used with brushes, pens and text. Each colourant is emitted as one `/Separation` colour space per document. Conflicting definitions of the same name are refused. See `docs/specs/spot-colours.md`. (#67, empira/PDFsharp#201)

- **`XGraphics.BeginContainer` accepts every `XGraphicsUnit`.** The source rectangle is interpreted using the supplied unit; drawing inside the container remains in points.

#### Changed

- **`XImage.FromFile` uses a single overload.** `XImage.FromFile(string, PdfReadAccuracy = PdfReadAccuracy.Strict)` replaces the two previous overloads. Existing source calls still compile; assemblies built against the previous signatures must be rebuilt.

- **`XGraphicsPath.StartFigure` now starts a real new figure.** The following segment no longer joins the previous figure. `AddPath(..., connect: true)` also honours the figure boundary.

#### Fixed

- **Fully transparent pens and brushes remain transparent when they are the first colour drawn or follow a gradient.** Previously an initial alpha-zero state could suppress the required `/CA` or `/ca`. (empira/PDFsharp#281, #59)

- **Dashed pens recalculate dash lengths when the pen width changes.** Standard dash patterns now track the pen width, and identical custom patterns are not redundantly emitted.

- **Appended world transforms render where `XGraphics.Transform` reports them.** Append-order transforms are converted to the equivalent prepend matrix before emitting PDF `cm`. Appending to a non-invertible matrix throws without changing the graphics state.

- **Gradient brush transforms are honoured.** Brush translation, scaling, rotation, multiplication and direct `Transform` assignment now affect rendering. Radial gradients use the pattern matrix when required to represent non-circular transformed rings.

- **`XGraphics.DrawImage(image, destRect, srcRect, srcUnit)` now crops to `srcRect`.** The requested source region is scaled to the destination and clipped correctly.

- **`XGraphics.BeginContainer` maps the source rectangle correctly when the source does not start at the origin.**

- **`XGraphics.WriteComment` comments every line.** Bare carriage returns no longer allow following text to escape the PDF comment.

### Fonts & Text

#### Changed

- **WinAnsi fonts with CFF outlines are no longer labelled as subsets when they are embedded whole.** (#78)

#### Fixed

- **`AddCharacter(char)` draws non-ASCII characters correctly.** Characters such as `é`, `ß`, `Ω` and `€` are no longer reduced to a low byte and decoded as broken UTF-8. Astral code points supplied through `SymbolName` are emitted as surrogate pairs where needed.

- **Non-breaking blanks render as U+00A0 and suppress line breaking around them.** `Character.NonBreakableBlank` / `HardBlank` now occupy normal space width. A token too long for any line can still be broken as a last resort.

- **Undefined `SymbolName` values are rejected.** Values naming no known symbol now throw `ArgumentException` instead of being written as MDDDL that the reader cannot read back. (#92)

### Annotations & Forms

#### Breaking

- **Iterating `PdfAnnotations` now yields `PdfAnnotation` instead of `PdfItem`.** `PdfAnnotations.GetEnumerator` returns `IEnumerator<PdfAnnotation>`, matching the indexer and eliminating the cast normally required in `foreach`. Enumeration through `IEnumerable<PdfItem>` still yields the annotations themselves. (#81)

#### Added

- **Annotations read from a file are wrapped as the class matching their `/Subtype`.** Nineteen annotation subtypes now return typed objects instead of `PdfGenericAnnotation`. The wrapping itself does not rewrite the appearance stream. (#60)

- **More annotation types are implemented.** `PdfInkAnnotation`, `PdfPolygonAnnotation`, `PdfPolyLineAnnotation`, `PdfCaretAnnotation`, `PdfRedactAnnotation` and `PdfPopupAnnotation` are available. Redaction annotations mark regions; they do not remove underlying content. (#60)

- **`PdfMarkupAnnotation` is available as the common markup base class.** It adds `Popup`, `InReplyTo`, `ReplyType`, `RichText` and `Intent`. (#60)

#### Changed

- **`PdfSquareCircleAnnotation.Interior` and `BorderWidth` read directly from `/IC` and `/BS`.** An annotation loaded from a file now reports the values stored in the PDF. (#60)

#### Fixed

- **Annotation colours stored as indirect arrays are resolved correctly.** (#81)

- **Links loaded from existing files no longer gain a zero-width border just because they were saved again.** (#60)

- **Outline collections reject invalid ownership and cycle arrangements.** An outline cannot belong to multiple lists, another document, itself, or one of its descendants. Removing or replacing entries no longer leaves broken links behind, and accessing `document.Outlines` no longer creates an unwritten catalog reference. (#93)

- **Form fields inherit `/FT` and `/Ff` from their ancestors.** Child fields now detect inherited field type and flags, including radio groups, and writing one flag to a child preserves the inherited flags it already had. (#75)

- **Read-only fields reject values consistently across all typed setters.** `Text`, `Checked`, radio/list/combo selection and `SelectedIndices` now enforce the same read-only rule as `PdfAcroField.Value`. (#62)

### Signatures & Metadata

#### Added

- **XMP metadata handling is configurable.** `PdfDocumentOptions.MetadataStrategy` can be `KeepExisting` (default), `AutoGenerate`, or `NoMetadata`. A PDF/A or PDF/UA claim cannot use `NoMetadata`. (#60)

- **Signature-field lock and seed dictionaries are modelled.** `PdfSignatureField.Lock` and `SeedValue` expose `/Lock` and `/SV` through `PdfSignatureFieldLock`, `PdfSignatureSeedValue` and `PdfCertificateSeedValue`. `PdfSignatureOptions.LockAction` and `LockFields` can write `/Lock`, add a `/FieldMDP` reference and mark covered fields read-only in the signed revision. Seed values are persisted but not yet enforced by the signer. (#60)

- **Signature annotation flags are configurable.** `PdfSignatureOptions.AnnotationFlags` writes the widget `/F` value. PDF/A profiles reject flags they forbid. The default remains `Print`. (#65, empira/PDFsharp#157)

#### Changed

- **`PdfDocumentOptions.WriteXmpMetadata` is retained as the older name for `MetadataStrategy = AutoGenerate`.** (#60)

#### Fixed

- **Wrapping a read object in a typed class no longer loses its dirty state.** Incremental saves now include changes made before the wrapper was created, including AcroForm changes used by signature locking. (#60)

### Charts

#### Added

- **Combination charts can contain stacked columns.** `ColumnStacked2D` can be combined with line or area series. The value axis accounts for stacked totals, and the legend orders stacked series from the top of the stack downward. Clustered and stacked columns cannot share one chart and are rejected explicitly. (#77)

#### Fixed

- **Chart text inherits colour, bold and italic correctly.** Axis titles, tick labels, legends and data labels inherit unset font properties from the chart font. An explicit `false` for bold/italic remains explicit. Series data labels inherit from the chart data label before falling back to the chart font. (#101)

- **Series data labels inherit chart-level label properties individually.** Format, position, type and font are now inherited property-by-property instead of a series-level label replacing the chart label wholesale. (#101)

- **Axis-title and tick-label fonts no longer wipe out style-level bold, italic or colour.** Flattening fills unset values from the named style or chart font before mapping. (#101)

- **Chart collections now work through non-generic `IList`.** `Add`, `Insert`, `Remove`, `RemoveAt`, `Contains` and `IndexOf` delegate to the typed members. Invalid values are rejected with `ArgumentException`. (#77)

- **Category labels are no longer repeated once per `XSeries`.** Multi-series charts label the category axis from the first series only, matching Excel and existing pie-legend behaviour. (empira/PDFsharp#286)

- **Wide legends wrap instead of running off the chart.** Top/bottom legends create multiple centred rows, oversized entries wrap within their own entry, and embedded line breaks are honoured. See `docs/specs/charting-renderer-findings.md`. (empira/PDFsharp#306)

- **Hidden chart lines are actually hidden.** A `Visible = false` line format no longer becomes a PDF hairline. The fix applies to line, area, bar and pie outlines and their legend keys. (empira/PDFsharp#287)

### PinataLayout & DDL

#### Breaking

- **`DdlReaderError` is read-only.** `ErrorLevel`, `ErrorMessage`, `SourceFile`, `SourceLine` and `SourceColumn` are now `readonly`, and `ErrorNumber` is no longer public.

#### Added

- **The DDL reader warns when a footnote contains another footnote.** The document is still read, but `DdlReaderErrors` receives a warning at the inner `\footnote`, because nested footnotes cannot be rendered.

- **The DDL reader warns when a hyperlink has no `Name`.** The hyperlink and its text are still parsed, but the reader records the missing obligatory property. The old misspelling “Obigatory” has also been corrected.

- **OMR standard mark distances are exposed.** `CodeOmr.StandardMarkDistance` maps `MarkDistance.Inch1_6`, `Inch2_6` and `Inch2_8` to the corresponding `MakerDistance`, and `CodeOmr.ToUnit` converts an enum value to its length.

#### Changed

- **`ThickThinBarCode` is renamed `TwoWidthBarCode`.** It remains the base for Code 39 and Interleaved 2 of 5. Only callers naming the base type itself need to change.

#### Fixed

- **Invalid `\fontsize` values become DDL reader errors instead of terminating the read.** The remaining document is still parsed and the inner text remains available.

- **MDDDL `\x` escapes are decoded correctly.** One or two hexadecimal digits produce the named character without consuming the following source character.

- **Oversized or malformed MDDDL colour numbers are reported as parser errors.** The reader now recovers instead of leaking `OverflowException` or `FormatException`. (#94)

- **A lone carriage return is recognised as a line ending in MDDDL.** CR, LF and CRLF inputs now behave consistently for comments, line numbers and text. (#94)

- **A PinataLayout image's `PictureFormat` crop performs a real crop.** Crop values no longer shrink the layout rectangle and squeeze the whole image into it.

### API & Packaging

#### Breaking

- **`PdfSharpException` is renamed `PdfPinataException`.**

  **Migration:** replace catches or references to `PdfSharpException` with `PdfPinataException`. `PdfReaderException` and `ContentReaderException` keep their names.

#### Deprecated

- **`XBitmapImage.CreateBitmap`.** It creates an empty bitmap intended only for the removed `XGraphics.FromImage` path and will be removed.

- **`PdfCustomValueCompressionMode`.** Its only consumer has been removed and the enum will be removed.

- **`XPoint + XSize`.** Treat displacement as an `XVector` instead:
  `point + new XVector(size.Width, size.Height)`.

#### Removed

- **`XGraphics.FromImage`.** It always returned `null`; use an `XForm` when you need drawable content that can later be placed like an image.

- **`PdfWriterLayout.Verbose`.** Nothing selected it, so the unused verbose writer layout and its formatting behaviour have been removed.

- **`PdfCustomValue`'s parameterless constructor and `CompressionMode` field.** Construct custom values from bytes with `new PdfCustomValue(byte[])`.

### Documentation & Samples

#### Added

- **A Gradients demo and documentation page** covering linear and radial gradients, two-centre radial gradients, extension and brush transforms.

---

## [0.2.1] - 2026-09-21

### PDF Reader & Writer

#### Added

- **`PdfDictionary.PdfStream.ExternalFile` exposes the file specification named by `/F`.** It reports where an external stream says its real data lives without attempting to fetch that file. See `docs/specs/external-file-streams.md`.

#### Fixed

- **Large-file trailer scanning no longer reads the entire file into a string.** `Lexer.FindLastMarker` scans backwards in reused 64 KiB blocks. A 1 GiB file with a distant `startxref` now opens without the previous huge allocation, and files larger than `int.MaxValue` are no longer rejected solely for their length. See `docs/specs/large-file-trailer-scan.md`. (empira/PDFsharp#390)

- **Files with no `startxref` now fail with the intended reader error.** The parser no longer attempts to assign stream position `-1`.

- **External-file streams no longer make the whole document unreadable.** A stream carrying `/F` is parsed normally and the external-file entries are preserved without attempting to fetch the target file.

- **Page trees that loop or are pathologically deep are rejected safely.** An ancestor loop is reported, depth beyond 256 is refused, and traversal also has a work bound to prevent exponential walks through repeatedly shared nodes. (empira/PDFsharp#361)

### Images

#### Fixed

- **Partly transparent images are no longer accidentally erased by a second transparency mask.** A 1-bit `/Mask` is no longer written when an 8-bit `/SMask` already represents the alpha. Binary transparency and pre-PDF-1.4 fallback continue to use the stencil mask. (empira/PDFsharp#392)

### Pages & Documents

#### Fixed

- **`PdfDocument(Stream)` writes a valid PDF version header.** Documents built directly on an output stream no longer save as `%PDF-0.0`.

- **A section that specifies a page format plus one explicit dimension keeps the dimension it set.** The flattening visitor no longer swaps the width/height decision.

### PinataLayout

#### Added

- **Merged-cell bounds are exposed.** `Cell.MergedRightColumnIndex` and `Cell.MergedBottomRowIndex` report the last table column and row actually covered, clamped to the table edge.

#### Fixed

- **A cell merged beyond the table edge is clamped instead of throwing during layout.** Borders, widths and structure spans use the effective table edge rather than indexing beyond the available rows or columns.

- **`Section.LastParagraph` and `Section.LastTable` return `null` for an empty section.**

- **Empty inline paragraph elements render safely.** `AddFormattedText("")`, empty hyperlinks and similar leaves no longer make the parent walk run off the document. (PinataLabs/PdfPinata#45)

### Drawing & Units

#### Changed

- **`XUnit` implements `IEquatable<XUnit>`.** This avoids boxing in generic equality scenarios and aligns `unit.Equals(72)` with `unit == 72` through the implicit numeric conversion. String equality deliberately remains `false` instead of triggering string parsing.

#### Fixed

- **`XUnit.Presentation` stores values in presentation units.** It no longer records the unit as points.

### PDF Objects

#### Fixed

- **Indirect booleans are written as lowercase `true` and `false`.**

- **`ViewerPreferences.Direction` reads `L2R` and `R2L` back correctly.**

---

## [0.2.0] - 2026-09-20

### API & Packaging

#### Breaking

- **The library, packages and namespaces are renamed.** PdfSharpCore becomes **PdfPinata** and MigraDocCore becomes **PinataLayout**. The corresponding NuGet IDs and root namespaces change in lockstep.

  **Migration:** replace `PdfSharpCore` with `PdfPinata` and `MigraDocCore` with `PinataLayout` in package references and `using` directives.

- **`PdfDocumentOpenMode` is now enforced where modification is requested.** Operations that mutate a document now reject `ReadOnly` and `Import` documents instead of silently accepting changes that could not be saved correctly.

  **Migration:** open documents with `Modify`, or with `Append` for incremental-save scenarios, before calling mutating APIs.

- **`ImageSource.IImageSource.SaveAsPdfBitmap(MemoryStream)` is replaced by `PixelBuffer GetPixels()`.** `XImage.AsBitmap()` is likewise replaced by `XImage.GetPixels()`. `PixelBuffer` is tightly packed, top-down, straight-alpha BGRA.

- **`IFontFallback.FamiliesFor` now accepts an `int codePoint` instead of `char`.** This allows fallback decisions for supplementary-plane Unicode characters.

- **`IXGraphicsRenderer.DrawString` now accepts an `XPen` before the `XBrush`.**

  **Migration:** `DrawString(s, font, brush, rect, format)` becomes `DrawString(s, font, null, brush, rect, format)`.

- **MigraDoc `PageFormat.B5` now means ISO B5.** The previous value was JIS B5. Use `PageFormat.JISB5` to preserve the old physical page size.

- **`PdfPage.Size`, `Width` and `Height` refuse to resize a page that already has content.**

  **Migration:** use `page.Resize(...)`. `PageResizeOptions.Crop` provides crop semantics where needed.

- **A non-contiguous `HeadingFormat` row in a PinataLayout table now throws during formatting.** Repeating headings must be a contiguous run beginning at row 0.

#### Removed

- **`PdfDocumentOpenMode.InformationOnly`.** It never implemented a partial metadata-only read. Use `ReadOnly`.

- **`PdfPinata.Text.ScriptItemizer` and `ScriptRun` are internal.** Use `TextItemizer.Itemize`, which combines script and bidirectional itemisation correctly.

- **`PdfDocumentOptions.EnableCcittCompressionForBilevelImages`.** The encoder it controlled was unreachable.

- **Unused CCITT encoding and legacy image-import infrastructure.** This includes the internal fax encoder, `PdfImage.ReadIndexedMemoryBitmap`, and the unused `Drawing.Internal` importer subsystem.

### Fonts & Text

#### Added

- **Supplementary-plane Unicode characters can be drawn.** The font reader now supports `cmap` format 12, and font fallback can receive complete Unicode code points rather than surrogate halves.

- **Bold simulation is decided per face.** Fallback faces with real bold variants are no longer simulated merely because the original face required simulation.

- **`XTextFormatter` supports text flow around obstacles.** Obstacles expose free horizontal runs for each line band through `IFlowObstacle`; `RectangleObstacle` is included. The formatter currently chooses the widest available run for each logical line.

- **Drop caps are supported by `XTextFormatter`.** `XDropCap` sizes the initial character by line depth and can use an `IGlyphOutlineProvider` for ink-aligned placement.

- **`XTextFormatter` gains paragraph and multi-column options.** Added options include `LineBreak`, `Indent`, `IndentAllLines`, `ParagraphGap`, `LineGap`, `Ellipsis`, `Rotation`, `Columns` and `ColumnGap`.

- **Text state is available on `XStringFormat`.** `CharacterSpacing`, `WordSpacing`, `HorizontalScaling`, `TextRise` and `ObliqueAngle` are written using the corresponding PDF text operators and text matrix.

- **Text can be stroked as well as filled.** `DrawString` overloads accept an `XPen` and/or `XBrush`.

- **Text underline and strikeout styles are available on `XStringFormat`.** `DecorationColor` can override the rule colour.

- **Additional line-alignment modes are available.** `XLineAlignment.Hanging`, `Ideographic` and `SvgMiddle` are measured against the text rather than the layout rectangle.

- **`XGraphicsPath.AddString` now produces real glyph outlines.** Geometry is provided through `IGlyphOutlineProvider` / `GlobalFontSettings.GlyphOutlineProvider`, with Skia and ImageSharp implementations.

#### Changed

- **`XGraphics.MeasureString(text, font, stringFormat)` honours the supplied format.** Text-state properties that affect width are now included in measurement.

- **`XLineAlignment.BaseLine` accepts layout rectangles with non-zero height.** The baseline remains anchored to the rectangle's top edge and height is ignored.

#### Fixed

- **Bold simulation no longer measures multi-line text as though all characters belonged to the widest line.**

- **Line feeds no longer consume character spacing.**

### Pages & Documents

#### Added

- **Existing pages can be resized.** `PdfPage.Resize` and `PdfDocument.ResizePages` support `Fit`, `Fill`, `Stretch` and `None`, alignment, margins, auto-rotation, annotation movement and destination movement. Signed, encrypted and tagged documents are refused. See `docs/specs/page-resize.md`.

- **Many additional `PageSize` values are available.** Added A7–A10, 2A0/4A0, B6–B10, C0–C10 and SRA0–SRA4, among others.

- **Many additional PinataLayout `PageFormat` values are available.** The enum now aligns with the expanded `PageSize` set while preserving MigraDoc's named-unit behaviour.

- **Named destinations are supported.** `PdfDocument.NamedDestinations`, `PdfPage.AddNamedLink`, `PdfLinkAnnotation.CreateNamedLink`, and matching `XGraphics` helpers allow links to survive page insertion or reordering.

- **Trimmed pages can reserve mark margins and draw crop marks.** `PdfPage.MarkMargins` and `DrawCropMarks` provide sheet room beyond the bleed and trim boxes.

#### Changed

- **Tagged content no longer nests MCID marked-content sequences.** An outer sequence is suspended and resumed around an inner content item.

- **Pages using `TrimMargins` now write nested page boxes correctly.** `/MediaBox` contains `/BleedBox`, which contains `/TrimBox`, with `MarkMargins` supplying sheet room for crop marks.

#### Fixed

- **Trimmed pages no longer grow each time they are saved.**

- **Uneven top and bottom trim margins are applied to the correct PDF rectangle edges.**

- **`PageSize.Executive` now measures 7.25 × 10.5 inches (522 × 756 points).**

- **Page transparency groups are emitted only when required.** Opaque pages no longer receive an unnecessary `/Group << /S /Transparency /CS /DeviceRGB >>`, while imported pages that already have a group keep it.

- **Page transparency groups survive page import through `XPdfForm`.**

- **PDF `null` values in transparency-related entries are treated as absent rather than as real masks or groups.**

### Drawing & Graphics

#### Added

- **Linear and radial gradients support alpha.** Transparent gradient stops are represented through a luminosity soft mask while opaque gradients remain unchanged.

#### Fixed

- **RGB gradients are valid PDF shadings.** Alpha is no longer incorrectly written as a fourth DeviceRGB component; it belongs in the soft mask.

### PinataLayout

#### Added

- **Text can flow beside shapes.** `WrapFormat.Style` adds `Left`, `Right`, `Largest` and `Both` for side-wrapping floating shapes. `DistanceLeft`, `DistanceRight`, `DistanceTop` and `DistanceBottom` define the obstacle margins.

- **MigraDoc fields can be evaluated without rendering.** `FieldEvaluator.Evaluate` and `FieldEvaluationContext` return field text when it can be known, while `FieldEvaluator.IsField` identifies evaluatable objects. `NumberFormatter` is public.

- **A Bleed demonstration page** shows trim, bleed, crop marks and page boxes.

#### Fixed

- **A heading containing an `InfoField` includes that field's text in its outline entry.**

- **A drop cap that is too wide for its column pushes text below itself instead of throwing text outside the column.**

### PDF/UA & Accessibility

#### Added

- **PDF/UA-1 heading levels cannot skip forward.** For example, H1 followed by H3 is rejected at save time.

- **`Footnote.Identifier` exposes the tagged-document `/ID` used for a note.** When unset, the renderer continues to generate identifiers.

### Images

#### Added

- **Image backends expose pixels directly through `PixelBuffer`.** This replaces the internal round-trip through an in-memory BMP file.

### Documentation & Samples

#### Added

- **Documentation and samples for page resize, bleed/crop marks, text flow, drop caps, gradients and related drawing features.**
