---
title: Footnotes
description: Add footnotes to a PinataLayout document, and set how they are numbered, marked and placed on the page.
demos: [Footnotes]
---

A footnote puts a small mark in the text and the note itself at the foot of the page that mark lands
on. PinataLayout does the layout for you. Before it lays out the paragraph that carries the mark, it
keeps room for the note at the foot of the page. The body text never runs into the notes, and the
page breaks where it has to. Numbering is worked out after layout, so the numbers stay in order when
text moves from one page to another.

Footnotes are part of the `PinataLayout.Rendering` package, like the rest of the layout engine.

## Add a footnote

Call `AddFootnote(text)` at the point where the mark belongs. The mark goes after the text added
before it. `Paragraph`, `FormattedText` and `Hyperlink` all have `AddFootnote`, so a note can hang
off a bold run or a link as well as plain text:

```csharp demo=Footnotes snippet=add-footnote
```

The mark is drawn as a superscript. The note is laid out in its own column below a short rule at the
foot of the page, with the mark hanging to its left.

## Notes with more than a line of text

`AddFootnote()` with no argument returns an empty `Footnote`. A footnote holds block content, like a
small section: add paragraphs to it with `AddParagraph`, or a table or an image with `AddTable` and
`AddImage`.

```csharp demo=Footnotes snippet=block-content
```

## Style the notes

Notes are set in the predefined `Footnote` style, which is based on `Normal`. Change that style
rather than each note. Space between notes comes from the style's `ParagraphFormat.SpaceAfter`;
nothing else is added.

```csharp demo=Footnotes snippet=footnote-style
```

## The four settings

Four properties on `Document` shape every footnote in it. Each applies to the whole document.

**`FootnoteNumberingRule`** decides when the numbers start again:

| Value | Numbers restart |
|---|---|
| `RestartPage` (the default) | On every page |
| `RestartSection` | At every section |
| `RestartContinuous` | Never: one sequence for the whole document |

The default restarts on every page, which most reports do not want. Set the rule you mean:

```csharp demo=Footnotes snippet=numbering-rule
```

**`FootnoteNumberStyle`** decides what a mark looks like: `Arabic` (1, 2, 3, the default),
`LowercaseLetter` (a, b, c), `UppercaseLetter` (A, B, C), `LowercaseRoman` (i, ii, iii) or
`UppercaseRoman` (I, II, III).

**`FootnoteStartingNumber`** is the first number of each sequence. Any value below 1, including the
default of 0, starts at 1.

**`FootnoteLocation`** decides where the block of notes sits. `BottomOfPage`, the default, puts it
at the foot of the text area. `BeneathText` puts it directly under the last line of text on the page.
Both keep the same room free while the page is laid out, so they differ only on a page that is not
full. On a full page they are the same place.

## Mark a note yourself

Set `Footnote.Reference` to show your own mark, such as an asterisk or a dagger, in place of a
number. A note with its own mark is left out of the count, so the numbered notes around it do not
skip a number:

```csharp demo=Footnotes snippet=own-mark
```

## Things to know

- **Numbering restarts on every page unless you say otherwise.** `RestartPage` is the default. Set
  `FootnoteNumberingRule` to `RestartContinuous` for one sequence through the document.
- **A footnote must be in a paragraph of the section itself.** A footnote in a table cell, a text
  frame, a header or footer, or another footnote throws a `NotSupportedException` when the document
  is rendered. None of those has a page foot of its own to put the note at. Move the footnote into a
  paragraph in the section, or write its text where it stands.
- **A note is never split across pages.** It is laid out whole on the page that carries its mark.
  Keep footnotes short: a note taller than the space left on any page overfills that page.
- **A paragraph that breaks across pages takes all its notes to the second page.** If a mark falls in
  the part of the paragraph on the first page, its note still appears on the next page.
- **The separator rule is fixed.** It is a thin line a third of the column wide, drawn once on each
  page that has notes. There is no setting to change it.
- **Tagged output links the mark to its note.** In a tagged document the mark is a `Reference`
  element and the note a `Note` element. See [Accessibility](../standards/accessibility.md).

## See it in action

[The Footnotes demo](../demos.mdx#footnotes) runs over five pages. It shows notes on formatted text,
a note of two paragraphs, a note with its own mark, the current number style, a short page where the
block stays at the foot of the sheet, and continuous numbering carried across a page break.

<details>
<summary>The full Footnotes demo</summary>

```csharp demo=Footnotes
```

</details>
