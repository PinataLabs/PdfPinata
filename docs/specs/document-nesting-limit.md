# Spec: how deeply a document may nest arrays and dictionaries (#414)

`Parser` (`src/PdfPinata/Pdf.IO/Parser.cs`) reads an array or a dictionary by a call inside the
call reading what holds it. Before #414 nothing bounded that depth. One object of 5,000 opening
brackets, or of `<</A` written 5,000 times, ran out of stack in `PdfReader.Open` in every open mode.
A stack overflow ends a .NET process and no `catch` sees it, so a file of 10 KB could kill any
program that opened it, the page renderer included.

## The limit

`Parser.MaximumNesting` is 500. It counts the arrays and dictionaries opened *inside* the one
being read, so an indirect object may be 501 deep counting itself. The same holds for a trailer and
for the dictionary of a cross-reference stream. The number comes from three measurements:

- **The stack.** On the main thread of a net10.0 Release process on Windows, which has 1 MB of
  stack, depth 4,000 opened and 5,000 did not. 500 is an eighth of what that stack held. The
  margin is for a thread started with a smaller stack, and for a caller that is already deep in
  its own frames when it opens a file.
- **Real files.** Deep structure in a PDF is held together by references, and a reference is not
  nesting. A page tree, an outline, a structure tree and a name tree each go one indirect object
  per level. Direct nesting is a few levels at most: a `/DecodeParms` array of dictionaries, a
  `/Border` dash array, a structure element's `/K` array of marked-content dictionaries.
- **Other readers.** qpdf stops at 499 (`parser_max_nesting` in `libqpdf/QPDFParser.cc`) and
  reports "ignoring excessively deeply nested data structure".

The content parser (#371) stops at 32 and the inline-image dictionary reader (#403) also stops at
32. Both read what a page draws, where no operator needs more. A document holds more kinds of data,
so it gets a limit of its own.

## What happens past it

The parser refuses the object with a `PdfReaderException`, "Arrays and dictionaries are nested
deeper than they may be here.", and does not read further into it. This is what the reader does
with any object it cannot parse, such as an unexpected token. Tolerant parsing exists for the
quirks of real producers, such as pdfTeX's keyless banner or XnView's early `endobj`. No producer
writes 500 levels, so a file that does is malformed or hostile. Replacing the object with `null`
and reading on would hide that.

- In the eager modes (`Modify`, `Import`, `ReadOnly`, `Append`) the exception comes from
  `PdfReader.Open`, under both `PdfReadAccuracy` values.
- Under `Lazy` it comes from the read that first reaches the object. That read goes through the
  one parser the document keeps, so the parser gives back every level it counted, also when the
  read fails. `Open` refuses without counting, and each nested read gives its level back in a
  `finally`. `AnObjectRefusedForItsDepthLeavesALazyDocumentReadingTheObjectsAfterIt` pins this.
  Without it, each refusal cost every later read of that document one level.

Every `Parser` starts with the limit as its default. This covers the parser that reads a
document's body, the parsers that `PdfObjectStream` makes over an object stream, and the
cross-reference and trailer reads. `ReadDictionary(string, int)` still sets a limit of its own for
inline images.

## What else was checked

- **Literal strings.** The `Lexer` keeps nested parentheses as an `int`, so 100,000 of them read as
  one string. `AStringWithParenthesesNestedFarTooDeepIsReadAsOneString` pins this.
- **The writer.** An object built in code can be nested to any depth, and writing it recursed in
  the same way. `PdfWriter.WriteBeginObject` now throws an `InvalidOperationException` before it
  opens more than `MaximumNesting` inside one object. A file this library wrote is then always one
  it can read back.
- **Reference walks.** `PdfCrossReferenceTable.TransitiveClosure`, used to save and to import,
  already puts off what lies 1,000 levels down and walks it later from a shallow stack. The parser
  follows no references while it reads (`ReadReference` is unused and throws).
- **Clone and import.** `PdfArray.Copy`, `PdfDictionary.Copy` and `ImportClosure`'s fix-up recurse
  once per level of direct nesting. What they copy out of a file is no deeper than the limit. An
  object built in code more deeply could still overflow them. Guarding them was left out: the
  caller builds that object, and the writer refuses to save it.
