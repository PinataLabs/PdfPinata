using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using PdfPinata.Pdf.Security;
using Xunit;

// This namespace has a StandardSecurity of its own, so the reader needs saying in full.
using Reader = PdfPinata.Pdf.IO.PdfReader;

namespace PdfPinata.Test.Security;

/// <summary>
///   Decrypting a document used to mutate the strings it had already built. <c>EncryptString</c>
///   received the very <see cref="PdfString"/> the owning dictionary or array was storing - taken
///   through <c>item.Value</c> or <c>array.Elements[idx]</c> - and wrote a new value and a new
///   encoding into it behind that collection's back, which is what the setter's own comment,
///   <em>"BUG: May lead to trouble with the value semantics of PdfString"</em>, was about.
///
///   <para>
///   A <see cref="PdfString"/> is a simple type and so must be immutable. The repair is that
///   <c>PdfString.FromEncryptionValue</c> builds the plaintext string and the caller puts it where
///   the ciphertext one was, through the collection's own indexer - the same path every other
///   caller that replaces an entry already takes.
///   </para>
///
///   <para>
///   Both are internal, and this repository carries no <c>InternalsVisibleTo</c> - stated directly
///   in <c>InternalHelperTests</c>, which reaches internal helpers the same way - so the factory is
///   called by reflection rather than by building a whole encrypted document per case.
///   </para>
/// </summary>
public class DecryptedStringReplacementTests
{
    private static readonly Assembly Library = typeof(PdfString).Assembly;
    private static readonly Type Flags = Library.GetType("PdfPinata.Pdf.PdfStringFlags", throwOnError: true);

    /// <summary>Calls the internal PdfString.FromEncryptionValue with flags named as the enum spells them.</summary>
    private static PdfString FromEncryptionValue(byte[] bytes, string flagName)
    {
        var factory = typeof(PdfString).GetMethod("FromEncryptionValue",
            BindingFlags.Static | BindingFlags.NonPublic);
        factory.Should().NotBeNull("the setter was replaced by a factory, not deleted outright");

        // ReSharper disable once PossibleNullReferenceException
        return (PdfString)factory.Invoke(null, new[] { bytes, Enum.Parse(Flags, flagName) });
    }

    [Fact]
    public void PlainBytesBecomeThePlainStringTheySpell()
    {
        var decrypted = FromEncryptionValue("Original title"u8.ToArray(), "RawEncoding");

        decrypted.Value.Should().Be("Original title");
        decrypted.Encoding.Should().Be(PdfStringEncoding.RawEncoding);
    }

    [Fact]
    public void ByteOrderMarkedBytesBecomeAUnicodeStringWithoutTheMark()
    {
        // The one case the old setter existed for. A text string keeps its mark inside the
        // ciphertext, so the lexer - which saw only ciphertext - had nothing to go on and filed the
        // string as raw. The mark decides once the bytes are readable.
        var bytes = new byte[] { 0xFE, 0xFF }.Concat(Encoding.BigEndianUnicode.GetBytes("Tîtlé")).ToArray();

        var decrypted = FromEncryptionValue(bytes, "RawEncoding");

        decrypted.Value.Should().Be("Tîtlé");
        decrypted.Encoding.Should().Be(PdfStringEncoding.Unicode,
            "the mark is what says the string was UTF-16BE all along");
    }

    [Fact]
    public void BytesAlreadyKnownToBeUnicodeAreReadAsUnicode()
    {
        var bytes = Encoding.BigEndianUnicode.GetBytes("Ångström");

        var decrypted = FromEncryptionValue(bytes, "Unicode");

        decrypted.Value.Should().Be("Ångström");
        decrypted.Encoding.Should().Be(PdfStringEncoding.Unicode);
    }

    [Fact]
    public void EverythingButTheEncodingSurvivesTheReplacement()
    {
        // HexLiteral lives in the same field as the encoding, and the string has to go back out the
        // way it came in. Only the encoding bits are allowed to move.
        var decrypted = FromEncryptionValue("(hex)"u8.ToArray(), "HexLiteral");

        decrypted.HexLiteral.Should().BeTrue();
    }

    [Fact]
    public void TheDecryptedStringIsANewObject()
    {
        // The whole point: the string handed in is not the string handed back, so nothing holding
        // the old reference sees it change under them.
        var factory = typeof(PdfString).GetMethod("FromEncryptionValue",
            BindingFlags.Static | BindingFlags.NonPublic);
        var original = new PdfString("Original title");

        // ReSharper disable PossibleNullReferenceException
        var decrypted = (PdfString)factory.Invoke(null,
            new[] { original.GetType()
                        .GetProperty("EncryptionValue", BindingFlags.Instance | BindingFlags.NonPublic)
                        .GetValue(original),
                    Enum.Parse(Flags, "RawEncoding") });
        // ReSharper restore PossibleNullReferenceException

        decrypted.Should().NotBeSameAs(original);
        // ReSharper disable once PossibleNullReferenceException
        decrypted.Value.Should().Be(original.Value);
    }

    [Fact]
    public void PdfStringNoLongerOffersAWayToAssignItsValue()
    {
        // The repair is only a repair while there is no second door back into the field.
        // ReSharper disable PossibleNullReferenceException
        typeof(PdfString).GetProperty("EncryptionValue",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .SetMethod.Should().BeNull("a simple type must be immutable");
        // ReSharper restore PossibleNullReferenceException
    }

    // ----- what going through the indexer costs ---------------------------------------------------

    [Fact]
    public void DecryptingADocumentOnOpenLeavesNothingMarkedAsChanged()
    {
        // Replacing an entry through DictionaryElements's own indexer calls MarkOwnerAsChanged,
        // which the old field-mutating setter never went near - so this is the one place the fix
        // could have been felt from outside, and it is worth an assertion saying where it stops.
        //
        // It stops at the reader. PdfDocument.CaptureOriginalBytes clears IsDirty on every object
        // once the document has been read, under a comment saying why: reading a document mutates
        // plenty of it, so whatever is dirty at that point is dirty from being read rather than
        // from being changed. Decryption is exactly that, and it happens before the capture.
        using var saved = new MemoryStream(EncryptedDocument());

        var reopened = Reader.Open(saved, OwnerPassword, PdfDocumentOpenMode.Append);

        reopened.Info.IsDirty.Should().BeFalse(
            "the information dictionary held the encrypted title, but decrypting it is reading, not changing");
    }

    [Fact]
    public void NothingAnEncryptedDocumentWasReadWithIsReportedAsChanged()
    {
        // The same fact for every object the decryption walk went through, not just the one that
        // held the title - IsDirty is read by SaveIncremental and by nothing else, so this is the
        // whole of what the indexer path could have cost. It costs nothing.
        using var saved = new MemoryStream(EncryptedDocument());

        var reopened = Reader.Open(saved, OwnerPassword, PdfDocumentOpenMode.Append);

        reopened.Internals.GetAllObjects().Should().OnlyContain(o => !o.IsDirty,
            "a document that has only been read has not been changed");
    }

    [Fact]
    public void ADecryptedDocumentStillSaysWhatItSaid()
    {
        // The fix has to be invisible from outside the library: same strings, same places, same
        // values, whichever way the document is reopened.
        using var saved = new MemoryStream(EncryptedDocument());

        var reopened = Reader.Open(saved, OwnerPassword, PdfDocumentOpenMode.Modify);

        reopened.Info.Title.Should().Be(Title);
        reopened.Info.Author.Should().Be(Author);
    }

    private const string Title = "Tîtlé wíth àccents";
    private const string Author = "Ångström";
    private const string OwnerPassword = "12343";

    private static byte[] EncryptedDocument()
    {
        var document = new PdfDocument();
        _ = document.AddPage();
        document.Info.Title = Title;
        document.Info.Author = Author;
        document.SecuritySettings.DocumentSecurityLevel = PdfDocumentSecurityLevel.Encrypted128Bit;
        document.SecuritySettings.OwnerPassword = OwnerPassword;

        using var output = new MemoryStream();
        document.Save(output, false);
        return output.ToArray();
    }
}
