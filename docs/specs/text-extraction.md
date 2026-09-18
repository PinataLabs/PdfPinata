# Spec — reading the text back out of a page

What text extraction covers, and what it deliberately leaves out.
Gap **G7** of the competitive gap analysis.

| item | what | status |
|---|---|---|
| 1 | `ToUnicodeCMap` — reads a font's `/ToUnicode` map | done |
| 2 | `FontInfo` — code width, character mapping, glyph advances | done |
| 3 | `PdfTextExtractor.ExtractRuns` — text with position and size | done |
| 4 | `PdfTextExtractor.ExtractText` — the page as a string | done |
| 5 | Per-glyph bounding boxes | not done, **deliberately** |
| 6 | Layout analysis — columns, blocks, reading order | not done, **deliberately** |
| 7 | `bfrange` in both forms, and multi-unit destinations | done |

Covered by `PdfPinata.Test/IO/TextExtractionTests.cs` and `TaggedTextExtractionTests.cs`.

---

## The gap

Everything needed was already here and none of it was connected. `Pdf.Content/CParser.cs` parses a
content stream, `Pdf.Content.Objects/Operators.cs` has the full operator table, and
`ContentReader.ReadContent(page)` hands back a tree of them. Nothing turned that into text. A library
that writes PDFs could not read back a word of its own output.

PdfPig (Apache-2.0) owns this niche in .NET and does more of it than this does. The case for having
it here at all rests on two things: redaction needs extraction **in process**, because removing text
means rewriting the content stream it came from; and a document with a structure tree can be
extracted in reading order, which an untagged file cannot be at any price — see
`docs/specs/tagged-pdf-accessibility.md`.

## How it works

A walker over the operator tree, keeping the state the specification describes:

```text
 q / Q  ──► the graphics stack, so a transform is undone when its scope ends
 cm     ──► the current transformation matrix
 BT     ──► text matrix and line matrix both to identity
 Tf     ──► which font, and at what size
 Td TD Tm T* ' "  ──► where the next line starts
 TL Tc Tw Tz Ts Tr ──► leading, spacing, horizontal scale, rise, render mode
 Tj TJ  ──► show, and advance the pen by what was shown
```

Each shown string is split into codes — one byte or two, as the font says — and each code is looked
up in the font's `/ToUnicode` map. **Without that map extracted text is glyph numbers**: a font
embedded as Identity-H writes the index of the glyph in the font file, which bears no relation to the
character, so the same word in two documents subsetting the same face is two different runs of
numbers.

That the round-trip tests pass is therefore two independent pieces agreeing:
`Advanced/PdfToUnicodeMap.cs` writes the map, `Extraction/ToUnicodeCMap.cs` reads it, and neither was
written from the other.

## Decisions worth knowing

**One run per show-text operator, not one box per glyph.** A per-glyph box is exact only when every
glyph's own advance is known and correctly composed with the text matrix, the horizontal scale, the
character spacing and the rise. A run's origin and total width need none of that composed per glyph
and are exact. Reporting an approximate box would be worse than reporting none, because a caller
cannot tell an approximate one from an exact one.

**Runs come back in drawing order, which is the order the producer chose.** It is frequently reading
order and is not required to be. Sorting them into the order a person would read is layout analysis.

**Word spacing applies to the single byte 32 and to nothing else.** Not to a two-byte code whose low
byte happens to be 32 — that is the trap in the advance arithmetic, and it is why the code checks the
code length as well as the value.

**Text in render mode 3 is skipped.** That is the invisible layer under a scanned page, and a caller
asking what the page says rarely wants it twice. This is a decision the class makes and does not yet
expose; the day somebody needs it, it becomes an option.

**A `/ToUnicode` destination is a string, not a scalar.** Reading it as one number and converting
with `char.ConvertFromUtf32` throws for anything longer than a single code unit — `<00660069>`, the
ligature "fi", reads as 6684777 — and the exception came out of *extraction* rather than out of the
map, taking the whole page with it. The destination is kept as text, and the increment across a range
applies to its last code unit, as the specification says.

**The array form of `bfrange` is read, and it had to be.** `<lo> <hi> [<d1> <d2> …]` gives one
destination per code rather than an arithmetic run. This note used to say the form was not read and
that the codes it covered came back unmapped; that was wrong twice over. Collecting every hexadecimal
string in the block and stepping through them three at a time does not skip an array — it swallows
the array's elements into the same stream and shifts the stride by as many as it holds, so every
later entry in the block mapped the wrong codes to the wrong text. Wrong text is worse than absent
text, which is the argument this note was making in favour of a behaviour it did not have.

**`CSequence` may not be iterated with `foreach`.** It implements `IEnumerable<CObject>` and its
generic enumerator throws `NotImplementedException`, so a `foreach` over one compiles and fails at
run time. Index it. The pre-existing `PdfPinata.Test/Helpers/TextOperators.cs` had already worked
around this; the workaround is now explained where the next person will hit it.

## What is deliberately left out

- **Per-glyph bounding boxes.** See above. The advance data to do it properly is loaded already, so
  this is a smaller job than it was — but it is a separate one.
- **Layout analysis.** Recursive XY-cut, Docstrum, nearest-neighbour; block and column detection;
  ALTO / PageXML / hOCR export. This is most of what PdfPig offers beyond the above.
- **`/Encoding` with `/Differences`.** A simple font with no `/ToUnicode` falls back to reading its
  codes as Latin-1, which is right for the standard encodings over the range that matters and wrong
  for a font that remaps its glyphs by name. Doing it properly needs the Adobe glyph list.
- **Extracting from a form XObject.** Only the page's own content stream is walked; text inside a
  `Do`-invoked form is not followed into.
- **Vertical writing mode.** The advance is applied along X unconditionally.

## Related

- `docs/specs/tagged-pdf-accessibility.md` — a structure tree is what would make extraction return
  reading order rather than drawing order.
- Redaction (gap N2 of the analysis) is the main reason to have this in process rather than compose
  with PdfPig.
