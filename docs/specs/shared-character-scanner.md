# Spec — one scanner, three grammars

What sharing the character-level scanner between the lexers covers, and what it deliberately leaves
out.

| item | what | status |
|---|---|---|
| 1 | One character scanner behind `Lexer` and `CLexer` | proposed |
| 2 | Seven guards the document lexer has and the content lexer lacks, closed | proposed |
| 3 | One `Chars` table in place of two | proposed |
| 4 | The dead `CSymbol` branches resolved — implemented or removed | proposed |
| 5 | `DdlScanner` folded in as well | **out of scope**, see below |

## Problem Statement

CLAUDE.md already states the rule: *"There are two independent lexers, and a change to one usually
belongs in the other."* It is a rule a reader has to remember, and the git record says they do not.

`Pdf.IO/Lexer.cs` is 920 lines and `Pdf.Content/CLexer.cs` is 966. Eighteen members exist in both
under the same name and the same role — `ScanNextToken`, `ScanComment`, `ScanName`, `ScanNumber`,
`ScanLiteralString`, `ScanHexadecimalString`, `ScanNextChar`, `AppendAndScanNextChar`,
`MoveToNonWhiteSpace`, `ClearToken`, `Token`, `TokenToInteger`, `TokenToReal`, `Position`,
`IsWhiteSpace`, `IsDelimiter`, `IsHexChar` and the symbol enum itself. Measured by method extent that
is roughly 535 of 920 lines and 619 of 966. Only thirteen members are unique to the document lexer
and six to the content lexer. `Pdf.Content/Chars.cs` is a 79-line stripped copy of the 188-line
`Pdf.IO/Chars.cs`, and says so in its own doc comment: *"Same as PdfPinata.Pdf.IO.Chars. Not yet
clear if necessary."*

Of 33 commits touching either file, **29 touched only one of them**. The result is a consistent
drift in one direction — the document lexer is the careful one and the content lexer is the rough
one:

| guard | `Lexer.cs` | `CLexer.cs` |
|---|---|---|
| vertical tab and soft hyphen treated as whitespace | present | absent |
| end of input throws while appending | present | absent |
| `{` and `}` are delimiters | present | commented out |
| odd-length UTF-16 hex string padded | present | `Debug.Assert` only |
| UTF-16LE literal strings decoded | present | absent |
| `\` before a carriage return continues a line | present | line feed only |
| integer overflow degrades to a real | present | throws |

The `Debug.Assert` is the sharpest of these: the guard exists in a Debug build and vanishes in
Release, which is the shape of defect `4ab1918` — *"Compile the same code in a Debug build and a
Release build"* — already went looking for once.

The copy is not even complete. `CSymbol.UnicodeString` and `CSymbol.UnicodeHexString` exist and
`CParser` has cases for both, but `CLexer` never returns either: dead branches mirrored from the
other module's interface, describing a capability the module does not have.

## Solution

Extract the character-level scanner both lexers are built on and leave a grammar above it.

What the two share is not "some helper methods" — it is the entire mechanism of reading: the
current-and-next character pair, the carriage-return-then-line-feed fold, appending to a token
buffer, the character-class predicates, and the position bookkeeping. What differs is the token
grammar: PDF body objects and cross-reference syntax on one side, content-stream operators and inline
images on the other.

One deep module, two thin grammars. A guard added to the scanner is a guard both lexers have.

## User Stories

1. As a developer reading a malformed PDF, I want the content-stream lexer to survive the same
   inputs the document lexer survives, so that a file that opens does not fail when its content is
   read.
2. As a developer, I want a vertical tab or a soft hyphen in a content stream treated as whitespace,
   so that a stream a document lexer would read is read.
3. As a developer, I want an unterminated string in a content stream to be refused rather than run
   off the end of the buffer.
4. As a developer, I want an odd-length UTF-16 hex string in a content stream handled in a Release
   build the way it is handled in a Debug build.
5. As a developer, I want an integer too large for its type to degrade the same way in both
   scanners, so that a value's meaning does not depend on where it was read.
6. As a developer, I want `{` and `}` treated as delimiters in a content stream, since they are.
7. As a maintainer, I want a guard added once to protect both lexers, so that the rule in CLAUDE.md
   stops being something to remember.
8. As a maintainer, I want the scanner testable on its own, so that a scanning defect has a test
   that does not need a whole PDF built around it.
9. As a maintainer, I want one `Chars` table, so that a character class cannot mean two things.
10. As a maintainer, I want the dead `CSymbol` branches either implemented or deleted, so that the
    enum describes what the module does.
11. As a maintainer, I want the existing lexer tests to pass untouched, so that the extraction is
    provably behaviour-preserving where behaviour is not deliberately being changed.
12. As a maintainer, I want each closed guard to arrive with a test that fails before it, so that
    seven behaviour changes are seven visible decisions.
13. As a consumer of the library, I want no public type to change, so that this costs me nothing.
14. As a consumer, I want a document that reads correctly today to read identically afterwards.

## Implementation Decisions

**Two lexers, not three.** `DdlScanner` runs the same one-character-lookahead algorithm with its own
`Chars` and its own keyword table, but it lives in `PinataLayout.DocumentObjectModel`, which has no
dependency on `PdfPinata` and should not acquire one for this. Folding it in is a bigger question
about assembly structure and is deliberately excluded.

**The scanner is `internal`.** Neither lexer is public API in the sense that matters here, and the
scanner is an internal seam — private to the implementation, used by its own tests. It should not
appear on any public surface.

**The direction of the merge is settled: the document lexer wins.** Where the two differ, the
document lexer's behaviour is the correct one in every one of the seven cases. This is not a
coincidence — it is the one that has had the bugs reported against it — and stating the direction up
front stops each case being re-argued.

**Each closed guard is a separate commit with its own test.** Seven behaviour changes to the content
lexer is seven ways to change what a content stream parses to. They are individually small and
individually explicable, and bundling them into the extraction commit would make the extraction
unreviewable.

**The `Debug.Assert` case is a bug fix, not a merge.** A guard that exists only in a Debug build is
not a guard. It should be closed first, independently, whether or not the rest of this happens.

**The dead `CSymbol` branches are a decision, not a cleanup.** Either the content lexer should return
`UnicodeString` and `UnicodeHexString` — in which case the document lexer's decoding moves into the
shared scanner and both get it — or it should not, in which case the enum members and the `CParser`
cases go. Extracting the scanner makes the first option nearly free, which is an argument for it, but
it changes what a content stream parses to and must be decided rather than fallen into.

**`ClearToken` is live in one and commented out in the other.** It goes in the scanner, live.

**Position bookkeeping is the risk.** The two lexers read from different sources — a `Stream` and a
`byte[]` — and the offsets they report are used differently. The scanner must abstract the source
without changing what either reports.

**Strings and names stay byte strings, one char per byte.** This is the invariant CLAUDE.md is
emphatic about: `(char)stream.ReadByte()` in, `RawEncoding` out, `ch < 256` asserted by the writer,
and nothing in the name path re-decoding to Unicode. The shared scanner must preserve it exactly.
`LexerNameEncodingTests` pins it.

## Testing Decisions

**A good test here feeds bytes in and asserts on tokens out.** That is what a scanner does. Tests
that assemble a whole PDF to reach a scanning question are testing through three modules to ask about
one, and the point of the extraction is that they no longer have to.

**Modules under test.** The shared scanner directly, once it exists. Both lexers through their
existing tests. `CParser` for anything about the dead symbol branches.

**Prior art to follow rather than reinvent.** `src/PdfPinata.Test/Pdfs/Content/CLexerTests.cs` and the
`Lexer*Tests` family — `LexerHexStringTests`, `LexerNameEncodingTests`, `LexerUnicodeStringTests` —
are the model: bytes in, symbol and token out. `src/PdfPinata.Test/IO/RawPdf.cs` builds byte-exact
documents by hand for the cases that genuinely need a document around them.

**Every closed guard gets the content-lexer twin of an existing document-lexer test.** Seven of
these. In most cases the assertion already exists for `Lexer` and needs writing again for `CLexer`,
which is itself an argument for a shared test body once the scanner is shared.

**A lexer change can hang the test host rather than fail it.** CLAUDE.md is explicit: tests that scan
malformed input carry `[Fact(Timeout = …)]`, which xUnit honours only on `async` tests — hence the
`Task.Run` wrappers in `CLexerTests`. Any new malformed-input test follows that pattern.

**`--blame-crash` and the repeat rule.** A run whose total is below what `dotnet test --list-tests`
finds did not pass, it stopped. If a name repeats across runs it is a document to go and look at; a
different name every run is the machine. See `docs/specs/test-host-crash-investigation.md`.

**veraPDF still gates.** `./verapdf-check.ps1` must stay green — the writer's `/Length` behaviour and
the corpus depend on reading and re-writing documents correctly.

## Out of Scope

- **`DdlScanner`.** Same algorithm, third implementation, different assembly with no dependency on
  `PdfPinata`. Folding it in is an assembly-structure question, not a lexer one.
- **The two `Symbols.cs` keyword lists in `DdlScanner`** — 80 `enumToName` entries and 56
  `nameToEnum` entries hand-kept in agreement, currently differing by 24. Real, and part of the
  question above.
- **The parsers.** `Parser` and `CParser` sit above the lexers and are not merged here.
- **Making `Parser` testable without a `PdfDocument`.** Worth doing; a separate change.
- **Rewriting either grammar.** The token sets stay as they are, apart from the `CSymbol` decision.
- **Performance.** Neither lexer is known to be a bottleneck and this should not change throughput
  measurably. If it does, that is a defect in the extraction.

## Further Notes

The 29-of-33 figure is the whole argument. This is not a case where a shared abstraction might one
day pay for itself — the duplication has already cost seven divergences, one of which only exists in
Release builds, and the file that is a copy says in its own comment that nobody was sure it needed to
be one.

The deletion test: delete `Pdf.Content/Chars.cs` and nothing is lost, because the 79 lines are a
subset of the 188 next door. Delete `CLexer`'s scanning half and it reappears — in `Lexer`, where it
already is.

Do the `Debug.Assert` fix first and separately. It is a real defect in shipped Release builds and it
should not wait behind a refactor.
