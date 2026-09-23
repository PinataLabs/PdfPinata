using System;
using System.IO;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel;
using Xunit;

namespace PinataLayout.Rendering.Tests;

/// <summary>
///   Where <see cref="PdfDocumentRenderer.Save(string)"/> puts the file.
/// </summary>
/// <remarks>
///   <see cref="PdfDocumentRenderer.WorkingDirectory"/> is what a relative path is resolved against,
///   and it used to decide nothing at all: the line meant to apply it called
///   <see cref="Path.Combine(string, string)"/> and threw the result away, so every relative path was
///   written against whatever the process's current directory happened to be. Nothing here covered
///   <c>Save</c> with a working directory, which is why a discarded return value stood for as long as
///   it did.
/// </remarks>pdf
public class SaveWorkingDirectoryTests
{
    [Fact]
    public void ARelativePathIsWrittenUnderTheWorkingDirectory()
    {
        using var directory = new TemporaryDirectory();
        var renderer = Rendered();
        renderer.WorkingDirectory = directory.Path;

        // Named uniquely, and the current directory's copy cleared away afterwards, so that what this
        // asserts is not there cannot be litter from an earlier run that wrote it there.
        var name = Unique();
        try
        {
            renderer.Save(name);

            File.Exists(Path.Combine(directory.Path, name)).Should().BeTrue();
            File.Exists(Path.GetFullPath(name)).Should().BeFalse(
                "the current directory is not where a working directory was named");
        }
        finally
        {
            File.Delete(Path.GetFullPath(name));
        }
    }

    /// <summary>
    ///   A path with directories of its own is still relative to the working directory rather than to
    ///   the current one.
    /// </summary>
    [Fact]
    public void ARelativePathMayNameDirectoriesOfItsOwn()
    {
        using var directory = new TemporaryDirectory();
        Directory.CreateDirectory(Path.Combine(directory.Path, "out"));
        var renderer = Rendered();
        renderer.WorkingDirectory = directory.Path;

        // No cleanup of the current directory needed, and none possible: it has no "out" directory, so
        // the broken behaviour this pins does not write a stray file - it fails to write one at all.
        var name = Path.Combine("out", Unique());
        renderer.Save(name);

        File.Exists(Path.Combine(directory.Path, name)).Should().BeTrue();
    }

    /// <summary>
    ///   An absolute path is what it says, working directory or no. This is what makes the fix safe
    ///   for a caller who already passes one: <see cref="Path.Combine(string, string)"/> answers an
    ///   absolute second argument with itself.
    /// </summary>
    [Fact]
    public void AnAbsolutePathIsWrittenWhereItSays()
    {
        using var working = new TemporaryDirectory();
        using var elsewhere = new TemporaryDirectory();
        var renderer = Rendered();
        renderer.WorkingDirectory = working.Path;

        var target = Path.Combine(elsewhere.Path, "document.pdf");
        renderer.Save(target);

        File.Exists(target).Should().BeTrue();
        File.Exists(Path.Combine(working.Path, "document.pdf")).Should().BeFalse();
    }

    /// <summary>
    ///   With no working directory named, a relative path is resolved against the current directory,
    ///   exactly as before.
    /// </summary>
    [Fact]
    public void WithNoWorkingDirectoryARelativePathIsStillTheCurrentDirectorys()
    {
        var name = Unique();
        var expected = Path.GetFullPath(name);

        try
        {
            Rendered().Save(name);

            File.Exists(expected).Should().BeTrue();
        }
        finally
        {
            File.Delete(expected);
        }
    }

    [Fact]
    public void APathIsStillRequired()
    {
        var renderer = Rendered();

        renderer.Invoking(r => r.Save(null)).Should().Throw<ArgumentNullException>();
        renderer.Invoking(r => r.Save("")).Should().Throw<ArgumentException>();
    }

    /// <summary>
    ///   A file name no other test and no earlier run can have written, because two of these assert
    ///   that a file is <em>not</em> somewhere.
    /// </summary>
    private static string Unique() => "save-working-directory-" + Guid.NewGuid().ToString("N") + ".pdf";

    /// <summary>A one page document, rendered and ready to be written.</summary>
    private static PdfDocumentRenderer Rendered()
    {
        var document = new Document();
        document.AddSection().AddParagraph("Where does this land?");

        var renderer = new PdfDocumentRenderer(true) { Document = document };
        renderer.RenderDocument();
        return renderer;
    }

    /// <summary>A directory of its own for a test that writes a file, removed with the test.</summary>
    private sealed class TemporaryDirectory : IDisposable
    {
        internal TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(), "PinataLayout-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        internal string Path { get; }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch (IOException)
            {
                // A directory the machine will not let go of is not a failure of what was under test.
            }
        }
    }
}
