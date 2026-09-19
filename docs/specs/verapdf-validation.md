# veraPDF validation in CI

The outside opinion on every conformance claim this library makes. Item 7 of
`docs/specs/tagged-pdf-accessibility.md`, and the step `docs/specs/pdf-a-conformance.md` has been
asking for since it was written — both specs named it, both said it should be decided once for the
two of them, and this is that decision.

| item | what | status |
|---|---|---|
| 1 | A corpus of documents that each claim a profile | done |
| 2 | veraPDF over the corpus, in CI and locally, one script | done |
| 3 | Gate the build on the verdict | done — **all six documents conform** |

## Why this was worth doing

Before it, every claim was self-certified. The writer refuses to save a document that breaks a rule
of the profile it claims, and `PdfUaValidator` lists in its own remarks which rules it holds a
document to and which it cannot reach — the largest being that no content sits outside the structure
tree, which needs a content-stream pass it does not make.

It found three real defects on the first run, none of which any test here could have caught. That is
the argument for it, made in one run.

## What it is

**A corpus, built by a program.** `ConformanceCorpus` writes one PDF per claim the library can make:
PDF/A-1b, PDF/A-2b, PDF/A-3b, PDF/A-2b set in a face with PostScript outlines, PDF/A-3b carrying
an associated file, and a tagged PDF/UA-1 document.
A program rather than a test, because what it produces is an input to something else — a test asserts
and returns nothing, and these have to exist as files for a validator to open.

Every document **makes a claim**, and that is the entry requirement. veraPDF is run with automatic
flavour detection, so each file is held to the profile it names in its own metadata. A file claiming
nothing would be held to the fallback flavour instead and fail for saying nothing rather than for
being wrong.

They are small on purpose. A corpus is read by whoever is looking at a failure, and a hundred-page
document says no more about a rule than a paragraph does while making the report far harder to place.

**One script, two places.** `verapdf-check.ps1` builds the corpus, runs the validator over it, prints
a summary and writes a report per document into `artifacts/verapdf-reports`. CI runs exactly that
script, so there is one behaviour to understand rather than two, and a developer reproducing a CI
failure runs the thing that failed.

**In Docker, pinned.** veraPDF is a Java application, and requiring a JRE on every machine that wants
to run this — and on Windows, where a developer is least likely to have one — costs more than a
container does. The image is pinned to `verapdf/cli:v1.30.2` so a validator release cannot change
the answer underneath a build; raise it deliberately. A machine with no Docker is told what to
install and nothing fails, because a developer who has not installed Docker has not broken anything.

## The ICC profile, which is its own small story

PDF/A needs an output intent, an output intent needs an ICC profile, and for most of this feature's
life **no profile shipped with the library** — the writer embedded the bytes it was given and said so
when given none. So the corpus had to get one from somewhere, and where it got it changed twice,
which is worth recording because each answer was right when it was made.

`SrgbProfile` **built** one for its first several months: an ICC v2.1 matrix-shaper display profile
written out byte by byte, with sRGB's primaries adapted to D50 and a single gamma of 2.2 standing in
for sRGB's piecewise transfer curve — a deliberate approximation, since the curve matters to colour
management and that profile managed no colour. 250 lines. It existed because the three obvious
alternatives were worse:

- A checked-in `.icc` is a binary blob in the repository with a licence of its own to account for.
- Downloading one during the build makes validation depend on a third party being up.
- Skia, already a dependency, parses profiles but does not write them. `SKColorSpace.ToProfile()`
  hands back a structure whose buffer is the bytes it was parsed from, so for a colour space that was
  never parsed from anything there are no bytes at all. This was tried first and is why it is written
  down: the failure is silent-looking, an empty buffer rather than an exception.

**Only the first of those was ever about licences rather than about tools, and it now has an
answer.** `src/assets/icc/sRGB-v2-micro.icc` is 456 bytes released to the public domain under CC0 — no
attribution owed, the true sRGB primaries, and the real transfer curve sampled at 42 points instead
of approximated. The other two bullets are untouched by the change and remain the reasons not to
fetch or generate one.

`SrgbProfile` then disappeared entirely rather than shrinking, because the profile went one step
further than the corpus: **the core package embeds it**, and an RGB document claiming PDF/A that
names no profile is given it. So the corpus sets nothing, and what is validated here is the default
path — the file a caller gets for writing `Options.Conformance = …` and nothing else.
`docs/specs/pdf-a-conformance.md` has the rule and the two colour modes it deliberately excludes.

Two things were settled along the way, each the answer to an objection worth raising:

- **`*.icc` is `binary` in `.gitattributes`.** A profile that has been through line-ending
  normalisation still embeds and still looks like a file; what it produces is every document here
  failing on its output intent at once, which reads as a regression in the writer.
  `PdfOutputIntents` additionally checks the `acsp` signature every profile carries at byte 36 and
  says so by name.
- **A binary cannot be read in a diff, and that is the real cost.** The code could be checked against
  the sRGB specification by reading it. The mitigation is the hash in `src/assets/icc/LICENSE.txt` and
  the fact that veraPDF parses the profile on every run, which is a stronger check than review was.

It has to be a *real* profile, which is the trap worth flagging and the reason none of this was
skipped. The unit tests covering the XMP writer pass the ASCII bytes `NOT-AN-ICC-PROFILE`, and they
are right to — nothing in this library parses a profile, so a legible stand-in makes those assertions
clearer and says plainly that they are not colour-management tests. A validator does parse it, and
reads the colour space out of the header to check the output intent agrees.

## What it found, and what was done about it

Three defects on the first run, each hitting several profiles, none of them reachable by any test in
this repository. **All three were in the writer, not in the corpus, and all three are now fixed** —
the corpus conforms and the step gates.

| clause | what | documents | fix |
|---|---|---|---|
| PDF/A-1 6.3.3.2-1, PDF/A-2/3 6.2.11.3.2-1, PDF/UA 7.21.3.2-1 | A Type 2 CIDFont has no `/CIDToGIDMap`. ISO 32000-1 Table 117 makes the entry optional with a default of `Identity`; PDF/A and PDF/UA require it written out | **all five** | `PdfCIDFont.PrepareForSave` writes `/Identity` for a Type 2 CIDFont and removes the entry for a Type 0, where it is meaningless |
| PDF/A-1 6.1.7-1, PDF/A-2/3 6.1.7.1-1 | A stream's `/Length` disagrees with the bytes between `stream` and `endstream` | all four PDF/A | `PdfWriter` now writes the end-of-line marker before `endstream` unconditionally |
| PDF/A-1 6.3.5-3 | A subset CIDFont has no `/CIDSet` in its font descriptor | `pdfa-1b` | `PdfCIDFont` writes one when the document claims PDF/A-1 |

### The `/Length` one is the interesting defect

The writer wrote its end-of-line marker before `endstream` **only when the stream data did not
already end with a newline**. ISO 32000-1 7.3.8.1 counts `/Length` over the data alone and puts that
marker after it, outside the count — so when the data ended with a newline, the data's own last byte
was left serving as the separator and the file declared one byte more than it appeared to hold.

Nothing here noticed because this library's own reader takes `/Length` at its word and gets the right
bytes either way, which is exactly the shape of defect an outside opinion is for. It went unseen for
as long as it did because most streams are compressed and end on whatever byte the compressor stopped
at; the XMP packet, uncompressed by design and ending with a newline by construction, is what
exposed it.

`StreamLengthTests` pins it by reading the raw bytes rather than going through `PdfReader` —
the reader would agree with the file whatever the file said. `CidFontConformanceTests` pins the other
two, including that `/CIDSet` is written for PDF/A-1 and for nothing else.

### A finding the validator did not make

The corpus later gained a document set in a face with **PostScript outlines**, because CFF takes a
different path through the writer from everything else here: the font is embedded whole, since it
cannot be subsetted, the descendant is a Type 0 CIDFont rather than a Type 2, and so it must carry
*no* `/CIDToGIDMap` where the others must carry one. That last branch had nothing exercising it.

It passed on the first run, and one thing it showed up was still wrong. `PdfType0Font` gave **every**
CID font the six-letter `ABCDEF+` tag, including one embedded entire. ISO 32000-1 9.6.4 makes that
tag a statement of fact — it marks a program holding only some of the face's glyphs, and exists so
two subsets of one face, same name and different glyphs, cannot be mistaken for each other. A font
embedded whole has nothing to be distinguished from, and the tag claims something untrue of it. No
rule in any profile checks this, so the validator never objected; a tool merging two documents is
nonetheless entitled to believe two differently-tagged programs differ, and to keep both. The tag is
now written only for a font that really was cut down, and
`PostscriptOutlineEmbeddingTest` pins both halves.

Adding that document also settled a question rather than raising one. Clause 6.3.5, the `/CIDSet`
rule, is **unreachable for CFF from this library**: embedding those outlines writes a `/FontFile3`
with `/Subtype /OpenType`, which arrives in PDF 1.6, and the writer refuses a PDF/A-1 claim on a
document past PDF 1.4. So the CFF document claims PDF/A-2b, not by preference but because the other
combination cannot be built.

Two results are worth reading the other way round, as the things that came back clean:

**The PDF/UA-1 document passes 105 of 106 rules**, and the one failure is the `/CIDToGIDMap` entry
above — a font dictionary, shared with every other document here. Nothing about the structure tree
failed. That answers a question left open when the footnote tagging was written: a `/Lbl` and body
paragraphs nested inside an inline-level `/Note` is the shape MigraDoc produces, the code review
flagged it against ISO 32000-1's table of standard structure types, and **veraPDF does not object to
it**. It is not proof the nesting is ideal, but it is the outside opinion that was wanted, and it
says the tagging is structurally sound.

**The associated-file machinery passes every PDF/A-3 rule about it.** `pdfa-3b-facturx` fails exactly
what `pdfa-3b` fails and nothing more, so `/AFRelationship`, the catalog `/AF` array and the
embedded-files name tree are all right — the part of PDF/A-3 that is easy to get two-thirds correct
and produce a file that opens perfectly and conforms to nothing.

veraPDF also warns `Nested MCID` four times on the tagged document. It is a warning rather than a
failed check and no rule turns on it; it is written down here because it has not been looked into.

## It gates

It reported without failing for exactly as long as the corpus did not conform, which was the three
defects above. They are fixed, so it gates: a non-conforming document fails the run, in CI and
locally alike, and `-NoGate` is there for reading a failure rather than being stopped by it. The CI
step carries no `continue-on-error`, because a step that cannot fail is a step that is not checking
anything.

**A validator that will not start fails the build too**, but only on a runner — the script checks
`CI`. Locally, a developer who has not installed Docker has not broken anything and is told what to
install. On a runner the opposite holds: a validation step that quietly validates nothing is how a
claim goes back to being self-certified without anybody deciding that it should.

The alternative considered and rejected was baselining the failures into an allow-list and gating on
anything new. It would have caught regressions a day sooner at the cost of a second mechanism to keep
honest — and with three defects, all in one place, the list would have been obsolete about as soon as
it was written. Fixing them was cheaper than describing them.

## Running it

```powershell
./verapdf-check.ps1                 # build the corpus, validate, summarise, fail if it does not conform
./verapdf-check.ps1 -NoGate         # the same, but always succeed - for reading a failure
./verapdf-check.ps1 -SkipBuild      # validate what is already on disk, for iterating on a failure
./verapdf-check.ps1 -Image verapdf/cli:latest   # try a different validator release
```

Windows, Linux and macOS alike: it is PowerShell, which the GitHub runners carry on every platform,
and the validator is a container. The only requirement is Docker.

Reports land in `artifacts/verapdf-reports`, one XML per document, and CI uploads them as a build
artifact so a failure can be read without reproducing it.

## Deliberately not done

- **A policy file.** veraPDF takes `--policyfile` for a schematron narrowing what counts as a
  failure. That is the machinery an allow-list would be built on, and it is not wanted yet.
- **PDF/A-1a, 2a, 3a and PDF/UA-2.** The `A` levels need a full tagged tree alongside the archival
  claim, which is a combination nothing in the library produces yet — `PdfAConformance` offers only
  the `B` levels. Adding the documents is cheap once it does.
- **Validating what the demos produce.** `SampleApp` writes a PDF per demo and none of them claims a
  profile, so a validator would hold them all to the fallback flavour and report noise.

## Related

- `docs/specs/pdf-a-conformance.md` — the claims being validated, and what enforcement does and does
  not check before the bytes are written.
- `docs/specs/tagged-pdf-accessibility.md` — item 7, and `PdfUaValidator`'s own account of which
  rules it cannot reach.
