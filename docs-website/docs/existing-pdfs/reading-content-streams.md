---
title: Reading content streams
description: Parse the drawing operators of a PDF page with ContentReader, inspect their operands, and write changed content back.
demos: [Inspect]
---

Everything drawn on a PDF page is stored in the page's **content stream**: a list of operators,
each with its operands. `100 200 50 30 re` adds a rectangle to the path, `S` strokes it, and
`(Hello) Tj` shows text. `ContentReader` parses that list into objects you can read in C#.

Read a page's content when you need to know what is really on it:

- to find out why a page is blank. A page whose operators are all there but whose coordinates are
  wrong looks the same as a page with nothing on it, and only the content tells you which it is;
- to check in a test what your code drew, without rendering the page;
- to see what a page from another producer is made of: mostly text, mostly paths, or one large
  image;
- to make a simple change, such as removing an operator, and write the content back.

`ContentReader` is in `PdfPinata.Pdf.Content`, and the objects it returns are in
`PdfPinata.Pdf.Content.Objects`. Both are in the core `PdfPinata` package. To read the text of a
page rather than its operators, use [Text extraction](./text-extraction.md) instead.

## Read a page's operators

`ContentReader.ReadContent(page)` reads every content stream of the page, decompresses it and
parses it. The Inspect demo saves the document it has drawn and opens it again, so that it
reads the content exactly as it was written to the file:

```csharp demo=Inspect snippet=read-content
```

`ReadContent` also takes a `byte[]` or a `MemoryStream` of content that has already been
decompressed, for example the stream of a form XObject.

## The object model

`ReadContent` returns a `CSequence`: a list of `CObject`. You can index it, loop over it with
`foreach`, and query it with LINQ. Each operator is a `COperator`, and its operands are a
`CSequence` of their own:

| Type | Holds |
|---|---|
| `COperator` | An operator. `OpCode.Name` is its name as the file writes it, such as `"re"` or `"Tj"`. `OpCode.OpCodeName` is the same as an enum value. `OpCode.Description` explains it. `Operands` holds its operands. |
| `CInteger`, `CReal` | A number, in `Value`. |
| `CName` | A name, such as a font's resource name `/F0`, in `Name`. |
| `CString` | A string, in `Value`, one character per byte. |
| `CArray` | An array of operands. It is a `CSequence`, so you can index and count it. |
| `CComment` | A comment in the content, in `Text`. |

The Inspect demo prints each operand by its type:

```csharp demo=Inspect snippet=operands
```

## Count what a page is made of

A count by operator is often more useful than the full list. It shows at a glance whether a page is
mostly text, paths or images:

```csharp demo=Inspect snippet=tally
```

These operators turn up most often:

| Operator | Means |
|---|---|
| `q`, `Q` | Save and restore the graphics state. |
| `cm` | Change the coordinate system. |
| `re`, `m`, `l`, `c`, `h` | Build a path: rectangle, move to, line to, curve to, close. |
| `S`, `f`, `B`, `n` | Stroke the path, fill it, do both, or do neither. |
| `W` | Use the path as a clipping path. |
| `rg`, `RG` | Set the fill colour and the stroke colour. |
| `w` | Set the line width. |
| `BT`, `ET` | Begin and end a text object. |
| `Tf` | Set the font and size. |
| `Td` | Move to the start of the next line of text. |
| `Tj`, `TJ` | Show text. `TJ` takes an array with spacing between the parts. |
| `gs` | Apply a set of graphics state parameters, such as transparency. |
| `Do` | Paint an image or a form XObject. |

The operands of `Tf`, `gs` and `Do` are resource names. The page's resource dictionary says which
font, graphics state or image each name stands for. See [Working with PDF objects](./pdf-objects.md).

## Why the text reads as numbers

In a document written by PdfPinata, the operands of `Tj` are not readable words. Fonts are embedded
as Unicode by default, and a Unicode font's text is written as two-byte glyph numbers, which mean
nothing without the font. Other producers do the same. To get the characters, use
`PdfTextExtractor`, which translates glyph numbers through the font's Unicode map.

If you set `XPdfFontOptions.WinAnsiDefault` on a font, its text is written as readable strings
instead, but only characters in the WinAnsi character set can be drawn. See
[Unicode and font embedding](../fonts-and-text/unicode-and-embedding.md).

## Change the content and write it back

To change a page's content, change the sequence and pass it to `page.Contents.ReplaceContent`. The
document must be open in `Modify` or `Append` mode. This removes every image and form that a page
paints:

```csharp
using PdfPinata.Pdf.Content;
using PdfPinata.Pdf.Content.Objects;

CSequence content = ContentReader.ReadContent(page);
for (int index = content.Count - 1; index >= 0; index--)
{
    if (content[index] is COperator op && op.OpCode.Name == "Do")
        content.RemoveAt(index);
}
page.Contents.ReplaceContent(content);
document.PruneUnusedResources();
```

`ReplaceContent` writes the sequence as the page's single content stream. The images themselves are
still named in the page's resources until `PruneUnusedResources` removes them.

To build a new operator, call `OpCodes.OperatorFromName("re")` and add its operands to
`Operands`.

## Things to know

- **Read the saved content.** The Inspect demo saves and reopens its document before it reads a page
  it has drawn in the same run. Reading a page from a file you opened needs no such step.
- **Inline images are not read.** A small image can be written directly into the content, between
  the `BI` and `EI` operators. The reader skips its data, so the `BI` operator appears without it.
  Do not write back content that holds an inline image: the image would be lost.
- **Content that does not parse throws `ContentReaderException`.**
- **Reading a page's content can change how the page stores it.** `ReadContent(page)` gathers the
  page's content streams into an array on the page. The page draws the same, but in a document open
  in `Append` mode the page counts as changed and is written into the next revision.
- **Only the page's own content is read.** A `Do` operator paints a form XObject whose content is a
  stream of its own. Read that stream separately if you need it.

## See it in action

[The Inspect demo](../demos.mdx#inspect) draws a page with six calls, reads the page back, lists the
operators the calls produced, and counts them.

<details>
<summary>The full Inspect demo</summary>

```csharp demo=Inspect
```

</details>
