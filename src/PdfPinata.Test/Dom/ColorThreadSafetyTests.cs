using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel;
using PinataLayout.DocumentObjectModel.IO;
using Xunit;

namespace PdfPinata.Test.Dom;

/// <summary>
///   Color.ToString names a standard colour by looking it up in a table it builds once. That table
///   used to be built on first use and assigned to its static while still empty, so a second thread
///   could read one that was half built, or race the ContainsKey/Add pair inside the loop.
///
///   Calling ToString on the same colour from 64 threads at once returned four different answers -
///   the name, "RGB(0,0,0)", an empty string, and ArgumentException "Item has already been added" -
///   depending on the timing. The silently wrong one mattered most: a document serialized with
///   RGB(0,0,0) where it should have said Black, and the DDL still parsed.
///
///   Deliberately not in the DomSerialization collection. These tests are the reason that
///   collection no longer needs to exist, so they have to be free to run alongside everything else.
/// </summary>
public class ColorThreadSafetyTests
{
    const int Threads = 64;

    static string[] InParallel(Func<string> work)
    {
        var results = new ConcurrentBag<string>();
        Parallel.For(0, Threads, _ =>
        {
            try
            {
                results.Add(work());
            }
            catch (Exception ex)
            {
                results.Add("THREW " + ex.GetType().Name + ": " + ex.Message);
            }
        });
        return results.ToArray();
    }

    [Fact]
    public void NamingAStandardColourFromManyThreadsGivesOneAnswer()
    {
        var results = InParallel(() => Colors.Black.ToString());

        results.Should().HaveCount(Threads);
        results.Distinct().Should().ContainSingle().And.Contain("Black");
    }

    [Fact]
    public void NamingDifferentStandardColoursFromManyThreadsIsStable()
    {
        var colors = new[] { Colors.Black, Colors.White, Colors.Red, Colors.Aqua, Colors.Fuchsia };

        var results = InParallel(() =>
            string.Join(",", colors.Select(color => color.ToString())));

        results.Distinct().Should().ContainSingle();
        results[0].Should().Be("Black,White,Red,Cyan,Fuchsia");
    }

    [Fact]
    public void SerializingDocumentsFromManyThreadsGivesOneAnswer()
    {
        var results = InParallel(() =>
        {
            var document = new Document();
            var paragraph = document.AddSection().AddParagraph("Hello");
            paragraph.Format.Shading.Color = Colors.Black;
            return DdlWriter.WriteToString(document);
        });

        results.Distinct().Should().ContainSingle();
        results[0].Should().Contain("Black").And.NotContain("RGB(0,0,0)");
    }

    /// <summary>
    ///   Aqua and Cyan are one ARGB value under two names, as are Fuchsia and Magenta. The table
    ///   keeps whichever name Enum.GetNames hands over first, which is Cyan for the one pair and
    ///   Fuchsia for the other - an ordering detail rather than a rule, and asymmetric because of
    ///   it. Pinned because it decides what goes into the DDL, and unchanged by the rewrite: both
    ///   the old table and the new one keep the first name of a pair.
    /// </summary>
    [Fact]
    public void ADoubleNamedColourAlwaysGetsTheSameName()
    {
        Colors.Aqua.ToString().Should().Be("Cyan");
        Colors.Cyan.ToString().Should().Be("Cyan");
        Colors.Fuchsia.ToString().Should().Be("Fuchsia");
        Colors.Magenta.ToString().Should().Be("Fuchsia");
    }

    [Fact]
    public void AColourWithNoStandardNameIsStillWrittenAsRgb()
    {
        new Color(0xFF, 0x12, 0x34, 0x56).ToString().Should().Be("RGB(18,52,86)");
    }

    [Fact]
    public void ATransparentColourWithNoStandardNameIsStillWrittenAsHex()
    {
        new Color(0x80, 0x12, 0x34, 0x56).ToString().Should().Be("0x80123456");
    }
}
