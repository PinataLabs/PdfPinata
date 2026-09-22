# Spec — finding "startxref" in a file bigger than a string, issue #390

[empira/PDFsharp#390](https://github.com/empira/PDFsharp/issues/390) reports
`System.OutOfMemoryException` out of `PdfReader.Open(path, PdfDocumentOpenMode.Import)` on a valid
PDF of about 1 GiB that every reader opens, with this stack:

```text
System.String.Ctor(Char[] value)
System.Text.Encoding.GetString(Byte[] bytes, Int32 index, Int32 count)
PdfSharp.Pdf.IO.Lexer.ScanRawString(Int64 position, Int32 length)
PdfSharp.Pdf.IO.Parser.ReadTrailer()
```

The reporter also notes that a real 13 GiB document opens and merges without trouble, so the size of
the file is not on its own what decides it. That is the important half of the report, and it is
right: what decides it is where `startxref` is.

The same defect was in this fork, under the name `Lexer.ReadRawString`, and reproduced on the
reported file byte for byte.

---

## What the file looks like

The reported document is a one-page PDF whose body, cross-reference table and trailer occupy the
first 2 150 bytes. `startxref` is at offset 2 130. Everything after it — 1 073 739 674 bytes, all
`A` — is a single `%` comment, and then `%%EOF` is the last five bytes of the file.

That is legal. Implementation note 18 of Appendix H asks only that `%%EOF` appear within the last
1 024 bytes, which it does; nothing says how far behind `%%EOF` the `startxref` line may be, and a
comment is white space to a parser. Readers that scan backwards for the word find it.

## What the reader did

`Parser.ReadTrailer` looked for `startxref` in the last 1 030 bytes of the file, and when it was not
there fell back on this:

```csharp
if (idx == -1)
{
    // If "startxref" was still not found yet, read the file completely.
    if (length > int.MaxValue)
        //TODO: Implement chunking to read long files.
        throw new NotImplementedException(
            "Reading >2GB files with a 'startxref' in the middle not implemented.");
    var trail = _lexer.ReadRawString(0, (int)length);
    idx = trail.LastIndexOf("startxref", StringComparison.Ordinal);
    _lexer.Position = idx;
}
```

The comment says what it does: the whole file, into one `string`, to call `LastIndexOf` on it. On
the reported document that is a 1 GiB `byte[]` and then a 2 GiB `char[]`, and it throws.

**It throws on any machine.** A `string` holds at most 1 073 741 791 characters, and the file is
1 073 741 824 bytes — 33 over. The failure is a hard limit of the type, not memory pressure: it was
reproduced here on a machine with 64 GiB free, in 1.6 seconds. So the fallback could never open a
file bigger than 1 GiB, and the `NotImplementedException` above it could never be reached, because
the `string` gives out a gigabyte before `int.MaxValue` does.

The 13 GiB document the reporter merged successfully had its `startxref` in the last 1 030 bytes,
so it never took this path at all. That is the whole of the asymmetry the report describes.

### A second defect, behind the first

The line the fallback ends with is `_lexer.Position = idx`, and it runs before `idx` is tested:

```csharp
    _lexer.Position = idx;
}

if (idx == -1)
    throw new Exception("The StartXRef table could not be found, the file cannot be opened.");
```

`Position`'s setter assigns `_pdfSteam.Position`, which refuses a negative value. So a file with no
`startxref` in it anywhere came out as
`ArgumentOutOfRangeException: value ('-1') must be a non-negative value`, and the sentence written
to explain the case was unreachable. Both branches above the fallback have the same shape.

## What it does now

One backward scan, in `Lexer.FindLastMarker`:

```csharp
internal long FindLastMarker(string marker)
```

It reads the file backwards 64 kiB at a time into one reused buffer, searching each chunk with
`MemoryExtensions.LastIndexOf`, and answers the position of the last occurrence or -1. Each chunk
reaches `marker.Length - 1` bytes into the chunk already searched, so a marker lying across a chunk
boundary is in neither chunk and is still found. Nothing is turned into a `string`.

`ReadTrailer` is then three lines, and the two-tier "last 30 bytes, then last 1 030 bytes, then the
whole file" arrangement is gone — a backward scan finds the last `startxref` in the last kilobyte
in one read when that is where it is, so the tiers bought nothing but the bug.

This is the `//TODO: Implement chunking to read long files.` that was sitting in the fallback.

### What it changes

- A document whose `startxref` is in the last kilobyte — which is nearly all of them — reads
  exactly as before, from one 64 kiB read instead of one 1 030-byte read.
- A document whose `startxref` is further back reads, at the cost of scanning back to it. The
  reported 1 GiB file opens in about 3 seconds with no measurable managed allocation.
- A document larger than `int.MaxValue` with a distant `startxref` reads, where it used to be
  refused with `NotImplementedException`.
- A document with no `startxref` at all is refused with the sentence that was written for it,
  rather than with a complaint about a stream position.

`Lexer.ReadRawString` is now unused inside the library. It is public on a public class, so it stays.

## Verification

`src/PdfPinata.Test/IO/TrailerLocationTests.cs`, 4 tests, all four failing before the change:

| test | before |
|---|---|
| the largest single read is the scan buffer, not the file | read the whole 2 MiB file at once |
| a file longer than a `string` opens | `OutOfMemoryException`, the reported stack |
| a file longer than `int.MaxValue` opens | `NotImplementedException` |
| a file with no `startxref` is refused by name | `ArgumentOutOfRangeException` about `value ('-1')` |

The two large documents are neither held in memory nor written to disk: `SplicedStream` serves a
head, a run of one repeated byte and a tail, so a 2.5 GiB file costs a few hundred bytes and the
four tests together run in under half a second.

The reported file itself was read outside the suite, before and after: `OutOfMemoryException` in
1.6 seconds, then one page in 3.0 seconds.

Whole suite green on net8.0 and net10.0, 4 724 discovered and 4 724 run on each, one pre-existing
skip (`CanCreatePdfOver2Gb`). netstandard2.1 builds with 0 warnings. `verapdf-check.ps1` was not
run — Docker is not available here — but `ConformanceCorpus` never calls `PdfReader`, so a change
confined to the read path cannot move that gate.

## Not in scope

- **Rebuilding a file whose cross-reference table is broken.** That is `PdfReadAccuracy.Moderate`,
  and it is unchanged.
- **Reading the body of a file larger than 2 GiB.** Positions are `long` throughout the lexer and
  the trailer scan, but this change was not an audit of every other `int` in the parser. The
  reported document is 1 GiB and its body is 2 kiB of it; a file with 2 GiB of *objects* is a
  separate question, and `LargePDFReadWrite.CanCreatePdfOver2Gb` is where it would be asked.
  One part of it has since been answered: an entry of a cross-reference stream carries its offset
  as a `long` (`CrossReferenceStreamEntry.Field2`), where `ReadXRefStream` used to cast it to an
  `int` and look for the object at a wrapped, often negative, position.
  `CrossReferenceStreamDecodingTests.AnOffsetPastWhatAnIntHoldsIsReadWhole` pins it for fields
  of four and five bytes, over a sparse stream rather than a real file. Still `int`: a trailer's
  `/Prev` and `/XRefStm`, which are read with `GetInteger` and so refuse a `PdfLong` outright,
  and the writer's four-byte offset field, which cannot say anything past 4 GiB.
- **A `string` overload for the marker.** `FindLastMarker` takes one and converts it a character to
  a byte, the way the rest of the lexer reads bytes. Markers here are ASCII keywords.
