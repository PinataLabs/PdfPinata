using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Advanced;
using Xunit;

namespace PdfPinata.Test.Pdfs;

/// <summary>
///   <see cref="PdfItem"/> carries the rule as a comment - <em>"All simple types (i.e. derived from
///   PdfItem but not from PdfObject) must be immutable"</em> - and <see cref="PdfItem.Clone"/>
///   leans on it, because the default <c>Copy</c> is <c>MemberwiseClone</c>, which only copies a
///   value that cannot later change under either the original or the copy. Nothing checked the
///   rule. There is no <c>sealed</c> requirement in the type system and this fork carries no
///   analyzer, so a simple type with a mutable field compiled and shipped: <c>PdfString</c> did,
///   with a comment above the field apologising for it.
///
///   <para>
///   This sweeps the assembly for every type the rule's own wording covers and reads it back as a
///   predicate rather than a paraphrase. A type that grows a mutable field fails here, by name.
///   </para>
/// </summary>
public class SimpleTypeImmutabilityTests
{
    /// <summary>
    ///   Every type the rule covers: derived from <see cref="PdfItem"/>, not from
    ///   <see cref="PdfObject"/>, and concrete. Excluded types are still enumerated here, so that
    ///   <see cref="TheOnlyExcludedTypeIsPdfReference"/> can assert what the exclusion list holds.
    /// </summary>
    internal static IEnumerable<Type> AllSimpleTypes() =>
        typeof(PdfItem).Assembly
            .GetTypes()
            .Where(t => typeof(PdfItem).IsAssignableFrom(t)
                        && !typeof(PdfObject).IsAssignableFrom(t)
                        && !t.IsAbstract)
            .OrderBy(t => t.FullName, StringComparer.Ordinal);

    /// <summary>
    ///   <see cref="PdfReference"/> keeps neither half of the rule, and is not meant to. It is not
    ///   a value the way a string or a number is; it is an indirection cell - an object identity
    ///   plus wherever the indirection currently points - and being re-pointed is its whole job.
    ///   <c>Position</c> is rewritten by the writer as objects are laid out, <c>ObjectID</c> moves
    ///   on renumbering, <c>Document</c> is assigned when the reference is filed into a
    ///   cross-reference table, and <c>Value</c> is reassigned for the "dead object" hack. Nothing
    ///   reads a reference's <c>Value</c> as <em>the</em> value the way code reads a
    ///   <see cref="PdfString"/>'s.
    ///
    ///   <para>
    ///   It is a named array rather than a filter predicate or a try/catch around the assertions,
    ///   because either of those would let a second type slip in silently. Adding to this list is a
    ///   one-line diff that also fails <see cref="TheOnlyExcludedTypeIsPdfReference"/>.
    ///   </para>
    /// </summary>
    private static readonly Type[] Excluded = { typeof(PdfReference) };

    public static TheoryData<Type> SimpleTypesUnderTheRule() =>
        new TheoryData<Type>(AllSimpleTypes().Where(t => !Excluded.Contains(t)));

    [Theory]
    [MemberData(nameof(SimpleTypesUnderTheRule))]
    public void ASimpleTypeIsSealed(Type type)
    {
        // An unsealed simple type invites a subclass that adds the mutable field the base was
        // careful not to have, and every type keeping the rule today is already sealed.
        type.IsSealed.Should().BeTrue(
            "{0} is a simple type, and a simple type must be immutable, subclasses included", type.Name);
    }

    [Theory]
    [MemberData(nameof(SimpleTypesUnderTheRule))]
    public void ASimpleTypeDeclaresNoFieldThatCanBeAssignedAfterConstruction(Type type)
    {
        // Not DeclaredOnly: a future intermediate class - another PdfNumber - could add a field,
        // and a field inherited from one is just as mutable as a field declared here.
        var writable = type
            .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(f => !f.IsInitOnly && !f.IsLiteral)
            .Select(f => f.Name)
            .ToArray();

        // Checking the fields is enough on its own. A readonly field can only be assigned inside a
        // constructor - the compiler says so, not this test - so an auto-property setter fails here
        // through its compiler-generated backing field, and a hand-written mutating method fails
        // because it would have nowhere to write. Enumerating setters as well could only ever agree.
        writable.Should().BeEmpty(
            "a simple type must be immutable, and a field that is not readonly can be assigned after construction");
    }

    [Fact]
    public void TheOnlyExcludedTypeIsPdfReference()
    {
        Excluded.Should().Equal(new[] { typeof(PdfReference) },
            "an exclusion has to be argued for in this test rather than added to a filter");
    }

    [Fact]
    public void TheAssertionsWouldIndeedFailATypeThatBrokeTheRule()
    {
        // An assertion is only worth making if what it forbids would fail it, and every type left
        // under the rule passes - so the check is made against the one type deliberately taken out
        // from under it, which is mutable in exactly the way the rule is about.
        typeof(PdfReference)
            .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(f => !f.IsInitOnly && !f.IsLiteral)
            .Should().NotBeEmpty("otherwise the field check above is asserting nothing");
    }

    [Fact]
    public void TheSweepFindsTheTypesTheRuleWasWrittenAbout()
    {
        // A sweep that found nothing would pass every assertion above. This is what says it looked.
        AllSimpleTypes().Should().Contain(new[]
        {
            // PdfInteger names a test class of this assembly as well, hence the full name.
            typeof(PdfBoolean), typeof(PdfDate), typeof(PdfPinata.Pdf.PdfInteger),
            typeof(PdfLiteral), typeof(PdfLong), typeof(PdfName), typeof(PdfNull),
            typeof(PdfRectangle), typeof(PdfReal), typeof(PdfReference), typeof(PdfString),
            typeof(PdfUInteger)
        });
    }
}
