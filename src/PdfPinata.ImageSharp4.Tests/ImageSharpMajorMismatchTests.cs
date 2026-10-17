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
///     their own beside ImageSharp 2.1.13, which the project downloads for this and nothing else.
///   </para>
/// </summary>
public class ImageSharpMajorMismatchTests
{
    [Test]
    public void AgainstImageSharp2TheBackendSaysWhichImageSharpItNeeds()
    {
        var png = File.ReadAllBytes(PathHelper.GetInstance().GetAssetPath("lenna.png"));
        var context = new BesideImageSharp2();
        try
        {
            var imageSharp = context.LoadFromAssemblyName(new AssemblyName("SixLabors.ImageSharp"));
            imageSharp.GetName().Version!.Major.Should().Be(2, "the context resolves ImageSharp to 2.1.13");

            var rgba32 = imageSharp.GetType("SixLabors.ImageSharp.PixelFormats.Rgba32", throwOnError: true)!;
            var backend = context.LoadFromAssemblyName(new AssemblyName("PdfPinata.ImageSharp4"))
                .GetType("PdfPinata.Utils.ImageSharpImageSource`1", throwOnError: true)!
                .MakeGenericType(rgba32);

            var imageSource = context.LoadFromAssemblyName(new AssemblyName("PdfPinata"))
                .GetType("PinataLayout.DocumentObjectModel.Shapes.ImageSource", throwOnError: true)!;
            imageSource.GetProperty("ImageSourceImpl")!.SetValue(null, Activator.CreateInstance(backend));

            var fromBinary = imageSource.GetMethod("FromBinary", [typeof(string), typeof(Func<byte[]>), typeof(int?)])!;
            var load = () => fromBinary.Invoke(null, ["probe", (Func<byte[]>)(() => png), null]);

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
    ///   PdfPinata and its ImageSharp 4 backend from this test's own output, and ImageSharp from the
    ///   2.1.13 copy beside it. Everything else - the runtime - comes from the default context.
    /// </summary>
    private sealed class BesideImageSharp2() : AssemblyLoadContext(nameof(BesideImageSharp2), isCollectible: true)
    {
        private static readonly string Directory = AppContext.BaseDirectory;

        protected override Assembly Load(AssemblyName name)
        {
            if (name.Name == "SixLabors.ImageSharp")
                return LoadFromAssemblyPath(Path.Combine(Directory, "ImageSharp2", "SixLabors.ImageSharp.dll"));

            if (name.Name is "PdfPinata" or "PdfPinata.ImageSharp4")
                return LoadFromAssemblyPath(Path.Combine(Directory, name.Name + ".dll"));

            return null;
        }
    }
}
