# Font Resolver

> **Runnable version:** the `Fonts` demo.
> `dotnet run --project SampleApp -- run -e Fonts`
>
> The demos are built on every commit and their page counts are asserted by
> `DemoSmokeTests`, so one that stops working fails the build. The code on this page is
> prose and has no such protection. See
> [Before any of this runs](index.md#before-any-of-this-runs) - this fork needs a backend
> registered before it will draw anything.

This sample shows how to use fonts that are included with your application. This allows you to use fonts that are not installed on the computer.


## There is no default font resolver

**This fork ships none, and that is deliberate.** The core package carries no font dependency of its
own, so `GlobalFontSettings.FontResolver` starts unset and throws a descriptive
`InvalidOperationException` when read. One of the backend packages has to supply it:

```cs
GlobalFontSettings.FontResolver = new SkiaFontResolver();          // PdfPinata.Skia
GlobalFontSettings.FontResolver = new ImageSharpFontResolver();    // PdfPinata.ImageSharp
```

Both are built on [`FontResolverBase`](../../../PdfPinata/Utils/FontResolverBase.cs), which does
the searching, and both use the fonts installed on the operating system. The directories searched
depend on which one that is — see
[`LinuxSystemFontResolver`](../../../PdfPinata/Utils/LinuxSystemFontResolver.cs) for the Linux
list:

**Windows**
1. `%SystemRoot%\Fonts`
1. `%LOCALAPPDATA%\Microsoft\Windows\Fonts`

**Linux**
1. `/usr/share/fonts`
1. `/usr/local/share/fonts`
1. `~/.fonts`

**iOS**
1. `/Library/Fonts/`


## Custom font resolver

When running on web services or servers, the operating system might **not** have the fonts installed you need or you can **not** install the font you need.
In this scenario you must provide the fonts yourself and therefore implement your own font resolver.

### IFontResolver interface

In your application you create a class that implements the `IFontResolver` interface.

There are three members. The first returns a `FontResolverInfo` for every supported font, or `null`
where the request cannot be satisfied.

```cs
public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
```

The second is called using the `FaceName` from the `FontResolverInfo` you previously returned. At
this stage, return the font data as a byte array.

```cs
public byte[] GetFont(string faceName)
```

The third names the family to fall back on when nothing else has been asked for — MigraDoc's image
placeholder draws its message in it, for one.

```cs
public string DefaultFontName { get; }
```

Now you only need one more step: register your font resolver using the global font resolver property.
Here MyFontResolver is the class that implements `IFontResolver`.

```cs
GlobalFontSettings.FontResolver = new MyFontResolver();
```

Note: The `FontResolver` is a global object and applies to all consumers of the PdfPinata library. It is also used when the PinataLayout library creates PDF files.

**Set it before any font is created.** The setter throws once one has been, because a face already
resolved cannot be un-resolved and the two answers would disagree about what a family name means.

If you also draw text as outlines with `XGraphicsPath.AddString`, register a
`GlobalFontSettings.GlyphOutlineProvider` as well. That seam can be set, replaced or cleared at any
time, but a provider takes its font bytes *through* `FontResolver` rather than resolving a family
itself — or the two will disagree about which face a family means.

### Code

This implementation is obviously not complete.
But it should be enough for everyone to implement their own.
For more details have a look at
[`FontResolverBase`](../../../PdfPinata/Utils/FontResolverBase.cs), which both shipped resolvers
are built on, or at
[`BundledFontResolver`](../../../SampleApp/Infrastructure/BundledFontResolver.cs), which serves
faces out of the assembly exactly as this page describes so that the demos lay out identically
wherever they are built.

```cs
using System;
using System.IO;
using PdfPinata.Fonts;

public class MyFontResolver : IFontResolver
{
    public string DefaultFontName => "OpenSans";

    public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
    {
        if (familyName.Equals("OpenSans", StringComparison.CurrentCultureIgnoreCase))
        {
            if (isBold && isItalic)
            {
                return new FontResolverInfo("OpenSans-BoldItalic.ttf");
            }
            else if (isBold)
            {
                return new FontResolverInfo("OpenSans-Bold.ttf");
            }
            else if (isItalic)
            {
                return new FontResolverInfo("OpenSans-Italic.ttf");
            }
            else
            {
                return new FontResolverInfo("OpenSans-Regular.ttf");
            }
        }
        return null;
    }
    
    public byte[] GetFont(string faceName)
    {
        var faceNamePath = Path.Join("my path", faceName);
        using(var ms = new MemoryStream())
        {
            try
            {
                using(var fs = File.OpenRead(faceNamePath))
                {
                    fs.CopyTo(ms);
                    ms.Position = 0;
                    return ms.ToArray();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw new Exception($"No Font File Found - " + faceNamePath);
            }
        }
    }
}
```
