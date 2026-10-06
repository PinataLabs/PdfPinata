using System;
using System.Reflection;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.AcroForms;
using PdfPinata.Pdf.Annotations;
using PdfPinata.Pdf.Structure;
using TUnit.Core;

namespace PdfPinata.Test.Pdfs;

/// <summary>
///   A PDF name is written with a leading solidus that is not part of the text it stands for, and
///   about twenty places in the library added or stripped it by hand, in several spellings - some
///   of them reading the first character without asking whether there was one. They now go
///   through <c>PdfName.WithSolidus</c> and <c>PdfName.WithoutSolidus</c>, both of which take an
///   empty string in their stride.
/// </summary>
/// <remarks>
///   The two helpers are internal and this repository carries no <c>InternalsVisibleTo</c>, so
///   they are reached by reflection, the way <c>TextNormalizationTests</c> reaches its subject.
///   The rest is asked through the public members that used to do the work inline.
/// </remarks>
public class NameSolidusTests
{
    [Test]
    [Arguments("Yes", "/Yes")]
    [Arguments("/Yes", "/Yes")]
    [Arguments("", "/")]
    [Arguments("/", "/")]
    [Arguments("//", "//")]
    public void WithSolidusPutsOneInFrontUnlessOneIsThere(string name, string expected)
    {
        WithSolidus(name).Should().Be(expected);
    }

    [Test]
    public void WithSolidusRefusesNull()
    {
        var act = () => WithSolidus(null);

        act.Should().Throw<ArgumentNullException>();
    }

    [Test]
    [Arguments("/Yes", "Yes")]
    [Arguments("Yes", "Yes")]
    [Arguments("/", "")]
    [Arguments("", "")]
    [Arguments(null, null)]
    public void WithoutSolidusTakesOneAwayWhenOneIsThere(string name, string expected)
    {
        WithoutSolidus(name).Should().Be(expected);
    }

    [Test]
    public void AnEmptyStructureTypeIsRefusedAsAnArgumentRatherThanByIndexingPastTheEnd()
    {
        var act = () => new PdfTag("");

        act.Should().Throw<ArgumentException>().Which.ParamName.Should().Be("name");
    }

    [Test]
    public void AnEmptyAppearanceStateIsRefusedAsAnArgument()
    {
        var document = new PdfDocument();
        var square = new PdfSquareAnnotation();
        document.AddPage().Annotations.Add(square);

        var act = () => square.SetAppearance("", new XForm(document, new XSize(10, 10)));

        act.Should().Throw<ArgumentException>().Which.ParamName.Should().Be("state");
    }

    [Test]
    public void AnEmptyStandardFontNameIsRefusedAsAnArgument()
    {
        var form = new PdfDocument().GetOrCreateAcroForm();

        var act = () => form.AddStandardFont("", "Helvetica");

        act.Should().Throw<ArgumentException>().Which.ParamName.Should().Be("resourceName");
    }

    [Test]
    public void AStructureTypeNamedWithoutItsSolidusIsGivenOne()
    {
        new PdfTag("Sect").Name.Should().Be("/Sect");
        new PdfTag("/Sect").Name.Should().Be("/Sect");
    }

    [Test]
    public void AnIconWrittenAsTheEmptyNameReadsAsNoIcon()
    {
        var document = new PdfDocument();
        var note = new PdfTextAnnotation();
        document.AddPage().Annotations.Add(note);

        // "/" is a legal name with nothing after its solidus - the one an empty string becomes.
        note.Elements["/Name"] = PdfName.Empty;

        note.Icon.Should().Be(PdfTextAnnotationIcon.NoIcon);
    }

    [Test]
    public void ALineEndingWrittenAsTheEmptyNameReadsAsNone()
    {
        var document = new PdfDocument();
        var line = new PdfLineAnnotation();
        document.AddPage().Annotations.Add(line);
        line.SetLine(new XPoint(10, 10), new XPoint(100, 100));

        line.Elements["/LE"] = new PdfArray(document, PdfName.Empty, new PdfName("/OpenArrow"));

        line.StartEnding.Should().Be(PdfLineEnding.None);
        line.EndEnding.Should().Be(PdfLineEnding.OpenArrow);
    }

    private static string WithSolidus(string name) => Invoke("WithSolidus", name);

    private static string WithoutSolidus(string name) => Invoke("WithoutSolidus", name);

    private static string Invoke(string method, string name)
    {
        var helper = typeof(PdfName).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static)
                     ?? throw new InvalidOperationException("PdfName." + method + " was not found.");
        try
        {
            return (string)helper.Invoke(null, [name]);
        }
        catch (TargetInvocationException e) when (e.InnerException != null)
        {
            throw e.InnerException;
        }
    }
}
