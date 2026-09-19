using System.Collections.Generic;
using System.Linq;
using AwesomeAssertions;
using PdfPinata.Pdf.Content.Objects;
using Xunit;

namespace PdfPinata.Test.Drawing;

/// <summary>
///   <see cref="CSequence"/> against the interfaces it declares.
/// </summary>
/// <remarks>
///   It declares <c>IList&lt;CObject&gt;</c> and implemented every member of it twice: once
///   publicly and correctly, and once as an explicit interface implementation that threw
///   <c>NotImplementedException</c>. C# binds an interface to the explicit implementation where
///   there is one, so a sequence held as <c>IList&lt;CObject&gt;</c> threw for everything - and
///   LINQ, which reaches a collection through <c>IEnumerable&lt;T&gt;</c>, could not touch a
///   content stream at all. Found by writing the Inspect demo, whose first LINQ call over an
///   operator's operands threw.
/// </remarks>
public class ContentSequenceTests
{
    static CSequence Three()
    {
        var sequence = new CSequence();
        sequence.Add(new CInteger { Value = 1 });
        sequence.Add(new CInteger { Value = 2 });
        sequence.Add(new CInteger { Value = 3 });
        return sequence;
    }

    [Fact]
    public void ASequenceCanBeEnumeratedThroughTheGenericInterface()
    {
        IEnumerable<CObject> sequence = Three();

        sequence.Select(item => ((CInteger)item).Value).Should().Equal(1, 2, 3);
    }

    [Fact]
    public void LinqWorksOverASequence()
    {
        // The case that found it: Select over an operator's operands.
        Three().Cast<CInteger>().Sum(item => item.Value).Should().Be(6);
        #pragma warning disable CA1829 // The test is that LINQ's Count() works over a sequence; the property would bypass it.
        Three().Count().Should().Be(3);
        #pragma warning restore CA1829
        Three().First().Should().BeOfType<CInteger>();
    }

    [Fact]
    public void EveryListMemberWorksThroughTheInterface()
    {
        IList<CObject> sequence = Three();
        var four = new CInteger { Value = 4 };

        sequence.Count.Should().Be(3);
        sequence.IsReadOnly.Should().BeFalse();
        sequence[1].Should().BeOfType<CInteger>();
        sequence.IndexOf(sequence[2]).Should().Be(2);
        sequence.Contains(sequence[0]).Should().BeTrue();

        sequence.Add(four);
        sequence.Count.Should().Be(4);
        sequence.Contains(four).Should().BeTrue();

        sequence.Remove(four).Should().BeTrue();
        sequence.Count.Should().Be(3);

        sequence.Insert(0, four);
        sequence[0].Should().BeSameAs(four);

        sequence.RemoveAt(0);
        sequence.Count.Should().Be(3);

        var target = new CObject[3];
        sequence.CopyTo(target, 0);
        target.Should().OnlyContain(item => item != null);

        sequence.Clear();
        sequence.Count.Should().Be(0);
    }

    [Fact]
    public void TheIndexerThroughTheInterfaceIsTheSameOneAsThePublicIndexer()
    {
        var sequence = Three();
        IList<CObject> asList = sequence;
        var replacement = new CInteger { Value = 9 };

        asList[1] = replacement;

        sequence[1].Should().BeSameAs(replacement);
    }

    [Fact]
    public void ACArrayIsASequenceAndBehavesLikeOne()
    {
        // CArray derives from CSequence, so it inherited every one of the throwing stubs.
        var array = new CArray();
        array.Add(new CReal { Value = 1.5 });
        array.Add(new CReal { Value = 2.5 });

        IEnumerable<CObject> asEnumerable = array;

        asEnumerable.Cast<CReal>().Select(item => item.Value).Should().Equal(1.5, 2.5);
    }
}
