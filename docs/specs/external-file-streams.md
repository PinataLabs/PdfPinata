# Spec — a stream whose data is in another file, issue #389

[empira/PDFsharp#389](https://github.com/empira/PDFsharp/issues/389) reports
`NotImplementedException: "File streams are not yet implemented."` on opening a document. The
message is the whole of the diagnosis: `Parser.GetStreamLength` refused, outright and before
reading anything, every stream dictionary carrying an `/F` entry.

| item | what | status |
|---|---|---|
| 1 | A stream dictionary with `/F` makes the whole document unreadable | done |
| 2 | `PdfStream.ExternalFile` — a typed way to read what the stream names | done |
| 3 | Fetching the data from the file the document names | deliberately not done |

---

## What `/F` is

ISO 32000-1 Table 5 gives a stream dictionary an optional `/F`, added in PDF 1.2, naming the file
the stream's data is really in. Three sentences of that entry decide everything here, and
`PdfDictionary.PdfStream.Keys.F` has carried all three as documentation since long before the
refusal was written:

- the bytes between `stream` and `endstream` **are to be ignored**;
- the filters are named by `/FFilter` rather than `/Filter`, with their parameters in
  `/FDecodeParms`;
- **`/Length` still says how many of those ignored bytes there are.** Usually there are none and it
  is 0.

The last one is the point. An external stream is not laid out differently, is not delimited
differently, and does not say its length differently. It is an ordinary stream with an entry saying
that what it holds is not the interesting part.

## The refusal

```csharp
if (dict.Elements["/F"] != null)
{
    throw new NotImplementedException("File streams are not yet implemented.");
}
```

That sat between the empty-dictionary check and the read of `/Length`, so it fired before anything
had been read and the exception came out of `PdfReader.Open`. Not the stream — **the document**.
One `/F` anywhere in a file, on a stream nothing was going to look at, and the whole file was
unopenable in all four open modes.

The reported case is the one that makes that hurt: a catalog whose `/Metadata` says its XMP is in a
file beside the document. Nothing in the document needs that metadata to render a page, and every
page was lost for it.

## What it does now

The branch is gone, and nothing replaces it. `/Length` is read the way it is read for any other
stream, the bytes it counts are read and skipped past, and the reader arrives at `endstream` where
it expects to. The entries saying where the data really is — `/F`, `/FFilter`, `/FDecodeParms`,
`/DL` — are in the dictionary like any others and come through untouched, which is what lets a
caller who does want the data go and get it.

Three shapes follow from reading it the ordinary way rather than specially:

- **`/Length 0`, the usual shape.** No bytes are read, the stream is empty, and the document reads.
- **`/Length` counting bytes that are there.** They are read and kept. The specification says to
  ignore them, and ignoring them is the caller's business; a reader that dropped them would lose
  them on the way out again, and reading them is in any case what puts the lexer on `endstream`
  rather than in the middle of them.
- **No `/Length` at all**, or one the file cannot hold. An external stream is no more exempt from
  [wrong-stream-length.md](wrong-stream-length.md) than any other, and takes the same recovery:
  scan for `endstream`, and record the length that was found.

## Item 2 — saying where the data is, in one property

Reading the document is the defect; saying what it asked for is the other half of being useful.
Before this, a caller who wanted to follow `/F` had `Elements.GetDictionary("/F")` and had to
reinvent the preference of `/UF` over `/F` that `PdfFileSpecification.FileName` already knows —
because that type's adopting constructor and `PdfAttachments.Resolve` are both `internal`, and
`document.Attachments` rightly does not list a stream's `/F` at all. That walk covers `/AF`, the
`/EmbeddedFiles` name tree and annotation `/FS`; an external stream is a reference *out* of the
document rather than a file it carries, and does not belong in it.

```csharp
public PdfFileSpecification ExternalFile { get; }   // null when the stream names no other file
```

It is a getter, and that is the whole of the promise. The two shapes 7.11.2 allows are answered
differently, on purpose:

- **A dictionary.** Answered as itself, through the same `PdfAttachments.Resolve` the attachment
  walk uses: the dictionary is transformed into a `PdfFileSpecification` in place. A specification
  that is an object of its own keeps its identity because the transformation re-points the
  reference at the result. One written out inside the stream dictionary has no reference to
  re-point, so it is put back under the key — without which every read would build another
  wrapper around the same entry.
- **A bare name**, the simple form, and the common one for a stream. Answered as a specification
  carrying that name and nothing else, made on the spot and **standing outside the document**.
  Holding it would mean rewriting `/F` from a string into a dictionary, which is not a thing a read
  should do to a file. So two reads give two instances and writing to one reaches nothing. That
  asymmetry is pinned by `ANameGivenAsAPlainStringIsReadWithoutRewritingTheDocument` rather than
  designed away, because the alternative — answering null for the commonest shape — leaves the
  property useless exactly where it is most wanted.

## Item 3 — the file is not fetched, deliberately

Reading the document is the defect. Resolving `/F` is a separate thing, and this does not do it:

- **A document need not have a location.** `/F` is resolved relative to the document, and
  `PdfDocument.FullPath` holds one only when `PdfReader.Open` was handed a path — it is
  `string.Empty` for the stream overloads. A document read from a network response or a database
  column has nothing for a relative path to be relative *to*, so the feature would work for some
  callers and silently not for the rest.
- **Following what a document names is the caller's decision, not the library's.** A file
  specification is written by a stranger, and 7.11.2 lets it be more than a filename: the string
  form carries PDF's own path syntax, `..` and a leading `/` for absolute included, and the
  dictionary form can say `/FS /URL`. A reader that resolved it would climb out of the directory
  the document was found in, reach a UNC path off the machine, or make an outbound request — on
  nothing but the document's say-so, as a side effect of opening a file. That is a phone-home
  primitive, and it belongs behind an explicit call the application makes with whatever sandboxing
  it has.
- **It would put a file opener or an HTTP client in the core.** That package carries no imaging,
  font or cryptography dependency on purpose; reaching outside the document is not the thing to
  make the first exception for.
- **Nothing in the library wants the data.** No page, font, image or metadata path reads the stream
  of an external stream, so there is no behaviour waiting on it.

`/FFilter` and `/FDecodeParms` are carried through and not applied, for the same reason: there is
no data here for them to apply to.

## Verification

`src/PdfPinata.Test/IO/ExternalFileStreamTests.cs`, 12 tests over the document of the report — a
catalog whose `/Metadata` names a `/Filespec`. Six on the reading: the stream is read, the file
specification and `/FFilter` survive, in-file bytes are counted by `/Length` and found, a missing
`/Length` is recovered, the document writes out and reads back, and all four open modes read it.
Every one of those six throws `NotImplementedException` before the change. Six on `ExternalFile`: a
stream naming no file, a specification reached by reference, one written out inside the stream
dictionary, a bare name, and the identity each of the last three does or does not keep.

The file attached to the report was read alongside, outside the suite: it opens in all four modes,
gives up its page, saves, and reads back with the `/F` reference intact.

Whole suite green on net8.0 and net10.0 — 4,731 passed in `PdfPinata.Test` on each with the one
pre-existing skip, against 4,732 discovered, and all nine assemblies present. Release build with 0
warnings. The veraPDF gate was not run: Docker is not available here, and the conformance corpus is
written rather than read, so nothing this touches is on its path.

## Not in scope

- **Writing an external-file stream.** Nothing stops a caller setting `/F` on a stream they built,
  and nothing helps them either. There is no typed API for it and none is proposed.
- **Validating the file specification.** `ExternalFile` tells the two shapes of 7.11.2 apart and
  reads neither any further: an empty `/F`, a name no filesystem would take, an `/FS` naming a
  filesystem nothing here knows, all come back as what the document said.
- **Refusing `/F` under PDF/A.** ISO 19005 forbids external stream data outright, and
  `PdfConformanceWriter` does not check for it — which reading such a document now makes reachable
  by import. That is the position `conformance-completeness.md` already states for every rule the
  code does not enforce: veraPDF is the authority, and nothing here changes that.
