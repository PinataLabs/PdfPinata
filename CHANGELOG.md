# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project
adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

This file starts at the entry below. Changes before that point are recorded only in the git history.


## [Unreleased]

### Added

- **Streams compressed with RunLengthDecode are decoded.** `Filtering.GetFilter` used to recognise
  `/RunLengthDecode` and answer null, so `UnfilteredValue` on such a stream gave back the text
  «Cannot decode filter» and `TryUnfilter` left it as it was. The new `RunLengthDecode` filter,
  also reached by the abbreviation `RL` and through `Filtering.RunLengthDecode`, decodes and encodes
  it as ISO 32000-1 7.4.5 describes. Data that ends without the end-of-data marker, or part way
  through a run, gives back what it holds rather than throwing.

- **The DDL reader warns about a footnote inside a footnote.** The note is still read as written,
  but the renderer refuses one, because the inner note has no page of its own to go at the foot of,
  and its exception gives no line number. The reader now adds a warning to the `DdlReaderErrors`,
  "A footnote inside another footnote cannot be rendered", with the line and column where the
  inner `\footnote` is.

- **The DDL reader warns about a hyperlink with no name.** Every kind of hyperlink goes to what
  its `Name` names, so `\hyperlink{there}` or `\hyperlink[Type = Web]{there}` goes nowhere, and
  writing the document back out throws. The link and its text are still read, and a warning,
  "Obligatory property 'Name' not set in 'Hyperlink'.", is added to the `DdlReaderErrors`. That
  message used to read "Obigatory"; its spelling is fixed wherever it appears.

- **An OMR code's mark distance can be given as one of the standard distances.**
  `CodeOmr.StandardMarkDistance` takes a `MarkDistance`: `Inch1_6` (12 pt), `Inch2_6` (24 pt) or
  `Inch2_8` (18 pt). It reads and writes `MakerDistance` rather than keeping a value of its own, and
  reads null when `MakerDistance` is none of them. A distance given in another unit is recognised
  despite rounding, so 25.4 / 6 mm, which is 12.000000000000002 points, reads as `Inch1_6`.
  `CodeOmr.ToUnit` converts a `MarkDistance` to
  the length it stands for. The enum and the conversion were in the source as comments, so the
  enum's file compiled to nothing.

- **An imported page keeps its transparency group, user unit, tab order and presentation
  entries.** Importing a page used to copy only its resources, contents, boxes, rotation and
  annotations. A page composited as a transparency group lost its group, a page with a `/UserUnit`
  came out a different physical size, and its `/Tabs`, `/Trans` and `/Dur` were lost. All five are
  now copied, and a page that brings a `/UserUnit` or a `/Tabs` raises the document to PDF 1.6 or
  1.5, the versions those entries belong to. The group's colour space is imported once and shared
  with the resources that name it. A tab order of `/S` (structure order) is dropped, because it
  orders the annotations by a structure tree that page import does not bring along; row and column
  order are kept. Entries that point into structures of the source document are left behind on
  purpose, because importing a page does not bring those structures along. They include
  `/StructParents`, `/B`, `/AA`, `/Metadata`, `/PieceInfo` and `/SeparationInfo` (#86).

- **A content stream read with `ContentReader` and written back keeps its inline images.** The
  parser used to step over everything between `BI` and `EI`, so `ToContent` wrote back a bare `BI`
  and `EI` and the image was lost. An inline image is now read as one `CInlineImage`, a `COperator`
  named `BI` carrying its dictionary entries as written and its raw data, and is written back as
  `BI … ID … EI`. The end of the data is still found by looking for the bytes `EI`, so binary data
  that contains them is still misread. Content that ends before an inline image's `ID` no longer
  makes the reader loop for ever (#84).

- **A stream predicted with the TIFF predictor is now decoded rather than refused.** A Flate or
  LZW stream whose `/DecodeParms` named `/Predictor 2` threw `NotImplementedException`, so a
  document using TIFF horizontal differencing could not be read past it. It is now undone for 1,
  2, 4, 8 and 16 bits per component with any number of colours, sixteen-bit samples big-endian and
  each row starting afresh, as ISO 32000-1 7.4.4.4 describes (#79).

- **A font whose licence forbids embedding can be refused.** Set
  `PdfDocumentOptions.RespectFontEmbeddingRestrictions` and the document reads the embedding
  permissions each font declares in its OS/2 `fsType`. A face marked Restricted License is refused
  with an exception naming the face and the restriction, and so is one marked Bitmap Embedding
  Only, because this library embeds outlines. The exception comes from the draw that first uses the
  font, and again from the save if the option was set later. A face marked No Subsetting is
  embedded whole and not named as a subset. Preview & Print, Editable and Installable faces are
  embedded as before, and an old font that sets several usage bits is read by the least
  restrictive, as the OpenType specification says. The option is off by default, so no document
  that saved before will now throw. A PDF/A claim does not turn it on, because `fsType` is what a
  font says about its licence, not the licence itself (#78).

- **A combination chart can stack its columns.** A series set to `ColumnStacked2D` in a chart
  whose other series plot as lines or areas used to be refused with "ChartType
  'ColumnStacked2D' not valid for combination of charts". It is now drawn stacked, the value
  axis is sized to the stacked totals and still reaches any line or area beyond them, and the
  legend lists the stacked series top of the stack first, as a stacked column chart does. Clustered
  and stacked columns cannot share one chart, because the columns have one slot per category, and
  mixing them throws an `InvalidOperationException` that says so (#77).

- **`XBaseGradientBrush.ExtendLeft` and `ExtendRight`** — whether a gradient goes on past its start
  and its end in the colour of that end, written as the shading's `/Extend`. The start and end are
  a linear gradient's two points and a radial gradient's two circles, so `ExtendRight` is what fills
  the corners a radial gradient leaves unpainted outside its outer circle, and `ExtendLeft` is what
  fills the hole inside a first circle whose radius is not zero. Both are false by default and write
  nothing then, so an existing document is written byte for byte as before. The names are PDFsharp's,
  where both properties exist and have no effect (empira/PDFsharp#321).

- **A Gradients demo and a Gradients page on the documentation site**, covering both brushes, two-
  centre radial gradients, extension and a brush's own transform.

- **An annotation read from a file is handed back as the class its `/Subtype` names**, for 19
  subtypes, where every one used to arrive as `PdfGenericAnnotation`. Wrapping a read annotation
  writes nothing into it, so its appearance is saved exactly as the file had it (#60).

- **`PdfInkAnnotation`, `PdfPolygonAnnotation`, `PdfPolyLineAnnotation`, `PdfCaretAnnotation` and
  `PdfRedactAnnotation`**, each drawing its own appearance, and **`PdfPopupAnnotation`**, which draws
  none because a reader draws the pop-up itself. A redaction marks a region and removes nothing
  under it (#60).

- **`PdfMarkupAnnotation`**, a base class between `PdfAnnotation` and every markup annotation, with
  `Popup` (linked both ways), `InReplyTo`, `ReplyType`, `RichText` and `Intent` (#60).

- **`PdfDocumentOptions.MetadataStrategy`** — `KeepExisting` (the default, and what happened
  before), `AutoGenerate` or `NoMetadata` for the XMP packet. A PDF/A or PDF/UA claim with
  `NoMetadata` is refused (#60).

- **`PdfSignatureField.Lock` and `SeedValue`**, modelling a signature field's `/Lock` and `/SV`
  dictionaries through `PdfSignatureFieldLock`, `PdfSignatureSeedValue` and
  `PdfCertificateSeedValue`. **`PdfSignatureOptions.LockAction` and `LockFields`** make `PdfSigner`
  write `/Lock` and a `/FieldMDP` reference and set the covered fields read-only in the signed
  revision. Seed values are written and read back but not yet honoured when signing (#60).

- **`PdfDocument.PageAdded`, `PageRemoved` and `PageGraphicsCreated`** events. A
  `PageGraphicsCreated` handler draws inside a saved graphics state, which makes it a place to put
  a running header or a watermark on every page; in a tagged document it has to draw inside an
  artifact (#60).

- **`PdfPage.HasMediaBox`, `HasCropBox`, `HasBleedBox`, `HasTrimBox` and `HasArtBox`**, and
  **`EffectiveCropBox`, `EffectiveBleedBox`, `EffectiveTrimBox` and `EffectiveArtBox`**, which apply
  the defaults of ISO 32000-1 14.11.2 — the crop box defaults to the media box, the other three to
  the crop box — and clip to the media box. See `docs/specs/page-boxes.md` (#58).

- **Spot colours.** `XSpotColor` names a colorant — a Pantone ink, a white underprint, a varnish —
  with a DeviceCMYK, DeviceRGB or DeviceGray alternate, and `XColor.FromSpot(spot, tint)` paints
  with it wherever an `XColor` goes: brushes, pens and text. It is written as one `/Separation`
  colour space per colorant name per document, with a Type 2 tint transform. Two definitions of one
  name with different alternates are refused, and under PDF/A the alternate is held to the output
  intent like any device colour. Gradients, DeviceN and PinataLayout colours are not covered; see
  `docs/specs/spot-colours.md` (empira/PDFsharp#201, #67).

- **`PdfDocumentOptions.DeduplicateResources`** writes one copy of each identical font, image,
  form, colour space, graphics state, pattern, shading and content stream when saving, so a
  document merged from many files by importing their pages no longer carries each file's copy of
  the same resources. Equality is structural all the way down, so a font merges together with its
  descriptor, program, widths and `/ToUnicode` map. Pages, annotations, fields, structure elements,
  optional content groups and signatures are never merged, and a document opened with
  `PdfDocumentOpenMode.Append` refuses the option. Twenty copies of a 735 KB page save as 740 KB
  rather than 14.7 MB. See `docs/specs/resource-deduplication.md` (empira/PDFsharp#275, #68).

- **`PdfSignatureOptions.AnnotationFlags`** sets the signature widget's `/F`, which was fixed at
  Print. It is written into the signed revision, so the signature covers it. The default is still
  `Print`, and no flags leaves `/F` out. When `Options.Conformance` claims PDF/A, flags the profile
  forbids are refused: Print is required, and Invisible, Hidden, NoView and — for parts 2 and 3 —
  ToggleNoView are forbidden (empira/PDFsharp#157, #65).

- **`XGraphicsPath.StartFigure` starts a new figure.** It was an empty method, so the next segment
  joined the figure before it. Now the next segment added begins a figure of its own and the current
  one stays open, as in GDI+. `AddPath(…, connect: true)` respects it too.

- **`XGraphics.BeginContainer` accepts every `XGraphicsUnit`.** The source rectangle is measured in
  the unit given, and what is drawn inside the container is still in points. It used to refuse
  anything but `Point`.

### Changed

- **A malformed date no longer throws inside the parser.** A date string that will not parse, such
  as a bad `/CreationDate`, still falls back to the default it always did. It now gets there
  without throwing and catching an exception, and without failing an assertion in a Debug build.
  Every well-formed date reads exactly as before (#83).

- **BREAKING: looping over a page's annotations yields `PdfAnnotation`, not `PdfItem`.**
  `PdfAnnotations.GetEnumerator` now returns `IEnumerator<PdfAnnotation>`, so
  `foreach (var annotation in page.Annotations)` needs no cast. Each annotation has the class
  its subtype names, the same as the indexer gives. As a `PdfArray`, through
  `IEnumerable<PdfItem>` (and so LINQ), or as plain `IEnumerable`, the collection still yields
  the annotations, not the references underneath. Code that stored the loop variable or the
  enumerator as `PdfItem` still compiles (#81).

- **A composite font's `/W` array writes each run of consecutive glyphs as one entry.** It
  used one `c [w]` entry per glyph. It now uses `c [w1 w2 …]` for every run
  (ISO 32000-1 9.7.4.3), so the array is much shorter for ordinary text. Each glyph's width is
  unchanged (#81).

- **A WinAnsi font with PostScript (CFF) outlines is no longer named as a subset.** It was always
  embedded whole but still carried a subset tag on its name; it now loses the tag, as the Type 0
  path already did (#78).

- **`PdfSquareCircleAnnotation.Interior` and `BorderWidth` are read from the dictionary** (`/IC`
  and `/BS`) rather than kept in fields, so an annotation read from a file reports what it says
  (#60).

- **`PdfDocumentOptions.WriteXmpMetadata` is now the older name for
  `MetadataStrategy = AutoGenerate`** (#60).

- **A name is written with every delimiter, `#`, and every byte outside `!`..`~` escaped as
  `#xx`.** The name's bytes are unchanged; only how they are spelled in the file differs, so a
  Shift-JIS name that used to be written raw is now written escaped (#59).

- **`/Producer` names the version that wrote the file**, e.g. `PdfPinata 0.2.1
  (https://github.com/PinataLabs/PdfPinata)`, where every document used to say
  `PdfPinata 1.50.4000-netstandard`, a number left over from PDFsharp. The version is MinVer's,
  read from the assembly's informational version without the commit hash. This also applies to
  `/Creator` when the caller sets none, and to the XMP packet's `pdf:Producer`.
  `ProductVersionInfo.Producer` is now a static property rather than a constant, and
  `ProductVersionInfo.Producer2` is gone.

- **`ThickThinBarCode` is renamed `TwoWidthBarCode`**, the usual name for bar codes whose bars and
  gaps are each either narrow or wide. It is the base class of `Code3of9Standard` and
  `Code2of5Interleaved`, and only code that names the base class itself has to change. The error
  for a `WideNarrowRatio` outside 2 to 3 now reads "The ratio of wide to narrow lines must be between
  2 and 3." It used to name the interleaved 2 of 5 code even when Code 39 threw it.

- **`XImage.FromFile(string)` and `FromFile(string, PdfReadAccuracy)` are one method**, with the
  accuracy optional and `Strict` by default. Source that called either still compiles unchanged, but
  an assembly compiled against the old overloads has to be rebuilt.

### Deprecated

- **`XBitmapImage.CreateBitmap`.** It makes a bitmap with a size and no pixels, whose only use was
  to be passed to `XGraphics.FromImage`, which is removed below. It is left from PDFsharp's GDI+ and
  WPF builds and will be removed too.

- **`PdfCustomValueCompressionMode`.** Its one use was `PdfCustomValue.CompressionMode`, which is
  removed below, so nothing reads it. It will be removed.

- **`XPoint + XSize`.** It moves a point by the extent of a size, which is a displacement written as
  a size. Add an `XVector` instead: `point + new XVector(size.Width, size.Height)`. WPF's `Point`
  has only that overload. It behaves as before and will be removed.

### Removed

- **`XGraphics.FromImage`.** It always returned null, because this library has no way to draw onto
  an image. It was left from PDFsharp's GDI+ and WPF builds. To draw something that can be placed
  like an image, draw onto an `XForm` through `XGraphics.FromForm`.

- **`PdfWriterLayout.Verbose`.** Nothing in the library ever selected it, since the writer that
  reads the layout is internal and is always given the default, `Compact`. So no document was
  written with it, and the header comments, indentation and sorted keys it stood for went with it.
  The members before it keep their values.

- **`PdfCustomValue`'s parameterless constructor and its `CompressionMode` field.** The constructor
  made a value with no bytes, and nothing ever read `CompressionMode`, so setting it did nothing.
  Construct a value from its bytes with `new PdfCustomValue(byte[])`.

### Fixed

- **`PdfAnnotationFlags` is marked `[Flags]`.** It always was a set of bits, ISO 32000-1
  Table 165, but a combination printed as a bare number. The PDF/A error that refuses a hidden
  or unprinted annotation now names the flags it objects to, `Invisible, Hidden, NoView` rather than
  `35`.

- **A DDL `\fontsize` that is not a size is reported rather than ending the read.**
  `\fontsize(abc){x}`, or a quoted size with a unit the reader does not know, threw an
  `ArgumentException` out of `DdlReader`, so the rest of the document was not read and nothing
  was added to the `DdlReaderErrors`. It is now an error in that list, "String 'abc' is not a valid
  value for structure 'Unit'.", with its line and column. The text inside the braces is still
  read, with no size of its own, and so is the rest of the document.

- **A chart's text takes its colour, bold and italic from the chart's font.** An axis title, the
  tick labels, the legend and the data labels each have a font of their own. Where that font sets
  nothing, the name and the size came from the chart's font, but the colour did not: all four were
  drawn in black, whatever colour the chart's font had. Bold and italic were added to the chart's,
  so a title set to not bold under a bold chart was still drawn bold. Each font now takes what it
  leaves unset from the chart's font, and an explicit `false` is kept. A series' data label takes
  it from the chart's data label first. **Charts whose font sets a colour now draw all their text in
  that colour**, and a title, legend or label set to not bold or not italic is now drawn that way.
  `Font.Bold`, `Font.Italic` and `Font.Color` still read only what was set on that font (#101).

- **A series' data label takes what it does not set from the chart's data label.** A series with a
  data label of its own used to replace the chart's outright. A series that set only a format lost
  the chart's position, type and font, and its labels were drawn outside the end, in the default
  font. Format, position, type and font are now taken from the chart's data label one by one. A
  position that neither sets still defaults as before: inside the end when there is no data label
  object at all, outside it when there is one (#101).

- **An axis title's or tick labels' own font no longer overrides its style's bold, italic and
  colour.** In PinataLayout, an axis title with both a `Style` and a `Font` was drawn with the
  font mapped over the style. The font's unset bold and italic read as `false` and its unset colour
  as none, so `Style = "Loud"` with `Font.Size = 14` gave a 14-point title that was neither bold nor
  coloured. Flattening now fills the title's and the tick labels' font from the style they name,
  or from the chart's font if they name none. The chart mapper copies only what a font sets (#101).

- **A character added by number is drawn as itself.** `AddCharacter(char)` with anything outside
  ASCII drew U+FFFD: the renderer decoded the character's low byte on its own as UTF-8, so `'é'`,
  `'ß'`, `'Ω'` and `'€'` all came out as the replacement character. A code above U+FFFF, which can
  only be given through `SymbolName`, was cut to 16 bits and could draw nothing at all. The whole
  code is now drawn, as a surrogate pair where it needs one.

- **An outline entry's text colour is written for a document read from a file.** `/C` was written
  only when the catalog's version string said PDF 1.4, and that string reads "1.3" for every
  document opened from a file, whatever its header says. So a colour set on an outline entry of an
  opened document was never saved. A new document saved as PDF 1.3 had the opposite problem: it
  got a `/C` key its version does not define. The check now uses the version the document is saved
  as, the same one the style entry `/F` beside it uses. `/C` is removed from an entry saved into a
  version too old for it.

- **A non-breakable blank is drawn as a space, and a line is not broken at it.**
  `Character.NonBreakableBlank` (and `HardBlank`, the same value) had no character. The renderer
  drew U+0000, which text normalization drops, so the blank took no room and the words either side
  of it ran together. It is now drawn as U+00A0 and takes a space's width. A line is also no longer
  broken before or after it: the words it joins are measured together and moved to the next line
  together. A run too long for any line is still broken at the blank, as a word longer than the
  line is broken inside it.

- **A content-stream string of any kind can be written, and is written back in the form it was
  read in.** `CString.ToString` threw `NotImplementedException` for `CStringType.HexString`,
  `UnicodeString` and `UnicodeHexString`, although the type can be set by anyone. A hex string is
  now written as two hex digits per byte, and refuses a character above U+00FF rather than writing
  its low byte. A Unicode hex string is written as `<FEFF…>` with four digits per UTF-16 code unit,
  and a Unicode string as a literal string of FE FF followed by big-endian UTF-16. `CParser` now
  sets the type of each string it reads. It used to give every string `String`, so a Unicode
  string read from content and written back went out as the low byte of each character. A hex
  string read from content is now written back as a hex string rather than as a literal one.

- **A dashed pen of another width gets dashes of its own width.** The standard dash styles are
  measured in the pen's width, but the renderer only compared the style with the one it had
  written. So a `Dash` line 3 points wide drawn after a `Dash` line 1 point wide used the thinner
  line's dashes. The renderer now compares the dash operator itself. For the same reason, a custom
  dash pattern is now written once for as many strokes as use it. It used to be written again for
  every stroke.

- **An outline entry writes `/F` only when it has a style and the document is PDF 1.4 or later.**
  Setting `PdfOutline.Style` wrote `/F` at once, whatever the value. So a regular entry carried
  `/F 0`, and an entry in a PDF 1.3 document carried a key that PDF 1.3 does not have. The style is
  now kept on the entry and written when the document is saved. A regular entry, or any entry in a
  document older than 1.4, carries no `/F`. An entry read with a style and then set to `Regular`
  loses its `/F` on the next save.

- **A `\x` escape in a quoted MDDDL string is the character it names.** `"\x41"` used to read as
  five literal question marks. The scanner also stepped over the character after the digits, so
  `"\x41 b"` lost its space and `"A\x41"` lost its closing quote and ran on into the next line.
  One or two hex digits are now converted to the character they name, and the character after them
  is kept. An escape with no digits, or more than two, is refused as an invalid escape sequence, as
  more than two always was. Nothing in the DOM writes `\x`, so this only affected files written by
  hand or by another tool.

- **A colour number in MDDDL that does not fit an unsigned integer is reported as a reader error.**
  A colour written as `99999999999`, as `-1`, or as a hex literal with a letter that is not a hex
  digit, such as `0x1G`, went straight to `UInt32.Parse`. The result was an `OverflowException`
  or `FormatException` rather than the parser error the reader reports and recovers from.
  `\fontcolor` and a `Color` attribute caught it only by wrapping the raw exception. The same is
  true inside `RGB(...)`. Such a value is now refused the way an oversized integer already was, with
  "Valid range only within '0 - 4294967295'." or "Integer expected", and the rest of the document is
  read as before (#94).

- **A lone carriage return in MDDDL ends a line.** A CR with no LF after it, how a classic Mac OS
  file ends its lines, was kept as an ordinary character. So a file written that way was one long
  line to the reader. Its first `//` comment swallowed the rest of the document, and every error
  was reported on line 1. A lone CR is now read as a line end, and a CRLF is still one. A comment
  holding a lone CR or LF is now written as one `//` line per line rather than one line with a line
  end in the middle, which the reader would end early. A `Text` holding a lone CR now reads back as
  a space, as one holding an LF always has (#94).

- **A name written with characters beyond Latin-1 is saved as its UTF-8 bytes rather than
  corrupted.** The writer escaped each character of a name as `#` and two hex digits, taking it to
  be a byte, so a name a caller built with `U+4E2D` in it was written as `#4E2D`, which a reader
  takes for the byte `0x4E` followed by the characters `2D`. A name holding any character past
  `U+00FF` is now encoded as UTF-8, as ISO 32000-1 7.3.5 recommends, and each byte is escaped. A
  name read from a file never holds such a character, so every name that round-trips is written
  byte for byte as before (#87).

- **A name in a content stream is escaped when it is written back.** `CLexer` turns `#20` in a
  name into the blank it stands for, and `CName` wrote the name out as it held it, so `/A#20B`
  came back as the two names `/A` and `B`, and a name holding a bracket or a parenthesis left a
  stray delimiter in the stream. It now goes through the same escaping as a name in the document
  body. A `CName` built with an escape already written into it, such as `/A#20B`, now has its `#`
  escaped in turn, as a `PdfName` always has (#87).

- **`PdfInteger` and `PdfUInteger` refuse to become a `DateTime`.** Their `IConvertible.ToDateTime`
  returned `DateTime.MinValue`, a date nobody asked for. Both now throw `InvalidCastException`, as
  the `int` and `uint` they hold do. `PdfLong` still reads its value as ticks (#87).

- **PDF/A-1 now refuses a page's own transparency group.** The PDF/A-1 transparency rule looked at
  a page's images, graphics states and forms, but not at the page's own `/Group`. So a PDF/A-1
  document holding a page imported with a group saved without error and then failed validation.
  The group is now refused under part 1 only, the same as other transparency. A page imported
  with a `/UserUnit` or a `/Tabs` is refused under PDF/A-1 too, because those entries raise the
  version past the 1.4 PDF/A-1 allows (#86).

- **An undefined `SymbolName` is refused rather than written as MDDDL that cannot be read back.**
  `Character.SymbolName` accepted any value, and one with the top nibble set that named no symbol
  was written out as `\symbol(<number>)`, which the parser rejects. It now throws
  `ArgumentException`, as every other DOM enum property does; a character code, top nibble clear,
  is still accepted as before. A `Character` now keeps its symbol and its character in separate
  fields, and reads, the value model and MDDDL output are unchanged (#92).

- **A stream given to a dictionary through `PdfDictionary.Stream` is written with its `/Length`.**
  Only `CreateStream` used to write the entry, so a dictionary given another dictionary's stream, or
  a stream from `PdfStream.Clone()`, was written with no `/Length` in a Release build. A shared
  stream whose data changed left the second dictionary declaring the old length. Assigning `Value`
  on a cloned stream threw a `NullReferenceException`. The length is now written when the stream is
  assigned, and checked again when the dictionary is written (#93).

- **An outline entry can be in one list only, and removing entries no longer breaks the file.** An
  entry already in a collection, from another document, or being placed under itself or anything
  below it is now refused with an `InvalidOperationException` or `ArgumentException` that says what
  to do instead. These used to write an entry that pointed to itself, put one entry in two lists,
  or overflow the stack on save. Removing entries from a document that had been saved or read left
  links to them, and the file then failed to reopen. Just reading `document.Outlines` made the
  catalog point to an outline that was never written. An entry replaced through the indexer, or
  removed, can now be added again anywhere, including after a save (#93).

- **A page removed and inserted again after a save is written with its content.** The save gave the
  removed page's object number to another object, and the next save threw "An item with the same
  key has already been added". The page, and everything it points to, now comes back under a
  number of its own (#93).

- **A number sign in a content-stream name no longer stops the whole stream from being read.**
  `/A#ZZ`, a single hex digit after `#`, or a `#` at the end of the content threw a
  `FormatException`. A `#` now stands for a byte only when two hexadecimal digits follow it, and is
  otherwise kept as a character of the name (#84).

- **A UTF-16 literal string in a content stream has its escapes resolved on its bytes before it is
  decoded.** A line continuation inside such a string used to shift every character after it by
  one byte, and the string ran on past its closing parenthesis. Strings in both byte orders now
  read as the document lexer reads them (#84).

- **A `#` in a name that does not begin a two-digit escape is kept rather than refused.** The
  document lexer read the two characters after every `#` in a name as hexadecimal digits, so
  `/A#ZZ`, a `#` followed by a single digit, or a `#` at the end of a name ended the read with a
  `FormatException`. Only `#` followed by two hexadecimal digits is an escape (ISO 32000-1 7.3.5);
  any other `#` is now kept as written, as readers do (#83).

- **Cross-reference stream offsets past 2 GiB are read whole.** Each object's offset in a
  cross-reference stream was cast to an `int`, so an object further than 2 GiB into the file was
  looked for at a wrapped, usually negative, position. An offset written in a five-byte field also
  lost its top byte. Offsets are now read as wide as the stream's `/W` says (#83).

- **PDFDocEncoding can be decoded as well as written, and both directions follow ISO 32000-1
  Annex D.** The internal decoder threw `NotImplementedException`; it now reads codes 0x18 to 0x1F
  as the spacing accents they are rather than as control characters, and the undefined codes 0x7F,
  0x9F and 0xAD as U+FFFD. The encoder wrote ƒ as the ellipsis, ‰ as the single right guillemet and
  š as the right single quotation mark, and wrote DEL, the soft hyphen and several control
  characters as codes that stand for other characters; those now go to their own codes, or to the
  currency sign WinAnsi already writes for what it cannot hold. What a document writes by default
  is unchanged: a `PdfString`'s value is written as raw bytes, so the difference shows in
  `PdfString.ToString()` on a string made as `PDFDocEncoding` and in text beyond ASCII written
  through `PdfWriter.WriteDocString` (#82).

- **An annotation colour written as an indirect object is read, not taken as black.**
  `PdfAnnotation.Color` treated `/C` as an array without following an indirect reference. So a
  file that stored the colour as its own object read back as black (#81).

- **ASCIIHexDecode stops at its end-of-data marker wherever it comes, and refuses what is not a
  hex digit.** `>` was recognised only as the last character, so data with the marker mid-stream
  was decoded marker and all, and a character that was not a digit or white space came out as
  whatever byte the digit arithmetic made of it. Nothing after the marker is read now, an odd digit
  before it is read as though a 0 followed, and any other character throws `ArgumentException`, as
  ISO 32000-1 7.4.2 requires. The decoder also no longer rewrites the caller's buffer (#79).

- **ASCII85Decode refuses a `z` inside a group.** A `z` stands for a whole group of zeros and can
  only begin one; inside a group it was read as a digit worth 89 and every group after it decoded
  out of step, silently. It now throws `ArgumentException` (#79).

- **The charting collections work as an `IList`.** `DocumentObjectCollection` (behind
  `SeriesCollection`, `SeriesElements`, `XValues` and `XSeriesElements`) declared the
  non-generic `Add`, `Insert`, `Remove`, `RemoveAt`, `Contains` and `IndexOf`, and every one threw
  `NotImplementedException`. They now use the typed members. A null is taken as a blank, and
  anything that is not a chart object is refused with an `ArgumentException`. An element set
  through the indexer or put in by `InsertObject` now belongs to the collection, as one added
  with `Add` always did (#77).

- **A form field reads the flags and the type it inherits.** `/FT` and `/Ff` are inheritable
  (ISO 32000-1 Table 220), and a form read from a file often sets them once, on a parent.
  `PdfAcroField.Flags` read only the field's own `/Ff`, so it answered no flags for every such
  child. Reading a child from a file read both entries the same way, so a radio button whose group
  said `/Btn` and `Radio` came back as a `PdfGenericField`. Both now use the field's own entry, or
  else the nearest ancestor's, and stop where a malformed `/Parent` chain repeats. Setting one flag
  on a child that inherits others writes all of them into the child's own `/Ff`, instead of
  dropping them (#75).

- **`PdfReader.Open` no longer hangs on a file whose cross-reference `/Prev` chain loops back on
  itself.** Under `Strict`, the default, it throws `PdfReaderException`; under `Moderate` it reads
  each section once and opens the document. The file attached to empira/PDFsharp#266, whose
  `startxref` is followed by a hex string that never ends, already threw here, and a test now pins
  that (#66).

- **`SaveIncremental` and `PdfSigner.Sign` no longer throw `NullReferenceException` on an
  unencrypted document whose `SecuritySettings` was read first**, which includes reading a
  permission and signing a document that claims PDF/A. An appended revision is now encrypted only
  when the file it extends was read encrypted (#65).

- **`XTextFormatter` no longer drops the last line of text laid out in a rectangle exactly as tall
  as `XGraphics.MeasureString` reports for it.** The two reach the same height by different
  floating-point routes, and the fit test now allows a millionth of a point for rounding. A
  rectangle sized as n times the height of one measured line is still too short by the line gaps
  between them; measure the whole text instead (empira/PDFsharp#198, #64).

- **A read-only form field refuses a value however it is given one.** `PdfAcroField.Value` threw
  `InvalidOperationException` for a field whose `ReadOnly` flag was set, but `PdfTextField.Text`,
  `PdfCheckBoxField.Checked`, `SelectedIndex` on radio groups, combo boxes and list boxes, and
  `PdfListBoxField.SelectedIndices` wrote `/V` without asking — so the same field was refused a value
  by one name and took it by another. They all make the same check now. Code that set `ReadOnly`
  before filling a field in has to fill it in first (#62).

- **An incremental save no longer writes a `/Size` smaller than the revision before it, or reuses a
  freed object number.** New objects were numbered from one past the highest object still in use,
  and the appended `/Size` was written the same way, for a classic trailer and for the
  cross-reference stream `SaveIncremental` now writes. When a file's last cross-reference section
  ended in free entries, the update shrank `/Size` and handed out numbers the file had already
  freed, both of which ISO 32000-1 7.5.5 forbids. Numbering now starts at the largest `/Size` any
  revision declares. A `/Size` beyond the 8,388,607 objects ISO 32000-1 allows is ignored rather than
  allowed to overflow the numbering (#63).

- **A fully transparent pen or brush is painted transparent when it is the first colour drawn, or
  the first after a gradient.** The colour last written started out as `XColor.Empty`, whose alpha
  is 0, so a first colour with alpha 0 matched it and no `/CA` or `/ca` was written — and the shape
  was painted opaque (empira/PDFsharp#281, #59).

- **A name containing `[`, `]`, `{` or `}` reads back whole.** The writer escaped only some of the
  delimiters, so `/A[B` was read back as the name `/A` followed by an array (#59).

- **A stream whose `/Filter` or `/DecodeParms` is an indirect object decodes.** So does one whose
  parameter array is indirect, or holds indirect or `null` elements. Each was reported as
  "Cannot decode filter" (empira/PDFsharp#323, #59).

- **`CSequence.Add` given a `CArray` adds the array as one operand.** The array matched the
  `Add(CSequence)` overload, which spread its items into the sequence and lost the brackets
  (empira/PDFsharp#300, #59).

- **Reading a page box no longer gives the page one.** Reading `CropBox`, `BleedBox`, `TrimBox` or
  `ArtBox` on a page without that box wrote `[0 0 0 0]` under its key, as did reading `MediaBox` on
  a page read from a file that has none. A missing box still reads as the empty rectangle. An entry
  that is not a rectangle now counts as no box rather than throwing `InvalidCastException` (#58).

- **A change to a read object is kept in an incremental save when the object is then wrapped in a
  typed class.** The wrapping lost the object's dirty flag, so `SaveIncremental` left the change out
  of the appended revision — and a signature with a field lock dropped the whole AcroForm (#60).

- **A link read from a file is no longer given a zero-width border when it is saved** (#60).

- **`SaveIncremental` on a file indexed by a cross-reference stream writes a revision that can be
  read back.** It wrote the keyword `trailer` and then the previous revision's `/XRef` stream, object
  header and all, where a trailer dictionary belonged, so appending to any PDF 1.5-style file made it
  unreadable. The appended revision is now indexed by a cross-reference stream of its own, with an
  `/Index` naming only the objects it changed and a `/Prev` naming the stream before it. A file
  indexed by a classic table is appended to exactly as before (#55).

- **A chart given more than one `XSeries` draws its category labels on the axis.** The category
  axis drew every series one after another without going back to the first slot, so a second
  series' labels ran off the right of a column, line or area chart and below the foot of a bar
  chart (empira/PDFsharp#286). The axis has room for one row of categories, and it is now labelled
  from the first series alone — the series the pie legend already used, and what Excel does — in
  both orientations. A bar chart no longer widens its category axis for labels it does not draw.

- **A chart legend too wide for its chart wraps instead of running off both sides of it.** A legend
  docked above or below the chart — which is where PinataLayout's `FooterArea.AddLegend()` and
  `HeaderArea.AddLegend()` put it — set every entry in one row however many there were, and centred
  that row on the chart, so a dozen category names ran off both edges of the chart and of the page
  ([empira/PDFsharp#306](https://github.com/empira/PDFsharp/issues/306)). The entries now start a
  new row whenever the next one would not fit across the chart, each row centred as the single row
  was. An entry wider than the chart on its own is word wrapped inside its entry, whichever side the
  legend is docked to, with its marker against the first line; a single word wider than the chart
  is kept whole. A line break in a series or category name now starts a new line of its entry,
  where `DrawString` used to drop it and run the two halves together. A legend that fits in one row
  is laid out exactly as before. See C14 in `docs/specs/charting-renderer-findings.md`.

- **A chart line format that says `Visible = false` is no longer drawn as a hairline**
  (empira/PDFsharp#287). The converter turns a hidden format into a pen of width 0, and PDF strokes
  a width of 0 as the thinnest line the device can draw rather than not at all. The column plot
  area already skipped such a pen; the line, area, bar and pie plot areas did not, and neither did
  the legend — which also drew a line chart's key at a width of 1 whatever the series said. A hidden
  series line, area outline, bar border or pie sector border is now not stroked, in the plot area or
  in the legend. A line series' markers are still drawn, and so is an area's fill. A format that
  sets a width or a colour but never `Visible = true` was already converted to width 0, and is now
  not drawn either, as a column's border already was.

- **A transform appended on `XGraphics` is drawn where `XGraphics.Transform` says it is.**
  `TranslateTransform`, `ScaleTransform`, `RotateTransform` and the rest take an `XMatrixOrder`, and
  `Transform` always honoured `Append`, but the page was handed the same matrix as a prepend, because
  a `cm` operator can say nothing else. So `TranslateTransform(100, 50)` and then an appended
  `ScaleTransform(2, 2)` drew at (100, 50) rather than (200, 100). An appended matrix `T` is now
  written as the prepend with the same effect, `W · T · W⁻¹`. Appending to a matrix with no inverse
  throws `InvalidOperationException` and changes nothing. Prepended transforms are written as before.

- **A gradient brush's own `Transform` is honoured.** `TranslateTransform`, `ScaleTransform`,
  `RotateTransform`, `MultiplyTransform` and the `Transform` setter all changed a matrix that nothing
  read, so a transformed gradient was drawn exactly as an untransformed one. The transform now applies
  to the brush before the transform of the graphics. A radial gradient stretched one way more than the
  other has ellipses for rings, which a type 3 shading cannot describe on its own, so its circles are
  written in the brush's own space and the mapping goes into the pattern matrix. A brush with no
  transform of its own is written as it was.

  Radial gradients themselves, the other half of empira/PDFsharp#321, were already drawn in colour
  here; `RadialGradientRenderingTests` now reads their pixels to keep it that way.

- **A document updated incrementally is read as its newest revision, where cross-reference streams
  are involved.** Two ways an older revision's object took the place of the newer one's, both met in
  signed documents (empira/PDFsharp#353, whose cause is empira/PDFsharp#213). An update may give a
  new object the number of an earlier revision's cross-reference stream, and the reader hung the
  stream on it, so an `/AcroForm` under that number read as a cross-reference stream and
  `AcroForm.Fields.Names` came back empty. And an object stored compressed in an older revision and
  written out in full by a newer one - a catalog a signing tool rewrites to add its form, for one -
  read as the older compressed copy. The newest entry for a number now decides what the number
  means, whichever form either revision stored it in.

- **An object a hybrid-reference file keeps in an object stream is read rather than lost.** ISO
  32000-1 7.5.8.4 lets one revision be described twice — a classic cross-reference table that marks
  every compressed object *free*, so a PDF 1.4 reader sees a smaller document rather than a broken
  one, and beside it a cross-reference stream, named by the trailer's `/XRefStm`, saying where those
  objects really are. `/XRefStm` was declared here and read by nothing, so the objects were not
  merely unavailable but contradicted: the table called them free and nothing said otherwise. A
  dangling reference reads as null by design, so nothing threw — the document opened, the page
  imported and the file saved with a font, an image or a whole `/Resources` dictionary silently
  gone. Acrobat and Canva both write files this way. The stream is now read after the table of the
  same revision and before its `/Prev`, for its entries alone: entries already in the table win, and
  the classic trailer beside it stays the document's. A stream that cannot be read stops the read
  under the default `Strict` accuracy, naming `/XRefStm` and the position, and under `Moderate` is
  dropped whole so the document opens as the PDF 1.4 file its table describes — a lossy fallback,
  short of every compressed object only the stream located. Reported upstream as
  [empira/PDFsharp#388](https://github.com/empira/PDFsharp/issues/388); see
  `docs/specs/hybrid-reference-files.md`.

- **`XGraphics.DrawImage(image, destRect, srcRect, srcUnit)` draws the part of the image `srcRect`
  names.** It ignored the source rectangle and drew the whole image squeezed into the destination.
  The whole image is now scaled so that the part asked for fills the destination, and it is clipped
  there. Asked for the whole image, it writes exactly what the plain overload does.

- **A PinataLayout image's `PictureFormat` crop crops.** `CropLeft`, `CropTop`, `CropRight` and
  `CropBottom` used to shrink the space the picture was given and squeeze all of it in. A cropped
  image is now drawn at its own scale and clipped to what is left. Uncropped images are unchanged.

- **`XGraphics.BeginContainer` maps a source rectangle's corner onto the destination's corner.** The
  matrix moved a point to `p·scale + destination − source` instead of `(p − source)·scale +
  destination`. That is right only when the source rectangle starts at the origin.

- **`XGraphics.WriteComment` keeps every line of a comment commented out.** A bare carriage return
  ended the comment, and whatever followed it was read as content-stream operators.

## [0.2.1] - 2026-09-21

### Added

- **`PdfDictionary.PdfStream.ExternalFile`** — the `PdfFileSpecification` a stream names in its
  `/F` entry when its data is in another file, and null when it names none. It is a way of reading
  where the data is: nothing fetches the file, because a file specification is written by a stranger
  and can name a path outside the document's directory or, in its dictionary form, a URL. A `/F` the
  document holds as a dictionary is answered as itself, so it comes back the same each time; one
  written as a bare name is answered as a specification carrying that name, made on the spot and
  outside the document. See `docs/specs/external-file-streams.md`.

- **`Cell.MergedRightColumnIndex` and `Cell.MergedBottomRowIndex`** — the last column and the last
  row a table cell actually covers. `MergeRight` and `MergeDown` are written while the table is
  still being built, so neither can be checked against one at the time: the rows and columns they
  speak of may be added afterwards, or never. What each says is therefore how far the cell reaches
  *for*, and these two say how far it reaches — the same number whenever the merge is inside the
  table, and the edge of the table when it is not.

### Changed

- **`XUnit` implements `IEquatable<XUnit>`.** A value type that overrides `Equals` and defines `==`
  is expected to, and without it `EqualityComparer<XUnit>.Default` boxed both operands on every
  comparison a `List<XUnit>`, a `Dictionary<XUnit, …>` or a LINQ `Distinct` made.

  **It changes one answer, and changes it towards the operator.** `XUnit` converts implicitly from
  `int`, `double` and `float`, and `XUnit` is a better conversion target than `object` — so
  `unit.Equals(72)` now binds to `Equals(XUnit)` and converts before comparing, exactly as
  `unit == 72` always has. It used to bind to `Equals(object)`, box the argument, ask whether an
  object was an `XUnit` and answer false, so the operator and `Equals` disagreed about the same
  pair of values. An argument declared as `object` still answers false, because at that point there
  is no conversion left to make.

  **A string is unchanged, deliberately.** `XUnit` converts implicitly from `string` too, so
  `unit.Equals("an inch")` would have bound the same way, parsed, and *thrown* rather than
  answered — and an `Equals` has to answer. A `bool Equals(string)` overload returning false takes
  that binding instead, so `unit.Equals(anyString)` is false exactly as before. `unit == "an inch"`
  still throws; that is the operator's business and is not changed here.

### Fixed

- **A file whose `startxref` is a long way from its end is read by scanning back to it, not by
  reading the file into one string.** The trailer scan looked in the last 1030 bytes and, failing
  there, read the whole file into a `string` to call `LastIndexOf` on it — so a document with a
  distant `startxref` cost twice its own size in memory, and one larger than 1,073,741,791 bytes
  could not be opened at all, because that is as long as a `string` gets whatever memory the
  machine has. `Lexer.FindLastMarker` now reads backwards 64 kiB at a time into one reused buffer.
  The 1 GiB document reported as [empira/PDFsharp#390](https://github.com/empira/PDFsharp/issues/390)
  — a two-kilobyte PDF followed by a gigabyte-long comment — threw `OutOfMemoryException` and now
  opens in about three seconds with no measurable allocation. A file past `int.MaxValue` used to be
  refused outright with `NotImplementedException`, and reads now too. See
  `docs/specs/large-file-trailer-scan.md`.

- **A file with no `startxref` anywhere in it is refused by name.** The scan assigned
  `Lexer.Position` from the index it had just failed to find, and a stream position cannot be -1,
  so every such file came out as `ArgumentOutOfRangeException: value ('-1') must be a non-negative
  value` and the sentence written to explain the case — "The StartXRef table could not be found,
  the file cannot be opened." — was unreachable.
- **A partly transparent image is drawn rather than erased.** Such an image was written with both
  an 8-bit `/SMask`, carrying its alpha exactly, and a 1-bit `/Mask` stencil rounding that same
  alpha to transparent or opaque at 128. ISO 32000-1 has the soft mask override the stencil, but
  Ghostscript and macOS Quartz apply both, so every pixel below 128 was discarded: soft edges lost
  their antialiasing, and an image whose alpha lay wholly under 128 — a watermark, a faint overlay
  — was embedded, referenced from the page, and invisible. The stencil is now written only where no
  soft mask is, which is where it loses nothing: transparency that is already binary, and a
  document below PDF 1.4. Reported upstream as empira/PDFsharp#392.

- **`XUnit.Presentation` stores a length in presentation units rather than in points.** The setter
  was a copy of the one for `Point` and recorded `XGraphicsUnit.Point` as the measure, so a length
  assigned in presentation units was kept as that many points — four thirds of what the caller
  asked for, and read back as such by every one of the five getters. The other four setters each
  named their own measure and always had.

- **A document built on an output stream writes a readable file.** `PdfDocument(Stream)` was the
  only one of the three constructors that never set the PDF version, so the field stayed 0 and the
  header read `%PDF-0.0` — enough bytes to look like a save had worked, and refused by every reader
  including this one. Every document written through `Close()` rather than `Save()` came out
  unopenable.

- **A section that names a page format and one of its two lengths keeps the length it set.**
  The two arms of that decision in the flattening visitor were swapped: a section given a height
  and no width had its height overwritten from the format and its width left unset, so the page
  came out no width at all — and the one measurement the caller did set was the one thrown away.

- **An indirect boolean is written as `true` or `false` rather than `True` or `False`.**
  `PdfWriter.Write(bool)` wrote `bool.TrueString`, and ISO 32000-1 7.3.2 spells the two keywords in
  lowercase — a reader looking for them finds nothing else. Its only caller is `PdfBooleanObject`,
  which nothing in this library creates for itself, so an indirect boolean was the one value written
  as a token no PDF defines. The `PdfBoolean` overload beside it always had it right.

- **`ViewerPreferences.Direction` reads back what it was set to.** The getter compared the stored
  name against `"L2R"` and `"R2L"`, and a PDF name carries its slash — so nothing ever matched
  and the property answered null however it had been set, in the same document and out of the file
  it wrote. Both spellings are now taken, for an entry set as a name by hand.

- **A document is read although one of its streams says its data is in another file.** ISO 32000-1
  Table 5 gives a stream dictionary an optional `/F` naming the file its data is really in, and the
  parser refused every dictionary carrying one with `NotImplementedException: "File streams are not
  yet implemented."` — before reading a byte, so the exception came out of `PdfReader.Open` and the
  whole document was lost for it, whatever that stream was for. The reported case is a catalog whose
  `/Metadata` says its XMP is in a file beside the document: nothing on any page needed it, and
  every page went with it. `/Length` counts the bytes that are in this file whether or not `/F` is
  there, so reading it the ordinary way is all such a stream ever needed. The entries saying where
  the data really is are carried through untouched; nothing goes and fetches it.

- **A cell merged past the last row or column of its table is laid out instead of throwing.**
  Nothing can check a merge when it is written, so a cell can claim more of a table than the table
  ever has — and every place that read a merge as a position then indexed past the end. The
  document was accepted by the object model without a word and the *formatter* threw
  `ArgumentOutOfRangeException`, naming nothing but `index`, from six places: the borders of a
  merged cell, its width, the map of bottom border positions, and the structure tree's spans. Such
  a merge is read as reaching the edge now, which is the reading `TableRenderer`'s own `KeepWith`
  arithmetic already took, so a cell merged nine columns right in a three column table draws
  exactly what one merged two columns right draws — and writes `/ColSpan 3` rather than `/ColSpan
  10`, which is a grid no reader could lay out. A merge that fits is unaffected: the bound is only
  ever the edge it already stopped at.

- **`Section.LastParagraph` and `Section.LastTable` answer null on an empty section** instead of
  raising a `NullReferenceException`. Both read the backing field rather than the `Elements`
  property, and a section nobody has added anything to has not built its element collection yet —
  so the one case each of them documents an answer for was the one case that threw.

- **A page tree that loops, or that is nested deeper than the stack can hold, is refused instead of
  killing the process.** ISO 32000-1 7.7.3.2 has a document's pages in a *tree*, and
  `PdfPages.GetKids` believed it: a `/Kids` entry leading back to a node the walk was already
  inside recursed until the stack ran out, and a stack overflow cannot be caught, so the process
  went with it. Opening the file was enough — the catalog asks for the pages while
  `PdfReader.Open` is still running — which makes it a denial of service on any program that opens
  a document it did not write. The walk now carries the nodes it is inside: a node that stands
  among its own ancestors is refused as a loop, naming it, and a tree nested more than 256 levels
  deep is refused as too deep, which is the case no loop detector catches — a chain of two thousand
  nodes repeats nothing and still died, at about the eighteen hundred frames the stack held. A node
  that two parents list is *not* a loop and still reads: it is a page counted twice, which is
  malformed but ends. Ending in principle is not enough, though: a chain of nodes each listing the
  next one twice doubles at every level, and forty levels were a trillion nodes to walk in a file
  of forty objects, so a walk entering more nodes than twice the objects the file holds is
  refused as well. Reported upstream as
  [empira/PDFsharp#361](https://github.com/empira/PDFsharp/issues/361).
- **A paragraph holding an inline element with no text in it renders.** `AddFormattedText("")` is
  accepted while the document is built, and `RenderDocument` then threw `ArgumentNullException`
  from `DocumentRelations.GetParent` — so a string that happened to be empty took the whole
  document with it, wherever the paragraph sat. An element holding nothing has nothing to descend
  to, so `ParagraphIterator` hands back its own empty collection as a leaf, one level above the word
  a leaf usually is. Everything the renderer does with a leaf tolerated that except the walk asking
  which hyperlink the leaf sits in: it stepped two levels at a time, so from there it landed on the
  collections rather than on the objects, never met the paragraph it stops at, and ran off the top
  of the document. It now takes one level at a time and stops at the top as well as at the
  paragraph. A hyperlink with no text in it is the same leaf and threw the same way. Reported as
  [PinataLabs/PdfPinata#45](https://github.com/PinataLabs/PdfPinata/issues/45).

## [0.2.0] - 2026-09-20

### Added

- **`PdfPage.AddFontProgram` and `PdfPage.TryGetFontProgramName`** — embed a font program you supply,
  under a name of your own, and get the resource name a content stream refers to it by. It is for a
  caller writing its own content stream; text drawn through `XGraphics` names its font with an
  `XFont` and needs none of this. The program is embedded as a composite font with Identity-H
  encoding, and the document holds one per name, so asking twice embeds it once.

  The plumbing underneath had been there since PDFsharp and could not be reached, which is why
  nobody had found that it did not work — see the fix below.

### Fixed

- **A malformed page tree says which node is wrong instead of raising a `NullReferenceException`.**
  `/Kids` was read as an array or else as a reference to one, with no third case, so a `/Kids` that
  was neither dereferenced null inside the reader; so did a reference whose target was not an array;
  and an entry in the array that was not an indirect reference threw `InvalidCastException` out of the
  loop. All three are reachable from a file, and none of them named the object at fault. They now
  throw `PdfReaderException` naming the page tree node and what was found there.

  A node with **no** `/Kids` is read as a node with no children, which is the tolerant reading the
  reader already takes for a node with no `/Type`. A `/Kids` whose reference the file never defines,
  or that is written as `null`, reads the same way — a null entry is the same as no entry.

- **A font program is held under its name rather than under a null key.**
  `PdfFontTable.GetFont(idName, fontData)` looked the font up under a hard-coded `null` and threw
  `ArgumentNullException` out of the dictionary; `TryGetFont(idName)` did the same and opened with
  `Debug.Assert(false)` besides, so in a Debug build it never reached the lookup it is documented to
  answer null from. Had the key worked, every program in a document would still have shared one
  entry, so the second name asked for would have been answered with the first name's font.

- **`PdfDocumentRenderer.Save(path)` honours `WorkingDirectory`.** The line meant to apply it called
  `Path.Combine` and threw the result away, so a relative path was written against the process's
  current directory and the property decided nothing.

  **This changes where a file lands** for a caller who sets `WorkingDirectory` and passes `Save` a
  relative path — which is what that caller was asking for, and the property has no other effect:
  nothing in this library reads `DocumentRenderer.WorkingDirectory`, and an image reaches the renderer
  as an `ImageSource` rather than as a path to resolve. A caller who passes an absolute path is
  unaffected either way, `Path.Combine` answering one with itself. To keep the old behaviour, leave
  `WorkingDirectory` unset and combine the path yourself.

### Changed

- **BREAKING: the doubled `MigraDoc.DocumentObjectModel` segment is gone from two namespaces.**
  `PinataLayout.DocumentObjectModel.MigraDoc.DocumentObjectModel.Shapes` is now
  `PinataLayout.DocumentObjectModel.Shapes`, which is where every other shape already was. The
  types that move are `ImageSource` and `PixelBuffer`. Both ship in the **PdfPinata** assembly, so
  this touches every consumer who registers an image backend. The internal
  `...MigraDoc.DocumentObjectModel.Resources` namespace moves the same way. To migrate, change
  `using PinataLayout.DocumentObjectModel.MigraDoc.DocumentObjectModel.Shapes;` to
  `using PinataLayout.DocumentObjectModel.Shapes;`. Type and member names are unchanged.

## [0.1.0] - 2026-09-18

### Added

- **Characters above the basic multilingual plane are drawn.** The font reader now reads the `cmap`
  format 12 subtable, which is the only one that reaches past U+FFFF. An emoji used to be three
  failures at once: a surrogate pair drew `.notdef` *twice* because each half was looked up
  separately, coverage could not answer for an astral character, and so font fallback could not be
  offered one either. All three went through `OpenTypeDescriptor.CharCodeToGlyphIndex` and all three
  are fixed together — including fallback, so a face that has the character can now rescue one that
  does not.

  A code point inside the basic multilingual plane is still answered out of format 4 even where the
  face carries both subtables. The two agree in practice, but that is not a reason to change which
  glyph an existing document draws.

- **Bold simulation is decided per face rather than once per string.** A family with no bold file has
  its boldness stroked and widened on; that is a property of the face, and a string that fell back is
  drawn out of more than one. A fallback with a real bold used to be stroked and widened anyway.
  Measuring agrees, so a line is laid out at the width the page draws.

- **Headings claiming PDF/UA-1 may not skip a level** (ISO 14289-1 7.4.2). `/H1` followed by `/H3` is
  refused at save time, naming the level that was skipped. Coming back up any distance is not a skip.
  From MigraDoc the level is `ParagraphFormat.OutlineLevel`, which is what a heading style sets.

- **`Footnote.Identifier`** — the `/ID` a note is known by in a tagged document, for when it has to
  mean something outside the document. Unset, the renderer generates `note1`, `note2` in citation
  order as before.

- **`XTextFormatter` flows text around things the caller puts in the block.** Give it obstacles and
  the lines whose band they stand in are narrowed around them, on either side, in any column.

  ```csharp
  var quote = new XRect(140, 150, 220, 108);        // where you drew it, in the block's own frame
  formatter.Obstacles.Add(new RectangleObstacle(quote, padding: 14));
  formatter.Columns = 2;
  formatter.DrawString(copy, font, XBrushes.Black, block);
  ```

  The geometry is asked one question per line — *at this band, which runs are free?* — and answers
  with a set of them, so an obstacle standing clear of both edges honestly reports a run either side.
  **The line is laid out in the widest run and the others are left empty.** That is a decision rather
  than a limitation: filling several would make one logical line span them, which justification,
  alignment and truncation all assume never happens. MigraDoc's `WrapStyle` settled the same way.

  `RectangleObstacle` is the only `IFlowObstacle` that ships. An ellipse, a polygon or an
  `XGraphicsPath` is a new implementation of that interface rather than a redesign — flatten,
  intersect the band, pair the crossings — but none is written, so text does not follow a silhouette
  yet. **Padding belongs to the obstacle**, not the formatter, because how much air a thing wants
  around it is a fact about that thing; it holds text off vertically as well as horizontally, so a
  line that would otherwise clear an image by a hair is pushed past it instead.

  Obstacles are positioned **relative to the layout rectangle and unrotated**. Layout is worked out
  in that frame and `Rotation` turns the drawing surface afterwards, so an obstacle given in it turns
  with the text. Supplying one while `Rotation` is set throws rather than guessing which frame was
  meant — rotate the `XGraphics` instead and leave `Rotation` alone.

  A band with nothing left in it moves the line down past what blocks it and tries again, rather than
  drawing across it. The drop cap is now an obstacle like any other, which is why a cap reserves room
  in the first column only: not a test on the column index any more, just where it stands.

  Not covered: contour wrapping, more than one run per line, per-side padding, and obstacles that
  push the block's top down — an obstacle narrows lines and never moves or grows the block.

- **Text flows beside a shape in MigraDoc.** `WrapFormat.Style` takes four new values — `Left`,
  `Right`, `Largest` and `Both` — and a shape carrying one of them stands in the area the following
  elements are laid out in rather than pushing them down the page.

  ```csharp
  var frame = section.AddTextFrame();
  frame.Width = Unit.FromCentimeter(4.5);
  frame.Height = Unit.FromCentimeter(4);
  frame.RelativeVertical = RelativeVertical.Paragraph;   // what makes it float at all
  frame.RelativeHorizontal = RelativeHorizontal.Margin;
  frame.Left = ShapePosition.Left;
  frame.WrapFormat.Style = WrapStyle.Right;              // the text runs down its right
  ```

  `Left` and `Right` name **the side the text occupies**, not the side the shape sits on. The
  opposite reading is equally natural and a caller who guesses wrong gets a page that looks
  deliberate and is backwards, so it is worth reading twice. `Largest` gives each line whichever
  side of the shape has more room. `Both` asks for either side; a line is given one span rather than
  every span, so it lays out as `Largest` does today, and the two are kept apart because they say
  different things and would part company if that changed.

  All four `WrapFormat` distances now mean something for a side-wrapped shape. `DistanceLeft` and
  `DistanceRight` hold the text off horizontally as they always claimed to; `DistanceTop` and
  `DistanceBottom` grow the obstacle vertically, so a line whose box would otherwise clear the shape
  by a hair is pushed past it instead. For a `TopBottom` shape they remain the element's own margins,
  unchanged.

  Not covered: contour wrapping (the shape is tested as a box), a shape spanning a page break, and
  wrapping beside a table — a shape too tall for the area left to it falls back to `TopBottom`
  rather than producing an obstacle that outlives its area. `XTextFormatter` is a drawing surface
  with no notion of a shape and is unaffected.

  **A document using one of the new styles cannot be read by an older version of this library.** The
  values are appended, so `TopBottom`, `None` and `Through` keep the numbers they had and an older
  document reads unchanged; but MDDDL writes the style by name, and an older reader meeting
  `Style = Left` refuses the file rather than falling back to a layout the document did not ask for.

  A document that asks for no side wrap lays out byte for byte as it did, pinned across ten
  documents and fourteen pages.

- `XTextFormatter.DropCap` — an initial letter set into the opening lines of a block, with those
  lines shortened to leave room for it.

  ```csharp
  var formatter = new XTextFormatter(gfx)
  {
      DropCap = new XDropCap(new XFont("Liberation Serif", 10, XFontStyle.Bold), lines: 3),
  };
  formatter.DrawString(text, body, XBrushes.Black, area);
  ```

  The depth is given in **lines**, not as a font size: lines are what the surrounding text is
  measured in, and a size implies a depth that is almost never a whole number of them. The formatter
  takes the first character of the text, scales it so its head is level with the head of the letter
  beside it and its foot rests on the baseline of the last line it is set into, reserves the room
  and narrows the lines that stand against it.

  The head is level by **cap height** rather than by the top of the line's box. A line's box reaches
  an ascent above its baseline and the letters in it reach only a cap height, the difference being
  the room the face keeps for accents; a cap hung from the box stands clear of the text it is set
  into by that much, magnified by the size of the cap. A face that declares no cap height in its
  OS/2 table gets the ascent, as it does everywhere else in the library.

  Placed by the glyph's **ink** where `GlobalFontSettings.GlyphOutlineProvider` is registered, so the
  cap sits flush with the margin rather than a side bearing's width inside it. Where no provider is
  registered it is placed by the advance instead — a drop cap does not require a backend seam.

  Behind it, `XTextFormatter` now works out the measure available to each line from where that line
  sits, rather than once for the whole block. A block with nothing narrowing it lays out exactly as
  it did, which is pinned byte for byte across the seventeen ways the formatter can be asked to lay
  text out.

- `PdfPage.Resize` and `PdfDocument.ResizePages` — change the size, shape or orientation of a page
  that already has content on it, in the document that holds it. The content is scaled into the new
  size rather than cropped by it, and the annotations of the page and the link destinations that
  point at it move with it.

  ```csharp
  page.Resize(PageSize.A5);                                  // fit the whole page in, centred
  page.Resize(PageSize.A4, PageOrientation.Landscape);       // reshape and refit
  document.ResizePages(PageSize.A4, PageOrientation.Portrait,
      new PageResizeOptions { AutoRotate = true });          // normalise a mixed batch
  ```

  `PageResizeOptions` carries the fit mode (`Fit`, `Fill`, `Stretch`, `None`), a nine-way
  alignment, a margin, `AutoRotate`, and switches for the annotation and destination passes.
  `PageResizeOptions.Default` and `PageResizeOptions.Crop` are the two common intents.

  Refused on a document that is encrypted, signed or tagged, rather than producing one whose
  signature no longer verifies or whose structure tree no longer describes the page.

  `PdfPage.Rotate` is unchanged and is still the free, lossless way to turn a page over without
  touching its content. See `docs/specs/page-resize.md`.

- 27 predefined page sizes that `PageSize` did not name: `A7`–`A10`, `TwoA0` and `FourA0` (the
  DIN 476 oversizes 2A0 and 4A0, spelled out because a C# identifier cannot begin with a digit),
  `B6`–`B10`, the whole ISO 269 `C0`–`C10` envelope series, and the untrimmed `SRA0`–`SRA4` stock.

  ```csharp
  page.Size = PageSize.C5;    // the envelope an A5 sheet goes into unfolded
  page.Size = PageSize.A7;
  ```

  With these, `PageSize` covers every size in the
  [pdfkit paper-size table](https://pdfkit.org/docs/paper_sizes.html), which it previously met only
  in part. Each is rounded to whole points as the existing entries are.

- 57 page formats that MigraDoc's `PageFormat` did not name. It knew twelve — A0–A6, B5, Letter,
  Legal, Ledger and P11x17 — and everything else had to be set as a `PageWidth` and a `PageHeight`.
  It now names every size `PdfPinata.PageSize` does: `A7`–`A10`, `TwoA0` and `FourA0`, the rest
  of the B series, the `C0`–`C10` envelopes, `RA0`–`RA5`, `SRA0`–`SRA4`, `JISB5`, and the North
  American and traditional sheets from `Tabloid` and `Executive` through to `QuadDemy`.

  ```csharp
  section.PageSetup.PageFormat = PageFormat.C5;
  ```

  The two enumerations stay separate types — MigraDoc records the format by name in MDDDL, and its
  sizes are held in the unit that defines them, whole millimetres for the ISO and DIN sheets, rather
  than rounded to whole points. The names now agree, which is what made them confusable.

  `P11x17` is kept alongside the `Tabloid` that names the same sheet, because MDDDL files hold it.

- Text state on `XStringFormat`, honoured by both drawing and measurement: `CharacterSpacing`,
  `WordSpacing`, `HorizontalScaling`, `TextRise` and `ObliqueAngle`, written as the PDF `Tc`, `Tw`,
  `Tz` and `Ts` operators and as a skewed text matrix.

  ```csharp
  var format = XStringFormats.Default;
  format.CharacterSpacing = 2;      // points after every glyph
  format.WordSpacing = 4;           // points after every space, on top of that
  format.HorizontalScaling = 80;    // percent
  format.ObliqueAngle = 12;         // degrees, leaning right
  gfx.DrawString("spaced out", font, XBrushes.Black, 20, 40, format);
  ```

  `Tw` counts the single-byte code 32 and is inert for a font embedded as Identity-H, which is what
  `GlobalFontSettings.DefaultFontEncoding` gives every `XFont` built without options of its own.
  Those have their words spaced out with a `TJ` array instead, so the same setting produces the same
  page whichever encoding the font uses.

- Stroked text. `DrawString` takes an `XPen` beside its `XBrush`, in the six shapes the brush-only
  overloads already came in. A brush alone fills the glyphs, a pen alone outlines them, both does
  both, and neither throws — as `DrawRectangle` has always answered the same question.

  ```csharp
  gfx.DrawString("outlined", font, new XPen(XColors.Black, 0.6), null, 20, 40);
  ```

- `XStringFormat.Underline` and `.Strikeout`, in the six shapes MigraDoc has always had —
  `Single`, `Words`, `Dotted`, `Dash`, `DotDash`, `DotDotDash` — plus `DecorationColor`, which draws
  the rule in a colour of its own. Setting them on the font through `XFontStyle` still works and
  still means one solid rule.

- `XLineAlignment.Hanging`, `.Ideographic` and `.SvgMiddle`, the three baselines the HTML canvas has
  and this did not. They are measured against the text rather than against the layout rectangle,
  which is what distinguishes them from `Near` and `Far`.

- `XTextFormatter` grew the paragraph options its own TODO list had named for years: `LineBreak`,
  `Indent`, `IndentAllLines`, `ParagraphGap`, `LineGap`, `Ellipsis`, `Rotation`, and `Columns` with
  `ColumnGap` for text that flows down one column and on into the next.

  ```csharp
  var formatter = new XTextFormatter(gfx)
  {
      Columns = 2, ColumnGap = 18,
      Indent = 12, ParagraphGap = 6,
      Ellipsis = XTextFormatter.DefaultEllipsis,
  };
  formatter.DrawString(text, font, XBrushes.Black, new XRect(40, 40, 500, 300));
  ```

- Named destinations. `PdfDocument.NamedDestinations` names pages and places on them, written into
  the catalog as a `/Names /Dests` name tree; `Resolve` reads one back out of a document, which
  until now only the import machinery could do. `PdfPage.AddNamedLink` and
  `PdfLinkAnnotation.CreateNamedLink` follow a name.

  ```csharp
  document.NamedDestinations.Add("chapter-3", document.Pages[7], top: 500);
  page.AddNamedLink(rect, "chapter-3");
  ```

  A name outlives the page it stands for. Insert a page in front of page 7 and every link to page 7
  is wrong; every link to `chapter-3` is still right.

- `XGraphics.AddWebLink`, `AddDocumentLink`, `AddNamedLink` and `AddNamedDestination`, which take
  the coordinates the drawing methods take. An annotation is placed in default page space, measured
  up from the bottom left, and everything drawn is placed in world space, measured down from the top
  left — the conversion was the whole of what stood between drawing a piece of text and linking it.

- `PdfPage.MarkMargins` and `PdfPage.DrawCropMarks` — the room on the sheet outside the bleed, and
  the eight standard crop marks drawn into it. A page with a trim margin gets both without asking:
  the allowance is 5mm on each edge, and the marks are drawn when the document is saved.

  ```csharp
  page.Size = PageSize.A5;
  page.TrimMargins.All = XUnit.FromMillimeter(3);   // the bleed, as before
  page.MarkMargins.All = XUnit.FromMillimeter(5);   // the room for marks; this is the default
  page.MarkMargins.All = 0;                         // no room, and so no marks
  ```

  Two marks meet at each corner of the trimmed page, one on each of its edges, and each runs
  outward from the bleed to the edge of the sheet. None crosses the bleed, so none can be mistaken
  for artwork or land on the part of the page that survives the cut.

- A `Bleed` demo in the demonstration app: a photograph drawn from negative coordinates so that it
  runs off three edges of the page, with the trim edge marked and the five page boxes listed. See
  `docs/specs/demonstration-app.md`.

- **A MigraDoc field can say what it reads as without a renderer.** `FieldEvaluator.Evaluate` takes
  a field and a `FieldEvaluationContext` — the page and section it is being asked on, the two page
  counts, the print date, and a way to look a bookmark up — and answers the string the field stands
  for. `FieldEvaluator.IsField` says which document objects have a value at all.

  ```csharp
  var context = new FieldEvaluationContext { DisplayPageNumber = 27 };
  var field = paragraph.AddPageField();
  field.Format = "ALPHABETIC";

  FieldEvaluator.Evaluate(field, context);   // "AA"
  ```

  It answers `null` rather than a placeholder when the value is not knowable yet — an unplaced
  bookmark, or a page count for a document or section still being laid out. What to show in the
  meantime stays a rendering decision, and `ParagraphRenderer` still makes it.

  `NumberFormatter`, which writes the roman numerals and letter sequences a numeric field's `Format`
  asks for, moves to `PinataLayout.DocumentObjectModel` with it and is public for the first time.

### Changed

- **BREAKING: the library is renamed, packages and namespaces both.** PdfSharpCore is now
  **PdfPinata** and MigraDocCore is now **PinataLayout**, published from
  [PinataLabs/PdfPinata](https://github.com/PinataLabs/PdfPinata) under new NuGet IDs:
  `PdfSharpCore.*` becomes `PdfPinata.*` (`PdfPinata`, `.Skia`, `.ImageSharp`, `.HarfBuzz`,
  `.Signing`, `.EInvoice`, `.Charting`) and `MigraDocCore.*` becomes `PinataLayout.*`
  (`.DocumentObjectModel`, `.Rendering`). Every namespace follows its package, so migrating is a
  package swap and a replacement of `PdfSharpCore` with `PdfPinata` and `MigraDocCore` with
  `PinataLayout` in `using` directives; the inner `MigraDoc.` segment of the layout namespaces is
  unchanged. Type and member names are unchanged. All nine packages now version in lockstep from
  one release tag, starting at 0.1.0.
- **BREAKING: the open mode is enforced where it is named.** `PdfReader.Open` has always taken a
  `PdfDocumentOpenMode`, and twelve operations have always guarded on `PdfDocument.CanModify` before
  changing the document. `CanModify` returned `true` unconditionally, with the real check commented
  out beside it, so none of those guards enforced anything. A caller who opened a document
  `ReadOnly` or `Import` and then added a page got the page, and found out much later — if at all —
  that none of it was going to be written, usually through a failure from a module three layers away
  that mentioned a different concept.

  `CanModify` is now `!IsReadOnly`, which is the check that already worked and was already public.
  These now throw `InvalidOperationException` on a document opened `Import` or `ReadOnly`, where
  before they silently did the wrong thing:

  `PdfDocument.AddPage`, `InsertPage`, `PlacePage`, `ImportPage`, `DuplicatePage`, `MovePage`,
  `Save`, and the `Version`, `PageLayout`, `PageMode` and `Language` setters; `PdfDocument.Pages.Add`, `Insert`,
  `Place`, `Import`, `Duplicate`, `MovePage`, `InsertRange`, `Remove` and `RemoveAt`.

  `XGraphics.FromPdfPage` and `PdfPageResizer` refused these documents already and still do; they
  now say so in the same words. **If your code is affected, it was producing a document that would
  not have been written correctly** — open with `Modify` (or `Append`, for an incremental save)
  instead. The break is a refusal rather than a silent difference, on purpose: you find out at the
  call.

  Every refusal now names both the mode the document was opened with and the modes the operation
  needs, because the mistake is nearly always at the call to `Open` rather than at the operation
  that reports it:

  ```text
  This document was opened with PdfDocumentOpenMode.ReadOnly and adding a page needs a document
  opened with PdfDocumentOpenMode.Modify or PdfDocumentOpenMode.Append.
  ```

  Two things deliberately did **not** become refusals. `PdfDocument.Close` dropped its guard rather
  than gaining teeth — closing a document is not changing it, and it writes only when the document
  was constructed on an output stream, which a document read by `PdfReader` never is. And
  `PdfDocument.PageCount` had a second way of counting behind the same dead guard, labelled
  "PdfOpenMode is InformationOnly", which no document had ever taken; it is gone rather than being
  taken for the first time, because every mode reads the file in full and builds the page tree.

- **BREAKING:** `ImageSource.IImageSource.SaveAsPdfBitmap(MemoryStream)` is replaced by
  `PixelBuffer GetPixels()`, and `XImage.AsBitmap()` by `XImage.GetPixels()`. Anyone who has written
  an implementation of that interface has to change the member; anyone who called `AsBitmap()` for
  the bytes gets pixels instead of a BMP file.

  ```diff
  - void SaveAsPdfBitmap(MemoryStream ms)
  + PixelBuffer GetPixels()
  ```

  What the member handed over was a hand-built 32bpp bottom-up BMP that nothing outside this library
  ever read: `PdfImage` parsed the magic number, the declared length, the width, the height, the
  plane count, the bit count and the compression field back out at fixed byte offsets, and every one
  of those fields was written moments earlier by the other half of the same call. The two ends of it
  also flipped the rows in opposite directions, so the file was bottom-up and the pixels that came
  out of it were the top-down ones that went in — correct by two mistakes cancelling.

  `PixelBuffer` says the one thing that was ever really being passed: `Width`, `Height` and a
  `ReadOnlyMemory<byte>` of tightly packed, top-down, straight-alpha **BGRA**, four bytes per pixel
  and no stride padding. There is no format tag, because there is one format. Grayscale and CMYK
  stay unsupported exactly as they were — the `components`/`bits`/`hasAlpha` parameters that used to
  suggest otherwise were called from one place with one set of values, and the grayscale branch
  under them could never run.

  Both backends produce the same bytes for the same image, as they did before. `PdfPinata.Skia`
  no longer writes a `BITMAPFILEHEADER` and `BITMAPINFOHEADER` by hand for a format SkiaSharp
  refuses to encode, and `PdfPinata.ImageSharp` no longer drives `BmpEncoder`'s general
  conversion — it performs the R/B reorder itself through ImageSharp's own bulk pixel conversion,
  where before it was borrowing one from an encoder as a side effect of BMP's on-disk byte order.

- **BREAKING:** `IFontFallback.FamiliesFor` takes an `int` code point where it took a `char`.
  Anyone who has written an implementation of that interface has to change the signature; the body
  usually needs no change, because a `char` widens to an `int` and the values below U+10000 are the
  same numbers.

  ```diff
  - public IEnumerable<string> FamiliesFor(char character, bool isBold, bool isItalic)
  + public IEnumerable<string> FamiliesFor(int codePoint, bool isBold, bool isItalic)
  ```

  The reason is the point of the change rather than a detail of it: neither half of a surrogate pair
  is a character and no `cmap` maps one, so a `char`-shaped question about an astral character could
  only ever be answered "nobody". The interface's own documentation used to say so. `FontFallbackList`
  and everything else in this repository are updated.

- **A tagged document no longer nests one marked-content sequence inside another.** A sequence
  carrying an MCID is a content item of exactly one structure element, so nesting two made the inner
  glyphs belong to both — a footnote mark was claimed by its `/Reference` and by the `/P` around it,
  with nothing to say which a reader should announce. The outer sequence is now suspended and resumed
  instead, which an element supports because it may own several content items. Content streams of
  tagged documents differ accordingly; the structure tree does not.

- **BREAKING:** a page with `PdfPage.TrimMargins` set is saved with different page boxes. The three
  areas now nest as the PDF specification describes them — `/MediaBox` ⊇ `/BleedBox` ⊇ `/TrimBox` —
  where `/BleedBox` used to be written equal to `/MediaBox`, leaving nowhere on the sheet for a crop
  mark to go. The sheet is correspondingly larger, by the new `MarkMargins` on each edge.

  Nothing changes for a page that sets no trim margin, which is almost every page: the whole feature
  stays invisible to a document that does not ask for it.

  `page.MarkMargins.All = 0` reproduces exactly the boxes this library wrote before, for a caller
  whose downstream tooling expects them.

- **BREAKING:** `IXGraphicsRenderer.DrawString` takes an `XPen` before its `XBrush`, so that text
  can be outlined as well as filled. The interface is public; anything implementing it outside this
  repository has to add the parameter. `XGraphicsPdfRenderer` is the only implementation here.

  Migration is `DrawString(s, font, brush, rect, format)` →
  `DrawString(s, font, null, brush, rect, format)`.

- `XGraphics.MeasureString(text, font, stringFormat)` now answers through the format it is given.
  It took one and passed `XStringFormats.Default` on instead, which nothing noticed while a format
  held only alignment — where a string sits does not change how wide it is. Every text state
  property added above does change how wide it is, and a width measured without them is what decides
  where a line wraps.


- **BREAKING:** MigraDoc's `PageFormat.B5` measured 182 mm × 257 mm, which is the **JIS** B5 sheet,
  not the ISO one. It was the only B format the enumeration had, so nothing sat beside it to
  contradict it; now that `B0`–`B4` and `B6`–`B10` are named, a JIS sheet in the middle of an ISO
  series would be a sheet that is not half of the one above it. `B5` is now ISO B5, 176 mm × 250 mm.

  A section set to `PageFormat.B5` therefore reflows: it loses 6 mm of width and 7 mm of height, and
  text that fitted a line may no longer. To keep the sheet you had, use the new `PageFormat.JISB5`,
  which measures exactly what `B5` used to.

- **BREAKING:** the `PdfPage.Size`, `PdfPage.Width` and `PdfPage.Height` setters now throw
  `InvalidOperationException` when the page already has content on it. Before this change they
  wrote a new media box and nothing else, which cropped the page rather than resizing it —
  silently, with no exception and no warning. Setting them on a page with no content, which is the
  usual `document.AddPage(); page.Size = PageSize.A4;`, is unchanged.

  Migration is `page.Size = X` → `page.Resize(X)`.

  If you were relying on the crop, note what it actually did: it wrote the new box at the origin,
  and the origin of a PDF page is its **bottom-left** corner, so it kept the foot of the page and
  cropped the heading away. `page.Resize(X, PageOrientation.Portrait, PageResizeOptions.Crop)`
  crops from the **top left** instead, which is almost certainly what was wanted. To reproduce the
  old anchoring exactly, ask for `PageAlignment.BottomLeft`.

- `XGraphicsPath.AddString` produces a real path. Both overloads used to report through
  `DiagnosticsHelper` and return, so the path stayed empty and whatever was being written vanished
  from the page — no exception, no warning.

  ```csharp
  GlobalFontSettings.GlyphOutlineProvider = new SkiaGlyphOutlineProvider();   // once

  var path = new XGraphicsPath();
  path.AddString("HEADLINE", new XFontFamily("Arial"), XFontStyle.Bold, 96, box, XStringFormats.TopLeft);
  gfx.DrawPath(new XLinearGradientBrush(box, XColors.Red, XColors.Blue, XLinearGradientMode.Horizontal), path);
  ```

  The glyphs are placed by the same arithmetic `DrawString` places them by, so a path agrees with
  the text it stands in for. **To stroke text you still do not need a path** — `DrawString` takes a
  pen as well as a brush. Come here for what that cannot do: fill glyphs with a gradient, clip an
  image to their shapes, or widen them as geometry.

- `IGlyphOutlineProvider` and `GlobalFontSettings.GlyphOutlineProvider` — a third static seam beside
  `GlobalFontSettings.FontResolver` and `ImageSource.ImageSourceImpl`, supplying the glyph geometry
  `AddString` needs. `SkiaGlyphOutlineProvider` and `ImageSharpGlyphOutlineProvider` ship with the
  two backends; unset, it throws an `InvalidOperationException` naming the property and the packages,
  exactly as the other two seams do.

  It exists so that the core package keeps carrying no font dependency: reading contours out of a
  font means a `glyf` decoder for TrueType and a Type 2 charstring interpreter for PostScript
  outlines. Both backends already ship a library that does both, so PostScript (CFF) families work
  from the first day rather than producing an empty path.

  It is a separate interface rather than a member on `IFontResolver`, which every consumer with a
  resolver of their own implements and which a new member would break. A provider reads its font
  bytes *through* the registered resolver, so the two cannot disagree about which face a family means.

- `XLinearGradientBrush` and `XRadialGradientBrush` honour the alpha of their colours. A gradient
  between a transparent colour and an opaque one used to paint a flat opaque band over whatever it
  was meant to veil, because a shading dictionary carries colour and no alpha anywhere.

  ```csharp
  var scrim = new XLinearGradientBrush(band,
      XColor.FromArgb(0, 0, 0, 0), XColors.Black, XLinearGradientMode.Vertical);
  gfx.DrawRectangle(scrim, band);   // now fades out as well as across
  ```

  Where either colour's alpha is below 1, the shading pattern is painted under a luminosity soft
  mask built from the same axis or circles, the same extent and the same interpolation, so the
  alpha ramps exactly as the colour does. The mask is taken off again before anything else is
  drawn, and two gradients on one page each carry their own. A gradient whose colours are both
  opaque takes none of this: no soft mask, no extended graphics state, no transparency group.

- `XLineAlignment.BaseLine` accepts a layout rectangle of any height. It threw
  `InvalidOperationException` unless the height was exactly `0`, which made `XStringFormats.Default`
  — the format a caller reaches for when not thinking about formats, and `BaseLineLeft` — throw on
  `DrawString(text, font, brush, rect)`, the most natural overload there is.

  ```csharp
  gfx.DrawString("Anchored", font, XBrushes.Black, new XRect(20, 60, 300, 20));  // used to throw
  ```

  The baseline sits on the rectangle's top edge and the height is ignored, which is what the
  placement arithmetic always did — nothing but the guard read the height for this alignment. Code
  passing a zero-height rectangle is unaffected. `XGraphicsPath.AddString` carried a second copy of
  the same guard and has lost it too.

- **BREAKING (narrow):** a MigraDoc table row marked with `HeadingFormat` which cannot be part of the
  heading now throws `InvalidOperationException` while the document is being formatted, naming the
  row. The heading a table repeats onto its later pages is the run of marked rows beginning at the
  first row; a row marked outside that run was discarded without a word.

  ```csharp
  table.Rows[0].HeadingFormat = true;   // a title band
  table.Rows[1].HeadingFormat = true;   // the column names — mark both, or neither repeats
  ```

  The rule has not changed, only its silence. Every document this throws for is a document that
  asked for a repeating heading and did not get one, so it was already producing the wrong output;
  the message says which row to mark or unmark. It is raised during formatting, before any page is
  written, so a caller never receives half a document because of it.

### Fixed

- **An annotation's colour came back a shade darker every time it was saved.**
  `PdfAnnotation.Color` reads `/C` back by multiplying each component by 255 and truncating, and a
  component is written as a fraction of 255 to the seven decimal places `PdfWriter` gives a real —
  so 127 goes out as `0.4980392` and comes back as 126.999996, which truncates to 126. Rounded now.
  `PdfLineAnnotation.Interior` read `/IC` the same way and is fixed with it.

- **A heading containing an `InfoField` lost that text from its outline entry.** The predicate that
  decided which of a heading's parts contribute to its PDF outline title tested for `DocumentInfo` —
  the document's own info object, which is never one of a paragraph's parts — and so never
  recognised the `InfoField` that is. A heading reading "Part One: Annual Report" on the page
  appeared in the outline as "Part One: ".

  This is an observable change: an outline or table-of-contents entry may now carry text it did not
  before. Nothing else about how the field draws is affected — it always drew correctly on the page.

- **A drop cap too wide for its column threw the text outside the column.** The cap is scaled to its
  own depth and nothing holds its width to the measure, so a deep cap set into a narrow column can
  leave the lines beside it no room at all. `XTextFormatter` had no way to say that: a measure
  starting at or past its own right limit read to the layout loop as a very narrow line rather than
  as no line, and the loop places the first block of a line whether it fits or not — which is right
  for a word wider than its measure and wrong here. The result was one word per line, drawn past the
  right edge of the column, for as many lines as the cap was deep, with nothing thrown.

  A line whose band has no room in it is now moved down to the foot of what blocks it and laid out
  there, so the text begins below the cap instead of beside it. The move always advances by at least
  one line, so an obstruction level with the band it blocks cannot stall the loop; where there is no
  room below either, the text is dropped exactly as text that runs past the last column is.

  Text that fits beside its cap is unaffected, and output with no cap at all is unchanged.

- **A trimmed page grew every time it was saved.** `PrepareForSave` derived the sheet by adding the
  trim margins to `PdfPage.Width`, and `Width` reads the media box that `PrepareForSave` had just
  overwritten with the sheet. So saving a document to a stream and then to a file — an ordinary
  thing to do — produced two files of different sizes, the second larger by another sheet's worth of
  margin on every edge.

  The size the page was asked for is now remembered before the media box is grown into the sheet.
  The same fix makes `Width` and `Height` go on reporting the page after it has been saved, where
  they used to start reporting the sheet, which moved every right-aligned and bottom-aligned thing
  measured off them.

- **An uneven trim margin put `/TrimBox` on the wrong edges.** `PrepareForSave` inset Y1 by the
  *top* margin and Y2 by the *bottom* one, and Y1 is the bottom edge of a PDF rectangle — so the two
  were swapped, and the trim box disagreed with the drawing origin, which was placed correctly. A
  page whose top and bottom margins match, which is the usual case and the case the original
  numbers were copied from, could not show the difference.

  Both this and the growth above were found by writing the first tests `TrimMargins` has ever had.

- **No gradient this library produced was visible in a conformant reader.** The interpolation
  function of an RGB shading was given a fourth value — the colour's alpha, which is not a colour
  component — so the function was wider than the `/DeviceRGB` space it fed. That is malformed, and
  Ghostscript answers it by painting nothing at all: a page with a gradient on it came out blank
  where the gradient was.

  ```text
  /ColorSpace /DeviceRGB
  /Function << /C0 [1 0 0 1] /C1 [0 0 1 1] ... >>   before, four values for three components
  /Function << /C0 [1 0 0]   /C1 [0 0 1]   ... >>   after
  ```

  Alpha now goes where alpha belongs, into the soft mask described above. CMYK gradients are
  unaffected — four values is what that colour space has always required. Nothing else about how a
  gradient is written changes: the content stream, the shading geometry and the pattern matrices
  are byte for byte what they were.

- Bold simulation measured multi-line text too wide. The widening it adds was counted over the whole
  string and charged to the widest line, so a simulated-bold string of three lines measured as
  though every character of all three sat on one of them.

- A line feed was charged a character spacing for a glyph it never drew.

- `PageSize.Executive` measured 540 × 720 points (7.5 × 10 inch), which is not the Executive sheet.
  It is 7.25 × 10.5 inch and now converts to 522 × 756 points — the size its own documentation
  always claimed, and the one ISO, `System.Drawing.Printing.PaperKind.Executive` and every other
  library give. A page asking for `PageSize.Executive` changes size as a result; a page whose width
  and height were set in points does not.

- A page-level transparency group (`/Group << /S /Transparency /CS /DeviceRGB >>`) was written onto
  every page of every saved document, whether or not anything on the page painted with transparency
  and whether or not the page arrived with one. Opening a document and saving it again was enough to
  add one to all of its pages.

  A transparency group is not inert: it tells a reader to composite the page as a unit against the
  backdrop, which can change how overprint and non-RGB content render, and `/CS /DeviceRGB` was
  imposed on pages whose content is not RGB.

  A page is now given a group only where it needs one: where something drawn on it uses an alpha
  below 1, or where an image or form placed on it paints with transparency of its own — a soft mask,
  a blend mode that reads what is underneath, or a transparency group of its own. A page whose
  content is opaque throughout, and an imported page that came in without a group, are written
  without one. A page that came in **with** a group keeps the one it had, as before.

  Documents that PdfPinata produced before this change are unaffected on the way in; they keep
  the group they were written with. The one visible difference is on the way out: opaque pages get
  smaller and no longer claim a colour space they do not use.

- Drawing a page of another document with `XPdfForm` dropped that page's transparency group. A group
  describes the content it wraps, and the content was being moved into a form XObject while the
  group was left behind in the document it came from, so the imported page arrived composited
  against the wrong backdrop. It is now imported with the rest of the page. The equivalent path for
  a page of the *same* document, which a page resize uses, already moved the group across.

- A PDF null was read as though it were the thing it stands in for. `/SMask null` in a graphics state
  or an image counted as a soft mask, which put a transparency group back onto pages whose content is
  opaque; `/Group null` on an imported page was cast to a dictionary, which threw rather than drew;
  and an indirect null anywhere in an imported page — `/SMask 6 0 R` with `null` in object six — hit
  a debug assertion while the page was being imported. A null now reads as the absent entry it is.

### Removed

- **BREAKING:** `PdfDocumentOpenMode.InformationOnly`. It carried `// TODO: not yet implemented`
  beside it from the beginning and never was implemented: the fast partial read it named — stop at
  the trailer so that `Info` can be had cheaply for a directory full of files — does not exist, and
  the reader has always read every object whatever mode it was handed. What a caller actually got
  was a document read in full that could not be modified, which is `ReadOnly` under another name.
  Code that names it will no longer compile; use `PdfDocumentOpenMode.ReadOnly`, which is what it
  was doing.

  **The other four members now state their values, and `3` is deliberately left vacant.** The C#
  compiler inlines an enum constant at the call site, so an assembly compiled against an earlier
  version goes on passing the number it was compiled with. Letting `Append` slide from `4` to `3` to
  close the gap would have silently redirected every such caller into the removed mode's place —
  the exact failure the enum's own note has warned about since `Append` was added. An old assembly
  passing `3` now hands over a value the enum does not define, and `IsReadOnly` answers anything
  that is not `Modify` or `Append` the same way, so that caller keeps the behaviour
  `InformationOnly` always had. A new member goes after `Append`; `3` stays vacant.

- **BREAKING:** `PdfPinata.Text.ScriptItemizer` and `PdfPinata.Text.ScriptRun` are internal.
  Script itemisation asked of a whole paragraph gives a plausible answer that disagrees with the
  bidirectional algorithm about where a run ends: UAX #24 sweeps a space into whichever script it is
  beside, and *beside* is not a property the paragraph can settle — asked of the whole of
  "one من" the space goes with the Latin, and UAX #9 then places it inside the Arabic. So the
  obviously-named public entry point was silently wrong on exactly the input this subsystem exists to
  get right, and it is now reachable only from `TextItemizer`, which asks it once per bidirectional
  run.

  Use `TextItemizer.Itemize`, which composes both algorithms and gives the bidi-correct answer. Its
  `TextRun` carries `Script` and `ScriptCode` exactly as `ScriptRun` did, plus the direction, and it
  hands the runs back in the order they are drawn. `UnicodeScript` and
  `UnicodeProperties.ScriptOf`/`ScriptCode` are unaffected and stay public.

- **BREAKING:** `PdfDocumentOptions.EnableCcittCompressionForBilevelImages`. The CCITT encoder this
  option gated was unreachable, so the option had no effect on any document — setting it changed
  nothing. Code that sets it will no longer compile; delete the assignment. No PDF that this library
  produces changes as a result.
- The CCITT Group 3/4 fax encoder (`PdfImage.FaxEncode.cs`) — `DoFaxEncoding`,
  `DoFaxEncodingGroup4`, and their `BitReader`/`BitWriter` helpers. Its only two call sites were the
  unreachable code removed below. Reading `/CCITTFaxDecode` streams from existing PDFs is unaffected;
  that is a separate path in `Pdf.Filters/Filtering.cs`.
- `PdfImage.ReadIndexedMemoryBitmap`, which had no callers. It could not have worked if called: it
  never filled its `MemoryStream`, so its `streamLength > 0` guard skipped the whole method body.
- The unused image importer subsystem in `PdfPinata/Drawing.Internal/` — `ImageImporter`,
  `ImageImporterBmp`, `ImageImporterJpeg`, `ImageImporterRoot`, `IImageImporter`, and the
  `StreamReaderHelper`, `ImportedImage`, `ImageInformation`, `ImagePrivateData` and `ImageData`
  types it defined. Nothing constructed it; `ImageImporter.GetImageImporter` had no callers and
  `ImageImporterBmp.PrepareImage` was an unimplemented stub. Every type was `internal` and the
  assembly has no `InternalsVisibleTo`, so nothing outside could reach them either.

Image handling continues to go through `PdfImage.ReadTrueColorMemoryBitmap`, which was already the
only live path. All 2,617 removed lines were unreachable before removal.
