---
title: PDF/A archiving
description: Claim PDF/A-1, PDF/A-2 or PDF/A-3 for a document, and let PdfPinata refuse to save it when it breaks a rule of that claim.
demos: [Archive]
---

PDF/A (ISO 19005) is the form of PDF for documents that must stay readable for decades. It forbids
anything whose meaning depends on something outside the file: a font installed on the machine, a
colour profile named but not embedded, encryption, JavaScript. Archives, courts, public bodies and the
e-invoicing formats ask for it.

PdfPinata does more than write the claim into the file. When a document claims a PDF/A level, the
writer checks the document before it writes a byte, and throws if the document breaks a rule it can
check. Everything on this page is in the core `PdfPinata` package.

## Choose a level

Set `PdfDocumentOptions.Conformance` to a value of `PdfAConformance`:

| Value | Standard | Use it when |
|---|---|---|
| `PdfA1B` | ISO 19005-1 | A reader requires PDF/A-1. It is the strictest: PDF 1.4 only, no transparency, no JPEG 2000 images, no attachments. |
| `PdfA2B` | ISO 19005-2 | You have no reason to choose another level. Transparency and JPEG 2000 are allowed. |
| `PdfA3B` | ISO 19005-3 | The document carries attachments. PDF/A-3 is the only level that allows files of any kind inside the document, which is why [e-invoices](./e-invoicing.md) use it. |
| `PdfA1A`, `PdfA2A`, `PdfA3A` | the same three parts | The document must also be accessible. The `A` levels add the tagging rules of PDF/UA-1, so the document must be tagged. See [Accessibility](./accessibility.md). |

The `B` ("basic") levels promise that the document will look the same in the future. The `A`
("accessible") levels also promise that its content can be read by a screen reader and extracted in
reading order.

## Claim a level

A PDF/A document needs a title. Set `Info.Title` first, then make the claim:

```csharp demo=Archive snippet=claim
```

The Archive demo sets the output intent by hand to show where it goes. You do not have to: an RGB
document that names no profile gets the same sRGB profile automatically (see below).

`Options.Conformance` is checked when you save. To be told about a problem at the point where you
make the claim, call `ClaimConformance` instead:

```csharp
document.Info.Title = "Annual report 2026";
document.ClaimConformance(PdfAConformance.PdfA2B);   // throws now if the title is missing
```

`ClaimConformance` checks what it can at once: the title, encryption, the colour mode, the PDF
version under PDF/A-1, and, for an `A` level, whether the document is tagged. It then sets
`Options.Conformance`. The save still checks everything again, because you can change the document
after the claim.

## Colour and the output intent

A PDF/A document that uses device colours (plain RGB or CMYK numbers) must embed an ICC profile
that says what those numbers mean. This is the output intent.

- **RGB documents get one automatically.** `PdfColorMode.Rgb` is the default colour mode. If you set
  no profile, PdfPinata embeds `PdfOutputIntents.SrgbProfile`, a small sRGB profile that ships in the
  core package, and names it `sRGB IEC61966-2.1`. Colours written as RGB with no other instruction
  are sRGB, so this describes your document accurately.
- **CMYK documents are refused without one.** The same four CMYK numbers print as different colours
  on different presses and papers, so there is no correct default. If `ColorMode` is `Cmyk`, set
  `Options.OutputIntentIccProfile` to the profile your print work was made for.
- **`PdfColorMode.Undefined` is refused without one.** In this mode each colour is written the way it
  was created, so one document can hold RGB and CMYK together, and no single profile describes both.
  Set `ColorMode` to `Rgb`, or supply a profile yourself.

A profile you set always wins. `Options.OutputIntentIdentifier` sets the name the profile is known
by. If your pages paint colours that the profile cannot describe, for example CMYK content under an
RGB profile, the save is refused and the message says which kinds of colour it found.

## The XMP metadata

A document that makes a claim carries an XMP metadata packet. PdfPinata builds it at save time from
`document.Info` (title, author, subject, keywords, dates), so the packet and the information
dictionary always agree. A validator checks that they agree. The packet also holds the PDF/A
identifier, which has no other place in the file.

To add your own properties, use `document.AddMetadataContributor`, or assign
`document.CustomizeMetadata`. Both receive the `XmpMetadata` object just before it is written.
`CustomizeMetadata` holds one delegate, so assigning it replaces any delegate already there.
`AddMetadataContributor` adds to the list and never replaces anything, so prefer it in library code.

PDF/A accepts only properties from schemas it knows or that the file declares. A property in a
namespace of your own must be declared in an extension schema. `XmpMetadata.DeclareSchema` declares
the schema and writes its properties in one step:

```csharp demo=Archive snippet=extension-schema
```

If you write your own XML into `XmpMetadata.AdditionalDescriptions` instead, you must also write the
`pdfaExtension:schemas` declaration yourself. Without it the file opens normally in every reader and
fails validation.

To write an XMP packet for a document that claims nothing, set `Options.WriteXmpMetadata = true`.

## What the writer refuses

Each refusal is an `InvalidOperationException` thrown from `Save` (or from `ClaimConformance` where
the rule can be checked early). The message names the rule and tells you what to change.

| Rule | Levels |
|---|---|
| The document has a title | all |
| The document is not encrypted | all |
| A CMYK or `Undefined` document has an output-intent profile | all |
| The output intent describes every device colour the pages paint | all |
| No image is set to interpolate (`XImage.Interpolate`) | all |
| No transparency, including transparency reached through a form or a soft mask | PDF/A-1 |
| No JPEG 2000 image | PDF/A-1 |
| The PDF version is no higher than 1.4, and `CrossReferenceFormat` is `Classic` | PDF/A-1 |
| No embedded files | PDF/A-1, PDF/A-2 |
| Every attachment is associated with the document, has a relationship and a media type | PDF/A-3 |
| The document is tagged | `A` levels, plus the PDF/UA-1 checks |

PDF/A-2 allows an attachment that is itself a PDF/A file. PdfPinata cannot prove that an attached
file is PDF/A, so it refuses all attachments under PDF/A-2. Claim PDF/A-3 if you need to attach
files. `document.Attachments.Add` writes the association, the relationship and the media type for
you, so a document built through it passes the PDF/A-3 checks.

## Validate with veraPDF

A successful save is not a validator's verdict. PdfPinata checks the rules it can check by looking at
the document, and there are rules it does not check. Use [veraPDF](https://verapdf.org), the
industry reference validator, before you rely on a claim. veraPDF reads the claim from the file's
own metadata, so it holds each file to the level it claims. With Docker installed:

```sh
docker run --rm -v "$PWD:/data" verapdf/cli:v1.30.2 --format text /data/report.pdf
```

PdfPinata's own build runs veraPDF on one document for each level it can claim, and the build fails
if any of them stops conforming.

## Things to know

- **Fonts are always embedded.** PdfPinata has no setting to turn font embedding off, so the rule
  that catches most PDF producers cannot be broken here. TrueType fonts are subset. Fonts with
  PostScript (CFF) outlines are embedded whole.
- **Setting a password raises the security level, and PDF/A forbids encryption.** A document that
  has both a PDF/A claim and a password will not save. See [Encryption](./encryption.md).
- **The message names the page.** A refusal for transparency, JPEG 2000 or an interpolated image
  says which page carries it.
- **Converting an existing PDF to PDF/A is not supported.** The claim is for documents you build.
  PdfPinata does not re-embed missing fonts, convert colours or flatten transparency in someone
  else's file.
- **PDF/A-4 is not supported.**
- **A PDF/A document can also claim PDF/UA-1.** The two claims are independent: set both
  `Options.Conformance` and `Options.UAConformance`, or use an `A` level.
- **The packet is not compressed.** Tools find it by scanning the file for its markers, so it is
  left readable on purpose.

## See it in action

[The Archive demo](../demos.mdx#archive) claims PDF/A-3b, prints the XMP packet it carries, and
prints the real messages from five documents built to break one rule each.

<details>
<summary>The full Archive demo</summary>

```csharp demo=Archive
```

</details>
