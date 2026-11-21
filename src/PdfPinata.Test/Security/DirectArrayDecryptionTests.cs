using System.IO;
using System.Linq;
using System.Text;
using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using TUnit.Core;

// This namespace has a PdfReader of its own, so the library's needs naming.
using Reader = PdfPinata.Pdf.IO.PdfReader;

namespace PdfPinata.Test.Security;

/// <summary>
///   Every string in an indirect object is encrypted with that object's key, however deep it sits in
///   the direct arrays and dictionaries the object holds (ISO 32000-1 7.6.2). The reader used to key a
///   string in a direct array on the array's own object number, which a direct array does not have,
///   and left that key in place for every string after it. A Modify save then wrote the misread bytes
///   back under the right key, corrupting the file for every other reader. Issue #411.
/// </summary>
public class DirectArrayDecryptionTests
{
    private static readonly byte[] Palette = [0x00, 0xFF, 0x80, 0x10, 0x20, 0xC3];

    private static readonly (string Path, byte[] Plain)[] Strings =
    [
        ("/Before", Ascii("Front")),
        ("/ProbeArr/0", Ascii("Hello")),
        ("/ProbeArr/1", Ascii("Two")),
        ("/Nested/0", Ascii("Outer")),
        ("/Nested/1/0", Ascii("Inner")),
        ("/Nested/1/1/0", Ascii("Deepest")),
        ("/Nested/2", Ascii("Tail")),
        ("/Dicts/0/Name", Ascii("InDict")),
        ("/Dicts/0/Arr/0", Ascii("InDictArr")),
        ("/Dicts/1", Ascii("AfterDict")),
        ("/Resources/ColorSpace/CS0/3", Palette),
        ("/ProbeAfter", Ascii("World")),
    ];

    private static CryptFilterDocument Document(string method, int revision) => new()
    {
        Method = method,
        Revision = revision,
        PageEntries = s =>
            $"/Before {s(Ascii("Front"))} /ProbeArr [{s(Ascii("Hello"))} {s(Ascii("Two"))}] " +
            $"/Nested [{s(Ascii("Outer"))} [{s(Ascii("Inner"))} [{s(Ascii("Deepest"))}]] {s(Ascii("Tail"))}] " +
            $"/Dicts [<< /Name {s(Ascii("InDict"))} /Arr [{s(Ascii("InDictArr"))}] >> {s(Ascii("AfterDict"))}] " +
            $"/Resources << /ColorSpace << /CS0 [/Indexed /DeviceRGB 1 {s(Palette)}] >> >> " +
            $"/ProbeAfter {s(Ascii("World"))}"
    };

    [Test]
    [Arguments("/V2", 3)]
    [Arguments("/V2", 4)]
    [Arguments("/AESV2", 4)]
    [Arguments("/AESV3", 6)]
    public void EveryStringOfThePageIsDecryptedWithThePagesKey(string method, int revision)
    {
        var read = Reader.Open(new MemoryStream(Document(method, revision).Build()), "", PdfDocumentOpenMode.Import);

        AssertStrings(read);
    }

    [Test]
    [Arguments("/V2", 3)]
    [Arguments("/V2", 4)]
    [Arguments("/AESV2", 4)]
    [Arguments("/AESV3", 6)]
    public void AModifySaveKeepsEveryStringOfThePage(string method, int revision)
    {
        var modified = Reader.Open(new MemoryStream(Document(method, revision).Build()), CryptFilterDocument.OwnerPassword,
            PdfDocumentOpenMode.Modify);
        modified.SecuritySettings.OwnerPassword = CryptFilterDocument.OwnerPassword;
        var saved = new MemoryStream();
        modified.Save(saved, false);

        var written = saved.ToArray();
        var independent = new StandardSecurity(written);
        independent.DerivedKeyMatchesTheDocument.Should().BeTrue();
        independent.DecryptedHexStrings(3)
            .Should().HaveCount(Strings.Length)
            .And.SatisfyRespectively(Strings.Select<(string Path, byte[] Plain), System.Action<byte[]>>(
                s => actual => actual.Should().Equal(s.Plain, $"{s.Path} is under the page's own key in the saved file")).ToArray());
    }

    private static void AssertStrings(PdfDocument document)
    {
        var page = document.Pages[0];
        foreach (var (path, plain) in Strings)
            BytesOf(At(page, path)).Should().Equal(plain, $"{path} is under the page's own key");
    }

    private static PdfItem At(PdfDictionary page, string path)
    {
        PdfItem item = page;
        foreach (var step in path.Split('/', System.StringSplitOptions.RemoveEmptyEntries))
            item = item is PdfArray array ? array.Elements[int.Parse(step)] : ((PdfDictionary)item).Elements["/" + step];
        return item;
    }

    private static byte[] BytesOf(PdfItem item)
    {
        item.Should().BeOfType<PdfString>();
        return Encoding.Latin1.GetBytes(((PdfString)item).Value);
    }

    private static byte[] Ascii(string text) => Encoding.ASCII.GetBytes(text);
}
