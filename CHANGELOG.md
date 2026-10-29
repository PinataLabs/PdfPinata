# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), with changes grouped by product area before change type, and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [Unreleased]

### Annotations & Forms

#### Fixed

- **A field whose `/DA` names a font size of 0 sizes its text to the box.** A size of 0 means auto-size (ISO 32000-1 12.7.3.3), and forms from form designers use it all the time, but text, combo box and list box fields drew it at a fixed 10 points: a value was a sliver in a tall box, and ran past the edge of a narrow one and was cut off. A single line, and a combo box's value, is now as large as the box is tall, and then smaller if that is what it takes to fit across it. A comb field fits its widest character to one cell. A multi-line value and a list box's rows start at 12 points and shrink until they fit. Nothing is drawn below 4 points; text that does not fit at 4 points is clipped. The size is chosen for each widget's appearance and is not written into `/DA`, so a reader that rebuilds the field still auto-sizes it. A `Font` set on the field, or a non-zero size in `/DA`, is drawn as before, and the `Font` property still answers 10 points for an auto-sized field. (#354)

## [0.5.0] - 2026-10-22

### API & Packaging

#### Added

- **`PdfPinata.ImageSharp4` is an ImageSharp backend on SixLabors.ImageSharp 4.x and SixLabors.Fonts 3.x.** `PdfPinata.ImageSharp` stays on ImageSharp 2.1.x, which is end of life and has five security advisories that will not be fixed; the new package has none of them, and suits an app that already uses a newer ImageSharp, since an app resolves one ImageSharp version. Both are built from the same source and have the same types in the same namespaces, so switching is a change to the project file, not to code. The new package targets `net8.0` and `net10.0` only, because ImageSharp 4 has no `netstandard2.1` build, so Unity stays on `PdfPinata.ImageSharp`. ImageSharp 4 and Fonts 3 are under the Six Labors Split License, not Apache-2.0. An app that references the package needs no Six Labors licence key to build, because the key is checked only when this repository builds the package. The two packages give the same results for the same input. Images decode to the same pixels, except that a JPEG can differ by up to 2 levels in 255, because ImageSharp 3 rewrote the JPEG decoder. An embedded ICC profile is kept and not applied, as 2.x does. `ImageSharpGlyphOutlineProvider` gives the same glyph outlines to within a few millionths of a point. As in 2.x, colour glyphs are drawn from their COLR version 0 layers, and SVG and COLR version 1 glyphs are drawn from their ordinary outlines. When the app loads another ImageSharp major at run time, the package throws an `InvalidOperationException` that names the version it needs. The ImageSharp dependency is `[4.1.3, 5.0.0)` and the Fonts dependency is `[3.2.0, 4.0.0)`. (#308)
- **Every package targets `net11.0` as well.** The libraries that target `netstandard2.1`, `net8.0` and `net10.0` now also target `net11.0`, and `PdfPinata.Signing` and `PdfPinata.ImageSharp4` target `net8.0`, `net10.0` and `net11.0`. The `net11.0` builds are compiled with the .NET 11 RC2 SDK, and `PdfPinata.Signing`'s `net11.0` build depends on `System.Security.Cryptography.Pkcs` `11.0.0-rc.2`. That dependency moves to `11.0.0` once .NET 11 is released.

#### Changed

- **`PdfPinata.ImageSharp`'s package description says that ImageSharp 2.1.x is end of life.** It has five security advisories that will never be fixed, and the description names `PdfPinata.ImageSharp4` as the package to use unless you target Unity or must keep your dependencies under Apache-2.0. `PdfPinata.ImageSharp4`'s description names its licence, the Six Labors Split License, and quotes Six Labors: an application that uses ImageSharp only through PdfPinata needs no Six Labors licence. (#310)

### Annotations & Forms

#### Added

- **`PdfAcroField.RedrawAppearance()` draws a field again from what it would show now.** A field redraws itself when one of its own properties changes and at no other time, so a kid field whose inherited `/V` changed on its parent went on showing the old value. Call this after a change like that. It is refused where setting the value is refused, and it throws an `InvalidOperationException` if it cannot draw: for a field that draws no appearance of its own (only text, combo box and list box fields do), a field in no document, or a field with no `Font` when no font resolver is registered. (#340)

#### Fixed

- **A text field read from a file keeps the background and border its widget's `/MK` names when it is redrawn.** `BackColor` and `BorderColor` return only what was set on the object, so they are empty for a field read from a file. The text field drew from those two alone, so setting its `Text` lost the box, and with no value the appearance was removed. Like a combo box and a list box, it now uses each widget's `/MK /BG` and `/BC` when the field sets no colour. (#340)
- **Removing an annotation from a page no longer removes it from the document.** `PdfAnnotations.Remove` took the annotation out of the cross-reference table as well as `/Annots`. An incremental save decides what to write from that table, so an annotation still reached from elsewhere lost its changes from the appended revision: a field merged with its widget, which is still in `/Fields`, a widget in its field's `/Kids`, or a markup annotation that a popup's `/Parent` names. `Remove` now changes `/Annots` only. A full save still drops an annotation that nothing reaches. (#337)
- **`PdfAnnotations.Add` writes `/P`, the page the annotation is on.** Only `AddWidget` and the signer wrote it. An annotation added to a page made with `new PdfPage(document)` before the page is placed is given `/P` when the page is placed. A screen annotation with a rendition action requires it, and some readers and importers find an annotation's page by it. (#337)
- **The missing field and annotation flags have names.** `PdfAcroFieldFlags` has `RadiosInUnison` (bit 26, radio buttons), `RichText` (bit 26, text fields) and `CommitOnSelChange` (bit 27, choice fields), and `PdfAnnotationFlags` has `LockedContents` (bit 10). Before, setting one meant casting an integer. Setting `RadiosInUnison` on a radio field keeps it a radio field. (#337)
- **Setting `Title` or `Subject` on a widget annotation throws instead of turning the widget into a field.** Both write markup annotation keys, `/T` and `/Subj`, but on a widget `/T` is the partial name of a form field. A widget from `AddWidget` given a `Title` read back after a save as a nested field: it moved from `Widgets` to `Fields`, its field no longer drew its appearance, and the field's full name gained a segment. On the widget of a field merged with its widget, `Title` renamed the field, even to a name with a period in it, which `Name` refuses. The setters now throw an `InvalidOperationException` that points to `PdfAcroField.Name` for the field's name and `PdfAcroField.ToolTip` for a label a person sees. Markup annotations are not affected, and a widget read from a file with `/T` already in it reads as before. (#330)
- **A field with no value of its own reads its parent's, as a check box already did.** `/V` is inheritable (ISO 32000-1 Table 220), but `PdfAcroField.Value`, `PdfTextField.Text`, the selection of a combo box or a list box and a radio group's `SelectedIndex` read only the field's own entry. A kid field that left its value to its parent read as empty, and a text field drew itself empty. They now read the nearest ancestor's `/V` when the field has none. A field's own `/V` still wins, and setting a value still writes it on the field and leaves the parent's alone. A list box cleared of its selection under a parent that has a value now writes an empty `/V` array, whether or not it had a selection of its own before, so that the parent's choice does not come back. (#334)

### Drawing & Graphics

#### Fixed

- **A grayscale JPEG drawn through either ImageSharp backend is written with a `/DeviceGray` colour space.** Both backends keep a JPEG's own colour type when they re-encode it, so a grayscale JPEG stayed one component, but `PdfImage` labelled every JPEG `/DeviceRGB`. That made an invalid image, which a reader refused, garbled or drew a third of its width. `PdfImage` now reads the component count from the JPEG's frame header and writes `/DeviceGray` for one component, which keeps a grayscale image a third of the size. Colour JPEGs are still written as `/DeviceRGB`, through all three backends. (#327)
- **The ImageSharp backends keep the transparency of a GIF, WebP, TIFF or any other image that is not a PNG.** `ImageSharpImageSource` called an image transparent only when it was a PNG, so every other image was written as a JPEG, which has no alpha, and its transparent pixels were drawn black. It now calls an image transparent when it is a PNG, as before, or when any of its pixels is less than opaque, and such an image is stored without loss with its alpha as a mask. An image with no transparent pixel is written as it was before, so existing documents do not change. A JPEG is never scanned, because it cannot have alpha. This applies to `PdfPinata.ImageSharp` and `PdfPinata.ImageSharp4`, and to `FromImageSharpImage` as well as the loads. (#305)
- **The Skia backend keeps the transparency of a GIF, WebP or any other image that is not a PNG.** `SkiaImageSource` called an image transparent only when it was a PNG, so a transparent GIF or a translucent WebP was written as a JPEG and its transparent pixels were drawn as a solid box, usually black. It now follows the ImageSharp backends' rule: a PNG is transparent, as before; a JPEG never is and is not scanned; and any other image is transparent when one of its pixels is less than opaque, unless its codec says the image is opaque, which skips the scan. An image with no transparent pixel is written as it was before, so existing documents do not change. `FromSkiaBitmap` is not affected, because its caller says whether the bitmap is transparent. (#313)
- **A page imported through `XPdfForm` is drawn in black where it names no colour, not in the caller's last colour.** A form XObject inherits the fill and stroke colours in force when it is drawn, and an imported page was written as page content, where a viewer starts in black, so a page that relied on that default took on whatever the caller had last painted with. An imported page is now drawn with `0 g 0 G` inside its own `q … Q`, so the caller's colours are in force again after it. A template drawn with `XForm` writes the same bytes as before. (#331)
- **An `XForm` whose first stroke is black draws it in black, not in the caller's stroke colour.** The renderer skips `RG` when the stroke colour has not changed, and before any stroke it took the colour to be black, the default a page starts with. A form XObject has no such default: it inherits the stroke colour in force where it is drawn, so a form that first stroked in black wrote no `RG`, and after a red line on the page its line came out red. Inside a form the first stroke colour is now always written. Page content streams write the same bytes as before. (#333)

### PDF Reader & Writer

#### Changed

- **A document given a user password and no owner password gets a random owner password, not the user password a second time.** Before, anyone with the user password opened the document as its owner, so every permission it was saved with was written into the file and did not apply. A document read with its owner password keeps that owner password, so a new `UserPassword` on its own no longer replaces the owner password. A new document with no owner password, or a document read with only its user password, is given 32 random bytes as one when it is saved. A document read with its owner password and given only a `DocumentSecurityLevel` is now saved encrypted under that owner password, with no user password; before, the save was refused for having no password. Nobody can recover that password, PdfPinata included, so set `OwnerPassword` if you need to lift the permissions later. As a result, opening such a document in `PdfDocumentOpenMode.Modify` with its user password is now refused, as it is for any document whose owner password differs. Saving is refused, with a message that says why, when the owner password is the same as the user password and any permission is turned off. Setting both to the same password with every permission on is still allowed. (#315)
- **Saving a document read from an encrypted file is refused until you choose its security.** Before, a document opened with its owner password, changed and saved without a new password was written unencrypted, so its permissions were removed too, and nothing said so. `Save` now throws `PdfPinataException`, and `CanSave()` gives the reason, until you set `UserPassword` or `OwnerPassword` to keep it encrypted, or set `DocumentSecurityLevel` to `PdfDocumentSecurityLevel.None` to remove the protection. To keep the old behaviour, set `DocumentSecurityLevel` to `None` before you save. The original encryption is not kept by itself: PdfPinata writes only RC4, and for AES-256 the user password cannot be recovered from the owner password. `SaveIncremental` is not affected, because it appends with the file's own encryption. (#318)
- **A `PdfPasswordProvider` that gives the user password again when `Modify` needs the owner password makes `PdfReader.Open` throw instead of asking again.** It used to be asked for as long as it answered, so a provider that knew only a user password, opening for `Modify`, hung the call. A wrong password given again is still asked about, as before. That became reachable for documents saved with only a user password, which until now opened for `Modify` with it. (#315)

#### Fixed

- **The writer no longer writes `-0`, a real a PDF cannot hold, `#00` in a name, or an empty content stream beside a page's content.** Every number format wrote `-0` for a value that rounds to zero from below, and `XGraphics` turned a plain translation into `1 -0 -0 1 50 90 cm`. Readers accept it, but it is noise, and a negative zero makes two identical documents differ byte for byte. Such a value is now written `0`, and `0.5` is still written `0.5`, so content streams change only where they held a `-0`. `new PdfReal(double)` now throws an `ArgumentException` for infinity, NaN and a value outside the single-precision range, which the writer used to write as `Infinity`, `NaN` or a 40-digit number; a real read from a file is kept as the file wrote it. A NUL character in a name is now left out rather than written `#00`, because ISO 32000-1 7.3.5 does not allow a name to hold one at all. A new `PdfResources` holds `/ProcSet` as a `PdfArray` of names rather than a literal, so `Elements.GetArray("/ProcSet")` answers the array before a save as it does after one, and the bytes written are the same. An `XGraphics` disposed without drawing anything on a page that already has content no longer leaves an empty content stream on the page, and no longer wraps the page's content in `q` and `Q`; the content is wrapped when something is drawn. (#336)
- **Two equal names are equal.** `PdfName.Equals` and `PdfNameObject.Equals` compared their string with the object passed in, so `new PdfName("/A").Equals(new PdfName("/A"))` was false although the hash codes agreed. Each now equals a `PdfName`, a `PdfNameObject` or a `string` with the same value. `PdfName.Comparer` sorts null before every name rather than after. (#337)
- **An object of another document is refused by the cross-reference table.** The check was a `Debug.Assert`, so a Release build wrote into one document a reference to an object of another, for example through `PdfFileSpecification.EmbeddedFile` given an embedded file made with a different document. It now throws an `InvalidOperationException` that says why. (#337)
- **A change to a direct value of a retyped object reaches an incremental save.** A page's `/Annots`, read from a file, is retyped as `PdfAnnotations` where it is read, and the page dictionary is retyped as `PdfPage`. Neither new object was recorded as sitting where the old one sat, so removing an annotation from a page opened in `Append` mode marked nothing changed and the appended revision left the page as it was. (#337)
- **Saving a document keeps every zero and `null` coordinate of its outline destinations.** `PdfOutline` wrote each coordinate of a `/Dest` array with the pattern `#.##`, which writes zero as nothing, so the zero vanished and the coordinates after it moved one place left: `[p /XYZ 0 792 0]` was saved as `[p /XYZ 792]`, which puts 792 in the left coordinate. A `null` coordinate was read as 0 and lost in the same way, so `[p /XYZ null 700 null]` was saved as `[p /XYZ 700]`. A zero is now written as `0`, a `null` is read as `NaN` and written back as `null`, and a coordinate keeps three decimal places rather than two. This applies to every destination type that has coordinates, and to a new outline whose `Left` is 0. (#324)
- **The signature value of an encrypted, signed document is read and saved as it stands in the file.** ISO 32000-1 says that the `/Contents` of a signature dictionary is never encrypted, but the security handler decrypted it on read and encrypted it on save like any other string. With RC4, `PdfSignatures.InDocument` gave back scrambled `Contents` bytes. With AES the bytes came through only when decrypting them failed, so some signature values were changed too. A `Modify` save with a password wrote `/Contents` encrypted, which no validator accepts. A dictionary that has a `/ByteRange` is now taken to be a signature dictionary, and its `/Contents` is left alone in both directions. Its other strings, such as `/Reason` and `/Name`, are encrypted as before, and so is the `/Contents` of any other dictionary, such as an annotation's text. (#325)
- **Data an encrypted document says is not encrypted is read as it stands.** With `/EncryptMetadata false`, the security handler used the flag to derive the key but still decrypted the metadata stream, so with an RC4 crypt filter the XMP was garbled, and with AES it survived only when the decryptor's padding check failed. A crypt filter named `/Identity` in `/StmF` or `/StrF` was taken for RC4, which garbled plain streams or strings, and a stream's own `/Crypt` filter was ignored. Now the metadata stream is left as it is when `/EncryptMetadata` is false, an `/Identity` crypt filter leaves what it covers as it is, and so does a stream's own `/Crypt` filter when it names `/Identity` or no filter. A stream's own `/Crypt` filter that names a crypt filter in `/CF`, such as `/StdCF`, decrypts the stream with that filter's method (`/V2` is RC4, `/AESV2` and `/AESV3` are AES) rather than with `/StmF`'s. That `/Crypt` filter is removed as the stream is read, because a save encrypts every stream, and so the stream unfilters. At `/V` 4 and 5, a missing `/StmF` or `/StrF` now means `/Identity`, as ISO 32000-1 Table 20 says, where it used to mean RC4. Documents whose metadata is encrypted read as before. (#328)
- **A refused `Save(path)` leaves the file at that path as it was.** `Save(path)` opened the file before it checked whether the document could be saved, and opening it emptied it. So a save refused for a reason that `CanSave()` gives, over the file the document was read from, left an empty file and no document. The checks now come first. (#318)
- **An encrypted document indexed by a cross-reference stream can be given a new password and saved.** The saved file could not be opened again, because it had no encryption dictionary, and its trailer's `/Encrypt` pointed at another object, such as the catalog. Reopening it failed with "The PDF document is protected with an encryption not supported by PDFsharp." The reader attached the encryption dictionary to a placeholder reference that it made while it read the cross-reference stream, and `Modify` mode then removed the dictionary as unreachable and gave its object number to another object. This happened whatever the cipher was: AES-256 at revisions 5 and 6, AES-128 at revision 4, and RC4 written by this library all failed in the same way. A document with a classic cross-reference table was not affected. The saved dictionary also kept the AES entries of the dictionary it was read from (`/CF`, `/StmF`, `/StrF`, `/OE`, `/UE`, `/Perms` and `/EncryptMetadata`), which do not apply to the RC4 encryption that this library writes. These entries are now removed. The message names PdfPinata instead of PDFsharp. (#314)
- **A PDF date is read with its UT offset when the offset has no closing apostrophe, or no minutes.** ISO 32000-2 writes the offset as `+HH'mm`, with no apostrophe after the minutes, and the parser read an offset only when one was there, so `D:20240101120000+05'00` from a PDF 2.0 producer was read as 12:00 UTC instead of 07:00 UTC, and `/CreationDate` and `/ModDate` said so. An offset of hours alone, such as `D:20240101120000-08`, was read as UTC too, and is now 20:00 UTC. A date without its `D:` prefix, such as `20240101120000Z`, now parses when it is shaped like a PDF date: digits alone, or at least a whole date of digits followed by an offset. Any other string without the prefix, such as `2024-05` or `2024-05-01`, is still read by `DateTime.TryParse` in the invariant culture, as before, and so do `D:2024` and `D:202403`: every field after the year is optional, and a missing one takes its default, month and day 1, time 00:00:00 and UT. For the same reason, a time short of its seconds, such as `D:199812231952`, now keeps its hours and minutes instead of being read as midnight. A malformed offset, such as `+05:00` or a designator other than `+`, `-` or `Z`, is still rejected, and the date reads as `DateTime.MinValue`. (#326)
- **A document whose page tree gives `/Rotate` indirectly or as a real, or gives `/Resources` that are not a dictionary, can be opened.** The entries a page inherits are collected when the pages are first read, and `/Rotate` was taken only as a direct integer and `/Resources` only as a dictionary, so `/Rotate 5 0 R` or `/Rotate 90.0` on a `/Pages` node threw `InvalidCastException` and the document could not be opened, imported from or counted. `/Rotate` is now read as a whole number whether it is direct, indirect or a real with no fractional part, on the page itself as well as inherited, and `PdfPage.Rotate` gives 90 for `/Rotate 90.0`. A `/Rotate` that is not a whole number is ignored, and `PdfPage.Rotate` gives 0 for it instead of throwing. `/Resources` that are not a dictionary are skipped as a null entry already was, so the page takes its resources from the nearest node that states them. (#332)
- **A page that names its `/Resources` by reference also gets the resource categories its page tree gives.** The inherited categories were added to a copy of the referenced dictionary that was then thrown away, so a page with `/Resources 5 0 R` under a node giving `/Font` read with no `/Font`. The page now holds that copy, with the inherited categories added; the dictionary it referred to, which other pages may share, is left as it is, and a page that has nothing to inherit keeps its reference. (#332)
- **Reading a malformed file reports what is wrong, or reads the file, instead of throwing an unrelated exception in four places.** A stream filtered with `/CCF` or `/DCT`, the abbreviations ISO 32000-1 gives for `CCITTFaxDecode` and `DCTDecode`, threw `NotImplementedException` from `TryUnfilter`; it now returns false, as the full names do, and the stream keeps its data and `/Filter`. A cross-reference stream with no `/Index` and a `/Size` that is missing or not an integer was read as zero entries, so the document failed later with no catalog; its entries are now counted from the stream's length and `/W`. A `/W` that is not three field widths is refused with a `PdfReaderException` that names `/W`. More entries than there is data for, an odd `/Index`, a cross-reference stream with a generation other than 0, and an entry whose generation differs from the object's are read tolerantly. Before, each of these failed a `Debug.Assert`, which ends the process in a debug build. `/PageMode` and `/PageLayout` given by indirect reference are now read through it, and a name the enumeration does not have, or a value that is not a name, reads as the default, `UseNone` and `SinglePage`. Before, `PdfDocument.PageMode` and `PageLayout` threw `ArgumentException`. An object stream that has no stream data, or that is not a dictionary, now makes `PdfReader.Open` throw a `PdfReaderException` that names the object stream. Before, it threw `NullReferenceException` or `InvalidCastException`. (#335)

### Fonts & Text

#### Fixed

- **Two font files with the same full name can both be used.** A resolver that served two different files whose name tables give one full name, such as two revisions of a font, under two face names made the second `XFont` throw `ArgumentException`, and every later attempt for that face fail differently. Fonts were cached under the full name inside the file as well as under the face name. They are now cached under the face name and their bytes alone, so each face measures and draws from its own file, and a document that uses both embeds both. Font creation on many threads at once is also safe now: the font caches are concurrent collections, and building a typeface holds the font lock from the first lookup to the last, where one lookup was made with no lock while other threads wrote the same cache. Nothing changes for a single thread or for fonts with distinct names. (#329)

### Documentation & Samples

#### Changed

- **The Blazor WebAssembly sample and its docs page use `PdfPinata.ImageSharp4`.** The sample takes it through a project reference, with no reference of its own to SixLabors.ImageSharp or SixLabors.Fonts, so it needs no Six Labors licence key, and no code changed, because the types and namespaces are the same. The page recommends `PdfPinata.ImageSharp4` and names `PdfPinata.ImageSharp` as the Apache-2.0 alternative. ImageSharp 4 gives no trim warning, where 2.1 gave one. (#311)
- **The sample's trim check fails on IL2026, a call to a member marked `[RequiresUnreferencedCode]`.** The check made ILLink collapse every trim warning from a PdfPinata assembly into one IL2104 and failed on that, but ILLink never collapses IL2026 for an assembly marked trimmable, and every PdfPinata assembly is. So an IL2026 was printed in full and the publish passed: a call to `Assembly.GetTypes()` in `PdfDocument.Save` did not fail it. IL2026 is now an error while the check is on, from any assembly; ASP.NET Core and ImageSharp give none today. (#311)
- **The installation guide says which ImageSharp package to choose.** A new section, *Choose an ImageSharp package*, covers Unity (2.x only), the licence of each package, the five 2.x advisories, and switching between the two, which is a change to the project file only. A JPEG can decode up to 2 levels in 255 differently after a switch, so the PDF is not always byte-identical. The licensing, platforms, troubleshooting, FAQ, migration and overview pages, and the README, name both packages and link to it. `docs/specs/imagesharp4-backend.md` records the decisions of #305 and what was left out: an ImageSharp 3.x package, a `netstandard2.1` build of the 4.x package, and retiring the 2.x one. (#310)

## [0.4.4] - 2026-10-17

#### Added

- **A font resolver can choose how a face with no bold of its own is made bold.** Simulated bold strokes each glyph and adds a character spacing after it, and both
were fixed at 2% of the em, the figure in the XPS specification. Microsoft Word strokes at 1/35 of the em and adds no spacing, so a bold title drawn here was wider
than Word's by 0.02 em per character. The reporter measured 13–18 pt at 12 pt. A smaller font size does not fix it, because the spacing is the same for every glyph,
 wide or narrow. `XBoldSimulation` holds the stroke width and the spacing. `XBoldSimulation.Default` is what was drawn before, and `XBoldSimulation.WordCompatible`
sets the glyphs at the regular face's advances, as Word does. A resolver gives one per face with the new `FontResolverInfo(faceName, styleSimulations, boldSimulatio
n)` constructor. A resolver derived from `FontResolverBase`, such as `SkiaFontResolver` or `ImageSharpFontResolver`, takes it from its new `BoldSimulation` property
, which must be set before the resolver is first used. `MeasureString` counts the same spacing that is drawn, so text laid out with one setting is never drawn with
another. Nothing changes for a resolver that does not set it. 
- **`OpenTypeFontMetadata` reads a font's family and style from bytes as well as from a path.** `Read(byte[])`, `Read(byte[], faceIndex)` and `ReadAll(byte[], faceCount)` read a single face, one face of a collection and every face of a collection from a font already in memory, and answer the same as the path overloads do for the same file. A resolver that holds its fonts in memory no longer needs a file, or a font library, to describe them. (#295)
- **`InMemoryFontResolver` serves fonts from bytes the app supplies, and never touches the file system.** It is the font resolver for Blazor WebAssembly, where `FontResolverBase` throws on a platform it does not know and there are no font files to find anyway. `AddFont(byte[])` reads each font's family and style from the font itself, and a TrueType collection adds every face it holds. `AddFont(Stream)` is for an embedded resource; in a browser, fetch a font with `await http.GetByteArrayAsync(...)` and add the bytes, because a browser `HttpClient` stream cannot be read synchronously. A face is chosen by the same rule `FontResolverBase` uses, so only a missing bold or italic is simulated, and `BoldSimulation` says how. `AddAlias("Arial", "Liberation Sans")` lets a document that asks for Arial still get a font. `DefaultFontName` is the first family added unless it is set, and an unknown family falls back to it in the style asked for, unless `NullIfFontNotFound` is set. With no fonts added, it throws an `InvalidOperationException` that says to add one. It needs no imaging backend. Add every font before a document uses one: the library asks about each family and style once and keeps the answer. (#297)

### PDF Reader & Writer

#### Changed

- **Opening an AES-encrypted document where the runtime has no AES now fails with a `NotSupportedException` that says so.** .NET on WebAssembly, which is what Blazor WebAssembly runs on, provides no AES. There `Aes.Create()` still returns an instance, and asking it for an encryptor or a decryptor throws a `PlatformNotSupportedException` whose message names neither the document's encryption nor a way round it (in a trimmed app it is only the resource key `Cryptography_AlgorithmNotSupported, Aes`), and that is what opening a document encrypted with revision 4 of the standard security handler (AES-128) or revisions 5 and 6 (AES-256) threw. The new message names the encryption and the platform, and says to open the document on a server or desktop, or to use a copy that is not encrypted or is encrypted with RC4. The runtime's exception is its `InnerException`. RC4-encrypted documents still open on every platform, and saving needs no AES, because PdfPinata encrypts only with RC4. Where the runtime has AES, nothing changes. (#296)

### Documentation & Samples

#### Added

- **`src/SampleBlazorWasm` is a Blazor WebAssembly app that builds a PDF in the browser, with no server.** It uses `PdfPinata.ImageSharp` and `InMemoryFontResolver`, fetching Liberation Sans from `wwwroot/fonts` at startup, which is the only place it registers anything. **Generate** lays out one page with PinataLayout, with text, a table, an image and a chart, saves it with `Save(Stream)` and downloads it through JavaScript interop. **Open AES-256 document** opens a bundled AES-256 PDF and shows the message the browser gets. It builds and publishes with the plain .NET SDK, with no `wasm-tools` workload, because ImageSharp is managed code. Publish it with `dotnet publish src/SampleBlazorWasm -c Release` and serve the `wwwroot` folder of the output from any static file server. (#298)

## [0.4.3] - 2026-10-14

### Fonts & Text

#### Added

- **`LineBreakAlgorithm` is the Unicode Line Breaking Algorithm, UAX #14, for Unicode 17.0.0.** It answers where a line may be broken in a text: at every position (`BreakOpportunities`), at one position (`BreakBefore`, `IsBreakAllowed`), or where two pieces of text meet (`IsBreakAllowedBetween`), without copying them into one string. It passes all 19,338 cases of the Unicode `LineBreakTest.txt`. `LineBreakTailoring.SouthEastAsianAsIdeographs` lets a line break between any two Thai, Lao, Khmer or Myanmar letters, as between two ideographs, because the default rules keep a whole sentence of those scripts together when there is no dictionary. `LineBreakTailoring.HangulAsAlphabetic` keeps the syllables of a Korean word together, so that Korean breaks only at its spaces. Like the bidi algorithm, it is in the core package and needs no font or backend. (#284)
- **`UnicodeProperties.LineBreakClassOf` gives the Line_Break class of a code point**, as the `LineBreakClass` enum. The table is generated from the Unicode 17.0.0 database, like the Bidi_Class and Script tables, so every target framework gives the same answer. (#284)

#### Fixed

- **A font subset whose glyphs are all blank no longer has a `glyf` table of zero bytes.** A subset that keeps only glyphs with no outline has no glyph data, and its `glyf` table was written with a length of zero, at the same offset as the `head` table after it. Liberation Sans Italic and Bold Italic draw `.notdef` as a blank, so italic text made only of spaces, or only of characters the face has no glyph for with no `FontFallback` set, was enough. On a Debug build this failed an assertion. On a Release build the PDF rendered, but FreeType, which most renderers read embedded fonts with, takes a zero-length table for a missing one; Windows refuses a font whose `glyf` has no bytes; and the OpenType Sanitizer in Chrome and Firefox rejects any zero-length table. The table now holds one zero byte that no `loca` entry reaches, as fontTools writes it, so every glyph is still blank. For the same reason, a `cvt `, `fpgm` or `prep` table that has no bytes in the face is now left out of the subset, where it was copied with a length of zero. (#291)

### PinataLayout & DDL

#### Changed

- **Where one run of formatting meets another, the line breaks only where UAX #14 allows a break.** The renderer used short rules of its own there: join two words unless the first ended in a hyphen or a dash, break beside a CJK or South East Asian letter, and, since #283, keep some opening and closing marks off the ends of a line. It now asks `LineBreakAlgorithm`. So an iteration mark (`々`, `ゝ`), the prolonged sound mark (`ー`), a small kana (`ぁ`, `ッ`) or the middle dot (`・`) in a run of its own no longer starts a line. A dash is no longer broken from a closing mark, `!`, `?`, `,` or `.` after it in another run. A footnote's reference mark stays with what it follows, even after `!`, `?` or `。`. Bookmarks and footnote marks between two runs do not change where the line may break, however many of them there are. A line can now break between two runs after `/`, `!`, `?`, `}` or an ellipsis, between two flags, and before an em dash, where it could not before. A seam beside a Thai, Lao, Khmer or Myanmar letter can still break, and a seam between two Hangul syllables still cannot. Inside one run nothing changes: the line still breaks only at blanks, after a hyphen-minus or a zero-width space, and at a soft hyphen. So `44-1` in one run still breaks after the hyphen, although UAX #14 would keep it together. Moving the breaks inside a run to UAX #14 is a separate change. (#284)

#### Fixed

- **A blank or a bookmark straight after a tab no longer breaks the line.** To decide whether a tab breaks the line, and where a right or centre stop places the text after it, the renderer measures that text. The measuring stopped at a blank or a bookmark as though it were the end of the text. So `"before"`, a tab and `" after"` were set on two lines even when the whole page width was free. And a bookmark inside the text after a right or centre stop shortened what the stop measured, so the rest of the text ran past the stop. Both are now stepped over. A decimal stop followed by a blank still aligns as a right stop. Reported against PDFsharp by @gaspardpetit (empira/PDFsharp#405). (#281)
- **A default tab stop near the right edge is used in a paragraph that does not start at the page's left edge.** The search for the next default stop added the paragraph's left offset to the stop before comparing it with the paragraph's width. So any paragraph with a margin, and any table cell above all, lost every default stop within that offset of its right edge, and a tab that should have reached one broke the line. Reported against PDFsharp by @gaspardpetit (empira/PDFsharp#404). (#282)
- **A Chinese or Japanese closing mark no longer starts a line, and an opening mark no longer ends one, when the mark is in a run of formatting of its own.** The break between two runs is always allowed beside a letter of a script written without spaces. That also allowed it before a closing mark, so in 「**漢字**」 with only the words emphasised, the `」` could start the next line. The run is now joined to the mark, and the two go down to the next line together when they do not fit. This applies after Ps and Pi opening marks, and before Pe and Pf closing marks and the CJK stops `、。，．！？：；`. Upstream lets the mark hang past the margin instead; that is not done here. A run too long for any line is still broken where the runs meet. The full Unicode line-breaking rules are #284. Reported against PDFsharp by @gaspardpetit (empira/PDFsharp#409). (#283)
- **A tab that cannot be set on its line ends that line, instead of indenting the next.** If no stop was left before the margin, or a left-aligned tab's word did not fit beside its stop, the line broke with the tab still to come. The tab then began the next line and moved its text in to the first stop, one stop in from every other line of the paragraph, and the same happened to the first line on a new page. The tab is now the last item of the line it could not be set on and takes no space. Right, centre and decimal tabs still go to the next line, so the text after them stays at their stop, as a table of contents' page number should. Upstream drops the tab only when the item before it is not also a tab; that rule is not used here. A centred line after such a break now centres without the tab's indent. Reported against PDFsharp by @gaspardpetit (empira/PDFsharp#406). (#285)
- **A `Document.DefaultTabStop` of zero, a negative value, NaN or infinity no longer hangs rendering or breaks the line.** The next default stop is found by adding the interval to a position until it passes the pen. Zero never moved, a negative value moved backwards, and a value far below a point took billions of steps, so rendering any paragraph with a tab never finished. Infinity found no stop, and the tab broke the line instead. An interval below 0.001pt, or one that is not a finite number, is now treated as unset, and the default of 1.25cm is used. (#286)
- **The border between two table cells is drawn once, not once for each cell.** Every cell stroked all four of its edges, so each rule inside a table was drawn twice in the same place. A viewer blends the part-covered pixels along a stroke, and blending them twice made every inner rule darker than the rules round the table's outside. Now, when a cell drawn later has an identical stroke on the same edge, the earlier cell leaves its edge out. The later cell keeps it, because its shading is painted after the earlier cell's borders and would cover half of the earlier stroke. Where the two strokes differ in width, colour or style, both are still drawn. An edge shared only in part, such as under a merged cell, is still drawn twice where the two overlap. Reported against PDFsharp by @gaspardpetit (empira/PDFsharp#408). (#287)

### Signatures & Metadata

#### Security

- **`PdfPinata.Signing` now depends on System.Security.Cryptography.Pkcs 10.0.13 rather than 10.0.12.** Every 10.0.x release up to 10.0.12 is covered by two .NET denial-of-service advisories published on 2026-10-13: [GHSA-846f-f764-76mx](https://github.com/advisories/GHSA-846f-f764-76mx) (CVE-2026-84008, high) and [GHSA-2fmv-rv2c-59x7](https://github.com/advisories/GHSA-2fmv-rv2c-59x7) (CVE-2026-70162, medium). (#293)

## [0.4.2] - 2026-10-12

### PinataLayout & DDL

#### Fixed

- **A no-break space typed as U+00A0 at the edge of a run keeps the words either side of it on one line.** A line was kept from breaking at a non-breakable blank only when it was the `SymbolName.NonBreakableBlank` symbol. Text is not split at U+00A0, so one ending a run of formatting (`AddFormattedText("online:\u00A0")` before a hyperlink), starting one, or written as a text of its own sat on a leaf boundary, and the line was broken there like at any other. It now joins the leaves as the symbol does, as do the narrow no-break space (U+202F), the figure space (U+2007) and the non-breaking hyphen (U+2011), and the run is moved down whole when it does not fit. Reported against PDFsharp by @gaspardpetit (empira/PDFsharp#402).
- **A line is no longer broken where one run of formatting meets another with no space between them.** Each leaf was measured on its own and the line broken before whichever did not fit, so "H", a subscript "2" and "O", "bold" and an emboldened "er", a word and the footnote mark, page field or symbol set against it could each be split across two lines. Two adjacent leaves of a word are now joined unless the first ends in a hyphen, a zero-width space or a dash, typed as text or as the symbol, and the joined run goes down to the next line whole when it does not fit; a run too long for any line is still broken at its seams. A seam beside a Chinese, Japanese, Thai, Lao, Khmer or Myanmar letter is still a place to break, as those scripts are written without spaces. A no-break character at the start of a run keeps it joined to an em dash before it, as UAX #14 asks, so the dash and no-break space of French dialogue stay together. (#279)
- **An underline or strikethrough over a blank set in another size is drawn as one rule.** A rule is a sixteenth of the font size thick and was ended wherever that changed, so a smaller space between two underlined words was drawn as three strokes with a thinner one in the middle. A blank or a tab now carries on the rule in progress when only the width would differ; a word in another size still gets a rule of its own thickness. Also from empira/PDFsharp#402.

## [0.4.1] - 2026-10-10

### PDF Reader & Writer

#### Fixed

- **Two LZW streams decoded on two threads at once no longer corrupt each other.** `Filtering.LzwDecode` is one instance for the process, and it kept its string table and read position in fields, so two documents read concurrently could each decode with the other's table and come back short or empty. Each decode now has an instance of its own.
- **A stream whose `/DecodeParms` is not in the shape of its `/Filter` is decoded where there is only one way to read it.** A single filter given its parameters in an array of one, a chain of one given a bare dictionary, and parameters that are all null whatever their count — `/Filter [/FlateDecode] /DecodeParms [null]`, as DocuSign writes it (empira/PDFsharp#399) — now decode. Parameters that could belong to more than one filter of a chain are not guessed at: the stream is left encoded and keeps its `/Filter`, where it used to be handed back undecoded with `/Filter` removed, so a page could be saved still deflated with nothing saying so.
- **The filter singletons on `Filtering` are made once.** They were created on first use with `??=`, which two threads asking at once could each run.

## [0.4.0] - 2026-10-06

### PDF Reader & Writer

#### Fixed

- **A literal string continued onto the next line with a backslash before CR LF no longer keeps the LF.** A CR LF is one end-of-line marker (ISO 32000-1 7.2.3), and the backslash is ignored together with the whole of it (7.3.4.2). Both the document lexer and the content-stream lexer dropped the CR and kept the LF as the first character of the next line. A backslash before a lone CR or a lone LF was already read correctly. (#131)
- **A stream whose `/Length` refers back to itself no longer overflows the stack in `PdfReader.Open`.** An indirect `/Length` is resolved by reading the object it names. When that object was the stream itself, or another stream whose `/Length` named the first, reading it resolved the same length again, until the stack overflowed and the process ended with no exception to catch. Such a length is now unknown, as a missing one is, and the stream is read up to its `endstream` keyword. (#128)
- **Text extraction no longer hangs on a font whose `/W` widths run up to the last `int` code.** A `cFirst cLast w` run was filled in with an `int` counter, which wraps at `int.MaxValue` rather than passing it, so a run ending there never finished and filled a dictionary until memory ran out. The guard against a run claiming more than 65,536 codes computed the span as an `int` too, so a run such as `-1 2147483647` overflowed past it. Both are now `long`, and such a run is either skipped by the guard or filled in and finished. (#137)
- **`PdfReader.Open` in `PdfDocumentOpenMode.Append` opens a file with padding after `%%EOF`.** Append mode looked for the last `startxref` a second time, in the last 2 KB only, and parsed the number in the current culture. So a file whose producer pads the end (SAP, for example; empira/PDFsharp#390) opened in every mode except Append, which threw "The document has no startxref". Append now uses the offset the parser already read. (#164)
- **PDF dates are written in the Gregorian calendar whatever the current culture.** `/CreationDate`, `/ModDate` and an annotation's `/M` were formatted in the current culture, so under th-TH, for example, they carried the Buddhist year 2569. (#160)
- **An XMP date of `DateTimeKind.Unspecified` carries the local offset, so it describes the same instant as `/Info`.** `/Info` wrote the local offset for such a date and XMP wrote none, and PDF/A requires the two to agree. Both now take the offset from one computation. (#160)
- **An array's `GetBoolean`, `GetInteger`, `GetReal`, `GetString` and `GetName` read the null object as no value**, whether it is written out or referred to, as a dictionary's already did, instead of throwing `InvalidCastException`. Both collections do the same for a reference that has no object behind it. (#157)
- **`GetReal` on a dictionary or an array accepts every kind of number**, including the `PdfLong` the reader makes of an integer outside the range of an `int`. `GetInteger` accepts an unsigned or long integer, direct or indirect, when it fits in an `int`. A dictionary's `GetInteger` used to wrap an unsigned integer above `int.MaxValue` round to a negative number; it now throws `InvalidCastException`. (#157)
- **A named destination or an embedded file whose name-tree key is an indirect string is listed and can be resolved.** The key was not followed, so the entry was invisible. (#157)
- **The reader checks all 32 bytes of `/U` for a revision 2 (40-bit RC4) password**, as Algorithm 6 of ISO 32000-1 requires, where it checked only the first 16. A revision 2 file whose `/U` is damaged in its second half no longer opens with either password. Revisions 3 and 4 still compare the first 16 bytes, which are all those revisions define. (#198)
- **The document lexer reads a literal string's escapes as the content-stream lexer does.** It took any digit after a backslash as octal, so `(8)` threw where ISO 32000-1 7.3.4.2 reads U+0001 followed by `8`, and a backslash on which the file ended added U+FFFF to the string. Both lexers now read escapes from one table, which is Table 3 of ISO 32000-1. (#224)
- **A stream is written deflated only when deflating makes it shorter.** Forms, fonts, `/ToUnicode` maps, images and their masks, object streams and cross-reference streams were deflated whatever the result, so an empty form was written as eight bytes of zlib framing, which Acrobat reports as an error. Such streams are now written unfiltered. Only content streams kept this rule before. (#222)

### Pages & Documents

#### Breaking

- **`PageSize.Post`, `PageSize.Elephant` and `PageSize.RA5` have the dimensions of their sheets.** Post is now 1116 × 1386 points rather than 1126 wide, and Elephant 1656 × 2016 rather than 1565 wide (the digits were transposed). RA5 is 434 points wide rather than 433, because 153 mm had been truncated rather than rounded. All three now match the DOM's `PageFormat`, and a test keeps the two tables in step. Documents using these sizes get a different page size. (#161)

#### Added

- **`PdfDocument.CanSave()` returns a `PdfSaveCheck`.** Its `CanSave` says whether the document can be saved and its `Reason` says why not, or is null when it can. It replaces `CanSave(ref string message)`, which is now deprecated.

#### Fixed

- **LINQ over a page's `PdfContents` now yields the `PdfContent` streams instead of their `PdfReference`s.** `foreach` already gave `PdfContent`, but enumeration as a `PdfArray`, through `IEnumerable<PdfItem>` (what LINQ sees) or through plain `IEnumerable` gave the references underneath, so `page.Contents.OfType<PdfContent>()` was empty and `Cast<PdfContent>()` threw. Every way of enumerating the array now yields the same content streams, as `PdfAnnotations` has since #81.
- **`PdfPage.Resize` no longer hangs on a malformed `/Dests` name tree or outline that leads back to itself.** The resize walked the name tree with a depth cap but no record of the nodes it had visited, so a `/Kids` naming its own node took 2^33 steps. It now uses the guarded `PdfNameTree` walk. The outline walk beside it hung the same way on an item that is its own `/First` and `/Next`, and now enters each item once. (#165)
- **`PdfPage.GetImagePlacements` reads a form `/Matrix` whose numbers are indirect**, instead of replacing it with the identity, so a flipped or scaled image drawn through such a form is reported as it is drawn. (#157)

### Drawing & Graphics

#### Breaking

- **`XColor.GS` means how light a colour is, 0 for black and 1 for white, whichever way the colour was built.** A colour built from RGB held its darkness there, while one built from CMYK or grey held its lightness, so `XColor.FromArgb(255, 255, 255).GS` was 0 and `XColor.FromGrayScale(1).GS` was 1. Code reading `GS` from an RGB colour now gets the other end of the scale. An RGB black declared grey was written to a form field's `/MK` background or border as white, and is now written as black. Black, white and pure red now compare equal whether they were built from RGB, CMYK or grey. (#187)

#### Fixed

- **An arc with a sweep of 0 is drawn as a single curve that stays at its start, and always returns.** `XGraphics.DrawArc` and `XGraphicsPath.AddArc` never returned for a zero sweep starting at exactly 360 or -360: the quadrant the arc ends in came out as 4, and the walk through quadrants 0 to 3 kept adding curves until the process ran out of memory. Off a quadrant edge, such as a start of 45, both control points were 0/0 and the content-stream writer refused the NaN, so `DrawArc` threw at once and a path holding the arc threw when it was drawn. On any other quadrant edge the arc was cut as though it crossed that edge, so a start of 90 drew the whole ellipse. A zero sweep, or one too small to move the start angle (such as float cancellation leaves), is now one piece from its start to its start, whose control points lie at that point. Arcs with a non-zero sweep are unchanged. (#129, #130)
- **`Code3of9Standard` refuses an apostrophe and the `*` start/stop character when the code is set.** The check accepted `'` and the lookup did not know it, so drawing the barcode threw `IndexOutOfRangeException`; the apostrophe is not a Code 39 character. A `*` inside the data, including the `"*ABC*"` convention of Code 39 fonts, drew a second delimiter that stops a scanner reading, because the barcode draws its own start and stop characters. Both now throw `ArgumentException` when the code is set. Pass `"ABC"` rather than `"*ABC*"`. (#158)
- **`quality: null` on `ImageSource.FromFile`, `FromBinary`, `FromStream`, `SkiaImageSource.FromSkiaBitmap` and `ImageSharpImageSource.FromImageSharpImage` means the default of 75.** The parameter is `int?`, but both backends cast it to `int`, so passing null threw "Nullable object must have a value". (#176)

### Fonts & Text

#### Added

- **`XGlyphSegment.QuadraticTo` converts a quadratic outline segment to the exact cubic.** A custom `IGlyphOutlineProvider` no longer has to write the conversion itself. Both built-in providers use it. (#229)
- **`OpenTypeFontMetadata` is public in the core package, and `FontResolverBase.ReadFontMetadata(string)` uses it by default.** The method was abstract and is now virtual, so a custom resolver no longer has to parse a font file to describe it, and an existing override still compiles and runs. Faces of a collection are still read by the resolver's own overrides. (#228)

### Annotations & Forms

#### Breaking

- **`PdfAcroField.PdfAcroFieldCollection` is an `IReadOnlyList<PdfAcroField>` and no longer a `PdfArray`.** A form's `Fields` and a field's `Fields` are views of the array underneath, so LINQ over them is typed: `form.Fields.Single(f => f.Name == "x")` is a `PdfAcroField` with no `Cast`. The collection has no `Elements`; the array as the file has it is `form.Elements.GetArray(PdfAcroForm.Keys.Fields)` or `field.Elements.GetArray(PdfAcroField.Keys.Kids)`. Reading a field's `Fields` no longer writes an empty `/Kids` into the field; the array is made when the first field or widget is added. (#146)
- **A check box's state is its field's `/V`, and every widget shows it.** `PdfCheckBoxField.Checked` used to read the first widget's own `/V` whenever the field had widgets under `/Kids`. A field with exactly two widgets was ticked by turning the first on and the second off, unticked the other way round, and given no value of its own; a field with three was not changed at all. A reader goes by the field's value, so what it showed and what `Checked` read could disagree. Now `Checked` reads the field's `/V`, inherited if need be, and the setter writes it and sets each widget's `/AS` to that state where the widget's appearances offer it and to `/Off` where they do not (ISO 32000-1 12.7.4.2.3). A box drawn twice is ticked in both places. A file with no `/V` anywhere is read by its widgets' `/AS`, and a `/V` left on a widget is removed when the state is set. (#146)
- **`PdfCheckBoxField.CheckedName` is read-only and `UncheckedName` is gone.** Both were properties nothing read. `CheckedName` now answers the on state the widgets' appearances name, which is what `Checked = true` writes; the off state is always `/Off`. (#146)
- **`BackColor` and `BorderColor` moved from `PdfTextField` to `PdfAcroField`.** Source that sets them compiles unchanged, but an assembly compiled against the old properties throws `MissingMethodException` until it is recompiled. (#153)
- **A field's `Fields` lists the fields nested under it and no longer its widget annotations.** A field's `/Kids` holds both kinds (ISO 32000-1 12.7.3.1), and the collection returned every kid as a field, so a widget came back as a nameless `PdfTextField` or `PdfCheckBoxField`. A kid is now a widget when it has `/Subtype /Widget`, a `/Parent` and neither `/T` nor `/Kids`, and `Fields`, its `Count`, its indexer and its enumerator skip it. A position given to the indexer therefore counts fields only, and is not the position in the array underneath (`field.Elements.GetArray(PdfAcroField.Keys.Kids)`) when a field has widgets. The widgets are `PdfAcroField.Widgets`. Code that reached a field's widget as `field.Fields[0]` reads `field.Widgets[0]` now. (#146)

#### Added

- **Every field has `BackColor` and `BorderColor`, and they are written to each widget's `/MK` as well as drawn.** They were properties of `PdfTextField` alone, drawn into the appearance stream and nowhere else. `/MK` is what a viewer builds a field from when it does not show that stream. pdf.js, in Firefox, lays its own input over every text and choice field and styles it from `/MK`; Acrobat does while a field has the focus; and every viewer honouring `/NeedAppearances` rebuilds each field from it. The colours therefore disappeared in all of them, and fields showed without a box. Setting a colour now writes `/MK /BG` or `/MK /BC` (with a solid one-point `/BS` for a border) on every widget, including widgets added later, and removes the entry when set back to `XColor.Empty`. **`PdfPushButtonField.Caption`** writes `/MK /CA`. (#153)
- **`PdfComboBoxField` and `PdfListBoxField` draw their own appearance.** A combo box shows its chosen option, or the text typed into an editable one. A list box shows its options from the new `TopIndex` (`/TI`) down, with the chosen rows highlighted. Both draw in the size and colour `DefaultAppearance` names, and have `Font`, `ForeColor`, `BackColor` and `BorderColor` as a text field does; left unset, the background and border come from each widget's `/MK`. Before, both left the drawing to the reader through `/NeedAppearances`, which Ghostscript, print pipelines and most previewers ignore, so the value was set and not shown. PDF 2.0 deprecates that flag and PDF/A forbids it. A field read from a file is redrawn only when it is changed, not on save. (#151)
- **`PdfAcroField.Widgets` lists the widget annotations a field is drawn as**, typed as `PdfWidgetAnnotation`: the widgets under its `/Kids`, or, for a field merged with its only widget in one dictionary, that widget. **`PdfWidgetAnnotation.Field`** goes the other way, and **`PdfAcroField.IsTerminal`** says whether a field has no fields nested under it. (#146)
- **`PdfAcroField.PdfAcroFieldCollection` has a `Count` and a typed enumerator.** A form's `Fields` and a field's `Fields` can now be counted without LINQ, and `foreach (var field in form.Fields)` is typed as `PdfAcroField`, yielding exactly what the indexer returns.

#### Fixed

- **Reading a form's fields no longer takes their widgets away from the page.** Typing a widget as a field pointed the dictionary's reference at the new field object, so `page.Annotations[i]` stopped being the widget `AddWidget` had returned, and reading the fields and the annotations in turn made a new object each time. A value written through the widget-as-field went into the widget's `/V`, where no reader looks, and the field kept none. A field and an annotation can no longer be made out of one another; attempting it throws `InvalidOperationException`. (#146)
- **A field merged with its widget in one dictionary is one field object and one widget object, whichever is read first.** Such a dictionary is canonically the field, and `page.Annotations` and `field.Widgets` give a view of it that shares its entries and takes nothing over. Before, reading the page after the field retyped the dictionary as an annotation, and the field object made first then threw `NotImplementedException` for `/Kids` when asked for its `Fields`. (#146)
- **`AddWidget` on a field merged with its widget separates the two first.** A field read from a file can be one dictionary with its only widget, and adding a second widget beside it wrote a field that was an annotation with widget kids, which is not valid PDF. The first widget's entries now move into a dictionary of their own under `/Kids`, which takes the field's place in the page's `/Annots`, and the widget object a caller already held is that dictionary from then on. (#146)
- **A text field's value no longer jumps when the field is clicked into.** A viewer edits a field in the font size and colour its `/DA` names, and centres one line vertically; the appearance the library drew used a fixed 10 points, black, pinned to the top left, so the text moved and changed size as the field gained and lost the focus. `Font` and `ForeColor` now default to what `/DA` names (the field's own, an ancestor's or the form's), and one line is centred vertically two points in from the side. The same drawing now wraps a `MultiLine` value instead of running it off the side, puts a `Comb` field's characters one to each of `MaxLength` cells, draws a `Password` field's value as asterisks instead of in the clear, and aligns to `/Q`. (#155)
- **Setting `Value` on a text field or a list box redraws it.** `Text`, `SelectedIndex` and `SelectedIndices` redrew the field and `Value` did not, so a form filled through `AcroForm.Fields[name].Value` kept showing what it showed before in any viewer that draws the appearance stream. A list box also drops the `/I` that named the rows chosen before, and writes a new one for a multiple-choice list. (#151)
- **A malformed colour in a field's `/DA` no longer makes a choice field throw.** `/Helv 9 Tf 1 rg` names too few numbers for `rg`, and was parsed as though it did not, so every setter that redraws the field threw `FormatException`. An operator given the wrong number of operands is read as black. (#151)
- **A choice field with no font resolver registered no longer throws from its setters.** It draws its own appearance since #151, which needs a font; with none set on the field and no resolver, the value is written and the appearance left to the reader, as before. (#151)
- **`PdfTextField` no longer draws its value into the fields nested under it.** It drew into every kid with a `/Rect`, which included nested fields merged with their widgets, over their own values. It draws into its widgets alone. (#146)
- **LINQ over a form's fields, or a field's kids, now yields the typed `PdfAcroField` objects instead of `PdfReference`s.** The collection's indexer returned `PdfTextField`, `PdfCheckBoxField` and the rest, but every enumeration yielded the references underneath, so `OfType<PdfAcroField>()` was empty and `Cast<PdfAcroField>()` threw. Every enumeration now yields the same objects the indexer does, typing each field on the way as the indexer always has.
- **`PdfDocument.MakeAcroFormsReadOnly` walks the form's fields once.** It counted them with LINQ's `Count()` in its loop condition, enumerating the whole collection again on every iteration.
- **`PdfOutlineCollection.Insert` accepts an index equal to `Count` and appends the outline.** `IList<T>.Insert` requires this, but the collection threw `ArgumentOutOfRangeException`, including for `Insert(0, outline)` on an empty collection. The outline is now placed in the tree exactly as `Add` places it, and is saved at the end of its list.
- **An outline's colour no longer loses a level when the document is saved and reopened.** `/C` was read by truncating each component, so 127, written as `0.498`, came back as 126. It is now rounded, as an annotation's colour already was. A component outside 0 to 1 in an outline or annotation colour now reads as the nearest valid value, where it used to throw. (#159)
- **A highlight, underline, squiggly or strike-out annotation stamps its modification date when its quadrilaterals change.** `AddQuad` and `ClearQuads` redrew the annotation without touching `/M`, although a redaction's `AddQuad` already stamped it. (#197)
- **A text markup annotation left with nothing to mark removes its appearance.** With no quadrilaterals and an empty rectangle it kept showing the last appearance it drew; every other annotation that draws itself already removed its `/AP` when asked for nothing. (#197)
- **A negative `BorderWidth` is refused the same way on every annotation that draws itself.** Line, Square, Circle, FreeText, Ink, Polygon and PolyLine now all throw `ArgumentOutOfRangeException` with the message "A border cannot be narrower than nothing." and `ParamName` `value`. Ink, Polygon and PolyLine used to name `width`, and Line said "A line cannot…". (#197)
- **An annotation that draws its own appearance shows its redraw after a caller's `SetAppearance`.** A highlight, underline, squiggly or strike-out kept drawing into a form `/AP` no longer named, so a change of colour, opacity or quadrilaterals never showed. A redraw also keeps a rollover (`/R`) or down (`/D`) appearance the caller added, on every annotation that draws itself: it replaces only the normal appearance, and leaves `/AS` alone while one of those is a set of states. (#210)
- **A text field reads `/MaxLen` from its parent when it has none of its own**, as ISO 32000-1 makes it inheritable, so a comb field grouped under a parent is drawn in its cells rather than as plain text. (#205)
- **A comb field puts one character in each cell, and a password field draws one asterisk a character, counting characters as the reader sees them.** A character outside the Basic Multilingual Plane, or a letter with a combining mark, counted as two. (#206)
- **A text or choice field whose widget is under a point in either direction removes its appearance** rather than keeping a stale one, and a widget whose `/Rect` names its corners the other way round is drawn rather than treated as too small. (#211)
- **A button's on state is read from its widgets.** The protected `PdfButtonField.GetNonOffValue()` looked in the field's own `/AP`. A field whose widgets are separate from it, which is the shape `AddWidget` always makes, has none, so it answered `/Yes` whatever its widgets named. It now reads the widgets and gives the same answer as `PdfCheckBoxField.CheckedName`. (#265)
- **A long run of digits in a field's `/DA` no longer hangs the setters that redraw the field.** The patterns that read the font size and colour out of `/DA` could split a run of digits between two quantifiers in as many ways as it had digits, from every digit it had, so five thousand of them took a minute and a half to fail to match, and did so again on every redraw. `/DA` is read from the file, so a small document could stall any code that touched its form. A number is now matched in one way only, and a megabyte of digits takes well under a second. A malformed number such as `1.2.3` before `Tf` is no longer read from its middle as `.3`; the size falls back to 10 points as for any `/DA` that names none. (#272)

### Signatures & Metadata

#### Added

- **`PdfSignatureVerification.IsTimestampIntact` says whether a signature's timestamp token is intact**: that the token's own signature verifies over its content, with the certificate the token carries (or, if the authority left it out, one the signature itself carries), and that its message imprint is the hash of this signature's value. It is null when the signature carries no token, or is too malformed to read one out of. It is answered even when the document has changed since signing, because the token covers the signature's value rather than the document. The verifier used to read the time out of the token without either check, so a damaged token, or one copied from another signature, reported a time the bytes do not support. `Timestamp` and `HasTimestamp` now report only an intact token's time; a broken token, or a signature-timestamp attribute that is not a token at all, gives `IsTimestampIntact` false and `Timestamp` null. The signature's own `IsIntact` and `IsValid` are unaffected, because the token lies outside what the signature covers. As before, nothing here decides whether the time-stamping authority is trusted. (#209)

#### Fixed

- **`Rfc3161TimestampProvider` no longer reads a response of any size, and its own client no longer follows redirects.** It read the whole body into memory with no limit and followed a 3xx to wherever it pointed. It now reads through the same guarded exchange as `OcspRevocationDataProvider`: headers first, then at most 1 MiB of body, with the client's `Timeout` over the whole exchange, body included. A larger or slower response fails the signing with an `InvalidOperationException` naming the authority. The client the provider makes for itself follows no redirect, because the caller named the authority it trusts; if your authority has moved, pass its new URI. A caller that supplies its own `HttpClient` keeps its own redirect policy. The OCSP provider also gains the timeout over the body, which it lacked before. (#166)
- **`Rfc3161TimestampProvider` asks the authority to put its certificate in the timestamp token** (`certReq`, RFC 3161 §2.4.1), so the timestamp can still be checked when the certificate is nowhere else. Tokens grow by a few kilobytes. A token whose signature does not verify now fails the signing, and so does one from an authority that ignores the request, which §2.4.2 does not allow. `PdfSignatureValidationData` stores the authority's certificates in `/DSS` and asks the revocation provider about them. (#193)

### Charts

#### Breaking

- **A chart line format that is not `Visible` is no line, on every axis and every element.** `Converter.ToXPen` turns such a format into a pen of width 0, which PDF draws as the thinnest line the device can. Only a column chart's value axis checked the width, so setting just `XAxis.LineFormat.Width` drew a hairline on the category axis and on a bar chart's value axis and nothing on a column chart's; tick marks, gridlines, the zero baseline and the legend border checked nothing. A pen of width 0 or less is now dropped wherever it is drawn, and a hidden legend border no longer takes up padding. A gridline format naming only a colour is therefore no longer drawn: set `Visible = true`, as a series already needed. (#173)

#### Added

- **The charting `LineFormat` has a `DashPattern`, so a chart line set to `XDashStyle.Custom` is drawn dashed.** The format had a dash style but no pattern, so `Custom` reached the page as `[] 0 d`, a solid line, for every series, point, axis and gridline. `DashPattern` takes dash and gap lengths in units of the line width, as `XPen.DashPattern` does. Setting it sets `DashStyle` to `Custom`, and a length that is not finite and greater than zero is refused with `ArgumentException`. A point that says nothing about its dashes takes its series' pattern with its series' style. `Custom` with no pattern is still drawn solid, as an `XPen` is. PinataLayout's chart `LineFormat` has no custom dash style, so nothing reaches this through the mapper. (#208)

#### Fixed

- **The charting `XSeries` implements `IEnumerable`, so LINQ can be used over it.** It had a public `GetEnumerator` and no interface, so `foreach` compiled and every LINQ operator, `Cast` included, did not. It is non-generic like the package's other collections: `Cast<XValue>()` yields a blank as null and `OfType<XValue>()` leaves it out.
- **A data point's own `LineFormat` is applied the same way on column, bar and pie charts, and only once the caller has set it.** Each renderer turned it into a pen differently. A bar or pie point that sets only a width is now drawn at that width, a pie point is outlined at its own width rather than 1, and a pie point set not to be `Visible` is no longer stroked. Merely reading `Point.LineFormat` no longer takes the point's border away, which it always did on a column chart. An area chart is outlined once per series and takes no notice of a point's line format. (#171)
- **Stacked column and stacked bar charts with an explicit `MinimumScale` or `MaximumScale` agree on which segments to draw.** A segment is drawn only when the whole of it lies on the scale, from where it starts on its pile to where the pile reaches with it, and blanks are skipped. Stacked columns used to be drawn past the plot area, and stacked bars were shown or hidden by the segment's own value rather than by where its pile reached. Scales worked out from the data are unaffected. (#168)
- **A column or bar left out of a chart for being off the scale no longer has its data label drawn**, whether the chart is stacked or clustered. The label was written wherever the missing column would have ended, often outside the plot area. (#168)
- **On a scale entirely below zero, a clustered bar starts at the scale's minimum even when the bar before it is off the scale.** The start was carried from one bar to the next, so a bar after one that fell off the scale started from that bar's value, as a clustered column never did. (#170)
- **An exploded pie leaves a gap between its wedges.** The 2° gap only turned the whole pie, because `Math.Max` always returned the full sweep. Each wedge now gives up the gap, half from each side. A share narrower than two gaps keeps half of itself, and a single share is drawn whole. (#169)
- **A chart too small for its axes no longer strokes a border around a plot area that has no room**, just as it draws no wall there. (#172)
- **A null value-axis title caption is treated as no caption.** It threw `ArgumentNullException` while a column or bar chart was drawn; the category axis already skipped it. (#174)
- **A pie chart's legend no longer throws on an empty category collection or a blank category.** The first now numbers the entries and the second gives that entry no text, as the category axis does. (#175)
- **A bar chart labels a zero on the positive side of the axis at `InsideEnd` and `OutsideEnd`**, as a column chart does. It put the label on the negative side. (#196)
- **A clustered column or bar chart no longer throws `ArgumentException` for a value between zero and a minimum the caller set above zero.** Such a value, 1 on a scale from 2 to 5 for example, is left undrawn, as any value off the scale is. (#196)
- **A stacked column chart whose value axis has its minimum above its maximum draws nothing rather than throwing**, as a stacked bar chart already did. (#196)
- **A chart point that sets only its line width or colour keeps its series' dash style**, on column, bar and pie charts and through PinataLayout's chart mapper. It was drawn solid, because the point's unset dash style could not be told from `Solid`. A point that sets `DashStyle = Solid` is still drawn solid. (#192)
- **A chart whose value axis is given the same `MinimumScale` and `MaximumScale` is drawn**, on every chart type and through PinataLayout; it threw for a NaN in the content stream. The scale is widened as a flat range of data is, by a tenth of the value beyond 1e12, without moving a value the caller set, and two ends the caller gave are compared exactly, so a minimum above the maximum still draws nothing. A value axis also no longer drops its top tick label or gridline to rounding, and a `MajorTick` of zero no longer hangs the draw. (#207)

### PinataLayout & DDL

#### Breaking

- **`Unit` implements `IEquatable<Unit>` (source).** Comparing units no longer boxes or uses reflection, and `GetHashCode` now hashes all three things `Equals` compares. Because `Unit` converts implicitly from `string`, `int` and `double`, `unit.Equals("3")` now parses the string and answers true for 3pt, as `==` always did. `unit.Equals(null)` now throws `ArgumentNullException`, as `unit == null` already did. Cast to `object` to keep the old answer, or test `IsEmpty`. `==` and `Equals` now always agree, so a unit holding NaN equals itself.

#### Fixed

- **A PinataLayout image whose source fails to open with anything but an `InvalidOperationException` gets a placeholder.** The exception used to escape formatting and end the whole render. It is now reported through `ImageFailed` as `ImageFailure.NotRead` and a placeholder is drawn, as it already was for the same exception thrown while the image was drawn. An `InvalidOperationException` is still reported as `InvalidType`. (#131)
- **A list that starts a new section is tagged in that section.** When a section ended with a list and the next began with one of the same kind and level, the tagger saw that the list's parent had changed, closed the old list and opened a new one, but under the previous section's `/Sect`. So the new section's list appeared in the structure tree inside the section before it. It is now opened under its own section. (#137)
- **An object added to a PinataLayout collection through its non-generic `IList` belongs to that collection.** `IList.Add`, `Insert`, `Remove` and the indexer setter on `DocumentObjectCollection` went straight to the list underneath. They set no parent, reset no cached values, and skipped what a collection does on `Add` or `InsertObject`. So `((IList)table.Rows).Add(row)` gave the row no cells, and `Styles` accepted a paragraph. They now go through the typed members, as the charting collections already did. One thing a caller can see: an argument that is not a `DocumentObject` now throws `ArgumentException` from `Add`, `Insert` and the indexer. It used to be stored and throw later, on read. `Contains`, `IndexOf` and `Remove` still answer false, -1 and nothing for one. A null is still accepted, as the typed members accept it.
- **An underline or strikethrough whose dash style changes between adjacent runs is drawn in each run's own style.** The two pen comparisons that decide where a rule ends compared colour and width only, so a dotted run followed by a dashed one was drawn as one dotted line. (#162)
- **An upward text frame restores every graphics state it saves.** It saved the state twice and restored it once, so everything drawn after it on the page ran one `q` deeper. (#163)
- **A PinataLayout chart no longer gives every point an empty line format.** The mapper wrote one for every point, always with a solid dash, so a dashed series' columns, bars and sectors were outlined solid. A point's line format is now mapped only when the document set one. (#171)
- **A CMYK colour in a PinataLayout document drawn in RGB has the RGB that `XColor.FromCmyk` gives.** The DOM's `Color` used its own copy of the conversion, which rounded black up by half a level, so about half of all K values came out one level darker. K = 0 is one of them, so CMYK white was 254, 254, 254. (#186)
- **A field's name and format are escaped when a document is written as DDL.** A page reference field's name, and the format of any field, were written as they stood, so one containing a quote or a backslash produced DDL that could not be read back. Both are now escaped as every other string is, and the output is unchanged for a name or format that needs no escaping. (#234)
- **A list in tagged output is no longer split in two by furniture.** An undescribed chart, a chart's title, and the running head of a page across which a list breaks each ended the open list, so the structure tree had two `/L` elements where the document had one list. An undescribed image already left the list open. Nothing drawn as an artifact now ends a list. (#227)
- **A field of a type derived from one of PinataLayout's field types is rendered.** The paragraph renderer chose a field's renderer by its type's name, so a subclass of `PageField`, for example, was silently left out. (#221)
- **A footnote no longer splits a tagged list that runs on to the next page.** A footnote is drawn at the foot of the page, which is where the page breaks, and tagging its paragraphs ended the open list, so the list's items on the next page went into a second `/L`. A list inside the footnote split it the same way. A footnote is now drawn with the body's open list set aside and put back afterwards, and a list inside it is the note's own `/L`. (#262)

### API & Packaging

#### Deprecated

- **`PdfDocument.CanSave(ref string message)`.** Use `CanSave()`, which returns a `PdfSaveCheck` carrying the reason as well as the answer. It behaves as before and will be removed.

#### Removed

- **`PdfDocument(string filename)`.** It built a document and then threw `NotImplementedException`, so no caller ever got one from it. Use `new PdfDocument()` and `Save(path)`.

#### Fixed

- **Every package is built with `Microsoft.Sbom.Targets` 4.1.12.** Each project's override to 4.1.12 was evaluated before the shared reference it meant to change, so it never applied and every package was built with 4.1.5. The version is now set once in `Directory.Build.targets`. (#167)

### Documentation & Samples

#### Fixed

- **The Inspect demo prints a PDF number with a full stop on every machine.** It formatted operands in the current culture, so on a German machine an operand of `0.5` was shown as `0,5`. (#238)

## [0.3.0] - 2026-09-22

### PDF Reader & Writer

#### Added

- **RunLengthDecode streams are now decoded and encoded.** `/RunLengthDecode` and its `RL` abbreviation are supported through `Filtering.GetFilter` and `Filtering.RunLengthDecode`, following ISO 32000-1 7.4.5. Truncated streams return the data decoded so far rather than throwing.

- **TIFF Predictor 2 streams are now decoded.** Flate and LZW streams using `/Predictor 2` are supported for 1, 2, 4, 8 and 16 bits per component, with any number of colours. Sixteen-bit samples are treated as big-endian and each row restarts prediction. (#79)

- **Imported pages preserve more page-level PDF state.** Page import now keeps `/Group`, `/UserUnit`, `/Tabs`, `/Trans` and `/Dur` in addition to the resources, contents, boxes, rotation and annotations already imported. `/UserUnit` raises the output to PDF 1.6 and `/Tabs` to PDF 1.5 where required. `/Tabs /S` is deliberately dropped because page import does not bring the structure tree with it. Entries tied to source-document structures, including `/StructParents`, `/B`, `/AA`, `/Metadata`, `/PieceInfo` and `/SeparationInfo`, are also deliberately left behind. (#86)

- **`ContentReader` preserves inline images when content is written back.** Inline images are now represented as `CInlineImage` and emitted as `BI … ID … EI`, preserving their dictionary entries and raw data. The end marker is still detected by scanning for `EI`, so binary data containing those bytes can still be misidentified. Truncated inline images no longer make the parser loop indefinitely. (#84)

- **Font embedding restrictions can optionally be enforced.** Set `PdfDocumentOptions.RespectFontEmbeddingRestrictions` to honour the font OS/2 `fsType` embedding permissions. Restricted License and Bitmap Embedding Only faces are refused; No Subsetting faces are embedded whole. Preview & Print, Editable and Installable faces remain embeddable. The option is off by default. (#78)

- **Resource deduplication is available at save time.** `PdfDocumentOptions.DeduplicateResources` can write a single structurally identical copy of fonts, images, forms, colour spaces, graphics states, patterns, shadings and content streams. Pages, annotations, fields, structure elements, optional-content groups and signatures are never merged. Append-mode documents refuse the option. See `docs/specs/resource-deduplication.md`. (#68, empira/PDFsharp#275)

#### Changed

- **Malformed PDF dates no longer rely on exceptions for normal fallback.** Invalid dates still fall back as before, but parsing no longer throws and catches internally or trips Debug assertions. (#83)

- **Composite-font `/W` arrays are written more compactly.** Consecutive glyph widths are emitted as `c [w1 w2 …]` runs rather than one `c [w]` entry per glyph. Glyph metrics are unchanged. (#81)

- **Names are escaped consistently when written.** Delimiters, `#`, and bytes outside `!` through `~` are written as `#xx`. The underlying name bytes do not change. (#59)

- **`/Producer`, default `/Creator`, and XMP `pdf:Producer` use the actual PdfPinata version.** The value now resembles `PdfPinata 0.2.1 (https://github.com/PinataLabs/PdfPinata)` instead of the old inherited PDFsharp version string. `ProductVersionInfo.Producer` is now a static property and `Producer2` has been removed.

#### Fixed

- **PDF names containing characters beyond Latin-1 are written as UTF-8 bytes.** Previously such names could be corrupted into ambiguous `#xxxx` text. Names read from an existing PDF continue to round-trip byte-for-byte. (#87)

- **Names in content streams are escaped when written back.** A name such as `/A#20B` no longer becomes separate tokens after a read/write cycle, and delimiters inside names are escaped consistently. (#87)

- **Malformed `#` sequences in names no longer abort parsing.** A `#` is treated as an escape only when followed by two hexadecimal digits; otherwise it is kept literally, both in document objects and content streams. (#83, #84)

- **Cross-reference stream offsets beyond 2 GiB are read without truncation.** Offsets now use the full width declared by `/W`, including five-byte fields. (#83)

- **PDFDocEncoding now decodes as well as encodes according to ISO 32000-1 Annex D.** Undefined codes map to U+FFFD and several previously incorrect encoder mappings are corrected. (#82)

- **ASCIIHexDecode honours `>` wherever it appears and rejects non-hex data.** Odd trailing digits are padded with zero as required, and the caller's buffer is no longer modified. (#79)

- **ASCII85Decode rejects `z` inside a partial group.** Such data previously decoded silently out of step. (#79)

- **Indirect `/Filter` and `/DecodeParms` objects are followed correctly.** Indirect parameter arrays and indirect/null elements are also handled. (empira/PDFsharp#323, #59)

- **`CSequence.Add(CArray)` adds the array as one operand.** The array is no longer spread into the surrounding sequence. (empira/PDFsharp#300, #59)

- **Incremental updates involving cross-reference streams are read from the newest revision.** Newer full objects now correctly override older compressed objects, and object-number reuse by cross-reference streams no longer causes stale objects to win. (empira/PDFsharp#353, empira/PDFsharp#213)

- **Hybrid-reference files now load objects described by `/XRefStm`.** The supplemental cross-reference stream is processed alongside the classic table, so compressed objects are no longer silently lost. Under `Strict`, an unreadable `/XRefStm` raises `PdfReaderException`; under `Moderate`, the supplemental stream is ignored and the document opens using only the classic table. See `docs/specs/hybrid-reference-files.md`. (empira/PDFsharp#388)

- **A looping `/Prev` chain no longer hangs `PdfReader.Open`.** `Strict` throws; `Moderate` reads each section once. (#66)

- **Incremental saves preserve valid `/Size` values and do not reuse freed object numbers.** New numbering begins from the largest `/Size` declared by any revision. (#63)

- **Incremental saves to files indexed by cross-reference streams now write a valid appended revision.** The appended revision uses its own cross-reference stream with `/Index` and `/Prev`; classic-table files continue to append in the classic format. (#55)

- **An unencrypted document stays unencrypted when `SecuritySettings` was merely read before `SaveIncremental` or signing.** (#65)

- **Streams assigned through `PdfDictionary.Stream` get a correct `/Length`.** Shared or cloned stream data is rechecked when written, and assigning `Value` on a cloned stream no longer throws `NullReferenceException`. (#93)

- **`PdfInteger` and `PdfUInteger` no longer convert to `DateTime.MinValue`.** `IConvertible.ToDateTime` now throws `InvalidCastException`, matching the underlying integer types. `PdfLong` continues to interpret its value as ticks. (#87)

- **A UTF-16 literal string in a content stream resolves escapes before decoding.** Line continuations no longer shift subsequent UTF-16 bytes or cause the parser to run past the closing delimiter. (#84)

- **Content-stream strings preserve their type on round-trip.** `CString` can now write hex strings, Unicode literal strings and Unicode hex strings, and `CParser` records the original string type instead of treating all strings as plain literals.

### Pages & Documents

#### Added

- **Page box presence and effective values are exposed.** `PdfPage.HasMediaBox`, `HasCropBox`, `HasBleedBox`, `HasTrimBox` and `HasArtBox` report whether each box exists. `EffectiveCropBox`, `EffectiveBleedBox`, `EffectiveTrimBox` and `EffectiveArtBox` apply ISO 32000-1 defaults and clip to the media box. See `docs/specs/page-boxes.md`. (#58)

- **Page lifecycle events are available.** `PdfDocument.PageAdded`, `PageRemoved` and `PageGraphicsCreated` allow consumers to react to page changes or draw recurring page content such as headers and watermarks. A `PageGraphicsCreated` handler runs inside a saved graphics state. (#60)

#### Fixed

- **Reading a missing page box no longer creates it.** Missing boxes still read as empty rectangles, but simply accessing them no longer writes `[0 0 0 0]` into the page dictionary. Invalid box values are treated as missing rather than throwing `InvalidCastException`. (#58)

- **A removed page can be reinserted after a save.** Reinserted pages and their dependent objects receive valid object numbers instead of colliding with numbers reassigned during the earlier save. (#93)

- **Page outline style and colour entries respect the output PDF version.** `/F` and `/C` are written only when the saved version supports them, and `Regular` removes `/F` on the next save.

- **PDF/A-1 now rejects unsupported page-level features.** A page transparency `/Group`, `/UserUnit` or `/Tabs` is refused under PDF/A-1 where those features exceed the profile's PDF 1.4 limits. (#86)

### Drawing & Graphics

#### Added

- **Gradient extension can be controlled.** `XBaseGradientBrush.ExtendLeft` and `ExtendRight` control whether a gradient continues beyond its start and end using the endpoint colour, mapping to a shading dictionary's `/Extend`. Both default to `false`. (empira/PDFsharp#321)

- **Spot colours are supported.** `XSpotColor` defines a named colourant with a DeviceCMYK, DeviceRGB or DeviceGray alternate, and `XColor.FromSpot(spot, tint)` can be used with brushes, pens and text. Each colourant is emitted as one `/Separation` colour space per document. Conflicting definitions of the same name are refused. See `docs/specs/spot-colours.md`. (#67, empira/PDFsharp#201)

- **`XGraphics.BeginContainer` accepts every `XGraphicsUnit`.** The source rectangle is interpreted using the supplied unit; drawing inside the container remains in points.

#### Changed

- **`XImage.FromFile` uses a single overload.** `XImage.FromFile(string, PdfReadAccuracy = PdfReadAccuracy.Strict)` replaces the two previous overloads. Existing source calls still compile; assemblies built against the previous signatures must be rebuilt.

- **`XGraphicsPath.StartFigure` now starts a real new figure.** The following segment no longer joins the previous figure. `AddPath(..., connect: true)` also honours the figure boundary.

#### Fixed

- **Fully transparent pens and brushes remain transparent when they are the first colour drawn or follow a gradient.** Previously an initial alpha-zero state could suppress the required `/CA` or `/ca`. (empira/PDFsharp#281, #59)

- **Dashed pens recalculate dash lengths when the pen width changes.** Standard dash patterns now track the pen width, and identical custom patterns are not redundantly emitted.

- **Appended world transforms render where `XGraphics.Transform` reports them.** Append-order transforms are converted to the equivalent prepend matrix before emitting PDF `cm`. Appending to a non-invertible matrix throws without changing the graphics state.

- **Gradient brush transforms are honoured.** Brush translation, scaling, rotation, multiplication and direct `Transform` assignment now affect rendering. Radial gradients use the pattern matrix when required to represent non-circular transformed rings.

- **`XGraphics.DrawImage(image, destRect, srcRect, srcUnit)` now crops to `srcRect`.** The requested source region is scaled to the destination and clipped correctly.

- **`XGraphics.BeginContainer` maps the source rectangle correctly when the source does not start at the origin.**

- **`XGraphics.WriteComment` comments every line.** Bare carriage returns no longer allow following text to escape the PDF comment.

### Fonts & Text

#### Changed

- **WinAnsi fonts with CFF outlines are no longer labelled as subsets when they are embedded whole.** (#78)

#### Fixed

- **`AddCharacter(char)` draws non-ASCII characters correctly.** Characters such as `é`, `ß`, `Ω` and `€` are no longer reduced to a low byte and decoded as broken UTF-8. Astral code points supplied through `SymbolName` are emitted as surrogate pairs where needed.

- **Non-breaking blanks render as U+00A0 and suppress line breaking around them.** `Character.NonBreakableBlank` / `HardBlank` now occupy normal space width. A token too long for any line can still be broken as a last resort.

- **Undefined `SymbolName` values are rejected.** Values naming no known symbol now throw `ArgumentException` instead of being written as MDDDL that the reader cannot read back. (#92)

### Annotations & Forms

#### Breaking

- **Iterating `PdfAnnotations` now yields `PdfAnnotation` instead of `PdfItem`.** `PdfAnnotations.GetEnumerator` returns `IEnumerator<PdfAnnotation>`, matching the indexer and eliminating the cast normally required in `foreach`. Enumeration through `IEnumerable<PdfItem>` still yields the annotations themselves. (#81)

#### Added

- **Annotations read from a file are wrapped as the class matching their `/Subtype`.** Nineteen annotation subtypes now return typed objects instead of `PdfGenericAnnotation`. The wrapping itself does not rewrite the appearance stream. (#60)

- **More annotation types are implemented.** `PdfInkAnnotation`, `PdfPolygonAnnotation`, `PdfPolyLineAnnotation`, `PdfCaretAnnotation`, `PdfRedactAnnotation` and `PdfPopupAnnotation` are available. Redaction annotations mark regions; they do not remove underlying content. (#60)

- **`PdfMarkupAnnotation` is available as the common markup base class.** It adds `Popup`, `InReplyTo`, `ReplyType`, `RichText` and `Intent`. (#60)

#### Changed

- **`PdfSquareCircleAnnotation.Interior` and `BorderWidth` read directly from `/IC` and `/BS`.** An annotation loaded from a file now reports the values stored in the PDF. (#60)

#### Fixed

- **Annotation colours stored as indirect arrays are resolved correctly.** (#81)

- **Links loaded from existing files no longer gain a zero-width border just because they were saved again.** (#60)

- **Outline collections reject invalid ownership and cycle arrangements.** An outline cannot belong to multiple lists, another document, itself, or one of its descendants. Removing or replacing entries no longer leaves broken links behind, and accessing `document.Outlines` no longer creates an unwritten catalog reference. (#93)

- **Form fields inherit `/FT` and `/Ff` from their ancestors.** Child fields now detect inherited field type and flags, including radio groups, and writing one flag to a child preserves the inherited flags it already had. (#75)

- **Read-only fields reject values consistently across all typed setters.** `Text`, `Checked`, radio/list/combo selection and `SelectedIndices` now enforce the same read-only rule as `PdfAcroField.Value`. (#62)

### Signatures & Metadata

#### Added

- **XMP metadata handling is configurable.** `PdfDocumentOptions.MetadataStrategy` can be `KeepExisting` (default), `AutoGenerate`, or `NoMetadata`. A PDF/A or PDF/UA claim cannot use `NoMetadata`. (#60)

- **Signature-field lock and seed dictionaries are modelled.** `PdfSignatureField.Lock` and `SeedValue` expose `/Lock` and `/SV` through `PdfSignatureFieldLock`, `PdfSignatureSeedValue` and `PdfCertificateSeedValue`. `PdfSignatureOptions.LockAction` and `LockFields` can write `/Lock`, add a `/FieldMDP` reference and mark covered fields read-only in the signed revision. Seed values are persisted but not yet enforced by the signer. (#60)

- **Signature annotation flags are configurable.** `PdfSignatureOptions.AnnotationFlags` writes the widget `/F` value. PDF/A profiles reject flags they forbid. The default remains `Print`. (#65, empira/PDFsharp#157)

#### Changed

- **`PdfDocumentOptions.WriteXmpMetadata` is retained as the older name for `MetadataStrategy = AutoGenerate`.** (#60)

#### Fixed

- **Wrapping a read object in a typed class no longer loses its dirty state.** Incremental saves now include changes made before the wrapper was created, including AcroForm changes used by signature locking. (#60)

### Charts

#### Added

- **Combination charts can contain stacked columns.** `ColumnStacked2D` can be combined with line or area series. The value axis accounts for stacked totals, and the legend orders stacked series from the top of the stack downward. Clustered and stacked columns cannot share one chart and are rejected explicitly. (#77)

#### Fixed

- **Chart text inherits colour, bold and italic correctly.** Axis titles, tick labels, legends and data labels inherit unset font properties from the chart font. An explicit `false` for bold/italic remains explicit. Series data labels inherit from the chart data label before falling back to the chart font. (#101)

- **Series data labels inherit chart-level label properties individually.** Format, position, type and font are now inherited property-by-property instead of a series-level label replacing the chart label wholesale. (#101)

- **Axis-title and tick-label fonts no longer wipe out style-level bold, italic or colour.** Flattening fills unset values from the named style or chart font before mapping. (#101)

- **Chart collections now work through non-generic `IList`.** `Add`, `Insert`, `Remove`, `RemoveAt`, `Contains` and `IndexOf` delegate to the typed members. Invalid values are rejected with `ArgumentException`. (#77)

- **Category labels are no longer repeated once per `XSeries`.** Multi-series charts label the category axis from the first series only, matching Excel and existing pie-legend behaviour. (empira/PDFsharp#286)

- **Wide legends wrap instead of running off the chart.** Top/bottom legends create multiple centred rows, oversized entries wrap within their own entry, and embedded line breaks are honoured. See `docs/specs/charting-renderer-findings.md`. (empira/PDFsharp#306)

- **Hidden chart lines are actually hidden.** A `Visible = false` line format no longer becomes a PDF hairline. The fix applies to line, area, bar and pie outlines and their legend keys. (empira/PDFsharp#287)

### PinataLayout & DDL

#### Breaking

- **`DdlReaderError` is read-only.** `ErrorLevel`, `ErrorMessage`, `SourceFile`, `SourceLine` and `SourceColumn` are now `readonly`, and `ErrorNumber` is no longer public.

#### Added

- **The DDL reader warns when a footnote contains another footnote.** The document is still read, but `DdlReaderErrors` receives a warning at the inner `\footnote`, because nested footnotes cannot be rendered.

- **The DDL reader warns when a hyperlink has no `Name`.** The hyperlink and its text are still parsed, but the reader records the missing obligatory property. The old misspelling “Obigatory” has also been corrected.

- **OMR standard mark distances are exposed.** `CodeOmr.StandardMarkDistance` maps `MarkDistance.Inch1_6`, `Inch2_6` and `Inch2_8` to the corresponding `MakerDistance`, and `CodeOmr.ToUnit` converts an enum value to its length.

#### Changed

- **`ThickThinBarCode` is renamed `TwoWidthBarCode`.** It remains the base for Code 39 and Interleaved 2 of 5. Only callers naming the base type itself need to change.

#### Fixed

- **Invalid `\fontsize` values become DDL reader errors instead of terminating the read.** The remaining document is still parsed and the inner text remains available.

- **MDDDL `\x` escapes are decoded correctly.** One or two hexadecimal digits produce the named character without consuming the following source character.

- **Oversized or malformed MDDDL colour numbers are reported as parser errors.** The reader now recovers instead of leaking `OverflowException` or `FormatException`. (#94)

- **A lone carriage return is recognised as a line ending in MDDDL.** CR, LF and CRLF inputs now behave consistently for comments, line numbers and text. (#94)

- **A PinataLayout image's `PictureFormat` crop performs a real crop.** Crop values no longer shrink the layout rectangle and squeeze the whole image into it.

### API & Packaging

#### Breaking

- **`PdfSharpException` is renamed `PdfPinataException`.**

  **Migration:** replace catches or references to `PdfSharpException` with `PdfPinataException`. `PdfReaderException` and `ContentReaderException` keep their names.

#### Deprecated

- **`XBitmapImage.CreateBitmap`.** It creates an empty bitmap intended only for the removed `XGraphics.FromImage` path and will be removed.

- **`PdfCustomValueCompressionMode`.** Its only consumer has been removed and the enum will be removed.

- **`XPoint + XSize`.** Treat displacement as an `XVector` instead:
  `point + new XVector(size.Width, size.Height)`.

#### Removed

- **`XGraphics.FromImage`.** It always returned `null`; use an `XForm` when you need drawable content that can later be placed like an image.

- **`PdfWriterLayout.Verbose`.** Nothing selected it, so the unused verbose writer layout and its formatting behaviour have been removed.

- **`PdfCustomValue`'s parameterless constructor and `CompressionMode` field.** Construct custom values from bytes with `new PdfCustomValue(byte[])`.

### Documentation & Samples

#### Added

- **A Gradients demo and documentation page** covering linear and radial gradients, two-centre radial gradients, extension and brush transforms.

---

## [0.2.1] - 2026-09-21

### PDF Reader & Writer

#### Added

- **`PdfDictionary.PdfStream.ExternalFile` exposes the file specification named by `/F`.** It reports where an external stream says its real data lives without attempting to fetch that file. See `docs/specs/external-file-streams.md`.

#### Fixed

- **Large-file trailer scanning no longer reads the entire file into a string.** `Lexer.FindLastMarker` scans backwards in reused 64 KiB blocks. A 1 GiB file with a distant `startxref` now opens without the previous huge allocation, and files larger than `int.MaxValue` are no longer rejected solely for their length. See `docs/specs/large-file-trailer-scan.md`. (empira/PDFsharp#390)

- **Files with no `startxref` now fail with the intended reader error.** The parser no longer attempts to assign stream position `-1`.

- **External-file streams no longer make the whole document unreadable.** A stream carrying `/F` is parsed normally and the external-file entries are preserved without attempting to fetch the target file.

- **Page trees that loop or are pathologically deep are rejected safely.** An ancestor loop is reported, depth beyond 256 is refused, and traversal also has a work bound to prevent exponential walks through repeatedly shared nodes. (empira/PDFsharp#361)

### Images

#### Fixed

- **Partly transparent images are no longer accidentally erased by a second transparency mask.** A 1-bit `/Mask` is no longer written when an 8-bit `/SMask` already represents the alpha. Binary transparency and pre-PDF-1.4 fallback continue to use the stencil mask. (empira/PDFsharp#392)

### Pages & Documents

#### Fixed

- **`PdfDocument(Stream)` writes a valid PDF version header.** Documents built directly on an output stream no longer save as `%PDF-0.0`.

- **A section that specifies a page format plus one explicit dimension keeps the dimension it set.** The flattening visitor no longer swaps the width/height decision.

### PinataLayout

#### Added

- **Merged-cell bounds are exposed.** `Cell.MergedRightColumnIndex` and `Cell.MergedBottomRowIndex` report the last table column and row actually covered, clamped to the table edge.

#### Fixed

- **A cell merged beyond the table edge is clamped instead of throwing during layout.** Borders, widths and structure spans use the effective table edge rather than indexing beyond the available rows or columns.

- **`Section.LastParagraph` and `Section.LastTable` return `null` for an empty section.**

- **Empty inline paragraph elements render safely.** `AddFormattedText("")`, empty hyperlinks and similar leaves no longer make the parent walk run off the document. (PinataLabs/PdfPinata#45)

### Drawing & Units

#### Changed

- **`XUnit` implements `IEquatable<XUnit>`.** This avoids boxing in generic equality scenarios and aligns `unit.Equals(72)` with `unit == 72` through the implicit numeric conversion. String equality deliberately remains `false` instead of triggering string parsing.

#### Fixed

- **`XUnit.Presentation` stores values in presentation units.** It no longer records the unit as points.

### PDF Objects

#### Fixed

- **Indirect booleans are written as lowercase `true` and `false`.**

- **`ViewerPreferences.Direction` reads `L2R` and `R2L` back correctly.**

---

## [0.2.0] - 2026-09-20

### API & Packaging

#### Breaking

- **The library, packages and namespaces are renamed.** PdfSharpCore becomes **PdfPinata** and MigraDocCore becomes **PinataLayout**. The corresponding NuGet IDs and root namespaces change in lockstep.

  **Migration:** replace `PdfSharpCore` with `PdfPinata` and `MigraDocCore` with `PinataLayout` in package references and `using` directives.

- **`PdfDocumentOpenMode` is now enforced where modification is requested.** Operations that mutate a document now reject `ReadOnly` and `Import` documents instead of silently accepting changes that could not be saved correctly.

  **Migration:** open documents with `Modify`, or with `Append` for incremental-save scenarios, before calling mutating APIs.

- **`ImageSource.IImageSource.SaveAsPdfBitmap(MemoryStream)` is replaced by `PixelBuffer GetPixels()`.** `XImage.AsBitmap()` is likewise replaced by `XImage.GetPixels()`. `PixelBuffer` is tightly packed, top-down, straight-alpha BGRA.

- **`IFontFallback.FamiliesFor` now accepts an `int codePoint` instead of `char`.** This allows fallback decisions for supplementary-plane Unicode characters.

- **`IXGraphicsRenderer.DrawString` now accepts an `XPen` before the `XBrush`.**

  **Migration:** `DrawString(s, font, brush, rect, format)` becomes `DrawString(s, font, null, brush, rect, format)`.

- **MigraDoc `PageFormat.B5` now means ISO B5.** The previous value was JIS B5. Use `PageFormat.JISB5` to preserve the old physical page size.

- **`PdfPage.Size`, `Width` and `Height` refuse to resize a page that already has content.**

  **Migration:** use `page.Resize(...)`. `PageResizeOptions.Crop` provides crop semantics where needed.

- **A non-contiguous `HeadingFormat` row in a PinataLayout table now throws during formatting.** Repeating headings must be a contiguous run beginning at row 0.

#### Removed

- **`PdfDocumentOpenMode.InformationOnly`.** It never implemented a partial metadata-only read. Use `ReadOnly`.

- **`PdfPinata.Text.ScriptItemizer` and `ScriptRun` are internal.** Use `TextItemizer.Itemize`, which combines script and bidirectional itemisation correctly.

- **`PdfDocumentOptions.EnableCcittCompressionForBilevelImages`.** The encoder it controlled was unreachable.

- **Unused CCITT encoding and legacy image-import infrastructure.** This includes the internal fax encoder, `PdfImage.ReadIndexedMemoryBitmap`, and the unused `Drawing.Internal` importer subsystem.

### Fonts & Text

#### Added

- **Supplementary-plane Unicode characters can be drawn.** The font reader now supports `cmap` format 12, and font fallback can receive complete Unicode code points rather than surrogate halves.

- **Bold simulation is decided per face.** Fallback faces with real bold variants are no longer simulated merely because the original face required simulation.

- **`XTextFormatter` supports text flow around obstacles.** Obstacles expose free horizontal runs for each line band through `IFlowObstacle`; `RectangleObstacle` is included. The formatter currently chooses the widest available run for each logical line.

- **Drop caps are supported by `XTextFormatter`.** `XDropCap` sizes the initial character by line depth and can use an `IGlyphOutlineProvider` for ink-aligned placement.

- **`XTextFormatter` gains paragraph and multi-column options.** Added options include `LineBreak`, `Indent`, `IndentAllLines`, `ParagraphGap`, `LineGap`, `Ellipsis`, `Rotation`, `Columns` and `ColumnGap`.

- **Text state is available on `XStringFormat`.** `CharacterSpacing`, `WordSpacing`, `HorizontalScaling`, `TextRise` and `ObliqueAngle` are written using the corresponding PDF text operators and text matrix.

- **Text can be stroked as well as filled.** `DrawString` overloads accept an `XPen` and/or `XBrush`.

- **Text underline and strikeout styles are available on `XStringFormat`.** `DecorationColor` can override the rule colour.

- **Additional line-alignment modes are available.** `XLineAlignment.Hanging`, `Ideographic` and `SvgMiddle` are measured against the text rather than the layout rectangle.

- **`XGraphicsPath.AddString` now produces real glyph outlines.** Geometry is provided through `IGlyphOutlineProvider` / `GlobalFontSettings.GlyphOutlineProvider`, with Skia and ImageSharp implementations.

#### Changed

- **`XGraphics.MeasureString(text, font, stringFormat)` honours the supplied format.** Text-state properties that affect width are now included in measurement.

- **`XLineAlignment.BaseLine` accepts layout rectangles with non-zero height.** The baseline remains anchored to the rectangle's top edge and height is ignored.

#### Fixed

- **Bold simulation no longer measures multi-line text as though all characters belonged to the widest line.**

- **Line feeds no longer consume character spacing.**

### Pages & Documents

#### Added

- **Existing pages can be resized.** `PdfPage.Resize` and `PdfDocument.ResizePages` support `Fit`, `Fill`, `Stretch` and `None`, alignment, margins, auto-rotation, annotation movement and destination movement. Signed, encrypted and tagged documents are refused. See `docs/specs/page-resize.md`.

- **Many additional `PageSize` values are available.** Added A7–A10, 2A0/4A0, B6–B10, C0–C10 and SRA0–SRA4, among others.

- **Many additional PinataLayout `PageFormat` values are available.** The enum now aligns with the expanded `PageSize` set while preserving MigraDoc's named-unit behaviour.

- **Named destinations are supported.** `PdfDocument.NamedDestinations`, `PdfPage.AddNamedLink`, `PdfLinkAnnotation.CreateNamedLink`, and matching `XGraphics` helpers allow links to survive page insertion or reordering.

- **Trimmed pages can reserve mark margins and draw crop marks.** `PdfPage.MarkMargins` and `DrawCropMarks` provide sheet room beyond the bleed and trim boxes.

#### Changed

- **Tagged content no longer nests MCID marked-content sequences.** An outer sequence is suspended and resumed around an inner content item.

- **Pages using `TrimMargins` now write nested page boxes correctly.** `/MediaBox` contains `/BleedBox`, which contains `/TrimBox`, with `MarkMargins` supplying sheet room for crop marks.

#### Fixed

- **Trimmed pages no longer grow each time they are saved.**

- **Uneven top and bottom trim margins are applied to the correct PDF rectangle edges.**

- **`PageSize.Executive` now measures 7.25 × 10.5 inches (522 × 756 points).**

- **Page transparency groups are emitted only when required.** Opaque pages no longer receive an unnecessary `/Group << /S /Transparency /CS /DeviceRGB >>`, while imported pages that already have a group keep it.

- **Page transparency groups survive page import through `XPdfForm`.**

- **PDF `null` values in transparency-related entries are treated as absent rather than as real masks or groups.**

### Drawing & Graphics

#### Added

- **Linear and radial gradients support alpha.** Transparent gradient stops are represented through a luminosity soft mask while opaque gradients remain unchanged.

#### Fixed

- **RGB gradients are valid PDF shadings.** Alpha is no longer incorrectly written as a fourth DeviceRGB component; it belongs in the soft mask.

### PinataLayout

#### Added

- **Text can flow beside shapes.** `WrapFormat.Style` adds `Left`, `Right`, `Largest` and `Both` for side-wrapping floating shapes. `DistanceLeft`, `DistanceRight`, `DistanceTop` and `DistanceBottom` define the obstacle margins.

- **MigraDoc fields can be evaluated without rendering.** `FieldEvaluator.Evaluate` and `FieldEvaluationContext` return field text when it can be known, while `FieldEvaluator.IsField` identifies evaluatable objects. `NumberFormatter` is public.

- **A Bleed demonstration page** shows trim, bleed, crop marks and page boxes.

#### Fixed

- **A heading containing an `InfoField` includes that field's text in its outline entry.**

- **A drop cap that is too wide for its column pushes text below itself instead of throwing text outside the column.**

### PDF/UA & Accessibility

#### Added

- **PDF/UA-1 heading levels cannot skip forward.** For example, H1 followed by H3 is rejected at save time.

- **`Footnote.Identifier` exposes the tagged-document `/ID` used for a note.** When unset, the renderer continues to generate identifiers.

### Images

#### Added

- **Image backends expose pixels directly through `PixelBuffer`.** This replaces the internal round-trip through an in-memory BMP file.

### Documentation & Samples

#### Added

- **Documentation and samples for page resize, bleed/crop marks, text flow, drop caps, gradients and related drawing features.**
