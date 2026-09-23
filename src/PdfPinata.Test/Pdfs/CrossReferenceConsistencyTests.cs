using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using Xunit;

// This namespace has a PdfReader of its own, so the one that opens documents needs saying in full.
using Reader = PdfPinata.Pdf.IO.PdfReader;

namespace PdfPinata.Test.Pdfs;

/// <summary>
///   The cross-reference table's own invariants: every object numbered once, every number in the
///   xref backed by an object, and the count the xref declares matching what is actually there.
/// </summary>
/// <remarks>
///   <para>
///   <c>PdfCrossReferenceTable</c> used to carry a <c>CheckConsistence</c> that asserted these with
///   <c>Debug.Assert</c>, called from <c>Compact</c>, <c>Renumber</c>, <c>TransitiveClosure</c> and
///   from <c>PdfReader.Open</c>. It never ran: it was marked <c>[Conditional("DEBUG_")]</c>, and the
///   trailing underscore is how this codebase spells a symbol that is never defined, so the compiler
///   removed every call in Debug and Release alike. It was also quadratic in the number of objects,
///   which is why it could not simply be switched on.
///   </para>
///   <para>
///   These assert the same invariants from outside, against the bytes the writer produced, which is
///   where they are actually observable and where a consumer would feel them break. The renumbering
///   path matters most and is the one the old checks bracketed: importing pages renumbers every
///   object, and two objects sharing a number is a corrupt file rather than a wrong-looking one.
///   </para>
///   <para>
///   Each entry is followed to the offset it gives rather than merely counted, because counting is
///   blind to the two ways the table goes wrong while still adding up: an offset that lands
///   somewhere other than the object's header, and a numbering with a gap in it. The section
///   declares itself as a single subsection <c>0 n</c>, so an entry stands for the object at its own
///   position and nothing in the file says so twice.
///   </para>
/// </remarks>
public class CrossReferenceConsistencyTests
{
    /// <summary>Every object number defined in the file, in the order the writer wrote them.</summary>
    private static IReadOnlyList<int> ObjectNumbersIn(byte[] pdf)
    {
        var text = Encoding.Latin1.GetString(pdf);
        return Regex.Matches(text, @"(?m)^(\d+) (\d+) obj\b")
            .Select(match => int.Parse(match.Groups[1].Value))
            .ToList();
    }

    /// <summary>One line of the xref section: where the object is, which generation, and whether
    /// the entry is in use.</summary>
    private readonly record struct XrefEntry(int Offset, int Generation, char Kind);

    /// <summary>
    ///   The entries of the xref section, entry zero first. The section declares itself as
    ///   <c>0 n</c> - one subsection numbering objects consecutively from zero - so an entry's
    ///   position in the list is the number of the object it points at, and the entry itself never
    ///   says which object that is. That is the invariant worth checking: nothing in the file
    ///   restates it, so a writer that numbered objects with a gap would produce a table pointing
    ///   at the wrong objects and no line of it would look wrong on its own.
    /// </summary>
    private static IReadOnlyList<XrefEntry> XrefEntriesIn(byte[] pdf)
    {
        var text = Encoding.Latin1.GetString(pdf);
        var section = Regex.Match(text, @"(?m)^xref\r?\n0 (\d+)\r?\n");
        section.Success.Should().BeTrue("the file has to have an xref section to be a PDF");

        var count = int.Parse(section.Groups[1].Value);
        var at = section.Index + section.Length;

        // Acrobat is pedantic about this and so is the writer: exactly 20 bytes per line.
        (at + 20 * count).Should().BeLessThanOrEqualTo(text.Length,
            "the section declares " + count + " entries, so the file has to hold them");

        var entries = new List<XrefEntry>();
        for (var index = 0; index < count; index++, at += 20)
        {
            var line = Regex.Match(text.Substring(at, 20), @"^(\d{10}) (\d{5}) ([nf]) ");
            line.Success.Should().BeTrue(
                "entry " + index + " has to be the fixed-width line the format defines");
            entries.Add(new XrefEntry(int.Parse(line.Groups[1].Value),
                int.Parse(line.Groups[2].Value), line.Groups[3].Value[0]));
        }

        return entries;
    }

    private static byte[] Save(PdfDocument document)
    {
        using var output = new MemoryStream();
        document.Save(output, false);
        return output.ToArray();
    }

    private static byte[] ADocumentOf(int pages)
    {
        var document = new PdfDocument();
        for (var page = 0; page < pages; page++)
            _ = document.AddPage();
        return Save(document);
    }

    private static void ShouldBeConsistent(byte[] pdf)
    {
        var text = Encoding.Latin1.GetString(pdf);
        var numbers = ObjectNumbersIn(pdf);

        numbers.Should().NotBeEmpty("a written document defines objects");
        numbers.Should().OnlyHaveUniqueItems("two objects sharing a number is a corrupt file");
        numbers.Should().AllSatisfy(number => number.Should().BeGreaterThan(0,
            "object zero is the free-list head and is never defined"));

        // The xref declares 0..n, so it counts one more entry than there are objects.
        var entries = XrefEntriesIn(pdf);
        entries.Should().HaveCount(numbers.Count + 1,
            "the xref has to account for exactly the objects the file defines");
        entries[0].Kind.Should().Be('f', "entry zero is the head of the free list");
        entries[0].Generation.Should().Be(65535, "which is the generation the format gives it");

        // Counting alone would pass a table whose offsets point at the wrong objects, or one whose
        // numbering has a gap the count cannot show. Each entry is followed to where it says the
        // object is, and the header found there has to name the object the entry stands for.
        for (var number = 1; number < entries.Count; number++)
        {
            var entry = entries[number];
            entry.Kind.Should().Be('n', "object " + number + " is defined, so its entry is in use");

            var header = number + " " + entry.Generation + " obj";
            entry.Offset.Should().BeInRange(0, text.Length - header.Length,
                "the entry for object " + number + " has to point inside the file");
            text.Substring(entry.Offset, header.Length).Should().Be(header,
                "the entry for object " + number + " has to point at that object");
        }
    }

    [Fact]
    public void AWrittenDocumentNumbersEveryObjectOnce()
    {
        ShouldBeConsistent(ADocumentOf(3));
    }

    [Fact]
    public void ADocumentOpenedAndWrittenAgainIsStillConsistent()
    {
        // PdfReader.Open in Modify mode compacts and renumbers, which is where the removed checks
        // were called from.
        var reopened = Reader.Open(new MemoryStream(ADocumentOf(3)), PdfDocumentOpenMode.Modify);

        ShouldBeConsistent(Save(reopened));
    }

    [Fact]
    public void ImportingPagesFromAnotherDocumentRenumbersWithoutCollision()
    {
        // The case the invariant exists for. Both documents number their objects from one, so
        // importing without renumbering would give two objects the same number.
        var source = Reader.Open(new MemoryStream(ADocumentOf(3)), PdfDocumentOpenMode.Import);
        var target = Reader.Open(new MemoryStream(ADocumentOf(2)), PdfDocumentOpenMode.Modify);

        foreach (var page in source.Pages)
            _ = target.AddPage(page);

        var written = Save(target);

        ShouldBeConsistent(written);
        Reader.Open(new MemoryStream(written), PdfDocumentOpenMode.Modify).PageCount.Should().Be(5);
    }

    [Fact]
    public void RemovingAPageLeavesTheRemainingObjectsConsistent()
    {
        var document = Reader.Open(new MemoryStream(ADocumentOf(4)), PdfDocumentOpenMode.Modify);
        document.Pages.RemoveAt(1);

        var written = Save(document);

        ShouldBeConsistent(written);
        Reader.Open(new MemoryStream(written), PdfDocumentOpenMode.Modify).PageCount.Should().Be(3);
    }
}
