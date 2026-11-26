using System.IO;
using System.Threading.Tasks;
using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using PdfPinata.Test.Helpers;
using TUnit.Core;
using Reader = PdfPinata.Pdf.IO.PdfReader;

namespace PdfPinata.Test.IO;

/// <summary>
///   A long chain of indirect objects, each referring to the next (#422). The transitive closure of
///   a document's objects used to follow each reference by a call inside the call that found it,
///   deferring only past a thousand levels. In Release a chain of six hundred overflowed a thread
///   of 256 kilobytes, the stack of a thread-pool or server request thread. A Debug build needs
///   less stack for the same walk and first overflowed at 128 kilobytes, which also stands for a
///   256-kilobyte thread half used by its caller. A stack overflow ends the process, so a closure
///   that still recursed would take the test host down rather than go red.
/// </summary>
public class ReferenceChainTests
{
    private const int ChainLength = 5_000;

    [Test, Timeout(60_000)]
    [Arguments(256)]
    [Arguments(128)]
    public async Task AChainOfReferencesOpensForModifyingOnASmallStack(int kilobytes)
    {
        var pdf = WithChain(ChainLength);

        var length = await Interruptibly.Run(() =>
        {
            using var document = Reader.Open(new MemoryStream(pdf), PdfDocumentOpenMode.Modify);
            return LengthOfChain(document.Pages[0]);
        }, kilobytes * 1024);

        length.Should().Be(ChainLength);
    }

    [Test, Timeout(60_000)]
    [Arguments(256)]
    [Arguments(128)]
    public async Task APageHoldingAChainOfReferencesIsImportedOnASmallStack(int kilobytes)
    {
        var pdf = WithChain(ChainLength);

        var length = await Interruptibly.Run(() =>
        {
            using var source = Reader.Open(new MemoryStream(pdf), PdfDocumentOpenMode.Import);
            using var target = new PdfDocument();
            return LengthOfChain(target.AddPage(source.Pages[0]));
        }, kilobytes * 1024);

        length.Should().Be(ChainLength);
    }

    /// <summary>
    ///   A document of one page whose resources name as <c>/Chain</c> the first of
    ///   <paramref name="length"/> indirect dictionaries, each naming the next as <c>/Next</c>.
    /// </summary>
    private static byte[] WithChain(int length)
    {
        var document = new PdfDocument();
        var page = document.AddPage();

        PdfDictionary next = null;
        for (var link = 0; link < length; link++)
        {
            var dictionary = new PdfDictionary(document);
            if (next != null)
                dictionary.Elements["/Next"] = next.Reference;
            document.Internals.AddObject(dictionary);
            next = dictionary;
        }
        page.Resources.Elements["/Chain"] = next.Reference;

        var saved = new MemoryStream();
        document.Save(saved);
        return saved.ToArray();
    }

    private static int LengthOfChain(PdfPage page)
    {
        var length = 0;
        for (var link = page.Resources.Elements.GetDictionary("/Chain"); link != null; link = link.Elements.GetDictionary("/Next"))
            length++;
        return length;
    }
}
