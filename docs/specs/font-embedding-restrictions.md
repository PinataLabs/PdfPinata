# Spec — honouring a font's embedding permissions

**Done** on `todo/font-embedding-restrictions`, behind `PdfDocumentOptions.RespectFontEmbeddingRestrictions`.
It replaces a `// TODO: Respect embedding restrictions.` in `OpenTypeDescriptor`, which sat next to a
commented-out `fsType == 0x0002` test.

## What a font says

The OS/2 table's `fsType` holds a font's embedding permissions. `OS2Table` has always read it, and
nothing used it. The bits that matter here:

| bits | name | what this library does with the option on |
|---|---|---|
| none of 0-3 | Installable | embeds, subsetted |
| `0x0002` | Restricted License | **refuses** |
| `0x0004` | Preview & Print | embeds, subsetted |
| `0x0008` | Editable | embeds, subsetted |
| `0x0100` | No Subsetting | embeds **whole** |
| `0x0200` | Bitmap Embedding Only | **refuses**: this library embeds outlines, never bitmaps |

OS/2 versions 0 to 2 allowed more than one usage bit, and the specification says to read such a font
by the least restrictive. So `0x000A` is Editable and `0x0006` is Preview & Print, and neither is
refused. A face with no OS/2 table is read as Installable.

`FontEmbeddingPermissions` (`Fonts.OpenType/`) is the only place that reads the bits.

## Decisions

**Off by default.** Fonts have always been embedded whatever they say. If this were on by default,
a document that saved yesterday would throw today. It is an option of the document, not of
`GlobalFontSettings`. The face is read once and shared, but it is embedded into each document
separately. A caller who opts in for one document must not change what another caller's documents
contain.

**Refused in two places.** `PdfFontTable.GetFont` refuses the font before it creates anything, so the
exception comes from the `DrawString` that asked for the font, and no orphan objects remain.
`PdfFontTable.PrepareForSave` asks again for every font before it prepares any of them, because the
option can be set after drawing. The exception is an `InvalidOperationException` that names the face,
the `fsType` value, the restriction and the option.

**No Subsetting is honoured, not refused.** The machinery was already there: a CFF face is always
embedded whole. A whole TrueType face goes into `/FontFile2` as it is. Its glyph indices are the ones
the document already uses, so `/CIDToGIDMap /Identity` stays true. `PdfFont.EmbedsSubset` is now the
one question that three things ask:

* the program written, in `EmbedFontProgram`;
* the subset tag on the name. The constructors still add the tag, because they must name the font
  before the option is certain. `RestoreWholeFontName` removes the tag at save time when the program
  goes in whole. ISO 32000-1 9.6.4 reserves the tag for subsets;
* PDF/A-1's `/CIDSet`, which is written for a subset only. A set that lists only the glyphs drawn would
  misdescribe a whole program.

A side effect: a *simple* (WinAnsi) font with CFF outlines was always embedded whole but still carried
a subset tag. It now loses the tag at save time, as the Type 0 path already did.

## Left out

**PDF/A does not turn the option on.** Every PDF/A part requires fonts that are "legally embeddable".
But `fsType` is what the font says about its licence. It is not the licence. A caller who has bought
an embedding licence for a Restricted font may embed it. veraPDF does not check `fsType` either. The
option's XML doc tells a PDF/A caller to set it alongside the claim.

**No bitmap embedding.** Bitmap Embedding Only is refused rather than satisfied with `EBDT`/`CBDT`
data, which PDF has no way to carry as a font program anyway.

## Tests

`src/PdfPinata.Test/Fonts/FontEmbeddingRestrictionTests.cs`. Each case uses Liberation Sans with
its `fsType` changed and its OS/2 checksum recomputed (`TrueTypeGlyphs.WithFsType`). Each value gets
its own first letter in the name table, because faces are cached by name.
