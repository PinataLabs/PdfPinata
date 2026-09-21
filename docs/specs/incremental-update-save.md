# Proposal — saving a document as an incremental update

What appending to a PDF instead of rewriting it covers, and what it deliberately leaves out.
Gap **G6** of the competitive gap analysis.

| item | what | status |
|---|---|---|
| 1 | `PdfDocument.SaveIncremental(Stream)` | done |
| 2 | Dirty tracking on `PdfObject` | done |
| 3 | ~~`PdfReader.Open` retains the source bytes in `Modify` mode~~ — a new `Append` mode | done |
| 4 | A new xref section with `/Prev`, and a trailer keeping `/ID[0]` | done |

Covered by `src/PdfPinata.Test/IO/IncrementalUpdateTests.cs`.

## The proposal was wrong about the open mode, and it matters

It said the document "must have been opened `PdfDocumentOpenMode.Modify`". **`Modify` is precisely
the mode that cannot work.** Opening that way ends with

```csharp
document._irefTable.Compact();
document._irefTable.Renumber();     // ← every object renumbered from 1
```

and an incremental update shadows an object by writing a new definition *under the same number*. A
document whose numbers have been reassigned can no longer be appended to at all — every appended
definition would shadow the wrong object, and the result opens and is quietly, thoroughly wrong.

Hence `PdfDocumentOpenMode.Append`: reads like `Modify`, but neither compacts nor renumbers, and
keeps the bytes. `PdfDocument.PrepareForSave` renumbers too, and is guarded the same way.

Compacting is skipped for its own reason, not merely as a bystander: an object unreachable from the
catalog is still *in the file being appended to*, so dropping it from the table would not remove it
from the document — it would only lose track of a number that remains taken.

## Two latent defects this surfaced

Both were pre-existing, and both were reachable only because nothing had ever written a PDF without
first writing its header.

**`PdfPages.PrepareForSave` read the `_pagesArray` field instead of the `PagesArray` property.** The
field is filled in lazily by the property, and every existing path happened to touch the property
first. An incremental save does not, and got a `NullReferenceException`.

**`PdfWriter.WriteEof` seeks backwards to patch its header comments.** In verbose layout — *the
default in a debug build* — it rewinds to `_commentPosition` and overwrites the creation date, the
elapsed time and the file size in place. A writer that never wrote a header has no such position,
and `_commentPosition` defaulted to 0, so an incremental save scribbled the comment block over the
first two hundred bytes of somebody else's document. It now defaults to -1 and the patching is
skipped. Note where this would have been found and where it would not: broken in development, fine
in release.

## A revision after a cross-reference stream is indexed by one (#55)

When the file's last revision is indexed by a cross-reference stream, the document's trailer is that
stream, read back in as a `PdfCrossReferenceStream`. The appended revision used to be written the
classic way regardless — `trailer`, then `_trailer.WriteObject` — which put the old stream there,
`7 0 obj` and all, and nothing could read the result. Every test fixture was classic, so nothing saw it.

`SaveIncremental` now asks which the trailer is. A classic trailer gets a classic section, as before.
A stream gets `PdfCrossReferenceStreamWriter.WriteIncrementalSection`: a new cross-reference stream
numbered one past the highest number the file accounts for (see below), with `/Index` naming the
runs the changed objects fall into, `/Prev` naming the previous stream, and `/Root`, `/Info`, `/ID`
and `/Encrypt` carried over. Three choices in it:

- **The new stream is not added to the object table**, just as the classic path adds nothing, so a
  second `SaveIncremental` from the same document still appends to the same original.
- **The changed objects stand on their own** (type 1 entries) rather than going into an object stream.
  A revision is usually a handful of objects, where packing saves little.
- **A classic section after a stream is not written**, though ISO 32000-1 does not forbid it: this is
  the convention this note set out from — a file is updated in the form it was written in.

`IncrementalUpdateOfCrossReferenceStreamTests` covers it. One thing it found and does not fix:
`SaveIncremental` on an *encrypted* document whose change touches an object throws a
`NullReferenceException` from the security handler, classic or stream alike, because nothing
prepares the handler for writing on this path.

## An appended revision never shrinks `/Size`

New objects used to be numbered from one past the highest object *in use*, and the appended `/Size`
was written the same way, on both paths. A file whose last cross-reference section ends in free
entries — its highest objects deleted, `/Size` left where it was — therefore got a revision whose
`/Size` was smaller than the previous one, and new objects, the new cross-reference stream among
them, took numbers the previous revision had freed. ISO 32000-1 7.5.5 has `/Size` one past the
highest number used anywhere in the file, and an update may not make it smaller.

`PdfReader` now raises the table's `MaxObjectNumber` to the last revision's `/Size` less one when it
opens a document for `Append`, before the page tree is flattened and so before anything can be given
a number. Every number handed out after that is at least the previous `/Size`, and both sections
write `/Size` as one past the highest number, so neither needed a rule of its own.
`AppendedRevisionSizeTests` hand-writes one file of each kind with `/Size 12` and four live objects.

## What it costs, honestly

The dirty set is **conservative**. After appending a change to nothing but a document property, the
information dictionary and one page object are rewritten — the page because reading and preparing it
mutates it in ways not worth unpicking. The font, the font descriptor, the content streams and the
catalog are not. That is the property the tests assert, and they assert it by looking at what the
appended bytes *contain* rather than by counting them: a byte count is a poor proxy in a debug build,
where a hundred-character rule sits between every object.

Erring towards rewriting is the safe direction. An object wrongly reported clean is silently left at
its old value, which is the worst shape a defect can take, because the file opens and looks right.

## Ordering that is not arbitrary

`PdfReader.Open` flattens the page tree **before** capturing, not after. Flattening mutates the page
tree, and capturing is what decides which objects count as untouched — the other way round, every
page is reported changed by the act of reading it and the saving evaporates.

---

## The defect

All three `PdfDocument.Save` overloads — `Save(string)`, `Save(Stream)`, `Save(Stream, bool)` — write
the whole file from the object graph. There is no path that appends.

For most work that is the right thing and nobody notices. It stops being right in three places:

**Signatures.** A signature covers a byte range of the file. Rewriting the file changes those bytes, so
the second signature invalidates the first, and so does any edit at all. Incremental update is not an
optimisation for signed documents — it is the only legal way to modify one.

**Non-destructive editing.** Fill a form, add an annotation, change a bookmark: rewriting the file
discards the original bytes, and with them anything the reader did not fully understand. An incremental
update preserves the original exactly and appends the change, which is also what makes the change
auditable and reversible.

**Large documents.** Adding one annotation to a 200 MB scanned PDF rewrites 200 MB.

---

## The shape of it

```text
┌─ original bytes, copied verbatim ─────────────────────────┐
│ %PDF-1.7                                                  │
│ 1 0 obj … endobj    2 0 obj … endobj    3 0 obj … endobj  │
│ xref                                                      │
│ trailer <</Root 1 0 R /ID[<A><B>]>>                       │
│ startxref 4096                                            │
├─ appended ────────────────────────────────────────────────┤
│ 3 1 obj … endobj          ← only the objects that changed │
│ 9 0 obj … endobj          ← and any new ones              │
│ xref                                                      │
│ trailer <</Root 1 0 R /Prev 4096 /ID[<A><C>]>>            │
│ startxref 8192            ← readers start here            │
└───────────────────────────────────────────────────────────┘
```

A reader starts at the last `startxref` and walks `/Prev` backwards, so later definitions of an object
shadow earlier ones. `/ID[0]` stays — it identifies the document across its whole life — and `/ID[1]`
is regenerated, because it identifies this revision. `Pdf/PdfDocument.cs` already has
`_trailer.CreateNewDocumentIDs()`; this needs the half that changes only the second element.

Once `docs/specs/cross-reference-streams.md` lands, the appended section can be a cross-reference
stream rather than a classic table. Both must work: a file whose original revision used a classic table
is conventionally updated with another classic table, and the reverse for streams.

## Item 2 — dirty tracking is the actual work

Everything above is bookkeeping. This is the part that needs care, because **an incremental update that
misses a changed object produces a file that is silently, subtly wrong** — the reader keeps using the
old definition and nobody finds out until a customer does.

`PdfObject` has no dirty flag today. Adding one is easy; making it *honest* is not, because mutation
reaches objects by several routes:

- `dictionary.Elements["/Foo"] = value` — `PdfDictionary.DictionaryElements` must mark its owner.
- `array.Elements.Add(…)` — likewise for `PdfArray`.
- `stream.Value = bytes` — and every path that rewrites stream data, including the filters.
- Objects reached through `PdfInternals`, which exists precisely to let callers reach past the API.

The safe default is to treat an object as dirty unless it can be proven clean, and to have a debug
mode that rewrites the file both ways and compares — an expensive check that only has to run in tests.

`PdfObject.IsNew` (objects created since the read) is the easy half and is already implicit in the
object-ID assignment.

## Item 3 — keeping the original bytes

`SaveIncremental` needs the file it is updating, byte for byte, and it needs the object numbers that
file was written with. **`PdfDocumentOpenMode.Append` is the mode that keeps both**, and it had to be
a new mode rather than a flag on `Modify`: `Modify` compacts and renumbers, and a renumbered document
cannot be appended to at all, because an appended definition shadows an object *by number*. That is
recorded here rather than left implicit, because this note originally said to make `Modify` retain
the buffer, which would have produced a file that opens and is quietly wrong.

**It is a memory-behaviour change** — opening a 200 MB PDF holds 200 MB — which is why it is a mode a
caller asks for rather than something `Modify` started doing. `CLAUDE.md` notes that the open mode
"decides far more than access" and that picking the wrong one is a common cause of "this API does
nothing"; a fifth thing for it to decide belongs in that list, and is in it.

`Append` is declared **last** in the enum, and is 4. The members carried no explicit values when it
was added, so putting it after `Modify` would have renumbered `Import`, `ReadOnly` and
`InformationOnly` — and because the C# compiler inlines an enum constant at the call site, an
assembly compiled against an earlier version would go on passing the old number and silently open in
the wrong mode. The same rule later decided what happened when `InformationOnly` was removed: every
member now states its value and 3 is left vacant rather than `Append` sliding down into it. See
`docs/specs/open-mode-enforcement.md`.

`SaveIncremental` must refuse — clearly, not silently — a document opened `Import` or `ReadOnly`, or one
built from scratch.

---

## What this deliberately does not cover

- **Rewriting history.** An incremental update appends. Removing a previous revision is a full save, by
  definition, and for a signed document it is destroying evidence.
- **Reverting to a previous revision.** Readable from `/Prev`, but exposing it is a separate feature.
- **Compacting away superseded objects.** That is what a full `Save` does; a "save flattened" helper is
  a one-line convenience over the existing path.
- **Object-level diffing to decide dirtiness.** Comparing serialised forms to avoid writing an object
  that was touched but not actually changed would shrink the appended section. Measure first; the
  common case is that a touched object did change.

## Tests

`PdfPinata.Test`. Open a fixture, change one annotation, save incrementally, and assert: the
original bytes are a prefix of the result; the appended section defines only the changed object; the
reopened document sees the new value; `/ID[0]` is unchanged and `/ID[1]` is not. Then apply **two**
successive updates and assert the `/Prev` chain resolves — one update is easy to get right by accident,
two is not.

The dirty-tracking check earns its own test class: mutate through `Elements`, through `PdfInternals`,
and through stream `Value`, and assert each marks its owner.

## Related

- `docs/specs/cross-reference-streams.md` — the same two-pass writer refactor; do it first.
- Digital signatures (gap G5) are blocked on this and are the main reason to build it.
