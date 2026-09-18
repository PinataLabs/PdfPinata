using System;
using System.Linq;
using System.Runtime.CompilerServices;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel;
using PinataLayout.DocumentObjectModel.Internals;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PdfPinata.Test.Dom;

/// <summary>
///   Two defects in the value descriptor layer, found while building the parity harness for the
///   move to a generated value model. Both were pre-existing and reachable through public API, and
///   both were pinned here as-is during the migration so that whichever way they were resolved was
///   a deliberate choice with a failing test to mark it, rather than a silent change.
///
///   Both are now fixed, and these assert the fixes and their blast radius. What the class is for
///   has not changed: it is where a defect in this layer gets written down before anyone acts on it.
/// </summary>
public class ValueModelKnownDefectsTests
{
    /// <summary>
    ///   Kept as a regression test, but note what now makes it pass. ValueKind.PlainValue was
    ///   introduced so SetNull would do nothing for a member with no null of its own, instead of
    ///   casting it to INullableValue and throwing. The five members that needed it were
    ///   FormattedText's bool and enum delegating properties - and those have since lost their [DV]
    ///   entirely, because the DDL reader resolves those names against the Font instead.
    ///
    ///   So there is no longer a single PlainValue member anywhere in the DOM, and this passes
    ///   because the shape that broke it is gone rather than because the handling works. The
    ///   handling itself is exercised by the generator's own tests, which construct one.
    /// </summary>
    [Fact]
    public void FormattedTextSetNullNoLongerThrows()
    {
        var formattedText = new FormattedText { Bold = true };

        var setNull = () => formattedText.SetNull();

        setNull.Should().NotThrow();
        formattedText.Bold.Should().BeFalse("the Font it delegates to was reset");
    }

    /// <summary>
    ///   The claim above, asserted rather than left as a comment: nothing in the DOM is classified
    ///   PlainValue any more. If a member ever is again, SetNull's handling of it starts mattering
    ///   to the real model and this test should be replaced by one that exercises it.
    /// </summary>
    [Fact]
    public void NoDomMemberIsAPlainValue()
    {
        var plainValues =
            from type in ReflectionMeta.AllDocumentObjectTypes()
            let meta = Meta.GetMeta((DocumentObject)RuntimeHelpers.GetUninitializedObject(type))
            from descriptor in meta.ValueDescriptors
            where descriptor.Kind == ValueKind.PlainValue
            select $"{type.Name}.{descriptor.ValueName}";

        plainValues.Should().BeEmpty();
    }

    /// <summary>
    ///   Bold and its eight siblings are no longer part of FormattedText's value model - the DDL
    ///   reader resolves them against the Font they delegate to, which is where Font.Serialize
    ///   writes them from. The typed property still works; only the name-addressed route moved.
    /// </summary>
    [Fact]
    public void FormattedTextDelegatesBoldToItsFont()
    {
        var formattedText = new FormattedText { Bold = true };

        formattedText.Bold.Should().BeTrue("the typed property is unchanged");
        formattedText.HasValue("Bold").Should().BeFalse("it is the Font's member, not FormattedText's");
        formattedText.Font.GetValue("Bold").Should().Be(true, "which is where it lives");
    }

    /// <summary>
    ///   With those nine gone, every member of FormattedText's model can actually be null, so
    ///   IsNull() means something for the first time. It used to be a constant false: five members
    ///   were plain value types with no null, and two more read Font.Name, which coalesces to "".
    /// </summary>
    [Fact]
    public void AnEmptyFormattedTextIsNull()
    {
        new FormattedText().IsNull().Should().BeTrue("nothing has been assigned to it");

        var withFont = new FormattedText { Bold = true };
        withFont.IsNull().Should().BeFalse("its font carries a value");
    }

    /// <summary>
    ///   DocumentObjectDescriptor.IsNull used to compute val.IsNull() on its property branch,
    ///   discard the result and return true unconditionally, so Style.Font - the only [DV] property
    ///   in the DOM whose type is a DocumentObject - reported itself null whatever it held.
    ///
    ///   It was carried forward unchanged through the move to a generated value model, so that the
    ///   parity harness gated a replacement rather than a behaviour change, and fixed afterwards.
    ///   These assert the fix, and that the fix changed nothing a caller can see.
    /// </summary>
    [Fact]
    public void ADocumentObjectPropertyDescriptorAnswersForTheObjectItHolds()
    {
        // A user-defined style, not Styles[0] - the built-in styles are read-only and now throw
        // on any write. See ReadOnlyStyleTests.
        var document = new Document();
        Style style = document.Styles.AddStyle("Probe", "Normal");
        style.Font.Bold = true;
        style.Font.Name = "Times New Roman";

        ValueDescriptor font = Meta.GetMeta(style)["Font"];

        font.IsNull(style).Should().BeFalse("the font plainly has values");
        font.IsNull(style).Should().Be(style.Font.IsNull(), "the descriptor must agree with the object");
    }

    [Fact]
    public void AnEmptyDocumentObjectPropertyStillReportsNull()
    {
        var document = new Document();
        Style style = document.Styles.AddStyle("Empty", "Normal");

        ValueDescriptor font = Meta.GetMeta(style)["Font"];

        font.IsNull(style).Should().BeTrue("nothing has been assigned to it");
    }

    /// <summary>
    ///   The blast radius was always small, which is why nothing noticed: Meta.IsNull(dom, name)
    ///   does not use the descriptor for a DocumentObject member - it calls GetValue and asks the
    ///   object itself - and the whole-object sweep had Style's separately tracked paragraphFormat
    ///   field masking the wrong answer. Both routes answered correctly before the fix and must
    ///   still answer the same afterwards.
    /// </summary>
    [Fact]
    public void TheRoutesCallersTakeAreUnchanged()
    {
        var document = new Document();
        Style style = document.Styles.AddStyle("Probe", "Normal");
        style.Font.Bold = true;

        style.IsNull("Font").Should().BeFalse("Meta.IsNull(dom, name) asks the object, not the descriptor");
        style.IsNull().Should().BeFalse("paragraphFormat already answered for the whole object");
    }
}
