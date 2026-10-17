using System;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using AwesomeAssertions;
using PdfPinata.Test.Helpers;
using TUnit.Core;

namespace PdfPinata.ImageSharp4.Tests;

/// <summary>
///   An application can end up running PdfPinata.ImageSharp4 against an ImageSharp of another major,
///   when something else it references pins one, and the two are not binary compatible. The backend
///   then says which version it needs and which it got, as PdfPinata.ImageSharp does for 2.1.x,
///   rather than letting a <c>TypeLoadException</c> from inside it reach the caller.
///   <para>
///     The mismatch is made for real: the backend and the core are loaded into a load context of
///     their own beside ImageSharp 2.1.13 or 3.2.0, which the project downloads for this and
///     nothing else.
///   </para>
/// </summary>
public class ImageSharpMajorMismatchTests
{
    [Test]
    public void AgainstImageSharp2TheBackendSaysWhichImageSharpItNeeds()
    {
        var load = LoadBeside("ImageSharp2", expectedMajor: 2, out var context);
        try
        {
            load.Should().Throw<TargetInvocationException>()
                .WithInnerException<InvalidOperationException>()
                .WithMessage("PdfPinata.ImageSharp4 is built against SixLabors.ImageSharp 4.x*version 2.0.0.0 was loaded*")
                .WithInnerException<TypeLoadException>();
        }
        finally
        {
            context.Unload();
        }
    }

    /// <summary>
    ///   3.x has the <c>DecoderOptions</c> type and the <c>Load</c> overloads that take one, but not
    ///   the <c>ColorProfileHandling</c> the backend sets on it, so the failure comes from inside the
    ///   options rather than from a type that is missing altogether. Built in a static initializer,
    ///   that arrived as a <c>TypeInitializationException</c>, which the backend did not recognise
    ///   as a binding failure - and then again on every later load.
    /// </summary>
    [Test]
    public void AgainstImageSharp3TheBackendSaysWhichImageSharpItNeedsEveryTime()
    {
        var load = LoadBeside("ImageSharp3", expectedMajor: 3, out var context);
        try
        {
            for (var attempt = 0; attempt < 2; attempt++)
            {
                load.Should().Throw<TargetInvocationException>()
                    .WithInnerException<InvalidOperationException>()
                    .WithMessage("PdfPinata.ImageSharp4 is built against SixLabors.ImageSharp 4.x*version 3.0.0.0 was loaded*")
                    .WithInnerException<MissingMemberException>();
            }
        }
        finally
        {
            context.Unload();
        }
    }

    /// <summary>
    ///   Registers the backend, loaded beside the ImageSharp in <paramref name="folder"/>, as the
    ///   image source of a PdfPinata of its own, and answers a call that loads lenna.png through it.
    /// </summary>
    private static Func<object> LoadBeside(string folder, int expectedMajor, out AssemblyLoadContext context)
    {
        var png = File.ReadAllBytes(PathHelper.GetInstance().GetAssetPath("lenna.png"));
        var beside = new BesideImageSharp(folder);
        context = beside;

        var imageSharp = beside.LoadFromAssemblyName(new AssemblyName("SixLabors.ImageSharp"));
        imageSharp.GetName().Version!.Major.Should().Be(expectedMajor, $"the context resolves ImageSharp from {folder}");

        var rgba32 = imageSharp.GetType("SixLabors.ImageSharp.PixelFormats.Rgba32", throwOnError: true)!;
        var backend = beside.LoadFromAssemblyName(new AssemblyName("PdfPinata.ImageSharp4"))
            .GetType("PdfPinata.Utils.ImageSharpImageSource`1", throwOnError: true)!
            .MakeGenericType(rgba32);

        var imageSource = beside.LoadFromAssemblyName(new AssemblyName("PdfPinata"))
            .GetType("PinataLayout.DocumentObjectModel.Shapes.ImageSource", throwOnError: true)!;
        imageSource.GetProperty("ImageSourceImpl")!.SetValue(null, Activator.CreateInstance(backend));

        var fromBinary = imageSource.GetMethod("FromBinary", [typeof(string), typeof(Func<byte[]>), typeof(int?)])!;
        return () => fromBinary.Invoke(null, ["probe", (Func<byte[]>)(() => png), null]);
    }

    /// <summary>
    ///   PdfPinata and its ImageSharp 4 backend from this test's own output, and ImageSharp from the
    ///   copy in <paramref name="folder"/> beside them. Everything else - the runtime - comes from the
    ///   default context.
    /// </summary>
    private sealed class BesideImageSharp(string folder) : AssemblyLoadContext(nameof(BesideImageSharp) + folder, isCollectible: true)
    {
        private static readonly string Directory = AppContext.BaseDirectory;

        protected override Assembly Load(AssemblyName name)
        {
            if (name.Name == "SixLabors.ImageSharp")
                return LoadFromAssemblyPath(Path.Combine(Directory, folder, "SixLabors.ImageSharp.dll"));

            if (name.Name is "PdfPinata" or "PdfPinata.ImageSharp4")
                return LoadFromAssemblyPath(Path.Combine(Directory, name.Name + ".dll"));

            return null;
        }
    }
}
