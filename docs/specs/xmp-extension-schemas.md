# Spec — declaring an XMP extension schema

What it takes to write a property PDF/A has never heard of, and what this deliberately leaves out.
Follows on from `docs/specs/pdf-a-conformance.md`, which built the packet this extends.

| item | what | status |
|---|---|---|
| 1 | `XmpMetadata` declares a schema and writes its properties as one act | done |
| 2 | Attribute-safe escaping on the new path | done |
| 3 | A prefix refused when XML would not take it as a name | done |
| 4 | `FacturXInvoice` becomes a caller rather than a second implementation | done |
| 5 | `ArchiveDemo` stops carrying a hand-written copy | done |

## Problem Statement

A caller who wants to put a fact in the metadata that XMP does not already have a word for — an
invoice's document type, a demo's note about which demo wrote the file, anything at all in a
namespace of their own — reaches `XmpMetadata.AdditionalDescriptions`, which takes a finished
`rdf:Description` element as a string. Writing the property is the easy half. The half that is not
in the signature is that a document claiming PDF/A must **declare** the namespace before it uses
one: ISO 19005 clause 6.6.2.3.1 holds every property in the packet to a schema the file either
predefines or describes, so the property has to be accompanied by a `pdfaExtension:schemas`
description naming it, its value type, its category and its description.

Nothing checks any of this. A packet that writes `sample:demo` with no declaration produces a
document that opens perfectly in every reader and fails validation for its metadata rather than for
anything a reader would notice — a confusing way to be wrong, and a slow one, because the only thing
in this repository that can detect it is veraPDF running in Docker under `./verapdf-check.ps1`.

This is not hypothetical. `docs/specs/pdf-a-conformance.md` records the outcome plainly: *"the one
place in this repository where somebody wrote an `AdditionalDescriptions` entry by hand, they got it
wrong, and nothing said so for as long as nobody validated the output."* The `Archive` demo shipped a
PDF/A-3b claim a validator rejects — in the demo whose whole subject is that claims are checked
rather than stamped on. It was repaired in that one copy. The seam it came through was left where it
was, so the next caller starts from the same place the last one got it wrong from.

There are two hand-built extension schemas in the repository today. `FacturXInvoice` builds one
correctly with a `StringBuilder` and solves, privately, the problem of keeping the declared set and
the written set in agreement. `ArchiveDemo` holds one as a `const string`. They disagree about
whitespace, about escaping — the demo's does none — and about category. Neither can be unit-tested
for the thing that matters about it.

## Solution

`XmpMetadata` learns to declare a schema. A caller describes the schema and its properties once, in
terms of what they mean, and the packet writer produces both the declaration and the values from
that one description.

The rule that a property must be declared before it is used stops being something a caller has to
know and becomes something they cannot express the opposite of: the declaration and the value are
written from the same list, so declaring a property that is never written, and writing one that was
never declared, both become unrepresentable rather than merely discouraged.

`AdditionalDescriptions` stays exactly as it is. It is the escape hatch for RDF the library knows
nothing about — a PDF/UA identifier, somebody's private vocabulary, a description with a shape
nobody anticipated — and two existing tests use it that way. What changes is that the one case with
a rule attached to it stops going through the hatch.

## User Stories

1. As a developer producing an archival document, I want to write a property in my own namespace, so
   that the document carries a fact XMP has no predefined word for.
2. As a developer producing an archival document, I want the namespace declared for me when I write
   a property in it, so that I cannot ship a PDF/A claim a validator rejects.
3. As a developer, I want the declared set of properties and the written set to be the same set by
   construction, so that neither can drift from the other as I add a property.
4. As a developer, I want a property's category to be a choice I make explicitly, so that I state
   whether the value came from the document's own content or from outside it rather than guessing.
5. As a developer, I want a property's human-readable description to travel with its name, so that
   the declaration says what the property is for.
6. As a developer, I want to declare more than one schema in a single packet, so that a document
   carrying both an invoice and my own annotations can describe both.
7. As a developer, I want a prefix that XML would not accept as a name to be refused when I supply
   it, so that I do not produce a packet no parser will read.
8. As a developer, I want the refusal to name the property I got wrong and the value I supplied, so
   that I can see what to fix without reading the specification.
9. As a developer, I want a quotation mark or an ampersand in my namespace URI to be escaped, so
   that an awkward URI does not silently end an attribute early and turn the rest of the packet into
   markup.
10. As a developer, I want the same escaping whether my text lands in element content or in an
    attribute value, so that I never have to work out which routine applies where.
11. As a developer writing an e-invoice, I want `FacturXInvoice` to keep the interface it has, so
    that this change costs me nothing.
12. As a developer writing an e-invoice, I want the four `fx:` properties to be declared exactly as
    they are today, so that documents I have already shipped and documents I ship after this change
    are held to the same standard.
13. As a maintainer, I want the extension-schema rule to live in one module, so that a correction to
    it reaches every caller.
14. As a maintainer, I want the rule to be assertable in a unit test, so that a regression is caught
    by `dotnet test` rather than only by veraPDF in CI.
15. As a maintainer, I want veraPDF to remain the final word on conformance, so that a passing unit
    test is not mistaken for a validator's verdict.
16. As a maintainer, I want `ArchiveDemo` to stop carrying a hand-written copy, so that the demo
    about PDF/A claims being checked demonstrates the checked way of making one.
17. As a reader of the demonstration app, I want the demo to still explain on the page why the
    declaration is required, so that the rule is taught rather than merely obeyed.
18. As a developer with RDF the library has never heard of, I want `AdditionalDescriptions` to keep
    working unchanged, so that the new interface does not close the door the old one opened.
19. As a developer, I want an empty schema — one with no properties — to be refused, so that I do
    not write a declaration that declares nothing.
20. As a developer, I want two schemas sharing a prefix in one packet to be refused, so that I do
    not produce a packet in which a property's namespace is ambiguous.
21. As a developer, I want the built-in descriptions — Dublin Core, `pdf:`, `xmp:`, `pdfaid:`,
    `pdfuaid:` — to keep the positions they have, so that nothing about existing documents changes.
22. As a developer, I want to read what my declaration produced, so that I can show it or check it
    without saving and reopening a document.
23. As a consumer of the library on Unity, I want this to work on `netstandard2.1`, so that the
    core package keeps the target it exists to keep.
24. As a maintainer, I want the conformance corpus to keep producing `pdfa-3b-facturx` through the
    e-invoicing package, so that the one thing only a validator can check is still checked the way
    it is checked today.

## Implementation Decisions

**The seam is the module callers already cross.** `PdfDocument.CustomizeMetadata` hands out an
`XmpMetadata`, so `XmpMetadata` is the highest point at which this can be expressed and it already
exists. It gains a method for declaring a schema. No new seam is introduced, and the number of
places a caller must learn about stays where it is.

**A supporting type describes a property.** A property needs a name, a description, a category and a
value. The category is a choice between the two the specification defines — `internal` for something
derived from the document's own content, `external` for something that came from outside it — and is
expressed as a choice, not as a string, because there are exactly two and a misspelling of either is
a validation failure. The value type is `Text` for everything both current callers write; the shape
must leave room for the others XMP defines without breaking callers when they arrive, but need not
deliver them.

**Declaration and value are produced from one list.** This is the design `FacturXInvoice` already
arrived at privately — one sequence of properties, walked twice, once to declare and once to write —
and the reason it gives is the reason it should move into the core: *"Declaring a property that is
never written and writing one that was never declared are both validation failures, and the way to
make neither is to have one place saying which four there are."*

**Escaping gains the quotation mark.** `XmpMetadata`'s existing `Escape` handles `&`, `<` and `>`,
which is right for element content and insufficient here: the namespace URI and the prefix are
written into *attribute* values, where a quotation mark ends the attribute early and everything after
it becomes markup. `FacturXInvoice` already carries the four-character version and explains why. The
new path uses four. The existing built-in descriptions keep the three-character routine, because
nothing about their content changes and there is no reason to alter bytes this change is not about.

**A prefix is validated, not escaped.** There is no escaping it — it becomes part of an element name
and of a namespace declaration, and neither is a place a character can be written as an entity. It is
checked as an XML NCName and refused with the property named and the value quoted. This moves out of
`FacturXInvoice` and into the core with its reasoning intact.

**Refusals are at the point of the mistake.** A malformed prefix, an empty schema, a schema with no
properties, and two schemas claiming one prefix are all refused when declared rather than at save
time. This is the opposite of how `Options.Conformance` behaves and is deliberate: nothing about a
schema declaration depends on the rest of the document, so there is nothing to wait for.

**Ordering in the packet.** Extension-schema descriptions are written before the descriptions that
use them, and both after the built-in ones. Entries in `AdditionalDescriptions` keep the position
they have relative to everything else, so a document that uses only the hatch is byte-identical
after this change.

**`FacturXInvoice` keeps its public interface exactly.** `AttachTo`, `FindIn`, `ReadFrom` and the
properties are untouched. Its two private builders — `ExtensionSchema` and `InvoiceProperties` — go,
and the `Properties()` sequence becomes the input to the new interface. The four property names,
descriptions and categories are unchanged; `external` stays `external`. Its `Escape` and
`RequiredName` helpers go, having moved to the core.

**`ArchiveDemo` loses its `const string`.** The demo declares its namespace through the new
interface. Its page-two prose explaining why the declaration is required stays, because the rule is
the subject of that demo. Its property keeps the `internal` category it has today, which is correct
for a note about which demo wrote the file.

**The whitespace of the two existing copies differs and cannot both be preserved.** The new writer
picks one shape. The packet is XML and its whitespace is not significant, but this does mean the
bytes of a Factur-X document change, so assertions that depend on exact packet text — rather than on
its structure — have to be checked.

**`netstandard2.1` applies.** The core package targets it for Unity, so nothing here may need a BCL
type that target lacks. `XmlConvert.VerifyNCName` is available on all three legs and is already used
by `PdfPinata.EInvoice`, which shares the same three targets.

## Testing Decisions

**A good test here asserts on the packet, not on the code that produced it.** The observable
behaviour is the bytes of the metadata stream and their structure as XML. A test that reaches for a
private builder, or asserts the order in which a `StringBuilder` was appended to, is testing past the
interface and will need changing the next time the implementation does. Parse the packet and ask it
questions.

**Modules under test.** `XmpMetadata` through `Build()`, which needs no document at all and is the
cheapest surface available. `PdfPinata.EInvoice` through a saved and reopened document, which is
how `EInvoiceTests` already works. `ArchiveDemo` through the existing demo smoke test, which asserts
it neither throws nor changes its page count.

**Prior art to follow rather than reinvent.** `PdfPinata.Test/Pdfs/EInvoiceTests.cs` already has
everything this needs: `XNamespace` constants for `pdfaSchema` and `pdfaProperty`, a `Packet` helper
that cuts the packet out of a saved document and hands back an `XDocument`, and a `Latin1` helper for
the cases where string containment is the honest assertion. `PdfPinata.Test/IO/XmpMetadataTests.cs`
has the lighter `Save(Action<PdfDocument>)` arrangement for packet-level questions. New tests belong
in `XmpMetadataTests`, beside the two that exercise the raw hatch.

**The behaviours worth pinning.** That every declared property is written and every written property
is declared, asserted as two set comparisons over the parsed packet rather than by counting. That a
namespace URI containing a quotation mark and an ampersand produces a packet that still parses —
`EInvoiceTests` already has this test for the invoice path and it should exist for the general one.
That a prefix XML would not accept is refused, naming the value. That two schemas can be declared in
one packet and both appear. That a schema with no properties is refused. That two schemas sharing a
prefix are refused. That a document using only `AdditionalDescriptions` produces the same bytes it
did before.

**What must keep passing unchanged.** Every test in `EInvoiceTests`, which is the regression proof
that `FacturXInvoice` still says what it said. The two tests in `XmpMetadataTests` that use the raw
hatch — they describe the hatch, which is staying, and deleting them would remove the only coverage
of it.

**veraPDF still gates and still has the last word.** `./verapdf-check.ps1` must stay green, and
`pdfa-3b-facturx` must keep being built through `PdfPinata.EInvoice` rather than by hand, because
the declaration is the one thing only a validator can check. What changes is that it stops being the
*only* thing that can check it. A passing unit test is not a conformance verdict and the tests should
not be written as though it were.

## Out of Scope

- **The `CustomizeMetadata` chaining trap.** A caller who assigns the hook after `AttachTo` silently
  drops the extension schema. Real, and a separate problem about a single assignable delegate rather
  than about schema declaration.
- **Making `Options.Conformance` refuse at the point of the claim.** The six preconditions discovered
  at `Save` are their own candidate and their own spec.
- **XMP value types beyond `Text`.** The shape must not prevent them; delivering `Date`, `Integer`
  and the structured types is work neither current caller needs.
- **Predefined schemas.** Dublin Core, `pdf:`, `xmp:`, `pdfaid:` and `pdfuaid:` are built in and need
  no declaration. Nothing here changes them.
- **Checking that a namespace URI resolves.** It is an identifier, not an address; PDF/A does not ask
  and neither does this.
- **PDF/A-4 and PDF/X.** Gated elsewhere, and both would use this rather than change it.
- **Generating invoice XML.** As `docs/specs/pdf-a-conformance.md` has always said, this library
  attaches an invoice; it does not author one.

## Further Notes

Two callers is what justifies moving this. One would be a hypothetical seam and an argument about
taste; two are a real one, and the second of them is the copy that got the rule wrong. The deletion
test agrees: delete the proposed module and the complexity does not move to one caller, it reappears
in every caller that ever needs a namespace of its own, which on the evidence of this repository is
every second feature in the archival area.

The change is unusually cheap for its position. The RDF that goes behind the interface already exists
and is already correct in `FacturXInvoice`; the work is mostly moving it, not writing it. No consumer
of any shipped package breaks, because `AdditionalDescriptions` stays and `FacturXInvoice`'s
interface does not move. The one thing that does change is the bytes of a Factur-X packet, and only
its whitespace.

Worth saying plainly, because the demo says it too: this makes the rule harder to get wrong, not
impossible to get wrong. A caller can still reach `AdditionalDescriptions` and hand it anything.
That door stays open on purpose — the alternative is a metadata writer that can only say what it has
been taught, which is the thing `AdditionalDescriptions` exists to avoid.
