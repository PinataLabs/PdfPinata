# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), with changes grouped by product area before change type, and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [Unreleased]

### PDF Reader & Writer

#### Fixed

- **A literal string continued onto the next line with a backslash before CR LF no longer keeps the LF.** A CR LF is one end-of-line marker (ISO 32000-1 7.2.3), and the backslash is ignored together with the whole of it (7.3.4.2). Both the document lexer and the content-stream lexer dropped the CR and kept the LF as the first character of the next line. A backslash before a lone CR or a lone LF was already read correctly. (#131)

### Pages & Documents

#### Added

- **`PdfDocument.CanSave()` returns a `PdfSaveCheck`.** Its `CanSave` says whether the document can be saved and its `Reason` says why not, or is null when it can. It replaces `CanSave(ref string message)`, which is now deprecated.

### Drawing & Graphics

#### Fixed

- **An arc with a sweep of 0 is drawn as a single curve that stays at its start, and always returns.** `XGraphics.DrawArc` and `XGraphicsPath.AddArc` never returned for a zero sweep starting at exactly 360 or -360: the quadrant the arc ends in came out as 4, and the walk through quadrants 0 to 3 kept adding curves until the process ran out of memory. Off a quadrant edge, such as a start of 45, both control points were 0/0 and the content-stream writer refused the NaN, so `DrawArc` threw at once and a path holding the arc threw when it was drawn. On any other quadrant edge the arc was cut as though it crossed that edge, so a start of 90 drew the whole ellipse. A zero sweep, or one too small to move the start angle (such as float cancellation leaves), is now one piece from its start to its start, whose control points lie at that point. Arcs with a non-zero sweep are unchanged. (#129, #130)

### PinataLayout & DDL

#### Breaking

- **`Unit` implements `IEquatable<Unit>` (source).** Comparing units no longer boxes or uses reflection, and `GetHashCode` now hashes all three things `Equals` compares. Because `Unit` converts implicitly from `string`, `int` and `double`, `unit.Equals("3")` now parses the string and answers true for 3pt, as `==` always did. `unit.Equals(null)` now throws `ArgumentNullException`, as `unit == null` already did. Cast to `object` to keep the old answer, or test `IsEmpty`. `==` and `Equals` now always agree, so a unit holding NaN equals itself.

#### Fixed

- **A PinataLayout image whose source fails to open with anything but an `InvalidOperationException` gets a placeholder.** The exception used to escape formatting and end the whole render. It is now reported through `ImageFailed` as `ImageFailure.NotRead` and a placeholder is drawn, as it already was for the same exception thrown while the image was drawn. An `InvalidOperationException` is still reported as `InvalidType`. (#131)

### API & Packaging

#### Changed

- **The source is cleaned up against JetBrains InspectCode.** Backing fields are now auto-properties or use the `field` keyword. Switch statements are now switch expressions, and casts and null checks are now patterns. Unused usings, casts and assignments are removed, and fields that are never reassigned are `readonly`. Public types and members, and the documents they write, are unchanged. Each suggestion was judged on its own, and the pull requests list the ones left alone and why. (#116, #117, #118, #119, #121, #123, #124, #125, #126)

#### Deprecated

- **`PdfDocument.CanSave(ref string message)`.** Use `CanSave()`, which returns a `PdfSaveCheck` carrying the reason as well as the answer. It behaves as before and will be removed.

#### Removed

- **`PdfDocument(string filename)`.** It built a document and then threw `NotImplementedException`, so no caller ever got one from it. Use `new PdfDocument()` and `Save(path)`.

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
