# Spec — line breaking by UAX #14

Where a line may be broken between two runs of formatting, decided by the Unicode Line Breaking
Algorithm rather than by hand-written rules. Issue **#284**, which followed #283 (the port of
empira/PDFsharp#409).

**The decision at a seam is now UAX #14's.** Where one run of formatting meets another,
`ParagraphRenderer.IsJoinedTo` asks `LineBreakAlgorithm.IsBreakAllowedBetween` whether a line may
be broken where the two leaves meet, and joins them when it may not. The algorithm is in the core
under `PdfPinata.Text`, is UAX #14 for Unicode 17.0.0, and passes `LineBreakTest.txt` in full.

| item | what | status |
|---|---|---|
| 1 | `LineBreakClass.g.cs` and `LineBreakFlags.g.cs`, generated from the UCD | **built** |
| 2 | `UnicodeProperties.LineBreakClassOf` | **built** |
| 3 | `LineBreakAlgorithm`, UAX #14 rules LB1 to LB31 | **built** |
| 4 | `LineBreakTest.txt` as one test: 19,338 of 19,338 | **built** |
| 5 | `IsJoinedTo` asks the algorithm | **built** |
| 6 | Breaking inside a run by UAX #14 | **not done — the follow-up** |
| 7 | Hanging punctuation | **deliberately not done** |

---

## The defect

#280 joined two leaves at a seam unless the first ended in a hyphen, a zero-width space or a dash,
and left a seam beside a CJK or South East Asian letter breakable because those scripts are written
without spaces. #283 then kept a CJK closing mark off the start of a line and an opening mark off
the end of one, with two helpers: `EndsInOpeningMark` read General_Category Ps and Pi, and
`BeginsWithClosingMark` read Pe and Pf and a string of ten stops. Three things were still wrong.

- **The list was incomplete.** UAX #14 forbids a break before far more than closing brackets and
  stops: the iteration marks `々` and `ゝ`, the prolonged sound mark `ー`, the small kana `ぁ` and
  `ッ`, and the middle dot `・` (classes NS and CJ) could all still start a line.
- **The rules were not UAX #14's.** A hyphen or a dash before a closing mark was a break, where
  LB13 forbids a break before a closing mark whatever precedes it.
- **The data was not versioned.** `char.GetUnicodeCategory` answers from the runtime, so the result
  depended on whether netstandard2.1 under Unity, net8.0 or net10.0 ran it. Every other property the
  text code reads comes from a table generated for Unicode 17.0.0.

## Items 1 and 2 — the tables · **built**

`tools/UnicodeTableGenerator` now also reads `LineBreak.txt` and writes `LineBreakClass.g.cs`: the
`LineBreakClass` enum, its members named by the short aliases UAX #14 writes its rules in (`AL`,
`NS`, `B2`), and a complete partition of U+0000..U+10FFFF in 3,616 runs. Unassigned code points are
not all `XX`: those among the CJK ideographs, in planes 2 and 3 and in two ranges of plane 1 are
`ID`, and those in the currency symbols block are `PR`. **17.0.0 states those defaults differently
from `DerivedBidiClass.txt`** — as explicit ranges of reserved code points, beside a single
`@missing: 0000..10FFFF; XX` line — and the generator materialises both forms, so a version that
moves them back into `@missing` lines produces the same table.

Six rules read a property besides the class, and that data is generated too, into
`LineBreakFlags.g.cs`, rather than asked of the runtime:

| property | rules | flag |
|---|---|---|
| East_Asian_Width F, W or H | LB19a, LB30 | `EastAsian` |
| General_Category Pi, of a QU | LB15a, LB19 | `InitialQuote` |
| General_Category Pf, of a QU | LB15b, LB19 | `FinalQuote` |
| General_Category Mn or Mc, of an SA | LB1 | `SouthEastAsianMark` |
| Extended_Pictographic and General_Category Cn | LB30b | `UnassignedPictographic` |

Each flag is recorded only where a rule reads it, which keeps the partition to under four hundred
runs rather than a General_Category partition of several thousand. The enum is internal and
generated beside the table, so the bit each flag stands for is written in one place.

`UnicodeProperties.LineBreakClassOf(int)` is the public lookup, named as `BidiClassOf` and
`ScriptOf` are. It answers the class as the database gives it, before LB1.

## Item 3 — the algorithm · **built**

`LineBreakAlgorithm` is public and has four entry points:

- `BreakOpportunities(ReadOnlySpan<char>)` — one `LineBreakOpportunity` per UTF-16 position, `None`, `Allowed`
  or `Mandatory`;
- `BreakBefore(ReadOnlySpan<char>, int)` and `IsBreakAllowed` — one position;
- `IsBreakAllowedBetween(ReadOnlySpan<char> before, ReadOnlySpan<char> after)` — the position where
  two pieces of text meet, read as one text without copying them into one.

The last is what a layout engine holding a paragraph in runs of formatting needs, and it is cheap
enough to ask at every seam: the two pieces are resolved into a buffer on the stack while together
they are 128 code units or fewer, and a buffer is rented beyond that.

How it is built, in the order UAX #14 states it:

- **LB1** resolves AI, SG and XX to AL, SA to CM where it is a mark and AL elsewhere, and CJ to NS.
  CB is left for LB20.
- **LB2 to LB8a** read the classes as they are.
- **LB9** absorbs a combining mark or ZWJ into the character before it, unless that is a hard break,
  a space or ZW, and **LB10** makes any mark left over an AL with the properties of U+0041 — its
  East_Asian_Width and Extended_Pictographic flags cleared, which matters to LB19a and LB30b. Every
  rule after that reads through an absorbed mark to its base.
- **LB25** is the set of regular expressions it has been since Unicode 15.1, which is the form
  `LineBreakTest.txt` tests.
- **LB28a** names U+25CC DOTTED CIRCLE by code point; **LB30a** reads how long the row of regional
  indicators before the position is, which is counted once, as the text is resolved, rather than
  again at every position in the row.
- **LB12a** in 17.0.0 is `[^SP BA HY HH] × GL`. Unicode 18 drops BA from it; this is 17.

**HH is new in 17.0.0, and it is the en dash's class.** U+2010 HYPHEN, U+2012 FIGURE DASH and
U+2013 EN DASH are HH, "unambiguous hyphen", where the en dash was BA. It behaves as HY in LB12a,
LB20a, LB21 and LB21a. The seam rule depends on it: a word-initial en dash before a letter does not
break (LB20a).

## Item 4 — conformance · **built**

`LineBreakTest.txt` 17.0.0 is checked in gzipped beside the bidi suites (217 KB against 3.2 MB) and
run as one `[Test]`, `LineBreakConformanceTests.TheLineBreakTestSuitePasses`, for the reason
`BidiConformanceTests` gives. **All 19,338 cases pass, with no tailoring, and none is skipped.**
A failure is reported with the position, what was expected, and the rule the suite's own comment
names for it, so "should be × by LB28 ([28.0]), and is ÷" says where to look.

**Every entry point is checked, not just one.** `BreakOpportunities` resolves the whole text in
one pass; `BreakBefore` and `IsBreakAllowedBetween`, which is what the seam calls, resolve two
pieces one after the other and work in a buffer on the stack up to 128 code units. So every
position of every case is asked through all three, and through the last two again after a
140-letter word and a hard line break, which takes them past the stack buffer. `BreakBefore` is held
to `BreakOpportunities`' exact answer, mandatory or allowed. It is still one `[Test]`, and still
takes under two seconds.

## Item 5 — the seam · **built**

`IsJoinedTo(left, right)` now takes the two leaves' iterators and:

1. returns false unless both are joinable, as before — a blank, a tab, a line break, a soft hyphen
   and an image still separate what is either side of them;
2. returns false for a boundary inside one run of text split after a hyphen-minus (see the
   tailorings below);
3. reads up to 32 code units either side of the seam, and for a leaf of one character, up to 32
   of the next leaf out as well, walking past any leaf the seam reads through;
4. asks `IsBreakAllowedBetween` with the `SouthEastAsianAsIdeographs` and `HangulAsAlphabetic`
   tailorings, and joins when the answer is no.

`EndsInOpeningMark`, `BeginsWithClosingMark`, `ClosingStops`, `BeginsWithNoBreak`,
`EndsInBreakOpportunity` and `IsWrittenWithoutSpaces` are gone.

**A leaf that is not text is read as the text it stands for.** A symbol is its character, the same
one `GetSymbol` draws - both ask `SymbolCodePoint` - written into a buffer on the stack rather than
spelt as a string: an em dash is B2, an en dash HH, a non-breakable blank GL, a trademark sign AL.
A symbol drawn more than once is written twice, which is as far back as any rule reads through a
row of one character. An image is U+FFFC, which is CB. A field is read as a letter, and joins as a
word does, which is what it did before; its value is not known until it is evaluated, and a letter
is the class most values have. A line break symbol is "\n", because it really does end the line.

**A footnote's reference mark, a bookmark, an empty text and a symbol that draws nothing are read
through** (`IsReadThrough`). After the seam such a leaf is U+034F COMBINING GRAPHEME JOINER, a CM,
which LB9 never breaks before whatever follows it. Before the seam it is the joiner after the text of
the nearest leaf before it that is not read through - that leaf's window, and the window of the one
before that if it is a single character - so LB9 absorbs the joiner and the seam is read as though it followed that
text. However many stand together, the character before them is what is read: a footnote mark after
a bookmark after "!" is decided as "!" before the next word, which may be broken, where reading each
leaf as a joiner made the two of them a letter by LB10 and joined. So a reference mark set after "!",
"?" or an ideographic full stop is not left to start a line, and a bookmark between an ideograph and
an iteration mark leaves the two joined. Read as a letter, both did the wrong thing: a break is
allowed between "!" and a letter, and between an ideograph and a letter. The one thing it moves is
where a bookmark sits when a line is broken just before it - after a hyphen, say - which is now at
the end of the line rather than the start of the next.

**A paragraph break inside a run of formatting is one of them.** `PdfFlattenVisitor` splits a
paragraph at a paragraph break only among its own elements, so one inside a run - an empty line in
the run, in MDDDL - reaches the renderer, which draws it as U+0000, a character text normalisation
drops, and never breaks the line there. Read as "\n" it made the seam after it a mandatory break
(LB5); before this work both of its seams joined, and read through they still do, unless what is
either side of it is a break.

An empty text cannot reach a seam through the public API - `AddText("")` adds nothing and
`PdfFlattenVisitor` removes an empty text - but is read through all the same.

**Why the leaf beyond a one-character leaf.** A seam between a lone en dash symbol and the
word after it is a break in "bbbb–cccc" and is not in "–cccc" at the start of a word (LB20a). Read
on its own, the dash is at the start of the text, which LB20a counts as the start of a word, and the
seam would join. One character beyond is not always enough, so the whole window of the neighbour is
read: a flag whose halves are in two runs after a run holding the first flag is told from the
start of a flag only by counting the indicators before it (LB30a), and "$" before a "(" in a run of
its own is kept from the line after it only when a number follows, perhaps after a stop (LB25,
PR × OP IS NU). Leaves longer than one character carry their own context and do not pay for the
walk of the tree that fetching a neighbour costs.

**Why 32 code units.** Every rule that reaches further than a character or two does so over a
number, a row of spaces or a row of regional indicators, and a leaf has no spaces in it. The bound
is there so that a long run of CJK text, which is one leaf, is not resolved whole at every seam.
What is outside the window counts as the start or the end of the text, which for a leaf longer
than the window could change the answer of the one rule that counts: LB30a, which pairs regional
indicators into flags from the start of their row. After 17 indicators the window held 16, and a
break was allowed between the two halves of the 9th flag. So where the window begins on a regional
indicator, `Tail` walks back over the row it cut into and, when it left out an odd number, leaves
out one more; the window keeps its bound and the row its pairing. **The limit that remains** is a
row of indicators that runs through more runs of formatting than the seam reads - the leaf before
it, and the one before that when the leaf is a single indicator: it is counted from the start of
what was read.

**A seam is decided once per pass.** The same seam is asked about again and again - as the one after
a leaf, straight away as the one before the next, and for every leaf of a joined run each time the
run is measured - so the renderer keeps every answer in a dictionary keyed by the leaf before the
seam, the leaf after it being always the one that follows. A renderer is made for one paragraph and
one pass, so no answer outlives its document. Only seams between two joinable leaves are kept, and
a paragraph that has none never makes the dictionary.

### The tailorings

**SA letters are ideographs.** LB1 resolves an SA letter — Thai, Lao, Khmer, Myanmar — to AL when
there is no dictionary to find the words with, and LB28 then joins every seam in a Thai paragraph.
A seam beside such a letter used to be breakable, and still is: the seam asks with
`LineBreakTailoring.SouthEastAsianAsIdeographs`, which resolves the letter to ID. A mark of the class
still resolves to CM and stays with its letter, and the ID still refuses a break before a closing
mark (LB13). The tailoring is public and off by default; the conformance suite runs without it.

**Hangul syllables are letters.** The default rules treat a Hangul syllable as an ideograph and allow
a break after any of them (LB31), so a Korean word whose syllables were in two runs could be broken
between them, though inside a run Korean is broken only at its spaces and the seam used to join.
The seam asks with `LineBreakTailoring.HangulAsAlphabetic` as well, which resolves JL, JV, JT, H2
and H3 to AL: the tailoring UAX #14's section 8 describes for Korean set with breaks at spaces.
Hanja, which are ID, are not changed.

**A hyphen-minus inside one run still breaks.** `PdfFlattenVisitor` splits a run of text into
leaves at blanks, after a hyphen-minus or a zero-width space, and at a soft hyphen, and the leaves
it makes are where a line may be broken inside a run. The boundary after a hyphen-minus is the only
one of those UAX #14 sometimes disagrees with: it does not break "44-1" (LB25, `HY × NU`) or a
word-initial "-x" (LB20a). Asking the algorithm there would change breaking *inside* a run, which is
the follow-up below, and it moved a pinned test straight away:
`NoBreakSpaceAcrossRunsTests.ACitationOfThreeAddressesKeepsEachLabelWithItsAddressInFiveLines` sets a
citation in five lines because "44-1" and "lovrics-2" break, and in six when they do not. So two
texts of the same run, the first ending in "-", are not joined, as before.

### What changed at a seam

Beyond what #283 did, a seam between two runs of formatting now:

- **joins** before a nonstarter or small kana — `々 ゝ ー ぁ ッ ・` and the rest of NS and CJ —
  whatever precedes it (LB21);
- **joins** a hyphen or a dash to a closing mark, `!`, `?`, `/`, `,` or `.` after it (LB13, LB15d);
- **joins** a hyphen-minus to a digit after it (LB25) and a word-initial hyphen or en dash to the
  letter after it (LB20a), across runs only;
- **joins** an ideograph to a combining mark in the next run (LB9), and to `%` or after `$`
  (LB23a);
- **may break** after `/` (SY), `!` or `?` (EX), `}` or a CJK closing mark (CL) or an ellipsis (IN)
  before a letter, where it used to join (LB31);
- **may break** before an em dash (B2 allows a break either side), and between two whole flags
  (LB30a), where it used to join;
- **keeps** a footnote's reference mark with whatever it follows, and reads it, a bookmark and a
  paragraph break inside a run through, however many stand together.

No golden image moved.

## Item 6 — breaking inside a run · **not done, the follow-up**

Inside one run of formatting a line is broken only where `PdfFlattenVisitor` split the text: at
blanks, after a hyphen-minus or a zero-width space, and at a soft hyphen. A run of Chinese or
Japanese with no spaces in it is one leaf and is set as one word. Moving that to UAX #14 would
involve:

- **splitting at every break opportunity rather than at four characters**, either in
  `PdfFlattenVisitor.SplitIntoWords` (the DOM already references the core) or in the renderer at
  layout time, so that a leaf is the text between two opportunities;
- **deciding what a leaf is to everything that reads one as a word**: justification counts blanks
  between leaves, the soft-hyphen logic and `BrokenWord` walk back to the front of a word, and the
  tagger puts one marked-content sequence per leaf;
- **accepting that existing documents break elsewhere**: "44-1" and "-x" stop breaking, a URL may
  break after "/", and a CJK paragraph breaks between ideographs for the first time, which moves
  golden images and pins like the five-line citation;
- **dropping the hyphen-minus exception in `IsJoinedTo`**, which then has nothing left to protect.

That is a change of its own, and the issue left it to be decided during the work; it was decided
against here.

## Item 7 — hanging punctuation · **deliberately not done**

empira/PDFsharp#409 lets a closing mark hang past the right margin rather than taking the ideograph
before it down to the next line. #280 deliberately sets a joined run that is too long for any line
from the start of a line and breaks it at its seams, and hanging the mark instead is a separate
decision. Nor is the mandatory break (`LineBreakOpportunity.Mandatory`) used by the layout engine,
which has its own line-break symbol.

## Bumping the Unicode version

The line-break table and `LineBreakTest.txt.gz` move with the bidi tables and suites; see
`tools/UnicodeTableGenerator/README.md`. `UnicodePropertyTests.TheTablesSayWhichUnicodeTheyCameFrom`
pins the version for all of them. Read the new UAX #14's rules against `LineBreakAlgorithm` before
running the suite: LB12a changes again in Unicode 18.
