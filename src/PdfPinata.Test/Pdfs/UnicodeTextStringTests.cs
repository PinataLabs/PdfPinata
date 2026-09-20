using System.IO;
using System.Text;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using PdfPinata.Pdf.Advanced;
using PdfPinata.Pdf.AcroForms;
using PdfPinata.Pdf.Annotations;
using PdfPinata.Pdf.Security;
using Xunit;

namespace PdfPinata.Test.Pdfs;

/// <summary>
///   A string given to the engine without an encoding used to be written one byte per
///   character, which keeps nothing but the low byte of anything above Latin-1 and means
///   something different to the reader for most of what is left. Text now decides for itself:
///   what ASCII can spell is written as it always was, and everything else goes out as
///   UTF-16BE. Entries that hold bytes rather than text still say so and are untouched.
/// </summary>
public class UnicodeTextStringTests
{
    // Neither can be written in a single byte that every reader agrees about.
    private const string Accented = "Ångström";
    private const string Japanese = "日本語";

    [Theory]
    [InlineData("Plain text")]
    [InlineData("")]
    [InlineData("()\\ escapes and \t tabs")]
    public void AStringAsciiCanSpellKeepsBeingWrittenOneBytePerCharacter(string text)
    {
        new PdfString(text).Encoding.Should().Be(PdfStringEncoding.RawEncoding);
    }

    [Theory]
    [InlineData(Accented)]
    [InlineData(Japanese)]
    // Latin-1 fits in a byte, but that byte does not mean the same character in the
    // encodings a reader may assume, so it is text for Unicode too.
    [InlineData("Grüße")]
    public void AStringAsciiCannotSpellIsWrittenAsUnicode(string text)
    {
        new PdfString(text).Encoding.Should().Be(PdfStringEncoding.Unicode);
    }

    [Fact]
    public void AStringAskedToBeRawIsStillRaw()
    {
        new PdfString(Accented, PdfStringEncoding.RawEncoding).Encoding
            .Should().Be(PdfStringEncoding.RawEncoding);
    }

    [Fact]
    public void ATextFieldValueOutsideAsciiIsReadBackAsItWasSet()
    {
        var reread = SaveAndReopen(SaveDocumentWithTextField(Accented));

        TextFieldOf(reread).Text.Should().Be(Accented);
    }

    [Fact]
    public void ATextFieldValueOutsideAsciiIsWrittenAsUnicode()
    {
        var written = AsLatin1(SaveDocumentWithTextField(Accented));

        written.Should().Contain("/V " + HexString(Accented));
    }

    [Fact]
    public void ATextFieldValueWithinAsciiIsWrittenExactlyAsBefore()
    {
        var written = AsLatin1(SaveDocumentWithTextField("Plain text"));

        written.Should().Contain("/V (Plain text)");
    }

    [Fact]
    public void AnAnnotationOutsideAsciiIsReadBackAsItWasSet()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        page.Annotations.Add(new PdfTextAnnotation(document)
        {
            Title = Accented,
            Subject = Japanese,
            Contents = Accented + " " + Japanese
        });

        var reread = SaveAndReopen(Save(document));

        var annotation = reread.Pages[0].Annotations[0];
        annotation.Title.Should().Be(Accented);
        annotation.Subject.Should().Be(Japanese);
        annotation.Contents.Should().Be(Accented + " " + Japanese);
    }

    /// <summary>
    ///   The owner and user entries of the encryption dictionary are key bytes, not text.
    ///   Written as UTF-16BE they would no longer be the key, and the document would not open
    ///   in any reader, including this one.
    /// </summary>
    [Theory]
    [InlineData(PdfDocumentSecurityLevel.Encrypted40Bit)]
    [InlineData(PdfDocumentSecurityLevel.Encrypted128Bit)]
    public void TheEncryptionKeysAreStillWrittenAsBytes(PdfDocumentSecurityLevel level)
    {
        const string ownerPassword = "12343";

        var document = new PdfDocument();
        _ = document.AddPage();
        document.Info.Title = Accented;
        document.SecuritySettings.DocumentSecurityLevel = level;
        document.SecuritySettings.OwnerPassword = ownerPassword;

        var saved = Save(document);

        AsLatin1(saved).Should().NotContain("/O <FEFF").And.NotContain("/U <FEFF");

        using var stream = new MemoryStream(saved);
        var reread = Pdf.IO.PdfReader.Open(stream, ownerPassword, PdfDocumentOpenMode.Modify);
        reread.Info.Title.Should().Be(Accented);
    }

    /// <summary>
    ///   A checksum is the bytes of a digest, so the bytes above ASCII in it are not
    ///   characters and must not be spelled out as any.
    /// </summary>
    [Fact]
    public void AnEmbeddedFileChecksumIsStillWrittenAsBytes()
    {
        var document = new PdfDocument();
        _ = document.AddPage();
        var checksum = new string(new[] { '\x00', '\xE4', '\xFF', '\x7F' });

        var embedded = new PdfEmbeddedFile(document, new byte[] { 1, 2, 3 }, checksum);

        var checksumString = (PdfString)((PdfDictionary)embedded.Elements
            .GetObject("/Params")).Elements["/CheckSum"];
        checksumString.Encoding.Should().Be(PdfStringEncoding.RawEncoding);
    }

    /// <summary>
    ///   /F is a file specification string rather than a text string, so it stays one byte
    ///   per character and cannot spell a name outside ASCII. /UF is its text counterpart
    ///   and is where such a name survives, so it is also the entry to read the name back
    ///   from when a document has one.
    /// </summary>
    [Fact]
    public void AnEmbeddedFileNameOutsideAsciiIsKeptInTheUnicodeEntry()
    {
        var specification = FileSpecificationNamed(Accented + ".txt");

        ((PdfString)specification.Elements["/F"]).Encoding
            .Should().Be(PdfStringEncoding.RawEncoding);
        ((PdfString)specification.Elements["/UF"]).Encoding
            .Should().Be(PdfStringEncoding.Unicode);
        specification.FileName.Should().Be(Accented + ".txt");
    }

    [Fact]
    public void AnEmbeddedFileNameWithinAsciiIsWrittenExactlyAsBefore()
    {
        var specification = FileSpecificationNamed("plain.txt");

        var name = (PdfString)specification.Elements["/F"];
        name.Encoding.Should().Be(PdfStringEncoding.RawEncoding);
        name.Value.Should().Be("plain.txt");
        ((PdfString)specification.Elements["/UF"]).Encoding
            .Should().Be(PdfStringEncoding.RawEncoding);
    }

    /// <summary>
    ///   A document written before /UF was set, or by another library, has only /F.
    /// </summary>
    [Fact]
    public void AnEmbeddedFileNameIsStillReadFromTheOldEntryAlone()
    {
        var specification = FileSpecificationNamed("plain.txt");
        specification.Elements.Remove("/UF");

        specification.FileName.Should().Be("plain.txt");
    }

    private static PdfFileSpecification FileSpecificationNamed(string fileName)
    {
        var document = new PdfDocument();
        _ = document.AddPage();
        var embedded = new PdfEmbeddedFile(document, new byte[] { 1, 2, 3 });

        return new PdfFileSpecification(document, fileName, embedded);
    }

    /// <summary>
    ///   The catalog of a form built by hand, because the field types have no public
    ///   constructor: a text field only ever reaches a caller by being read from a document.
    /// </summary>
    private static byte[] SaveDocumentWithTextField(string text)
    {
        var document = new PdfDocument();
        var page = document.AddPage();

        var field = new PdfDictionary(document);
        field.Elements.SetName("/Type", "/Annot");
        field.Elements.SetName("/Subtype", "/Widget");
        field.Elements.SetName("/FT", "/Tx");
        field.Elements.SetString("/T", "aField");
        field.Elements.SetString("/DA", "/Helv 10 Tf 0 g");
        field.Elements["/Rect"] = new PdfRectangle(new XRect(20, 20, 200, 20));
        document.Internals.AddObject(field);
        page.Elements.SetObject("/Annots", new PdfArray(document, field.Reference));

        var fields = new PdfArray(document, field.Reference);
        var form = new PdfDictionary(document);
        form.Elements.SetObject("/Fields", fields);
        document.Internals.AddObject(form);
        document.Internals.Catalog.Elements["/AcroForm"] = form.Reference;

        // Read it back so the field becomes a PdfTextField, set the value through the
        // property under test, and write it again.
        var withForm = SaveAndReopen(Save(document));
        TextFieldOf(withForm).Text = text;
        return Save(withForm);
    }

    private static PdfTextField TextFieldOf(PdfDocument document)
    {
        return (PdfTextField)document.AcroForm.Fields[0];
    }

    private static byte[] Save(PdfDocument document)
    {
        using var output = new MemoryStream();
        document.Save(output, false);
        return output.ToArray();
    }

    private static PdfDocument SaveAndReopen(byte[] saved)
    {
        var stream = new MemoryStream(saved);
        return Pdf.IO.PdfReader.Open(stream, PdfDocumentOpenMode.Modify);
    }

    private static string AsLatin1(byte[] saved)
    {
        return Encoding.Latin1.GetString(saved);
    }

    /// <summary>
    ///   The hexadecimal string the writer produces for Unicode text: the byte order mark,
    ///   then the text as UTF-16BE.
    /// </summary>
    private static string HexString(string text)
    {
        var bytes = Encoding.BigEndianUnicode.GetBytes("\uFEFF" + text);
        var hex = new StringBuilder("<");
        foreach (var value in bytes)
            hex.AppendFormat("{0:X2}", value);
        hex.Append('>');
        return hex.ToString();
    }
}
