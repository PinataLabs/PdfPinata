---
title: Digital signatures
description: Sign a PDF with an X.509 certificate, add a trusted timestamp and long-term validation data, and check a signature's integrity.
demos: [Signing]
---

A digital signature proves two things about a PDF: who signed it, and that no byte it covers has
changed since. PdfPinata signs with an X.509 certificate and writes PAdES signatures, the form the
European eIDAS regulation expects.

Signing is split across two packages:

- The core `PdfPinata` package holds the PDF side: the signature field, the space reserved for the
  signature, and the byte range it covers. It contains no cryptography. It is in namespace
  `PdfPinata.Pdf.Signatures`.
- [`PdfPinata.Signing`](https://www.nuget.org/packages/PdfPinata.Signing) does the cryptography
  with `System.Security.Cryptography.Pkcs`, which is part of .NET. It targets `net8.0` and `net10.0`
  only, not `netstandard2.1`, so it is not available on Unity.

```sh
dotnet add package PdfPinata.Signing
```

## Sign a document you have built

Signing never rewrites a file. It appends a new revision to the bytes that already exist. So save the
document first, then sign the saved bytes:

```csharp
using System.Security.Cryptography.X509Certificates;
using PdfPinata.Pdf.Signatures;
using PdfPinata.Signing;

using X509Certificate2 certificate = new X509Certificate2("signer.pfx", pfxPassword);
Pkcs7Signer signer = new Pkcs7Signer(certificate);   // PAdES, SHA-256

using MemoryStream unsigned = new MemoryStream();
document.Save(unsigned, false);
unsigned.Position = 0;

using FileStream output = File.Create("signed.pdf");
PdfSigner.Sign(unsigned, output, signer, new PdfSignatureOptions
{
    Reason = "Approved for payment",
    Location = "London",
});
```

The certificate must have its private key. `Pkcs7Signer` signs through the platform's own key
storage, so a key on a smart card or in a hardware security module works without PdfPinata ever
reading the key. On .NET 9 and later, the compiler suggests
`X509CertificateLoader.LoadPkcs12FromFile` in place of the `X509Certificate2` constructor. Both give
you a certificate you can pass to `Pkcs7Signer`.

`Pkcs7Signer` takes these options:

- `format`: `PdfSignatureFormat.Pades` (the default) or `PdfSignatureFormat.Pkcs7`. Use `Pkcs7` only
  for a reader too old to understand PAdES. PAdES binds the certificate into the signed data, which
  stops anyone from swapping in a different certificate.
- `hashAlgorithm`: SHA-256 by default. SHA-1 and MD5 are refused.
- `chain`: intermediate certificates to embed, so a verifier can build the chain without fetching
  them.
- `timestampProvider`: see [Add a trusted timestamp](#add-a-trusted-timestamp).

## Sign an existing file

To sign a PDF that is already on disk, pass its stream to the same `PdfSigner.Sign` overload. To make
changes first, open it with `PdfDocumentOpenMode.Append` and use the overload that takes a document:

```csharp
PdfDocument document = PdfReader.Open("contract.pdf", PdfDocumentOpenMode.Append);
using FileStream output = File.Create("contract-signed.pdf");
PdfSigner.Sign(document, output, signer, new PdfSignatureOptions { FieldName = "Signature2" });
```

A document can be signed more than once. Each signature needs its own `FieldName`; the default is
`"Signature1"`. An earlier signature stays valid, but it no longer covers the whole file, because the
later revision comes after it. A reader shows this as "signed, then changed", which is correct.

## Visible and invisible signatures

A signature with no `Rectangle` is invisible. It is still a field on the page and still covers the
whole document. Most machine-applied signatures are invisible.

To show the signature on a page, set `PageIndex` (counted from zero), `Rectangle` (in the same
top-left coordinates `XGraphics` uses) and `DrawAppearance`. `DrawAppearance` receives an `XGraphics`
the size of the rectangle, with its origin at the rectangle's top-left corner:

```csharp
PdfSignatureOptions options = new PdfSignatureOptions
{
    PageIndex = 0,
    Rectangle = new XRect(50, 640, 230, 70),
    DrawAppearance = (gfx, area) =>
    {
        gfx.DrawRectangle(XBrushes.WhiteSmoke, area);
        gfx.DrawString("Signed by Accounts", font, XBrushes.Black, 8, 18);
    },
};
```

What you draw is decoration. A reader validates the signature, not the picture.

## Certification signatures

An ordinary signature is an approval: "I signed this". A certification signature also says what may
happen to the document afterwards. Set `PdfSignatureOptions.Certification`:

| `PdfCertificationLevel` | Later revisions may |
|---|---|
| `NotCertified` (default) | do anything; this is an approval signature |
| `NoChangesAllowed` | change nothing |
| `FormFillingAllowed` | fill in form fields and add signatures |
| `FormFillingAndAnnotationsAllowed` | fill in forms, sign, and add or change annotations |

A document can carry only one certification signature, and it must be the first signature. PdfPinata
refuses to certify a document that is already certified. It also enforces the level on a certified
document you open: an operation the level forbids throws `InvalidOperationException`, and a full
`Save` of a certified document is refused.

## Add a trusted timestamp

The signing time in `/M` comes from the signer's own clock, so it proves nothing. A timestamp from a
time-stamping authority (TSA) proves when the signature existed. That is PAdES B-T. Pass a
timestamp provider to `Pkcs7Signer`:

```csharp
using Rfc3161TimestampProvider tsa = new Rfc3161TimestampProvider(new Uri("https://tsa.example.com"));
Pkcs7Signer signer = new Pkcs7Signer(certificate, timestampProvider: tsa);
```

If the TSA cannot be reached, signing fails. It never falls back to an untimestamped signature
without telling you.

## Keep a signature verifiable for years

A certificate expires, and the services that could confirm it was valid stop answering. PAdES B-LT
solves this by storing the certificates and revocation responses in the document itself, in a
security store (`/DSS`). `PdfSignatureValidationData.Add` gathers that data for every signature in a
document and appends it as a new revision. It works on a document someone else signed, and it does
not invalidate any signature:

```csharp
PdfDocument signed = PdfReader.Open("contract-signed.pdf", PdfDocumentOpenMode.Append);
using OcspRevocationDataProvider ocsp = new OcspRevocationDataProvider();
using FileStream output = File.Create("contract-ltv.pdf");
PdfSignatureValidationData.Add(signed, output, ocsp);
```

`OcspRevocationDataProvider` fetches OCSP responses from the responder each certificate names. It
does not fetch CRLs. To supply revocation data another way, implement `IRevocationDataProvider`.
`PdfValidationData.IsPresent(document)` tells you whether a document already has a security store.

## Verify a signature

`PdfSignatureVerifier.Verify` checks every signature in a file and answers two separate questions:

- `IsIntact`: the signature verifies over the bytes it covers.
- `CoversWholeDocument`: those bytes are the whole file, apart from the signature itself.

`IsValid` is true only when both are true. A signature over the first revision of a longer file is
intact but proves nothing about what was added later.

```csharp
foreach (PdfSignatureVerification result in PdfSignatureVerifier.Verify(File.ReadAllBytes("signed.pdf")))
{
    Console.WriteLine($"{result.Signature.FieldName}: valid={result.IsValid}, problem={result.Problem}");
    if (result.HasTimestamp)
        Console.WriteLine($"  timestamped {result.Timestamp}");
}
```

To read what a signature claims without checking it, use `PdfSignatures.InDocument(document)`, which
returns the field name, reason, location, signing time, byte range and certification level.

## Things to know

- **Do not call `Save` after signing.** `Save` writes the whole file again from the object model. That
  renumbers objects and drops every earlier revision, so the signature covers bytes that no longer
  exist. To change a signed document, open it with `PdfDocumentOpenMode.Append` and use
  `SaveIncremental`. See [Incremental saving](../existing-pdfs/incremental-saving.md).
- **The verifier checks integrity, not trust.** It builds no certificate chain, consults no trust
  store and checks no revocation. A signature it calls valid may still use a certificate nobody should
  trust. A green tick in a PDF reader depends on the certificate chaining to a root that reader
  trusts.
- **Space for the signature is reserved in advance.** `Pkcs7Signer.EstimatedSignatureSize` defaults
  to 16 KB. If a signature with a long chain or an embedded timestamp does not fit, `Sign` throws and
  names the property. Raise it and sign again.
- **PDF/A allows signatures.** It forbids encryption, not signing. PdfPinata's own veraPDF checks do
  not include a signed document, so validate a signed PDF/A file yourself.
- **Filling in an existing empty signature field is not supported.** `PdfSigner` always creates its
  own field.
- **PAdES B-LTA** (archive timestamps refreshed over time) is not supported.
- **Write your own signer** for a remote signing service by implementing `IPdfSigner`: a `SubFilter`,
  an `EstimatedSignatureSize`, and a `Sign(Stream)` method that returns the detached CMS signature.
  The core package needs nothing else, and this route also works on `netstandard2.1`.

## See it in action

[The Signing demo](../demos.mdx#signing) signs a document with a PAdES signature and a visible
appearance, then reads the signature back and verifies it. The part printed below draws the pages.
The signing itself is in the demo's `Save` override and helper methods, in
[SigningDemo.cs](https://github.com/PinataLabs/PdfPinata/blob/main/src/SampleApp/Demos/SigningDemo.cs).

<details>
<summary>The full Signing demo</summary>

```csharp demo=Signing
```

</details>
