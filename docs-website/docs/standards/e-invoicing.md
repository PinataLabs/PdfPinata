---
title: "E-invoicing: Factur-X and ZUGFeRD"
description: Attach invoice XML to a PDF/A-3 document as a Factur-X or ZUGFeRD hybrid invoice, and read the XML back out of one you receive.
demos: [FacturX]
---

A hybrid e-invoice is one file that a person reads and an accounts system books. The PDF shows the
invoice on the page. The same invoice is attached to the PDF as UN/CEFACT Cross Industry Invoice
(CII) XML, which software reads without anyone retyping a figure. Factur-X and ZUGFeRD (version 2.1
and later) are the same format under two names, and the European e-invoicing mandates accept it.

The format is a PDF/A-3 document with the XML attached under exact rules. Getting any rule slightly
wrong produces a file that opens in every reader and is rejected by the system it was sent to. The
[`PdfPinata.EInvoice`](https://www.nuget.org/packages/PdfPinata.EInvoice) package applies those rules
for you. It has no dependencies beyond the core package and targets `netstandard2.1`, `net8.0` and
`net10.0`.

```sh
dotnet add package PdfPinata.EInvoice
```

## You bring the XML

`PdfPinata.EInvoice` does not create the invoice XML, and it does not check it. Producing valid
EN 16931 XML, with each country's business rules, is a separate job for a separate library or
service. `FacturXInvoice` takes the bytes that job produced and puts them into the PDF correctly.

## Build the invoice

1. Build the PDF that shows the invoice, as you would any other document.
2. Set `Info.Title`. The document will claim PDF/A-3, and PDF/A requires a title.
3. Create a `FacturXInvoice` from the XML bytes, and set its `Profile`.
4. Call `AttachTo(document)`.
5. Save the document.

```csharp demo=FacturX snippet=prepare
```

```csharp demo=FacturX snippet=attach
```

`AttachTo` does all of this:

- attaches the XML as `factur-x.xml`, the name a receiver looks for;
- marks it as `text/xml`, with the relationship `/Data`, which says the XML and the page are the
  same invoice;
- associates the attachment with the document, which PDF/A-3 requires;
- claims PDF/A-3b, if the document claims nothing yet;
- adds an XMP extension schema to the metadata that declares the four Factur-X properties
  (`fx:DocumentType`, `fx:DocumentFileName`, `fx:Version` and `fx:ConformanceLevel`), and writes
  them.

It returns the `PdfFileSpecification` of the attachment, so you can change anything it did not set.

The document needs no colour setup. An RGB document that claims PDF/A gets an sRGB output intent
automatically. A CMYK document must supply its own profile. See [PDF/A archiving](./pdf-a.md).

## Choose the profile

`Profile` says how much of an invoice the XML contains. It is written to `fx:ConformanceLevel`.
Set it to the profile your XML really meets: PdfPinata does not read the XML to check.

| `EInvoiceProfile` | Written as | What the XML carries |
|---|---|---|
| `Minimum` | `MINIMUM` | Parties, dates and totals. No line items. Not a legal invoice in France. |
| `BasicWithoutLines` | `BASIC WL` | The full header, tax breakdown and totals. No line items. |
| `Basic` | `BASIC` | A subset of EN 16931, with line items. |
| `En16931` (default) | `EN 16931` | The full European standard EN 16931. What the mandates mean by "e-invoice". |
| `Extended` | `EXTENDED` | EN 16931 plus sector or national extensions. |
| `XRechnung` | `XRECHNUNG` | The German XRechnung rules, carried as CII. |

The enum exists because of the spelling. Two of the values contain a space, and a document that writes
`EN16931` passes every PDF check and is then rejected by the receiver.

## Read an invoice you receive

To take the XML out of a hybrid invoice, open the PDF and call `FacturXInvoice.ReadFrom`. It returns
the XML bytes exactly as they were embedded, or `null` if the document carries no invoice:

```csharp
using PdfPinata.EInvoice;

PdfDocument received = PdfReader.Open("incoming.pdf", PdfDocumentOpenMode.Import);
byte[] xml = FacturXInvoice.ReadFrom(received);
if (xml == null)
{
    // Not a hybrid invoice.
}
```

`FacturXInvoice.FindIn` returns the attachment's `PdfFileSpecification` instead. Both look for the
invoice by file name: `factur-x.xml`, `zugferd-invoice.xml`, `xrechnung.xml` or `order-x.xml`,
ignoring case. They do not guess from the media type, because a document can carry other XML that is
not an invoice.

## Add your own metadata

`AttachTo` adds its metadata through `document.AddMetadataContributor`, which adds to the metadata
rather than replacing it. Your own `document.CustomizeMetadata` delegate and any contributors you
register still run, whether you set them before or after `AttachTo`. If your own properties use a
namespace of your own, declare it with `XmpMetadata.DeclareSchema`, as the
[PDF/A page](./pdf-a.md) shows. Each declared schema needs its own prefix; `fx` is taken.

## Things to know

- **The claim is enforced.** A document that reaches `Save` with no title, or as CMYK with no output
  intent profile, is refused. The message says what to set.
- **A PDF/A-1 or PDF/A-2 claim is refused, not upgraded.** Neither level may carry an attachment. If
  the document already claims one, `AttachTo` throws and leaves the document unchanged. Set
  `Options.Conformance` to `PdfA3B`, or leave it unset.
- **An accessible invoice keeps its claim.** If the document already claims `PdfA3A`, `AttachTo`
  keeps that claim. `PdfA3A` requires the document to be tagged; see
  [Accessibility](./accessibility.md).
- **One invoice per name.** Attaching a second invoice under a file name already used throws, and the
  document is left as it was.
- **An empty XML array is refused.** `new FacturXInvoice(Array.Empty<byte>())` throws.
- **ZUGFeRD 1.0 and Order-X are possible but not tested.** `FileName`, `NamespaceUri`, `Prefix`,
  `SchemaName`, `DocumentType` and `Relationship` are settable, so you can produce the older ZUGFeRD
  1.0 layout or an Order-X document. The defaults are Factur-X, and only Factur-X has been validated.
  ZUGFeRD 1.0 is superseded.
- **Validate the whole invoice.** PdfPinata's own build validates a Factur-X document built this way
  with veraPDF, so the PDF/A side is checked. Check the XML with a validator for your profile, and
  test the PDF against the receiver's own checks where you can.

## See it in action

[The FacturX demo](../demos.mdx#facturx) draws an invoice, attaches the same invoice as CII XML built
from the same line items, and prints the metadata the package wrote. It then reopens the file and
reads the invoice back, the way a receiving system would.

<details>
<summary>The full FacturX demo</summary>

```csharp demo=FacturX
```

</details>
