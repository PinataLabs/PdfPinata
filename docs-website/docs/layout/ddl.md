---
title: "DDL: documents as text"
description: Write a PinataLayout document out as DDL text, read it back into an object model, and render the copy.
demos: [Ddl]
---

DDL is PinataLayout's own text format for a document. A `Document` built in C#, with its styles,
sections, paragraphs, tables and fields, can be written out as DDL and read back into an equal
`Document`. The reader and writer are in the `PinataLayout.DocumentObjectModel.IO` namespace, in the
[PinataLayout.DocumentObjectModel](https://www.nuget.org/packages/PinataLayout.DocumentObjectModel)
package. To turn a document you read into a PDF, you also need
[PinataLayout.Rendering](https://www.nuget.org/packages/PinataLayout.Rendering).

DDL is useful for three things:

- **Seeing what a document holds.** Write a document to a string and read it. This is often the
  fastest way to find out why a style or a table border is not what you expected.
- **Keeping templates as text.** Store a report layout as a DDL file, read it at run time, and fill
  it in, instead of building the whole layout in C#.
- **Storing documents.** Save the document model itself, and render it again later.

## What DDL looks like

Keywords start with a backslash, braces hold content, and square brackets hold attributes as
`Name = value` pairs:

```text
\document[Info{Title = "Monthly report"}]
{
  \section[PageSetup{PageFormat = A5}]
  {
    \paragraph
    {
      Totals are in \bold{bold} and notes in \italic{italic}.
    }
    \paragraph
    {
      Page \field(Page)[] of \field(NumPages)[]
    }
  }
}
```

A paragraph's text sits between its braces. Runs of spaces and line breaks in the text read as one
space, so you can indent a file however you like. To write a brace or a backslash inside text, put a
backslash in front of it: `\{`, `\}`, `\\`.

## Write a document to DDL and read it back

`DdlWriter.WriteToString` turns a document into a string. To read it back, create a `DdlReader`
with a `DdlReaderErrors` object and call `ReadDocument`:

```csharp demo=Ddl snippet=round-trip
```

The copy is an ordinary `Document`. You can change it, add sections to it, or render it:

```csharp demo=Ddl snippet=render
```

Other members do the same work with files and with parts of a document:

| To do this | Write with | Read with |
| --- | --- | --- |
| A whole document, as a string | `DdlWriter.WriteToString(document)` | `DdlReader.DocumentFromString(ddl)` |
| A whole document, as a file | `DdlWriter.WriteToFile(document, path)` | `DdlReader.DocumentFromFile(path)` |
| One object, such as a table | `DdlWriter.WriteToString(table)` | `DdlReader.ObjectFromString(ddl, errors)` |
| One object, as a file | `DdlWriter.WriteToFile(table, path)` | `DdlReader.ObjectFromFile(path, errors)` |

`DdlWriter` and `DdlReader` also have constructors that take a `Stream`, a `TextWriter` or
`TextReader`, or a file name. `WriteToString` and `WriteToFile` have overloads that take an indent
width.

## Check for errors

The reader records many problems in `DdlReaderErrors` instead of throwing. An attribute that names
no property, for example, is skipped and reported, and the rest of the document is still read. If you
pass no errors object, those problems are lost.

`DdlReader.DocumentFromString` and `DocumentFromFile` take no errors object. To find out what the
reader could not parse, use a `DdlReader` constructor that takes one, as the demo does, and check it
after reading:

```csharp
if (errors.ErrorCount > 0)
{
    foreach (DdlReaderError error in errors)
        Console.WriteLine($"{error.SourceLine}:{error.SourceColumn} {error.ErrorMessage}");
}
```

`ErrorCount` counts only entries whose `ErrorLevel` is `DdlErrorLevel.Error`. Enumerating the object
returns every entry, including warnings and information.

Some problems still throw. Text that is not DDL at all, or a keyword in a place the grammar does not
allow, raises an exception with a message that says what was expected.

## Use DDL as a template

There are two ways to fill in a template you have read:

- **Change the object model after reading.** Read the template, find the sections or paragraphs you
  need through the `Document` object model, and add content to them in C#. This is the safer way,
  because nothing you add is parsed.
- **Replace text before reading.** Put markers in the DDL and replace them with values before you
  parse it. If you do this, escape every `{`, `}` and `\` in the values, or the reader will take them
  as DDL.

A field such as `\field(Info)[Name = "Title"]` prints a value from the document's `Info`, so you can
set `document.Info.Title` after reading and let the renderer put it on the page.

## Things to know

- **Some malformed files make the reader hang.** A file that ends inside a section, an attribute
  value that is not a valid enum member, or an attribute block with a missing bracket can make the
  reader run forever instead of throwing. Read only DDL that your own code wrote, or DDL you have
  checked. If you must read files from elsewhere, read them on a separate thread that you can abandon
  after a time limit.
- **Images are stored as paths.** An image is written as the file path it was loaded from, not as its
  pixels. To render the copy, the image file must still be at that path.
- **Newer attribute values need a newer reader.** The writer records enum values by name. A document
  that uses a value added in a later version, such as the side-wrap styles in
  [Columns, drop caps and wrapping](./advanced-layout.md), cannot be read by an older version of this
  library.
- **The DOM is what is saved.** DDL holds the `Document` object model, not the PDF. Settings on the
  renderer, such as `PdfDocumentRenderer.TagContent`, are not part of it.

## See it in action

The [Ddl demo](../demos.mdx#ddl) builds a document with styles, formatted text and a table, writes it
to DDL, reads it back and renders the copy. It then prints the first eighty lines of the DDL and a
table of what survived the round trip, read from the copy.

<details>
<summary>The full Ddl demo</summary>

```csharp demo=Ddl
```

</details>
