# Spec — reading outline destinations, issue #8

[empira/PDFsharp#8](https://github.com/empira/PDFsharp/issues/8) reports that
`PdfDocument.Outlines.Count` throws on a document written by LaTeX, with an inner exception
reading *"Destination Array expected"*. The reporter attached the document
([document.pdf](https://github.com/empira/PDFsharp/files/11376575/document.pdf)); it opens and
reads on this fork's `master` and throws on the first line that asks it for a bookmark.

What follows is the design as built, on `fix/outline-named-destinations`.

| item | what | status |
|---|---|---|
| 1 | An entry naming its destination throws instead of going there | done |
| 2 | An entry performing any action but a GoTo trips an assertion | done |
| 3 | A destination that does not describe a page of this document throws | done |
| 4 | A destination of an unknown type is rewritten as an `/XYZ` when saved | done |
| 5 | A GoTo action leading on to another loses what follows it when saved | done |

---

## Item 1 — a destination can be named rather than written out

`PdfOutline.Initialize` read the `/D` entry of a GoTo action and accepted two shapes: an array, and
a reference to an array. Anything else was

```csharp
throw new Exception("Destination Array expected.");
```

The third shape is the one LaTeX writes. `hyperref` gives every section a *named* destination and
has the entries name it:

```
19 0 obj << /S /GoTo /D (sec1.section) >> endobj
```

with the catalog holding what the name stands for, in the `/Names` `/Dests` name tree:

```
/Names [(Doc-Start) 6 0 R (page.1) 5 0 R (sec1.section) 9 0 R …]
```

That is not an exotic corner of the format — it is [ISO 32000-1 §12.3.2.3][spec], the whole point
of which is that a destination can be moved without every link to it having to be rewritten. Any
document written by `pdflatex` with `hyperref` loaded has an outline shaped this way, which is why
the reporter's file and the second reporter's files were all TeX output.

Resolving the name needs the catalog, and this fork already has the code that searches it:
`PdfNamedDestinations.Lookup`, written for page import, walks the `/Names` tree and the `/Dests`
dictionary that PDF 1.1 used, and follows the `/D` entry of a destination held in a dictionary of
its own. `PdfOutline` now calls it, through one method that turns whatever an entry holds into the
destination array behind it:

```csharp
PdfArray ResolveDestination(PdfItem dest)
{
    if (dest is PdfReference iref)
        dest = iref.Value;

    return dest as PdfArray ?? PdfNamedDestinations.Lookup(Owner, dest);
}
```

Both places a destination can be written go through it: the `/Dest` entry, which previously
accepted an array and asserted on everything else, and the `/D` of a GoTo action. So a name is now
read in either place, whether it is written as a string (PDF 1.2 onwards) or as a name (PDF 1.1).

**A name the document holds nothing under is not an error.** The entry keeps its action and is read
without a destination, which is what a reader shows for it. Refusing to hand over the outline at
all — the behaviour being fixed — is the one response that helps nobody.

## Item 2 — not every action goes somewhere in this document

The action branch ended in

```csharp
Debug.Assert(false, "See what to do when this happened.");
```

for every action that is not a GoTo. An outline entry opening a web page (`/URI`), a page of
another document (`/GoToR`), or a file (`/Launch`) is ordinary, and under xUnit that assertion is
a failed test rather than a note to a developer.

Those entries are now read and left exactly as they stand. There is no destination page to hand
out, and the `/A` entry stays in the dictionary, so the entry still does what it did when the
document is written out again. Only a GoTo action is replaced by the `/Dest` entry it amounts to,
which is what the code already did for the shapes it understood.

The `Debug.Assert(dest == null || a == null, …)` above it went the same way. A document holding
both is malformed, `/Dest` is what §12.3.3 says the action replaces, and reading it is a better
answer than stopping.

## Item 3 — a destination that does not describe a page

`SplitDestinationPage` read `destination.Elements[0]` and cast it to a `PdfReference`, then read
`Elements[1]`, then `Enum.Parse`d the type name and read its parameters by index. Each of those is
an exception waiting for a destination that is empty, truncated, or of a type this library does
not know — and this method is now reached with arrays that came out of a name tree written by
something else, so what it is given is no longer only what this library wrote.

It now answers the two questions separately: *which page* (`DestinationPageOf`, which returns null
for a destination naming no page, a page number outside this document, or a first element that is
neither) and *where on it* (`RealAt`, which gives NaN past the end of the array — the same "not
set" the properties already default to and the writer already writes as `null`). An unknown type
name leaves the entry pointing at its page and nothing more, rather than throwing.

Reading the type name is its own trap. `Enum.TryParse` takes a name that is a number for the value
that number stands for, so `/999` parsed to a type there is none of and went straight on into the
`ArgumentOutOfRangeException` the switch ends with — the throw this item exists to remove, reached
by the check meant to prevent it. `/3` was worse for being quiet: it parsed to `FitV`, and a
document saying it went one way was written back saying it went another. `TryParseDestinationType`
answers only for the eight names, so a name that reads as a number is an unknown type like any
other.

None of this makes a well-formed destination read differently. `[4 0 R /XYZ 11 22 0]` still gives
page, left, top and zoom.

## Item 4 — reading a destination one cannot describe is not licence to rewrite it

Item 3 leaves an entry whose type name is not one of the eight pointing at its page and nothing
more. That is the right thing to read, but `PrepareForSave` writes `/Dest` from `DestinationPage`
and `PageDestinationType` whenever there is a destination page, and `PageDestinationType` defaults
to `Xyz` with no position. So `[4 0 R /FitNothing 1]` — read for the page it names, as it should be
— was saved as `[4 0 R /XYZ null null null]`, quietly replacing a destination the library did not
understand with a different one it does.

An entry read that way is now written back out as it was found. It still says where it goes, and
what it says is what the document said, which is more than this library can express. Setting
`DestinationPage` or `PageDestinationType` gives the entry back to the library, so an outline the
caller changes is written from those properties as it always was.

The same holds for the two shapes item 1 added: a `/Dest` naming a destination of an unknown type
keeps the name, and a GoTo action resolved to one keeps the array the name stood for, which
`InitializeFromAction` had already put in `/Dest`.

## Item 5 — an action can lead on to another one

Replacing a GoTo action with the `/Dest` it amounts to is a fair trade only when the action does
nothing else. `/Next` (§12.6.3) is the action or actions performed after this one, so an entry
written

```
/A << /S /GoTo /D [4 0 R /XYZ 11 22 0] /Next << /S /URI /URI (https://example.com) >> >>
```

goes to page 2 **and** opens a web page. `Elements.Remove(Keys.A)` dropped the second half of that
without a word, and the entry came back from a save saying only where it goes. The behaviour is
older than item 1 — `master` does the same — but it is the same mistake as item 4, made about the
action rather than about the destination.

An action carrying a `/Next` is now kept where it was found, and the destination is read out of it
as before, so the entry hands out its page and still does both things when the document is saved.
`PrepareForSave` writes no `/Dest` beside it, because §12.3.3 has one of the two entries and not
both — and for the same reason, an entry the caller gives a `DestinationPage` now loses its `/A`,
since it goes where the caller said rather than where the action led.

---

## Verification

`src/PdfPinata.Test/Outlines/ImportedOutlineTests.cs`, 25 tests over fixtures shaped like the
documents in the issue — 19 of them fail on `master`:

- a destination named through a GoTo action, resolved through a name tree with `/Kids` and
  `/Limits` so the search has to walk it;
- the same name in a `/Dest` entry, and the same held as a name in a PDF 1.1 `/Dests` dictionary;
- a name the catalog holds nothing under: no destination, action kept;
- a `/URI` action and a `/GoToR` action: read, no destination, action kept;
- destinations written out, held indirectly, and given as a page number, in and out of range, and
  an action held in an object of its own;
- destinations that are empty, name no page, stop short of their parameters, or give a type name
  that is not one of the eight — including `/999` and `/3`, which `Enum.TryParse` reads as numbers
  — all read without throwing, and the type kept as it was found;
- a destination taken from a name survives being saved and read again, and one of a type this
  library does not know is saved as it was found rather than as an `/XYZ`;
- an action with a `/Next`: the destination is read out of it, the action and what follows it
  survive a save, and giving the entry a destination page replaces the action rather than
  standing beside it.

The reporter's own document was read alongside, outside the suite, since it is not ours to check
in: two entries, *1st section* on page 1 at 529.041 and *2nd section* on page 2 at 667.198, where
`master` throws on the first of them.

Whole suite green on net8.0 and net10.0, 1065 passed on each, one pre-existing skip
(`CanCreatePdfOver2gb`).

## Not in scope

- **Writing named destinations.** An outline saved by this library still writes its destination
  out as an array, so a name read from a document is replaced by what it stood for when that
  document is saved again. `docs/specs/bookmarks-and-outlines.md` lists the same gap.
- **The second report on the issue.** A comment there describes `Outlines.Clear()` failing on
  documents with many entries unless an `await Task.Delay(500)` precedes it. Nothing in the outline
  code is timing-dependent, and no document was attached; there is nothing here to reproduce.

[spec]: https://www.pdfa.org/resource/iso-32000-pdf/
