# Spec — writing one copy of each identical resource

Upstream: [empira/PDFsharp#275](https://github.com/empira/PDFsharp/issues/275), "Optimising merged
PDFs to reduce file size through duplicated resources".

**Done** on `feat/deduplicate-resources`, as `src/PdfPinata/Pdf.Advanced/PdfResourceDeduplicator.cs`
behind `PdfDocumentOptions.DeduplicateResources`.

## The problem

Merging many documents by importing their pages — `PdfReader.Open(…, Import)` then `AddPage` — keeps
one imported-object table per source document, so each source's fonts, images and forms are copied
in separately. A logo or a font every document of a batch carries is written once per document. The
reporter merged three files of 221 KB into 221 KB where generating them as one gave 82 KB, and
worked around it by hashing each font's `/FontFile2` and each image's stream and repointing resource
dictionaries at the first copy.

That workaround has three gaps the issue itself raises: `/FontFile` and `/FontFile3` are missed, only
the font *dictionary* is repointed so its descriptor, widths and `/ToUnicode` map are the only copies
of themselves left and nothing collapses below the top, and "are there other resources I should
optimise?" goes unanswered.

## The shape

```csharp
var merged = new PdfDocument();
merged.Options.DeduplicateResources = true;
foreach (var path in paths)
{
    using var source = PdfReader.Open(path, PdfDocumentOpenMode.Import);
    foreach (var page in source.Pages)
        merged.AddPage(page);
}
merged.Save("merged.pdf");
```

**An option applied by the save, not a method the caller calls.** The closest precedents,
`PruneUnusedResources` and `ConsolidateImages`, are methods — but both look only at what was imported,
which is complete the moment it arrives. This compares *everything*, and a font this document draws
with is not complete until the save subsets it: before `PdfFontTable.PrepareForSave`, two fonts of
the document's own have empty programs and empty widths, and a comparison run then would find them
equal. Running it last in `PdfDocument.PrepareForSave`, after the fonts, the named destinations, the
catalog, the structure tree and the XMP packet and immediately before `Compact`, means every object
compared is the object written, and `Compact` is what drops the copies nothing refers to any more.

Off by default: it costs a walk of everything the pages draw with and a hash of every stream, and a
document that was never merged has nothing for it to find.

## The algorithm

### What is compared

Candidates are found by walking down from each page:

- the entries of the page's `/Resources` — `/Font`, `/XObject`, `/ExtGState`, `/ColorSpace`,
  `/Pattern`, `/Shading` — and the page's `/Contents`;
- the entries of the `/Resources` of each annotation's appearance streams (`/AP` `/N`, `/R`, `/D`,
  and each state of an appearance with states);
- and everything reached from those, through any reference, recursively.

The resource dictionary and each category dictionary belong to the page or form holding them and are
only looked through. The walk **stops**, neither merging nor following, at:

- an object of any type but plain `PdfDictionary` or `PdfArray`. A `PdfFont`, `PdfFontDescriptor`,
  `PdfImage`, `PdfFormXObject`, `PdfExtGState`, `PdfContent` or `PdfResources` belongs to this
  document's own object model, which may write into it again at the next save — a font drawn with
  after one save is subset again at the next. Merging one would leave the model writing into an
  object nothing refers to. Everything a document *imports* is plain, which is where the duplicates
  are;
- a dictionary whose `/Type` names something that stands for itself: `/Catalog`, `/Pages`, `/Page`,
  `/Annot`, `/OCG`, `/OCMD`, `/StructTreeRoot`, `/StructElem`, `/MCR`, `/OBJR`, `/Sig`,
  `/DocTimeStamp`, `/SigRef`, `/TransformParams`, `/Outlines`, `/Action`, `/Filespec`,
  `/EmbeddedFile`, `/Encrypt`, `/Collection`, `/Thread`, `/Bead`, `/Template`, `/Namespace`;
- a dictionary carrying `/Parent`, `/P`, `/Kids`, `/FT`, `/Rect`, `/Dest`, `/Names` or `/Nums` — a
  tree node, a field, an annotation, a structure element — whatever its `/Type` says, because `/Type`
  is optional on most of them;
- a stream whose data is not held in memory.

Anything the walk stops at is still *referred to* by what it passed; such a reference is compared by
object number, so it is equal only to itself. That is what keeps a form belonging to one optional
content group apart from an identical form belonging to another identically named one.

### Deciding equality

Partition refinement, the way a finite automaton is minimised:

1. Each candidate is described with its references to other candidates blanked out and their targets
   recorded in order: dictionary keys sorted, every simple value tagged by type (so the integer `1`
   and the real `1.0` differ), strings length-prefixed so that no two sequences run together.
   A stream adds its length and a 64-bit FNV-1a hash of its **encoded** bytes, and within one
   description the bytes are compared outright, so a hash collision cannot merge two streams. A
   stream's `/Length` is left out of the description, since the bytes decide it and the writer
   restates it.
2. Candidates with the same description and bytes start in one class.
3. Every class is split by the classes its references lead to, until a round splits nothing. A round
   can only split, so the number of classes rising is the only change to look for.

The result is the coarsest partition in which equal objects say the same thing and refer to equal
objects — so a font collapses together with its descriptor, program, widths and `/ToUnicode` map, a
form together with its resources and everything they name, and two forms that each draw themselves
are found equal without a special case for the cycle. A depth-first comparison would need one.

The first object found of each class stands for it. Every reference in the document — every object in
the cross-reference table and the trailer, direct dictionaries and arrays inside them included — to
any other member is pointed at it. Nothing is deleted.

### What is compared as bytes, not as meaning

Two subsets of one font taken for different text are different programs, and both are kept. This is
the reporter's "ArialUnicodeMS is different on every PDF", and the answer upstream gave is the right
one: subset the same characters in every document (`PdfDocument.AddCharacters`) and the subsets come
out identical. Merging the glyphs of two subsets into one is font surgery, and out of scope.

Likewise a stream compressed at two levels, or an image re-encoded, is two different objects, and a
dictionary written `/Filter /FlateDecode` is not a dictionary written `/Filter [/FlateDecode]`.

## Traps

- **Refused for an appended revision.** A document opened with `PdfDocumentOpenMode.Append` keeps
  every object of the revisions before it, so merging duplicates saves nothing and rewrites every
  dictionary that referred to one. `Save` and `SaveIncremental` both throw naming the option, checked
  at the top of `PrepareForSave` so that a save which cannot happen has changed nothing.
- **The imported-object tables are redirected too.** Importing remembers which copy each object of
  the source became, so that importing a second page of a source reuses what the first brought. A
  copy merged away is no longer in the document, and a later import from the still-open source would
  hand it out again — and `Compact`, finding it reachable, puts it back under an object number
  `Renumber` has since given to something else. `ArgumentException: An item with the same key has
  already been added` on the next save. `PdfFormXObjectTable.RedirectImportedObjects` points those
  entries at the representative. With the option still on the next save merges it away and hides
  this, which is why the test turns the option off for its second save.
- **Appearance streams themselves are not merged**, only what their resources name. A form field
  redraws its appearance when its value changes, and two widgets sharing one stream would change
  together.
- **Encryption needs no special handling.** An encrypted source is decrypted as it is read, so the
  bytes compared are the plain ones, and the output is encrypted per object by number as it is
  written — after the merge, so the representative is encrypted once under its own number.

## Measurements

From `ResourceDeduplicationRenderingTests`, on net10.0:

| document | without | with |
|---|---:|---:|
| one page of subsetted text and a PNG photograph (735,217 bytes), merged 20 times | 14,690,551 | 740,123 |
| `FamilyTree.pdf`, `test.pdf` and `Pdf20.pdf`, merged twice over | 98,986 | 47,580 |

Twenty copies weigh 0.7 % more than one: a page dictionary each and nothing else.

## Verification

`src/PdfPinata.Test/IO/ResourceDeduplicationTests.cs`, over documents written by hand on `RawPdf`,
read, saved with the option and read back:

- identical images on two pages → one object; without the option, two;
- images differing by one byte, or only by an entry of their dictionary → kept apart;
- two TrueType fonts identical all the way down → one font, one descriptor;
- the same, differing only in their widths → two fonts sharing one program;
- two forms each drawing itself → merged;
- two identical forms each in an identically named optional content group → neither the groups nor
  the forms merged;
- two identical pages, each with an identical annotation → two pages and two annotations, two
  appearance streams, one content stream and one font;
- two identical graphics states carrying `/Parent` → kept apart;
- an `Append` document → `Save` and `SaveIncremental` both refuse;
- an encrypted save → merged, and the image reads back under the password;
- saving twice → the same size as saving once;
- a page imported after the save from the same source → resolves to the kept copy; fails with the
  redirect of the imported-object tables removed.

`src/PdfPinata.Test/IO/ResourceDeduplicationRenderingTests.cs` measures the table above and renders a
generated document merged three times, and the three assets merged twice, with and without the
option, comparing page by page.

## Left out

- **Merging subsets.** See above.
- **Objects reached only from the catalog** — output intents, the XMP packet, embedded files,
  `/AcroForm` default resources. None is duplicated by importing pages, which is the case this
  answers, and each is referred to from one place.
- **`/Properties` resources.** Nearly always optional content groups, which are identity; a property
  list that is not one would be safe to merge, and telling the two apart is not worth what it saves.
- **Objects this document built for itself**, for the reason under *What is compared*. A document
  built in one piece already shares its fonts and images through its font and image tables.
- **Normalising before comparing** — decompressing streams, reordering arrays whose order does not
  matter, treating `1` and `1.0` alike. Each is a way for two things that differ to be called equal,
  and the case asked about is byte-identical copies of one file.
- **`ConsolidateImages`** stays. It does a strict subset of this — images named directly by a page's
  resources, matched by stream alone — as a method that acts at once, and callers use it.
