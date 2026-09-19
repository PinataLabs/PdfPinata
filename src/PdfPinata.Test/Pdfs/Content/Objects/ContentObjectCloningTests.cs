using System;
using System.Text;
using AwesomeAssertions;
using PdfPinata.Pdf.Content;
using PdfPinata.Pdf.Content.Objects;
using Xunit;

namespace PdfPinata.Test.Pdfs.Content.Objects;

/// <summary>
///   Every content object can be cloned, through <see cref="ICloneable"/>, through
///   <see cref="CObject.Clone"/>, or through the <c>Clone</c> each class declares to hand back its
///   own type. Whichever is asked, the answer is a separate object of the original's type that
///   writes the same content, and a change to the copy is not a change to the original.
/// </summary>
public class ContentObjectCloningTests
{
    [Fact]
    public void EveryWayOfAskingForACloneComesToTheSameCopy()
    {
        var original = new CReal { Value = 1.5 };

        var viaInterface = ((ICloneable)original).Clone();
        var viaObject = ((CObject)original).Clone();
        var viaNumber = ((CNumber)original).Clone();
        var viaReal = original.Clone();

        foreach (var clone in new[] { viaInterface, viaObject, viaNumber, viaReal })
        {
            clone.Should().BeOfType<CReal>().And.NotBeSameAs(original);
            ((CReal)clone).Value.Should().Be(1.5);
        }
    }

    [Fact]
    public void AnIntegerCloneCanBeChangedWithoutChangingTheOriginal()
    {
        var original = new CInteger { Value = 7 };

        var clone = original.Clone();
        clone.Value = 8;

        clone.Should().NotBeSameAs(original);
        original.Value.Should().Be(7);
        clone.ToString().Should().Be("8");
    }

    [Fact]
    public void AnIntegerClonedAsANumberIsStillAnInteger()
    {
        CNumber original = new CInteger { Value = 7 };

        var clone = original.Clone();

        clone.Should().BeOfType<CInteger>().Which.Value.Should().Be(7);
        clone.Should().NotBeSameAs(original);
    }

    [Fact]
    public void ARealCloneCanBeChangedWithoutChangingTheOriginal()
    {
        var original = new CReal { Value = 0.25 };

        var clone = original.Clone();
        clone.Value = 0.75;

        original.Value.Should().Be(0.25);
    }

    [Fact]
    public void ACommentCloneKeepsItsTextAndCanBeRewrittenAlone()
    {
        var original = new CComment { Text = "first" };

        var clone = original.Clone();

        clone.Should().NotBeSameAs(original);
        clone.Text.Should().Be("first");

        clone.Text = "second";
        original.ToString().Should().Be("% first");
        clone.ToString().Should().Be("% second");
    }

    [Fact]
    public void AStringCloneKeepsBothItsValueAndItsType()
    {
        var original = new CString { Value = "<</MCID 3>>", CStringType = CStringType.Dictionary };

        var clone = original.Clone();

        clone.Should().NotBeSameAs(original);
        clone.Value.Should().Be("<</MCID 3>>");
        clone.CStringType.Should().Be(CStringType.Dictionary);

        clone.Value = "(changed)";
        clone.CStringType = CStringType.String;
        original.Value.Should().Be("<</MCID 3>>");
        original.CStringType.Should().Be(CStringType.Dictionary);
    }

    [Fact]
    public void ANameCloneCanBeRenamedWithoutRenamingTheOriginal()
    {
        var original = new CName("/F1");

        var clone = original.Clone();
        clone.Name = "/F2";

        clone.Should().NotBeSameAs(original);
        original.Name.Should().Be("/F1");
        clone.ToString().Should().Be("/F2");
    }

    [Fact]
    public void AnOperatorCloneIsTheSameOperatorAndWritesTheSameContent()
    {
        var original = (COperator)Read("1 0 0 1 20 30 cm")[0];

        var clone = original.Clone();

        clone.Should().NotBeSameAs(original);
        clone.OpCode.Should().BeSameAs(original.OpCode, "an op code is a shared description, not state");
        clone.Name.Should().Be("cm");
        Written(new CSequence { clone }).Should().Be("1 0 0 1 20 30 cm\n");
    }

    [Fact]
    public void AnOperatorCloneHasOperandsOfItsOwn()
    {
        var original = (COperator)Read("1 0 0 1 20 30 cm")[0];
        var firstOperand = original.Operands[0];

        var clone = original.Clone();

        // The operands used to be shared, so adding one to the clone added it to the original.
        clone.Operands.Should().NotBeSameAs(original.Operands);
        clone.Operands.Should().HaveCount(6);
        clone.Operands[0].Should().NotBeSameAs(firstOperand);
        original.Operands[0].Should().BeSameAs(firstOperand);

        clone.Operands.Add(new CInteger { Value = 7 });
        ((CInteger)clone.Operands[4]).Value = 99;

        original.Operands.Should().HaveCount(6);
        Written(new CSequence { original }).Should().Be("1 0 0 1 20 30 cm\n");
        Written(new CSequence { clone }).Should().Be("1 0 0 1 99 30 7 cm\n");
    }

    [Fact]
    public void AnOperatorWithoutOperandsClonesToOneWithoutOperands()
    {
        var original = OpCodes.OperatorFromName("q");

        var clone = original.Clone();
        clone.Operands.Add(new CInteger { Value = 1 });

        original.Operands.Should().BeEmpty();
        Written(new CSequence { original }).Should().Be("q\n");
    }

    [Fact]
    public void AnOperatorCanBeDerivedFromAndIsClonedAsItsOwnType()
    {
        var original = new NamedOperator("sh");
        original.Operands.Add(new CName("/Sh0"));

        var clone = original.Clone();

        clone.Should().BeOfType<NamedOperator>().And.NotBeSameAs(original);
        clone.Name.Should().Be("sh");
        clone.Operands.Should().ContainSingle().Which.Should().BeOfType<CName>()
            .Which.Name.Should().Be("/Sh0");
    }

    [Fact]
    public void ASequenceCloneHoldsCopiesOfTheItemsInAListOfItsOwn()
    {
        const string content = "q 1 0 0 1 20 30 cm (text) Tj Q";
        var original = Read(content);
        var before = Written(original);

        var clone = original.Clone();

        clone.Should().NotBeSameAs(original);
        clone.Count.Should().Be(4);
        for (var index = 0; index < clone.Count; index++)
            clone[index].Should().NotBeSameAs(original[index]);
        Written(clone).Should().Be(before);
        Written(original).Should().Be(before, "cloning does not change what the original writes");

        clone.Add(OpCodes.OperatorFromName("n"));
        clone.RemoveAt(0);

        original.Count.Should().Be(4);
        original[0].ToString().Should().Be("q");
        Written(original).Should().Be(before);
    }

    [Fact]
    public void CloningASequenceLeavesTheOriginalHoldingItsOwnItems()
    {
        var first = new CInteger { Value = 1 };
        var second = new CName("/F1");
        var original = new CSequence { first, second };

        var clone = original.Clone();

        // The copies go into the clone. They used to go into the original, which was left
        // holding copies of what it had been given while the clone held the items themselves.
        original[0].Should().BeSameAs(first);
        original[1].Should().BeSameAs(second);
        clone[0].Should().NotBeSameAs(first).And.BeOfType<CInteger>().Which.Value.Should().Be(1);
        clone[1].Should().NotBeSameAs(second).And.BeOfType<CName>().Which.Name.Should().Be("/F1");

        ((CInteger)clone[0]).Value = 2;
        ((CName)clone[1]).Name = "/F2";
        first.Value.Should().Be(1);
        second.Name.Should().Be("/F1");
    }

    [Fact]
    public void CloningAnArrayLeavesTheOriginalHoldingItsOwnItems()
    {
        var shown = new CString { Value = "A" };
        var original = new CArray { shown, new CInteger { Value = -250 } };

        var clone = original.Clone();

        original[0].Should().BeSameAs(shown);
        clone[0].Should().NotBeSameAs(shown);

        ((CString)clone[0]).Value = "B";
        shown.Value.Should().Be("A");
        original.ToString().Should().Be("[(A)-250]");
        clone.ToString().Should().Be("[(B)-250]");
    }

    [Fact]
    public void AnArrayCloneIsAnArrayAndKeepsItsBrackets()
    {
        var show = (COperator)Read("[(A) -250 (B)] TJ")[0];
        var original = show.Operands[0].Should().BeOfType<CArray>().Subject;

        var clone = original.Clone();

        clone.Should().NotBeSameAs(original);
        clone.ToString().Should().Be("[(A)-250(B)]");

        clone.Add(new CString { Value = "C" });
        clone.ToString().Should().Be("[(A)-250(B)(C)]");
        original.ToString().Should().Be("[(A)-250(B)]");
        original.Count.Should().Be(3);
    }

    [Fact]
    public void AnArrayClonedAsASequenceIsStillAnArray()
    {
        CSequence original = new CArray { new CInteger { Value = 1 } };

        var clone = original.Clone();

        clone.Should().BeOfType<CArray>();
        clone.ToString().Should().Be("[1]");
    }

    private static CSequence Read(string content)
    {
        return ContentReader.ReadContent(Encoding.Latin1.GetBytes(content));
    }

    private static string Written(CSequence sequence)
    {
        return Encoding.Latin1.GetString(sequence.ToContent());
    }

    /// <summary>
    ///   An operator a caller defines, through the protected constructor, rather than one of the
    ///   operators ISO 32000 names.
    /// </summary>
    private sealed class NamedOperator : COperator
    {
        private readonly string _name;

        public NamedOperator(string name)
        {
            _name = name;
        }

        public override string Name => _name;
    }
}
