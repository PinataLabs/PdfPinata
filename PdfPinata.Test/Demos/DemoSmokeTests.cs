using System.IO;
using AwesomeAssertions;
using PdfPinata.Fonts;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using PdfPinata.Test.Helpers;
using SampleApp.Infrastructure;
using Xunit;

namespace PdfPinata.Test.Demos;

/// <summary>
///   Runs every demo the demonstration app offers and checks that it still produces the PDF it
///   says it does.
/// </summary>
/// <remarks>
///   <para>
///     This catches a demo that throws, writes nothing, or quietly repaginates. It cannot catch a
///     demo that has stopped demonstrating what it claims, which is why each one carries a list of
///     what to look for and why nothing here compares images - a deliberate change to how a demo
///     looks should not be a reference image to update.
///   </para>
///   <para>
///     The demos are run directly rather than through the app's runner, so that nothing here
///     touches <see cref="GlobalFontSettings.FontResolver"/>. The test assembly installs its own
///     resolver for everything in it, the setter throws once a font has been used, and a demo that
///     registered a backend would either fail depending on what had run before it or move every
///     other test in this assembly onto different font metrics.
///   </para>
/// </remarks>
public class DemoSmokeTests
{
    public static TheoryData<string> EveryDemo
    {
        get
        {
            TheoryData<string> data = new TheoryData<string>();
            foreach (string name in DemoRegistry.Names)
                data.Add(name);
            return data;
        }
    }

    [Theory]
    [MemberData(nameof(EveryDemo))]
    public void A_demo_writes_the_pdf_it_says_it_does(string name)
    {
        DemoRegistry.TryGet(name, out PdfDemo demo).Should().BeTrue();

        DemoContext context = new DemoContext(OutputDirectoryFor(name));
        DemoResult result = demo.Run(context);

        result.OutputPath.Should().Be(Path.Combine(context.OutputDirectory, name + ".pdf"));
        File.Exists(result.OutputPath).Should().BeTrue();
        new FileInfo(result.OutputPath).Length.Should().BeGreaterThan(0);

        // The password is almost always null. The one demo that encrypts its own output declares it
        // rather than being named here, so that this stays a theory over the registry with no demo
        // it knows about by name.
        using PdfDocument opened = demo.OpenPassword is null
            ? Pdf.IO.PdfReader.Open(result.OutputPath, PdfDocumentOpenMode.Import)
            : Pdf.IO.PdfReader.Open(result.OutputPath, demo.OpenPassword, PdfDocumentOpenMode.Import);

        // Pinned rather than derived, so that a change to the layout engine which silently
        // repaginates a document is a failing test rather than a surprise months later.
        opened.PageCount.Should().Be(demo.PageCount);
        result.PageCount.Should().Be(demo.PageCount);
    }

    [Theory]
    [MemberData(nameof(EveryDemo))]
    public void A_demo_can_show_the_source_it_was_written_in(string name)
    {
        DemoRegistry.TryGet(name, out PdfDemo demo).Should().BeTrue();

        // The one failure the whole source-printing design exists to prevent is a panel of
        // code that is not the code that ran. A demo whose constructor forgot its ": base()"
        // would report another file's name here.
        demo.SourceFileName.Should().Be(demo.GetType().Name + ".cs");

        // Read prefers the file on disk, which exists on the machine that built it. The
        // embedded copy is what a published binary and any other machine actually get, so
        // it is checked directly rather than through the fallback that would hide its
        // absence here.
        Assets.Exists(Assets.SourcePrefix + demo.SourceFileName).Should().BeTrue();

        string example = DemoSource.Example(demo);
        example.Should().NotBeNullOrWhiteSpace();
        example.Should().NotContain(DemoSource.BeginMarker);
    }

    [Fact]
    public void Running_a_demo_leaves_the_font_resolver_the_tests_installed()
    {
        DemoRegistry.TryGet("HelloWorld", out PdfDemo demo).Should().BeTrue();
        demo.Run(new DemoContext(OutputDirectoryFor("ResolverCheck")));

        // A demo that registered a backend of its own would swap out the resolver every
        // other test in this assembly is laid out with - or throw, depending on what ran
        // first, which would pass in isolation and fail in the suite.
        GlobalFontSettings.FontResolver.Should().BeOfType<PinnedFontResolver>();
    }

    [Fact]
    public void Every_demo_has_a_name_that_is_usable_as_a_file_name()
    {
        foreach (PdfDemo demo in DemoRegistry.All)
        {
            demo.Name.Should().NotBeNullOrWhiteSpace();
            demo.Name.IndexOfAny(Path.GetInvalidFileNameChars()).Should().Be(-1);
            demo.Summary.Should().NotBeNullOrWhiteSpace();
            demo.Shows.Should().NotBeEmpty();
            demo.PageCount.Should().BeGreaterThan(0);
        }

        DemoRegistry.Names.Should().OnlyHaveUniqueItems();
    }

    /// <summary>
    ///   A directory of its own per demo, under the test assembly's output, which the existing
    ///   tests already use and which sits under bin and is therefore ignored.
    /// </summary>
    static string OutputDirectoryFor(string name) =>
        Path.Combine(PathHelper.GetInstance().RootDir, "Out", "Demos", name);
}
