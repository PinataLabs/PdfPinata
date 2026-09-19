---
title: International text
description: Right-to-left text, complex-script shaping with PdfPinata.HarfBuzz, font fallback and paragraph direction.
demos: [International]
---

PdfPinata draws Hebrew, Arabic and other right-to-left text in the order it is read, with no extra
package. Three separate features are involved, and you can use each one on its own:

| Feature | What it does | What you need |
| --- | --- | --- |
| Reordering | Puts right-to-left text in reading order, using the Unicode Bidirectional Algorithm | Nothing. It is built in. |
| Shaping | Joins Arabic letters, forms Indic conjuncts, and applies kerning and ligatures | The `PdfPinata.HarfBuzz` package |
| Font fallback | Draws a character with another font when the chosen font has no glyph for it | A list of families to try |

## Right-to-left text

`DrawString` and `MeasureString` reorder text before they draw or measure it. You pass the string in
the order it is typed, and each run of right-to-left characters turns round inside itself. A
left-to-right word in a right-to-left sentence keeps its own order, and the reverse is also true:

```csharp demo=International snippet=mixed-direction
```

Reordering is not the same as reversing the string. Hebrew needs nothing more than this. Arabic is
drawn in the right order, but each letter keeps its isolated form unless you register a shaper.

## Say which way a paragraph runs

The base direction of a line decides which end it starts from. By default
(`BidiParagraphDirection.Automatic`) it comes from the first strong character, which is any
letter. A Hebrew line that starts with a Latin brand name is therefore laid out left to right. When
you know the direction, say so with `XStringFormat.TextDirection`:

```csharp demo=International snippet=declared-direction
```

The same property exists in three places, and all three take `BidiParagraphDirection` from the
`PdfPinata.Text` namespace:

- `XStringFormat.TextDirection`, for one line drawn with `DrawString`.
- `XTextFormatter.TextDirection`, for a paragraph wrapped into a rectangle.
- `ParagraphFormat.TextDirection`, for a PinataLayout paragraph.

`XTextFormatter` lays out right-to-left paragraphs with every alignment, justified included:

```csharp demo=International snippet=rtl-formatter
```

## Shape complex scripts with HarfBuzz

Many scripts need more than one glyph per character. Arabic letters change form depending on their
neighbours, Devanagari combines consonants into conjuncts, and Latin text uses kerning and
ligatures. This work is called shaping. The rules for it are in the font, and a shaper reads them.

To turn shaping on:

1. Add the `PdfPinata.HarfBuzz` package.
2. Add the `HarfBuzzSharp.NativeAssets` package for each platform you deploy to, for example
   `HarfBuzzSharp.NativeAssets.Linux`.
3. Register the shaper once, at startup:

```csharp
using PdfPinata.Fonts;
using PdfPinata.HarfBuzz;

GlobalFontSettings.TextShaper = new HarfBuzzTextShaper();
```

`PdfPinata.HarfBuzz` works with either backend, and with no backend at all. The
[Installation](../installation.md) page lists the packages.

With no shaper registered, each character maps to one glyph through the font's character map. That
is right for most Latin, Greek and Cyrillic text, apart from kerning and ligatures.

Shaped text stays searchable and copyable. PdfPinata records which characters each glyph stands for,
so a reader can copy a ligature or a joined Arabic word back out as the original characters.

## Fall back to another font

If a font has no glyph for a character, PdfPinata draws the font's `.notdef` glyph, usually an empty
box, and reports no error. Font fallback gives it other families to try:

```csharp
using PdfPinata.Fonts;

GlobalFontSettings.FontFallback =
    new FontFallbackList("Noto Sans Arabic", "Noto Sans Devanagari");
```

Your font resolver must be able to serve each family in the list. PdfPinata tries them in order
for each character the chosen font cannot draw. It then draws that part of the string in the first
family that can, and goes back to the chosen font after it. One `DrawString` call can therefore
use several fonts, and each one is embedded:

```csharp demo=International snippet=fallback
```

To choose families by character, implement `IFontFallback`. Its one method,
`FamiliesFor(codePoint, isBold, isItalic)`, returns the families to try for a Unicode code point. If
your font resolver implements `IFontFallback` as well, PdfPinata uses it without a separate
registration.

## Things to know

- **All three features need Unicode-encoded fonts.** That is the default for `XFont`. Text drawn
  with a font created with `XPdfFontOptions.WinAnsiDefault` is not reordered, shaped or given a
  fallback, and WinAnsi cannot hold Hebrew or Arabic in any case. PinataLayout's
  `new PdfDocumentRenderer()` uses WinAnsi, so use `new PdfDocumentRenderer(true)` for any
  international text. See [Unicode and font embedding](./unicode-and-embedding.md).
- **Shaping changes widths.** Kerning and ligatures make text narrower or wider, so lines can break
  in different places. Register the shaper before you lay anything out, and keep the setting the
  same between runs if the output must match.
- **Set fallback once, before drawing.** You can set, replace or clear `TextShaper` and
  `FontFallback` at any time. Changing `FontFallback` part-way through a document changes the fonts
  used from the next string on.
- **Fallback does not search your installed fonts.** It tries only the families you list, in order.
- **Some characters stay with the font around them.** Spaces, combining marks, and the zero-width
  joiner and non-joiner (U+200D and U+200C) never switch font on their own. This keeps shaping intact
  across word boundaries.
- **The zero-width non-joiner works.** Put U+200C between two Arabic letters to stop them joining.
  PdfPinata draws no glyph for it.
- **Automatic direction is decided per line in `XTextFormatter`.** In a right-to-left paragraph, one
  line that starts with a Latin word or a number is laid out the other way round from the others.
  Set `TextDirection` when you know the direction.
- **Do not dispose the shaper while text is being drawn.** `HarfBuzzTextShaper` is `IDisposable`.
  Register one instance for the life of the application.
- **Not supported:** vertical writing, Arabic justification by stretching letters (kashida),
  choosing OpenType features such as small capitals, automatic language detection and hyphenation.
  In a right-to-left PinataLayout paragraph, tab stops stay where a left-to-right paragraph would
  put them.

## See it in action

[The International demo](../demos.mdx#international) draws Hebrew and Arabic in reading order,
declares a paragraph direction, joins Arabic letters through HarfBuzz and draws Arabic from a Latin
font through fallback. The demo app registers a shaper and a fallback list at startup. Without them,
the second and third pages show what you get instead.

<details>
<summary>The full International demo</summary>

```csharp demo=International
```

</details>
