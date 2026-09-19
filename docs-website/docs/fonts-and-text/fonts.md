---
title: Fonts
description: How PdfPinata finds font files, how to serve your own, and how families, styles, sizes and decorations work.
demos: [Fonts]
---

Every piece of text in a PDF needs a font file behind it, and PdfPinata embeds that file in the
document. The core `PdfPinata` package does not look for font files itself. A **font resolver** does
that: it turns a family name and a style, such as "Liberation Sans, bold", into the bytes of one
font file. You register one resolver when your application starts, before you create any `XFont`.

The two backend packages each include a resolver that reads the fonts installed on the machine. If
your fonts are not installed, for example in a container or on a web server, write your own
resolver or point a backend resolver at your own files.

## Register a resolver

Register the resolver from your backend package once, at startup:

```csharp
using PdfPinata.Fonts;
using PdfPinata.Utils;

GlobalFontSettings.FontResolver = new SkiaFontResolver();          // PdfPinata.Skia
// or
GlobalFontSettings.FontResolver = new ImageSharpFontResolver();    // PdfPinata.ImageSharp
```

Both resolvers search the operating system's font folders the first time they are asked for a
font:

- On Windows, they read `%SystemRoot%\Fonts` and the per-user fonts folder.
- On Linux, they ask fontconfig. If fontconfig is not installed, they read the usual font folders,
  such as `/usr/share/fonts`.
- On macOS, they read `/Library/Fonts/`.
- On other platforms, such as Android and iOS, the search throws. Use your own files there, as
  the next section shows.

They find `.ttf`, `.otf`, `.ttc` and `.otc` files, and each face in a collection file is available
on its own.

The [Installation](../installation.md) page covers the rest of the backend setup.

## Use your own font files

A server or container often has few fonts or none. You then have two choices.

**Point a backend resolver at your files.** `SetupFontsFiles` replaces the system search with the
files you give it, and works on every platform:

```csharp
var resolver = new SkiaFontResolver();
resolver.SetupFontsFiles(Directory.GetFiles("/app/fonts"));
GlobalFontSettings.FontResolver = resolver;
```

**Write your own resolver.** Implement `IFontResolver`, which has three members:

- `ResolveTypeface(familyName, isBold, isItalic)` returns a `FontResolverInfo` that names one face,
  or `null` if you cannot serve the request.
- `GetFont(faceName)` returns the bytes of the face you named.
- `DefaultFontName` is the family to use when a document asks for no font in particular. For
  example, PinataLayout's Normal style uses it.

This resolver serves one family from files embedded in your assembly, and answers every family name
with it:

```csharp
using System.IO;
using PdfPinata.Fonts;

public sealed class EmbeddedFontResolver : IFontResolver
{
    public string DefaultFontName => "Open Sans";

    public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
    {
        if (isBold && isItalic) return new FontResolverInfo("OpenSans-BoldItalic.ttf");
        if (isBold) return new FontResolverInfo("OpenSans-Bold.ttf");
        if (isItalic) return new FontResolverInfo("OpenSans-Italic.ttf");
        return new FontResolverInfo("OpenSans-Regular.ttf");
    }

    public byte[] GetFont(string faceName)
    {
        using Stream stream = typeof(EmbeddedFontResolver).Assembly
            .GetManifestResourceStream("MyApp.Fonts." + faceName)
            ?? throw new FileNotFoundException("No embedded font called " + faceName);
        using var bytes = new MemoryStream();
        stream.CopyTo(bytes);
        return bytes.ToArray();
    }
}
```

The face name is yours to choose, but each face must have a different one. PdfPinata caches the
answers for the life of the process, so it calls `GetFont` once for each face.

`GetFont` must return a single face. To serve one face of a `.ttc` or `.otc` collection, pass the
file's bytes and the face's index to `TrueTypeCollection.ExtractFace` (namespace `PdfPinata.Utils`)
and return the result.

The demos use a resolver of this kind, so that they look the same on every machine. It serves
"Liberation Sans", "Liberation Serif", "Source Code Pro" and "Noto Sans Arabic" from embedded
files. Those names do not work in your application unless your resolver serves them.

## Families, styles and sizes

An `XFont` is a family name, a size in points and an `XFontStyle`. The four styles are `Regular`,
`Bold`, `Italic` and `BoldItalic`:

```csharp demo=Fonts snippet=families-and-styles
```

`MeasureString` tells you how much room text takes in a given font, so you can place lines by
their real height instead of by a fixed step:

```csharp demo=Fonts snippet=size-ramp
```

## Simulated bold and italic

If a family has no bold or italic file, the resolver can ask PdfPinata to fake the style. It draws
simulated bold by stroking the outline of each glyph as well as filling it, and simulated italic by
slanting the upright glyphs. The backend resolvers do this for you: they use the closest face the
family has and simulate only what is missing.

In your own resolver, pass `XStyleSimulations` flags with the face:

```csharp
XStyleSimulations simulate =
    (isBold ? XStyleSimulations.BoldSimulation : XStyleSimulations.None)
    | (isItalic ? XStyleSimulations.ItalicSimulation : XStyleSimulations.None);

return new FontResolverInfo("SourceCodePro-Regular.otf", simulate);
```

A simulated style is a fallback. The demo sets a stroked bold beside a designed one, and the designed
face looks better:

```csharp demo=Fonts snippet=simulated
```

## Underline and strikeout

`XFontStyle.Underline` and `XFontStyle.Strikeout` draw a plain line:

```csharp demo=Fonts snippet=font-style-decorations
```

For a dotted or dashed line, or a line in a different colour from the text, set `Underline`,
`Strikeout` and `DecorationColor` on an `XStringFormat` instead:

```csharp demo=Fonts snippet=string-format-decorations
```

## Embedding

PdfPinata always embeds the fonts a document uses, and there is no setting to turn this off. How
much of the font goes into the file depends on its outlines:

- **TrueType outlines** (most `.ttf` files) are subset. The PDF holds only the glyphs the document
  uses.
- **PostScript (CFF) outlines** (many `.otf` files) are embedded whole. A document that uses one
  character of such a font carries the whole file.

[Unicode and font embedding](./unicode-and-embedding.md) shows what each choice writes to the file.

## Things to know

- **Register the resolver before you create a font.** Reading `GlobalFontSettings.FontResolver`
  before one is set throws an `InvalidOperationException`. After a font has been created, setting a
  different resolver also throws. Setting the same instance again does nothing.
- **An unknown family does not fail with the backend resolvers.** `SkiaFontResolver` and
  `ImageSharpFontResolver` answer a family they cannot find with a face from another installed
  family. To get `null` instead, set `NullIfFontNotFound = true` on the resolver. If a resolver
  returns `null`, creating the `XFont` throws `InvalidOperationException`.
- **A missing glyph draws as an empty box.** If the face has no glyph for a character, you get the
  font's `.notdef` glyph and no error. Font fallback can fix this: see
  [International text](./international-text.md).
- **Large CFF fonts make large files.** A CJK font with CFF outlines can add several megabytes to
  every document that uses it. Prefer a TrueType version of the font if one exists.
- **Text as outlines needs one more setting.** `XGraphicsPath.AddString` needs
  `GlobalFontSettings.GlyphOutlineProvider` set, to `SkiaGlyphOutlineProvider` or
  `ImageSharpGlyphOutlineProvider`. Only `XTextFormatter` drop caps also use it, and they work
  without it. You can set it at any time.
- **Coming from PDFsharp or PdfSharpCore?** There is no default resolver, and there is no
  `PdfFontEmbedding` setting, because fonts are always embedded. See
  [Migrating](../reference/migrating.md).

## See it in action

[The Fonts demo](../demos.mdx#fonts) sets three families in four styles, compares a simulated bold
with a designed one, draws a size ramp and shows every decoration style.

<details>
<summary>The full Fonts demo</summary>

```csharp demo=Fonts
```

</details>
