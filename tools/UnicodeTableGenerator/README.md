# UnicodeTableGenerator

Writes the character property tables `PdfPinata.Text` looks Bidi_Class, Script, Line_Break and
paired brackets up in.

```
dotnet run --project tools/UnicodeTableGenerator -- --version 17.0.0 --out PdfPinata/Text
```

Options, all optional: `--version` (default `17.0.0`), `--out` (default `PdfPinata/Text`),
`--cache` (default a directory under the system temp). Files are downloaded from
`unicode.org/Public/<version>/` on first use and read from the cache afterwards.

It writes six files, all checked in:

| file | what |
|---|---|
| `BidiClass.g.cs` | the `BidiClass` enum and a complete Bidi_Class partition of U+0000..U+10FFFF |
| `UnicodeScript.g.cs` | the `UnicodeScript` enum, the Script partition, and the ISO 15924 codes |
| `LineBreakClass.g.cs` | the `LineBreakClass` enum and a complete Line_Break partition |
| `LineBreakFlags.g.cs` | what UAX #14's rules read besides Line_Break, as one partition of flags |
| `BracketPairs.g.cs` | the canonical paired brackets, for rule N0 |
| `UnicodeVersion.g.cs` | the version the others came from |

It reads `PropertyValueAliases.txt`, `extracted/DerivedBidiClass.txt`, `Scripts.txt`,
`LineBreak.txt`, `EastAsianWidth.txt`, `extracted/DerivedGeneralCategory.txt`,
`emoji/emoji-data.txt` and `BidiBrackets.txt`.

## Why it is not part of the build

Deliberately outside `PdfPinata.slnx`, so `dotnet build` and CI never see it. The alternative —
generating during the build — would put a network fetch on the critical path of every build on
every target framework, and make an offline build impossible. What it produces is small (about
117 KB of source for tables of 1,611, 984, 3,616 and 384 ranges), so checking it in costs nothing
and buys a reviewable diff whenever the Unicode version moves.

## Bumping the Unicode version

Three things move together and a test enforces it:

1. Run the generator with the new `--version`. That regenerates the bidi, script and line-break
   tables together.
2. Replace `PdfPinata.Test/Assets/Unicode/BidiTest.txt.gz`, `BidiCharacterTest.txt.gz` and
   `LineBreakTest.txt.gz` with the same version's, gzipped. The first two are in `ucd/`, the third
   in `ucd/auxiliary/`.
3. Update the version in `UnicodePropertyTests.TheTablesSayWhichUnicodeTheyCameFrom`, which pins
   it for all of them.

Read the new version of UAX #14 against `LineBreakAlgorithm` too. Its rules change between versions
more often than UAX #9's do - 17.0.0 added the class HH, and Unicode 18 changes rule LB12a - and a
new rule is not in the table.

Doing the first without the second tests one Unicode against another's expectations, which is
exactly the sort of failure that looks like an algorithm bug and is not. Expect the conformance
suites to grow — they are generated combinatorially and have roughly doubled over the last decade.

## What it does that is not obvious

**The `@missing` lines are load-bearing.** `DerivedBidiClass.txt` lists the assigned characters and
leaves unassigned ones to defaults declared inside comments, and those defaults are *not* all
`Left_To_Right`: unassigned code points in the Hebrew block default to `R` and in the Arabic blocks
to `AL`. An implementation reading only the explicit ranges is quietly wrong for exactly the scripts
the bidirectional algorithm exists for. The generator materialises them into the table, so there is
nothing left to default at run time and a lookup is one binary search.

`LineBreak.txt` has the same kind of defaults - ID for unassigned code points among the CJK
ideographs, in planes 2 and 3 and in two ranges of plane 1, PR in the currency symbols block - but
17.0.0 lists them as explicit ranges of reserved code points beside a single `@missing` line for
`XX`. The generator reads both forms, so it does not matter which a version uses.

**Only what a rule reads is in `LineBreakFlags.g.cs`.** East_Asian_Width, General_Category and
Extended_Pictographic are read by six of UAX #14's rules, each for a narrow purpose: a flag is set
only where a rule looks at it - Pf only on a QU, Mn and Mc only on an SA - so the table is a few
hundred runs rather than a General_Category partition of thousands.

**Defaults are painted before explicit ranges, in two passes**, rather than everything being sorted
by width. Width sorting happens to work today and stops working the day an explicit range is wider
than a default it overlaps.

**Value names are reconciled through `PropertyValueAliases.txt`.** `DerivedBidiClass.txt` names its
values by short alias (`AL`), `Scripts.txt` by long name (`Arabic`). The `BidiClass` and
`LineBreakClass` enums use the short forms because that is how UAX #9 and UAX #14 write their rules
and each algorithm should read like its specification; `UnicodeScript` uses long names and carries the four-letter codes separately, because
nobody calls Arabic "Arab" in prose but a shaper needs `arab`.
