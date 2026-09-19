using System;
using System.Collections.Generic;
using System.IO;
using AwesomeAssertions;
using PdfPinata.Fonts;
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
            var data = new TheoryData<string>();
            foreach (var name in DemoRegistry.Names)
                data.Add(name);
            return data;
        }
    }

    [Theory]
    [MemberData(nameof(EveryDemo))]
    public void A_demo_writes_the_pdf_it_says_it_does(string name)
    {
        DemoRegistry.TryGet(name, out var demo).Should().BeTrue();

        var context = new DemoContext(OutputDirectoryFor(name));
        // ReSharper disable once PossibleNullReferenceException
        var result = demo.Run(context);

        result.OutputPath.Should().Be(Path.Combine(context.OutputDirectory, name + ".pdf"));
        File.Exists(result.OutputPath).Should().BeTrue();
        new FileInfo(result.OutputPath).Length.Should().BeGreaterThan(0);

        // The password is almost always null. The one demo that encrypts its own output declares it
        // rather than being named here, so that this stays a theory over the registry with no demo
        // it knows about by name.
        using var opened = demo.OpenPassword is null
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
        DemoRegistry.TryGet(name, out var demo).Should().BeTrue();

        // The one failure the whole source-printing design exists to prevent is a panel of
        // code that is not the code that ran. A demo whose constructor forgot its ": base()"
        // would report another file's name here.
        // ReSharper disable once PossibleNullReferenceException
        demo.SourceFileName.Should().Be(demo.GetType().Name + ".cs");

        // Read prefers the file on disk, which exists on the machine that built it. The
        // embedded copy is what a published binary and any other machine actually get, so
        // it is checked directly rather than through the fallback that would hide its
        // absence here.
        Assets.Exists(Assets.SourcePrefix + demo.SourceFileName).Should().BeTrue();

        var example = DemoSource.Example(demo);
        example.Should().NotBeNullOrWhiteSpace();
        example.Should().NotContain(DemoSource.BeginMarker);
        example.Should().NotContain(DemoSource.SnippetMarkerPrefix);
    }

    [Fact]
    public void The_printed_example_leaves_out_the_documentation_excerpt_markers()
    {
        const string source = """
            class Demo
            {
                void Build()
                {
                    #region example
                    // docs:begin first
                    int a = 1;
                    // docs:end first
                    int b = 2;
                    #endregion
                }
            }
            """;

        var example = DemoSource.ExampleFrom(source);

        example.Should().Be("int a = 1;" + Environment.NewLine + "int b = 2;");
    }

    [Theory]
    [MemberData(nameof(EveryDemo))]
    public void Every_documentation_excerpt_a_demo_marks_is_closed_once_and_in_order(string name)
    {
        DemoRegistry.TryGet(name, out var demo).Should().BeTrue();
        var source = DemoSource.Read(demo);
// ReSharper disable once AssignNullToNotNullAttribute

        // The website quotes these excerpts by name and fails its build on one it cannot find, but
        // it only looks for the ones some page asks for. An excerpt nobody quotes yet is checked here.
        var open = new HashSet<string>();
        var seen = new HashSet<string>();
        // ReSharper disable PossibleNullReferenceException
        foreach (var raw in source.Replace("\r\n", "\n").Split('\n'))
        {
        // ReSharper restore PossibleNullReferenceException
            var line = raw.Trim();
            if (!line.StartsWith(DemoSource.SnippetMarkerPrefix, StringComparison.Ordinal))
                continue;

            var parts = line[DemoSource.SnippetMarkerPrefix.Length..].Split(' ', 2);
            parts.Should().HaveCount(2, "a marker is '// docs:begin name' or '// docs:end name': {0}", line);
            var excerpt = parts[1].Trim();

            if (parts[0] == "begin")
            {
                seen.Add(excerpt).Should().BeTrue("excerpt '{0}' is begun twice", excerpt);
                open.Add(excerpt);
            }
            else
            {
                parts[0].Should().Be("end", "the only markers are begin and end: {0}", line);
                open.Remove(excerpt).Should().BeTrue("excerpt '{0}' is ended without being begun", excerpt);
            }
        }

        open.Should().BeEmpty("every excerpt begun is ended");
    }

    [Fact]
    public void Running_a_demo_leaves_the_font_resolver_the_tests_installed()
    {
        DemoRegistry.TryGet("HelloWorld", out var demo).Should().BeTrue();
        // ReSharper disable once PossibleNullReferenceException
        demo.Run(new DemoContext(OutputDirectoryFor("ResolverCheck")));

        // A demo that registered a backend of its own would swap out the resolver every
        // other test in this assembly is laid out with - or throw, depending on what ran
        // first, which would pass in isolation and fail in the suite.
        GlobalFontSettings.FontResolver.Should().BeOfType<PinnedFontResolver>();
    }

    [Fact]
    public void Every_demo_has_a_name_that_is_usable_as_a_file_name()
    {
        foreach (var demo in DemoRegistry.All)
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
