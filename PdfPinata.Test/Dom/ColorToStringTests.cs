using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel;
using Xunit;

namespace PdfPinata.Test.Dom;

/// <summary>
///   Color.ToString used to build its colour-name table lazily, assigning the static an empty
///   Hashtable and then filling it. A thread arriving in between saw a non-null table, skipped
///   construction and read from one that was still filling; two threads inside the loop could both
///   find a key absent and both add it, which Hashtable.Add throws on; and a Hashtable supports one
///   writer, not several.
///
///   Calling Colors.Black.ToString() from 64 threads at once returned four different answers:
///   "Black", "RGB(0,0,0)", "" and ArgumentException. The middle one is the worst - it does not
///   throw and the DDL it produces still parses, so a document simply serializes with RGB(0,0,0)
///   where it should say Black.
///
///   The table is now a static readonly Dictionary built by a static initializer, which the CLR
///   guarantees runs once and completes before any thread reads the field.
/// </summary>
public class ColorToStringTests
{
    /// <summary>
    ///   The original reproduction, kept as a regression test.
    /// </summary>
    /// <remarks>
    ///   Note what this can and cannot prove. A static initializer runs once per process, so by the
    ///   time this test executes the table is almost certainly already built by some earlier test
    ///   and the original race window no longer exists to be hit. It would have caught the defect
    ///   had it run first, and it still proves ToString is safe to call concurrently - but the
    ///   deterministic checks below are what actually pin the table's contents.
    /// </remarks>
    [Fact]
    public void ToStringIsConsistentAcrossThreads()
    {
        var results = new ConcurrentBag<string>();

        Parallel.For(0, 64, _ =>
        {
            try { results.Add(Colors.Black.ToString()); }
            catch (Exception ex) { results.Add("THREW " + ex.GetType().Name); }
        });

        results.Should().HaveCount(64);
        results.Distinct().Should().ContainSingle().And.Contain("Black");
    }

    [Fact]
    public void ANamedColourSerializesByName()
    {
        Colors.Black.ToString().Should().Be("Black");
        Colors.Firebrick.ToString().Should().Be("Firebrick");
        Colors.DarkBlue.ToString().Should().Be("DarkBlue");
    }

    [Fact]
    public void AnUnnamedColourFallsBackToRgb()
    {
        new Color(1, 2, 3).ToString().Should().Be("RGB(1,2,3)");
    }

    /// <summary>
    ///   Several names share an ARGB value, so the table can only hold one of them. The old
    ///   ContainsKey guard kept whichever came first out of Enum.GetNames, and TryAdd has to keep
    ///   the same one or these colours change how they serialize.
    /// </summary>
    /// <remarks>
    ///   Which one that is is not obvious, and not what the declaration order in ColorName would
    ///   suggest: Enum.GetNames orders by value rather than by declaration, and for two names
    ///   sharing a value the tie-break is unspecified. In practice Cyan wins over Aqua and Magenta
    ///   over Fuchsia, even though Aqua and Fuchsia are declared first. Asserted as "both alias to
    ///   the same name" rather than to a literal, so this does not become a runtime-ordering test.
    /// </remarks>
    [Fact]
    public void AliasedColoursAgreeOnOneName()
    {
        // Aqua == Cyan == 0xFF00FFFF
        Colors.Aqua.ToString().Should().Be(Colors.Cyan.ToString());
        Colors.Aqua.ToString().Should().BeOneOf("Aqua", "Cyan");

        // Fuchsia == Magenta == 0xFFFF00FF
        Colors.Fuchsia.ToString().Should().Be(Colors.Magenta.ToString());
        Colors.Fuchsia.ToString().Should().BeOneOf("Fuchsia", "Magenta");
    }

    [Fact]
    public void ACmykColourDoesNotConsultTheTable()
    {
        Color.FromCmyk(0, 100, 100, 0).ToString().Should().StartWith("CMYK(");
    }
}
