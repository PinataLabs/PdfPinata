# UnicodeTableGenerator

Writes the character property tables `PdfPinata.Text` looks Bidi_Class, Script and paired
brackets up in.

```
dotnet run --project tools/UnicodeTableGenerator -- --version 17.0.0 --out PdfPinata/Text
```

Options, all optional: `--version` (default `17.0.0`), `--out` (default `PdfPinata/Text`),
`--cache` (default a directory under the system temp). Files are downloaded from
`unicode.org/Public/<version>/` on first use and read from the cache afterwards.

It writes four files, all checked in:

| file | what |
|---|---|
| `BidiClass.g.cs` | the `BidiClass` enum and a complete Bidi_Class partition of U+0000..U+10FFFF |
| `UnicodeScript.g.cs` | the `UnicodeScript` enum, the Script partition, and the ISO 15924 codes |
| `BracketPairs.g.cs` | the canonical paired brackets, for rule N0 |
| `UnicodeVersion.g.cs` | the version the other three came from |

## Why it is not part of the build

Deliberately outside `PdfPinata.slnx`, so `dotnet build` and CI never see it. The alternative —
generating during the build — would put a network fetch on the critical path of every build on
every target framework, and make an offline build impossible. What it produces is small (about
57 KB of source for tables of 1,611 and 984 ranges), so checking it in costs nothing and buys a
reviewable diff whenever the Unicode version moves.

## Bumping the Unicode version

Three things move together and a test enforces it:

1. Run the generator with the new `--version`.
2. Replace `PdfPinata.Test/Assets/Unicode/BidiTest.txt.gz` and `BidiCharacterTest.txt.gz` with
   the same version's, gzipped.
3. Update the version in `UnicodePropertyTests.TheTablesSayWhichUnicodeTheyCameFrom`.

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

**Defaults are painted before explicit ranges, in two passes**, rather than everything being sorted
by width. Width sorting happens to work today and stops working the day an explicit range is wider
than a default it overlaps.

**Value names are reconciled through `PropertyValueAliases.txt`.** `DerivedBidiClass.txt` names its
values by short alias (`AL`), `Scripts.txt` by long name (`Arabic`). The `BidiClass` enum uses the
short forms because that is how UAX #9 writes its rules and the algorithm should read like the
specification; `UnicodeScript` uses long names and carries the four-letter codes separately, because
nobody calls Arabic "Arab" in prose but a shaper needs `arab`.
