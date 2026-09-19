# Spec — a signature that outlives the certificate that made it

The remainder of gap **G5**. `docs/specs/digital-signatures.md` is the note for what shipped — the
signing seam, PAdES B-B, certifying signatures and verification — and records these three as open.
This is the spec for them.

| item | what | status |
|---|---|---|
| 1 | `ITimestampProvider` in `PdfPinata.Signing`; `Rfc3161TimestampProvider` over HTTP; `LocalTimestampAuthority` for tests | done |
| 2 | PAdES B-T — the token folded into the CMS as an unsigned `signature-time-stamp` attribute; verification reports it | done |
| 3 | `IRevocationDataProvider`; `OcspRevocationDataProvider` (OCSP only, CRL fetch left empty) | done |
| 4 | PAdES B-LT — `PdfValidationData.Add` writes `/DSS` and `/VRI` through an incremental save, leaving every signature intact | done |
| 5 | A certifying signature's `/DocMDP` level enforced through `EnsureCanModify`, by `PdfChangeKind` | done |
| 6 | A full `Save` of a certified document is refused; the incremental save is the permitted route | done |
| 7 | B-LTA, chain building, trust, revocation *checking*, a timestamped corpus document | not done, **deliberately** |

Covered by `src/PdfPinata.Test/IO/SignatureTimestampTests.cs`, `SignatureValidationDataTests.cs` and
`CertificationEnforcementTests.cs`. Neither network provider is exercised by the suite.

Two things were decided while building it. **Adding validation data is not gated by the certification
level** — a `/DSS` is what LTV exists for and is not a change `/DocMDP` restricts. And
**`PdfAcroField.Value` and `PdfAnnotations.Add`/`Remove` gained the open-mode guard they never had**, because
routing them through `EnsureCanModify` for certification widened both at once; signing itself counts as
form fill-in, as `/DocMDP` groups it.

## Problem Statement

**A signature made today cannot be shown to have been made today.** The shipped signer can record a
signing time, and the document says so, but that time is the producer's own clock: a claim by the
party with the most to gain from it. A verifier has no way to distinguish a document signed while the
certificate was valid from one signed after it expired, or from one whose clock was simply wrong.
That is the entire purpose of PAdES B-T, and it is the difference between a signature that means
something in a year and one that does not.

**A signature stops being verifiable when its certificate expires.** Everything needed to check
it — the chain, the revocation responses that were current at the time — lives outside the file, on
services that will not answer for a certificate that expired years ago. A document archived for a
decade and verified at the end of it will fail, not because anything is wrong with it but because the
evidence was never put inside. That is PAdES B-LT, and it is the profile that archival and e-invoicing
workflows actually ask for.

**And a certifying signature's permissions are decorative.** The library can write a certification
level saying the document may not be changed, may take form fill-in only, or may additionally take
annotations. Having written it, the library then honours none of it: the same document reopened can be
changed however the caller likes, and a full save will rewrite it from the object model, invalidating
every signature in it and discarding every earlier revision. The library enforces a permission level
it invented and ignores one it was told.

## Solution

**A timestamp arrives through a seam that has a name and lives where the cryptography lives.** The
core's signature machinery holds the placeholder, the byte range and the patching and knows nothing
about cryptography; that split is deliberate and stays. So a time-stamping authority is reached
through a seam in the signing package, with an implementation over HTTP shipped for real use and a
local one available to tests. The token it returns is folded into the signed message as an unsigned
attribute — which means it travels inside the blob the existing signing seam already hands back, and
the core learns nothing new.

**Validation data arrives the same way and is written by the core.** A sibling seam supplies the
certificates and revocation responses that prove the chain; the core writes them into the document's
security store, which is PDF machinery with no cryptography in it, exactly like everything else the
core does for signatures. The store is added by incremental update, so the signature it vouches for
is not disturbed.

**And permissions are enforced through the question the library already asks.** A document that
declares it may not be changed is a document not opened for changing. That question is already
singular, already sits in front of the operations that matter, and already answers with a message
naming why. It widens from "may this be changed" to "may this take **this kind** of change", because a
certification level permits some changes and not others, and the answer then comes from the open mode
and the certification together.

## User Stories

1. As a developer signing a contract, I want a trusted timestamp on the signature, so that the time of
   signing is evidence rather than my own assertion.
2. As a developer, I want to name the time-stamping authority my organisation uses, so that the
   timestamp comes from a party my counterparties already trust.
3. As a developer, I want a timestamp failure to fail the signing rather than to be silently skipped,
   so that I never ship a document that claims a profile it does not meet.
4. As a developer without a timestamping service, I want to keep signing exactly as I do today, so
   that adding this costs nothing to anyone who does not want it.
5. As a developer archiving documents, I want the certificate chain and revocation data embedded in
   the file, so that the signature can still be verified after the certificate has expired.
6. As a developer archiving documents, I want that data added without invalidating the signature it
   describes, so that adding evidence does not destroy what it is evidence of.
7. As a developer, I want to add validation data to a document that was signed earlier and elsewhere,
   so that I can archive documents I did not produce.
8. As an auditor verifying a document, I want the verifier to tell me whether a signature carries a
   timestamp and what it says, so that I can judge when it was made.
9. As an auditor, I want the verifier to tell me whether validation data is present, so that I know
   whether the file can be checked without network access.
10. As an auditor, I want the verifier to keep telling me plainly that it does not build chains or
    check revocation itself, so that I do not mistake a structural check for a trust decision.
11. As a developer who certified a document against all changes, I want an attempt to change it to be
    refused, so that the permission I set means something.
12. As a developer who certified a document for form fill-in, I want filling a field to be allowed and
    inserting a page to be refused, so that the level I chose is the level enforced.
13. As a developer who certified a document for annotations, I want an annotation to be allowed and
    the page tree to stay closed, so that the third level is distinct from the second.
14. As a developer, I want the refusal to name whether the open mode or the certification refused, so
    that I know whether to reopen the document differently or to stop.
15. As a developer, I want a full save of a certified document to be refused, so that I do not destroy
    every signature in it by writing it out the ordinary way.
16. As a developer, I want an incremental save of a permitted change to a certified document to
    succeed, so that the permitted path is genuinely available.
17. As a developer of an unsigned document, I want none of this to apply, so that the common path is
    untouched.
18. As a maintainer, I want one refusal path rather than two, so that a caller cannot pass one guard
    and fail the other in a differently worded way.
19. As a maintainer, I want the core to stay free of cryptography, so that the split that keeps the
    core dependency-light survives this work.
20. As a maintainer, I want the timestamp to travel inside the message the existing signing seam
    already returns, so that no implementor of that seam has to learn a new concept.
21. As a maintainer, I want tests that never reach the network, so that the suite stays hermetic and
    fast and does not fail when somebody else's service is down.
22. As a maintainer, I want the revocation seam to be shaped like the timestamp seam, so that the two
    network-facing capabilities are learned once.
23. As a developer on the netstandard leg, I want none of this to reach the packages Unity consumes,
    so that the target that exists for Unity keeps building.

## Implementation Decisions

**The timestamp seam is named, and it lives in the signing package.** Not a bare delegate, because a
capability with a name is discoverable and this one will shortly have a sibling; and not in the core,
because fetching a token is cryptography and network, and the core's signature namespace deliberately
holds neither. An implementation over HTTP ships for real use.

**The token travels inside the signed message.** A signature timestamp is an unsigned attribute of the
signer information, so it is inside the blob the signing seam already returns as a byte array. No
change to the core seam, no second interface for implementors of it to notice, and a third-party
signer that wants to timestamp for itself can already do so today without any of this.

**Validation data is a sibling seam in the same package**, supplying the certificates and revocation
responses. The core writes them into the document's security store; the security store is a
dictionary of streams and is PDF machinery, so it belongs on the core side of the split exactly as the
byte range and the placeholder do.

**Validation data is added by incremental update.** Rewriting the file would invalidate the signature
the data exists to support. This is the capability incremental save was built for and it needs nothing
new from it.

**Adding validation data is available separately from signing**, because the document being archived
was often signed by somebody else. The two capabilities are related and are not the same call.

**A timestamp failure fails the signing.** A signature that silently falls back to B-B while the caller
believes it is B-T is worse than no signature, because it will be discovered by a verifier and not by
its author.

**Permissions are enforced through the existing modification guard.** The guard already fronts the
operations that can change a document and already produces a message naming the mode the document was
opened with and what the operation needs. It gains the notion of **what kind of change** is being
attempted, because a certification level permits some kinds and refuses others; the answer then
consults the open mode and any certifying signature, and the message says which of the two refused.

**The change kinds are the ones the standard distinguishes**, and no more: changes to the document's
structure and content, changes to annotations, and changes to form field values including signing.
That is what the three certification levels are defined in terms of, and inventing finer categories
would mean deciding things the standard does not.

**A full save of a certified document is refused.** Saving rewrites from the object model, which
discards every earlier revision and invalidates every signature; for a certified document that is
never what the caller meant. The permitted route is an incremental save, which already exists.

**This sequences after the open-mode work merges**, because it widens a guard that has only just
become real. Building it against the older shape would mean writing the guard twice.

**Nothing here reaches the netstandard leg.** The signing package already does not target it, for the
reason its own spec records, and this work stays inside that package and the core PDF machinery that
carries no cryptography.

## Testing Decisions

**What makes a good test here.** Sign a document with a signer configured to timestamp, then verify
the resulting bytes through the public verifier and assert what it reports. Both ends are public API
and neither knows how the token was fetched or where the attribute was placed. A test that asserts on
attribute identifiers or on the byte offsets of the store is asserting on an encoding, and encodings
are exactly what a library is for hiding.

**A test never reaches the network.** The timestamp seam exists partly so that a test can supply a
token from a locally issued authority certificate, and the revocation seam so that a test can supply
responses it minted itself. This is the practical reason the seams are named rather than implicit.

**Modules tested.** `PdfPinata.Test` for signing, verification and the permission refusals, which is
where the shipped signature tests already live and where the certificate helper already is. The
permission work also touches the modification guard, whose matrix of modes against operations already
exists; certification becomes a second dimension of the same matrix rather than a new test file with a
new shape.

**Prior art.** The existing signing tests for the sign-verify-assert round trip. The existing
certificate helper for producing certificates in a test without touching a machine store. The open-mode
enforcement tests for the shape of a refusal matrix — modes against operations, each cell asserting
either success or a refusal that names the reason.

**Cases that must exist.**

- A signature with a timestamp verifies, and the verifier reports the timestamp.
- A signature without one verifies exactly as it does today, and reports no timestamp.
- A timestamp source that fails causes the signing to fail, and the document is not written.
- Validation data added to a freshly signed document leaves the signature intact and still covering
  the whole document.
- Validation data added to a document signed by another producer likewise.
- A document with validation data reopened and verified reports that the data is present.
- Each certification level: the permitted kind of change succeeds, and the kinds above it are refused.
- A refusal by certification names certification; a refusal by open mode names the mode; a document
  that fails both is refused once, with a message that says which.
- A full save of a certified document is refused; an incremental save of a permitted change succeeds.
- An unsigned document is unaffected across the whole matrix.

**What is not tested here.** Whether a certificate is trusted, whether a chain builds, and whether a
certificate was revoked. The verifier deliberately does not answer those and this spec does not change
that; a test asserting a trust decision would be asserting something the library does not claim.

## Out of Scope

- **Trust stores, chain building and revocation checking as decisions.** The library gathers evidence
  and reports structure. Deciding whether to trust it is the caller's, as the shipped spec says.
- **PAdES B-LTA.** Refreshing validation with document timestamps over time is an operational
  workflow rather than a document feature, and it wants a scheduler more than it wants a library. The
  security store written here does not preclude it.
- **Signature appearances beyond what already exists.** The appearance callback shipped and is enough.
- **Removing or replacing a signature.** A signed document is appended to, never edited.
- **A corpus document for a timestamped signature.** The corpus validates archival and accessibility
  claims; veraPDF is not a PAdES validator, so a signed corpus document would prove less than it
  appears to. If a PAdES validator is ever added to the build, this is the first thing to put through
  it.

## Further Notes

The three items in this spec look like three features and are really one theme: **a signature is a
statement about a moment, and a document outlives the moment.** A timestamp says when, validation data
says what was true then, and the permission level says what may happen afterwards. Building any one of
them without the others leaves a signature that is strong in one direction and weak in the others.

The permission half is the one worth doing even if the other two are deferred. It is small, it closes
a gap where the library ignores an instruction it wrote down itself, and it lands in a guard that has
just been made singular — which is exactly the moment to add the second reason for the same refusal,
before anything else grows a competing one.
