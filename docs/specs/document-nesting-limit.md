# Spec: how deeply a document may nest arrays and dictionaries (#414)

`Parser` (`src/PdfPinata/Pdf.IO/Parser.cs`) reads an array or a dictionary by a call inside the
call reading what holds it. Before #414 nothing bounded that depth. One object of 5,000 opening
brackets, or of `<</A` written 5,000 times, ran out of stack in `PdfReader.Open` in every open mode.
A stack overflow ends a .NET process and no `catch` sees it, so a file of 10 KB could kill any
program that opened it, the page renderer included.

## The limit

`Parser.MaximumNesting` is 200. It counts the arrays and dictionaries opened *inside* the one
being read, so an indirect object may be 201 deep counting itself. The same holds for a trailer and
for the dictionary of a cross-reference stream. The number comes from what a level costs on a small
stack, from real files, and from other readers.

### What a level of nesting costs

Two walks recurse once per level when a document is opened. The parser reads each array and
dictionary inside the call reading what holds it. Then, for an encrypted document,
`PdfStandardSecurityHandler.EncryptObject` decrypts each object it read: `EncryptDictionary` calls
`EncryptElements`, which calls `EncryptDictionary` again for each dictionary it holds, and
`EncryptArray` does the same for arrays. The decrypt walk runs after the parse, not inside it, so
the deeper of the two is what has to fit.

Each was measured with a net10.0 Release console program on Windows x64. The program opened the
document on a thread started with a given `maxStackSize`, and a bisection found the deepest nesting
that did not end the process. Two stack sizes give the cost of one level, whatever the frames
below the first level take:

| Walk | 128 KB | 192 KB | 256 KB | Bytes per level |
|---|---|---|---|---|
| Parse, arrays | 262 | 432 | 612 | about 385 |
| Parse, dictionaries | 248 | 417 | 583 | about 390 |
| Open an encrypted file (`Modify`) | 213 | 353 | 489 | about 475 |

The parse rows read one object through `Parser` with no limit set. The last row opens a file whose
catalog names an array and a dictionary nested that deep, each ending in a string, and the decrypt
walk is the frame on top when it fails. The three figures of each row are the deepest that
survived, to within 2% for the parse and 2 levels for the open. At 256 KB the frames below the
first level of the encrypted open take about 30 KB: 262,144 - 489 × 475 bytes.

256 KB is the stack this limit is planned for. A program that starts a thread with a stack size of
its own often asks for that much or less, and a library cannot know what thread it is called on.
The main thread has more: a .NET apphost on Windows reserves 1,572,864 bytes (1.5 MB, its
`SizeOfStackReserve`), and there an unencrypted object opened 4,000 deep and not 5,000.

At 200 levels the decrypt walk takes about 93 KB (200 × 475 bytes). With the 30 KB the open already
uses, that is about half of a 256 KB stack, and the other half is left to whatever called
`PdfReader.Open`. 200 is 41% of the 489 levels that a 256 KB thread survived. The parse alone takes
about 76 KB at that depth. The test `AnEncryptedObjectAsDeepAsMayBeReadOpensOnAThreadOf256Kilobytes`
opens such a file at the limit on a thread of 256 KB, in each open mode that decrypts. `Lazy` is
not one of them, because it refuses an encrypted document. With the limit raised to 600, the test
ends the test host with a stack overflow.

The first version of this limit was 500, chosen from the main thread alone. An encrypted file
nested 490 deep then overflowed a 256 KB thread in the decrypt walk.

### Real files and other readers

- **Real files.** Deep structure in a PDF is held together by references, and a reference is not
  nesting. A page tree, an outline, a structure tree and a name tree each go one indirect object
  per level. Direct nesting is a few levels at most: a `/DecodeParms` array of dictionaries, a
  `/Border` dash array, a structure element's `/K` array of marked-content dictionaries. 200 is far
  above any of them.
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
writes 200 levels, so a file that does is malformed or hostile. Replacing the object with `null`
and reading on would hide that. Reading any unparseable object as `null` under `Moderate` is #423,
and would cover this refusal too if it is done.

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
  the same way. `PdfDocument.Save` refuses such a document with an `InvalidOperationException`
  before it opens the destination, as it does for the other saves it cannot make. The message names
  the object by its number and says the document cannot be saved. `PdfWriter.NestsTooDeepToRead`
  makes the check with a stack of its own, so an object 100,000 deep is refused rather than
  overflowing the check. `SaveIncremental` makes the same check before it writes. Refusing only when
  the writer reached the object would be too late: `Save(path)` has emptied the file by then, and
  the save would leave part of a document behind.
  `AnObjectBuiltDeeperThanCanBeReadLeavesTheFileItWasToBeSavedOverUntouched` pins this.
  `PdfWriter.WriteBeginObject` still refuses past the limit, for whatever reaches it without the
  check. A file this library wrote is then always one it can read back.
- **Reference walks.** `PdfCrossReferenceTable.TransitiveClosure`, used to save and to import,
  recurses once per reference and puts off what lies 1,000 references down, to walk it later from
  a shallower stack. A 1.5 MB stack holds 1,000 levels of that walk and a 256 KB stack does not, so
  a long chain of references can still overflow a small stack. That is #422, and is not fixed here.
  The parser follows no references while it reads (`ReadReference` is unused and throws).
- **Clone and import.** `PdfArray.Copy`, `PdfDictionary.Copy` and `ImportClosure`'s fix-up recurse
  once per level of direct nesting. What they copy out of a file is no deeper than the limit. An
  object built in code more deeply could still overflow them. Guarding them was left out: the
  caller builds that object, and the writer refuses to save it.
