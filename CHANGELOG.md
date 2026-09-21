# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project
adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

This file starts at the entry below. Changes before that point are recorded only in the git history.


## [Unreleased]

### Added

- **`XBaseGradientBrush.ExtendLeft` and `ExtendRight`** — whether a gradient goes on past its start
  and its end in the colour of that end, written as the shading's `/Extend`. The start and end are
  a linear gradient's two points and a radial gradient's two circles, so `ExtendRight` is what fills
  the corners a radial gradient leaves unpainted outside its outer circle, and `ExtendLeft` is what
  fills the hole inside a first circle whose radius is not zero. Both are false by default and write
  nothing then, so an existing document is written byte for byte as before. The names are PDFsharp's,
  where both properties exist and have no effect (empira/PDFsharp#321).

- **A Gradients demo and a Gradients page on the documentation site**, covering both brushes, two-
  centre radial gradients, extension and a brush's own transform.

### Fixed

- **A chart line format that says `Visible = false` is no longer drawn as a hairline**
  (empira/PDFsharp#287). The converter turns a hidden format into a pen of width 0, and PDF strokes
  a width of 0 as the thinnest line the device can draw rather than not at all. The column plot
  area already skipped such a pen; the line, area, bar and pie plot areas did not, and neither did
  the legend — which also drew a line chart's key at a width of 1 whatever the series said. A hidden
  series line, area outline, bar border or pie sector border is now not stroked, in the plot area or
  in the legend. A line series' markers are still drawn, and so is an area's fill. A format that
  sets a width or a colour but never `Visible = true` was already converted to width 0, and is now
  not drawn either, as a column's border already was.

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
