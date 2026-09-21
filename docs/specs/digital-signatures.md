# Spec — signing a document

What digital signing covers, and what it deliberately leaves out.
Gap **G5** of the competitive gap analysis.

| item | what | status |
|---|---|---|
| 1 | `IPdfSigner` — the seam that keeps cryptography out of the core package | done |
| 2 | Placeholder / `/ByteRange` / patch machinery, on top of the incremental save | done |
| 3 | `Pkcs7Signer` — detached CMS, PAdES B-B and `adbe.pkcs7.detached` | done |
| 4 | Visible appearance drawn through `XGraphics` | done |
| 5 | Certifying signatures — `/DocMDP` and `/Perms` | done |
| 6 | `PdfSignatures` / `PdfSignatureVerifier` — read back, and check integrity and coverage | done |
| 7 | PAdES B-T — an RFC 3161 timestamp | done, [signature-lifetime.md](signature-lifetime.md) |
| 8 | PAdES B-LT / LTV — a DSS dictionary with OCSP and CRL data | done, [signature-lifetime.md](signature-lifetime.md) |
| 9 | Chain building, trust stores, revocation checking | not done, **deliberately** |
| 10 | Enforcing what a `/DocMDP` level permits | done, [signature-lifetime.md](signature-lifetime.md) |

Covered by `src/PdfPinata.Test/IO/SigningTests.cs`.

---

## The gap

`Pdf.AcroForms/PdfSignatureField.cs` declared `/ByteRange`, `/Contents`, `/SubFilter`, `/Filter`,
`/Reason` and `/Location` **as key constants and nothing else**. Nothing ever wrote one. iText,
Syncfusion, Aspose and Apryse all sign; it is the standard enterprise checkbox and a common reason
to buy a commercial licence.

## How it works

A PDF signature is a circular problem the format solves by cheating. The signature covers the file,
and the signature is *in* the file, so the bytes to be hashed cannot be known until the signature has
been written and the signature cannot be computed until they are.

The way out is two passes over one buffer:

```text
 1. build the signature dictionary with two placeholders
      /ByteRange [0 0000000000 0000000000 0000000000]
      /Contents  <0000 … 0000>          ← EstimatedSignatureSize bytes of room
 2. append the revision to a MemoryStream       (PdfDocument.SaveIncremental)
 3. find the two placeholders in what was written
 4. patch the real offsets over /ByteRange      ← same length, so nothing moves
 5. hash and sign everything except the hole    (IPdfSigner.Sign)
 6. write the signature into the hole as hex, zero-padded to fill it
```

**Every field is written at a fixed width**, which is what makes step 4 a byte patch rather than a
re-layout. Ten digits per byte-range value covers a ten gigabyte file; the values are right-aligned
with spaces, which a PDF array does not mind and a person reading the file finds easier.

Both placeholders are `PdfLiteral`, and that is load-bearing twice over. It is raw text the writer
emits untouched, so the reserved run of zeros arrives in the file the length it was written; and a
`PdfLiteral` is not a string, so an encryption handler does not encrypt it — which is right, because
PDF 32000-1 exempts `/Contents` from encryption.

The revision is always **appended**, never rewritten. That is not an optimisation: rewriting a file
invalidates every signature already on it. See `docs/specs/incremental-update-save.md`, which is why
G6 came first.

## The dependency, and why it is not BouncyCastle

The analysis proposed `BouncyCastle.Cryptography`. `PdfPinata.Signing` uses
`System.Security.Cryptography.Pkcs` instead, for a reason that outweighs the rest: **`SignedCms`
signs through the platform's own key storage**. A certificate whose private key lives on a smart card
or in an HSM — which is what an enterprise signing setup actually looks like — signs without this
package ever seeing the key material, because it never has to. A library that implements the
arithmetic itself needs the key bytes, and a key worth protecting will not give them up. It is also a
fraction of the size, and it is Microsoft's to maintain.

The package targets `net8.0;net10.0` and not `netstandard2.1`. netstandard2.1 exists in this
repository for Unity, and Unity's scripting runtime is the least likely place for a CMS
implementation to work — a signature that silently fails to verify is worse than one that will not
compile. Consumers there implement `IPdfSigner` themselves, which is exactly what the seam is for.

`System.Security.Cryptography.Pkcs` ships **in** the runtime but is not in the reference pack, so it
needs a `PackageReference` to compile against and is version-matched per target so the build does not
ship an older copy than the runtime already has.

## Decisions worth knowing

**PAdES by default.** `PdfSignatureFormat.Pades` produces a CAdES-BES signature carrying the
`signing-certificate-v2` signed attribute and declares `/ETSI.CAdES.detached`. That attribute is the
whole difference between this and a plain PKCS#7 signature, and it closes a real hole: without it the
certificate is merely carried *alongside* the signature, and an attacker with a second certificate
whose key verifies the same signature can swap it in and change who the document appears to have been
signed by. `PdfSignatureFormat.Pkcs7` is there for readers too old to know what CAdES is.

**`ESSCertIDv2` omits its hash algorithm for SHA-256.** RFC 5035 gives the field a DEFAULT of
SHA-256 and DER requires a field equal to its default to be left out. Writing it anyway produces an
encoding some verifiers reject.

**Verification answers two questions and needs both.** `IsIntact` says the signature verifies over
the bytes it covers. `CoversWholeDocument` says those bytes are the whole file bar the signature.
Reporting only the first would report a document as sound when a reader would not: a signature over
the first page of a five page document verifies perfectly and proves nothing about pages two to five.

**A document signed twice has one signature that no longer covers it, and that is correct.** The
second revision comes after the first signature's byte range, so the earlier signature is intact but
partial. That is what an appended revision *is*, and a reader shows it as "signed, then changed" —
not as a failure.

**Signing time is a claim, not evidence, and PAdES makes it in one place only.** `/M` and the CMS
`signing-time` attribute are both the producer's own clock. PAdES uses `/M`, and the ETSI profiles
have said since TS 102 778 that the CMS attribute should not also be there — two claimed times that
can disagree help nobody — so `Pkcs7Signer` writes it for `Pkcs7` and not for `Pades`, and
`IncludeSigningTime` is there for anyone who needs the other answer. Making the time provable is a
timestamp token from a time-stamping authority, which is PAdES B-T and is item 7.

**A certifying signature must be the first one.** `/DocMDP` says what later revisions may still do.
Certifying an already-signed document produces a file that opens and that readers report as invalid;
that rule belongs to the reader and is not enforced here.

**The reserved size cannot be revised.** `EstimatedSignatureSize` is committed to before the bytes
being signed exist, so a signature that turns out not to fit can only be found out afterwards — and
then it is an `InvalidOperationException` naming the property, never a truncated signature in a file
that opens. The default of 16 kB is generous on purpose: a one-certificate RSA-2048 signature is
around 1.5 kB, and guessing high costs file size while guessing low costs the save.

**The widget's annotation flags are chosen up front or not at all** (empira/PDFsharp#157). The field
is created while signing, inside the revision the signature covers, so its `/F` cannot be changed
afterwards without changing a signed object. `PdfSignatureOptions.AnnotationFlags` is written as asked,
defaulting to `Print` — the `/F 4` every earlier version wrote — and no flags at all omits `/F`, whose
default is zero. `Hidden` and `NoView` are allowed: they govern what a reader draws on the page, not
whether it finds the signature, which it does through the AcroForm. PDF/A requires `Print` and forbids
`Invisible`, `Hidden` and `NoView` — parts 2 and 3 `ToggleNoView` too — and `PdfConformanceWriter.CheckAnnotationFlags`
refuses them before anything is added — but only when the document being signed has
`Options.Conformance` set. A file opened for appending does not read its own PDF/A claim back out of
its XMP, so signing an archival file without restating the claim is held to the rule by the caller.

## What is deliberately left out

- **Trust.** `PdfSignatureVerifier` builds no certificate chain, consults no trust store and checks
  no revocation. A signature it calls valid may have been made with a certificate nobody should
  believe. What it does check is what actually goes wrong in practice: a document edited after
  signing, a byte range computed wrongly, or a signature covering only part of a file.
- **B-T and B-LT.** A timestamp needs an RFC 3161 client and a network call at signing time; LTV
  needs a `/DSS` dictionary with the OCSP responses and CRLs to validate the chain years later.
  Both are additive on top of what is here — an unsigned `signature-time-stamp` attribute and a
  catalog entry — and neither is started.
- **Enforcing `/DocMDP`.** The level is written and read back; nothing checks that a later revision
  stayed inside it. Doing that means diffing two revisions of an object graph.
- **Signing an existing signature field.** A field placed by someone else and left empty — the
  "please sign here" workflow — is not filled in; `PdfSigner` always creates its own field.
- **Multiple widgets on one field.** An AcroForm feature rather than a signature one, and not
  reachable through what is here. `/Lock` was listed here too; it is now written and honoured on the
  field `PdfSigner` creates through `PdfSignatureOptions.LockAction`, and `/SV` is modelled - see
  `upstream-7-interactive-port.md` item 9. Honouring either on a field somebody else placed waits on
  signing an existing field, above.
- **Interoperability testing against a reader.** The tests verify with the same CMS implementation
  that produced the signature, which proves the byte range and the encoding and does not prove that
  Acrobat agrees. `pdfsig` from poppler-utils in CI would; it is not installed there.

## Related

- `docs/specs/incremental-update-save.md` — G6, which signing is built on and could not work without.
- `docs/specs/cross-reference-streams.md` — G1. A signed document written with a cross-reference
  stream works, because the placeholder search runs over the appended bytes either way.
- `docs/specs/pdf-a-conformance.md` — G4. PDF/A forbids encryption but not signatures.
