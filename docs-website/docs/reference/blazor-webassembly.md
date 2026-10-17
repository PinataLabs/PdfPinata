---
title: Blazor WebAssembly
description: Build PDFs in the browser with Blazor WebAssembly - choose a backend, serve fonts from wwwroot, download the result, and what does not work there.
---

PdfPinata and PinataLayout run inside Blazor WebAssembly, in the browser, with no server. The
library is managed code and starts no threads, so most of it works as it is. Four things are
different in the browser:

- **There are no font files.** The backend font resolvers fail at the first font. Use
  `InMemoryFontResolver` and give it the fonts yourself.
- **There is no real file system.** `Save(path)` writes to a file system in memory that the user
  cannot see. Save to a stream and start a download from JavaScript.
- **There is no AES.** A document encrypted with AES does not open.
- **There is no signing.** `PdfPinata.Signing` does not run. Sign on a server.

**Blazor Server needs none of this.** It runs on the server like any other .NET application, and
everything on this page applies only to Blazor WebAssembly. The one trap of Blazor Server is the
same as for any Linux host: in a container with no fonts, the first font fails. See
[Containers](./platforms-and-deployment.md#containers).

## Choose a backend

**Use `PdfPinata.ImageSharp4`.** It is managed code only, it is trimmable, and it needs nothing
added to the build. A Blazor WebAssembly project publishes with it as it is. The publish prints a
notice that recommends the `wasm-tools` workload. You need that workload only for
[AOT compilation](#performance) or for the native libraries below.

```sh
dotnet add package PdfPinata.ImageSharp4
dotnet add package PinataLayout.Rendering
```

`PdfPinata.ImageSharp4` is built on ImageSharp 4 and Fonts 3, which use the Six Labors Split
License. Your application needs no Six Labors licence key for them. In Six Labors' words: "If you
use a third party package that depends on our libraries, you do not need a license." If your
application also references ImageSharp directly, the licence applies to that use.

**`PdfPinata.ImageSharp` is the Apache-2.0 alternative.** It is built on ImageSharp 2.1, which is
end of life and has security advisories that will not be fixed. It works in the browser in the same
way. The two packages have the same types in the same namespaces, so to change from one to the
other, you change only the package reference. The code on this page is the same for both.
ImageSharp 4 adds about 120 KB to the compressed download.
[Choose an ImageSharp package](../installation.md#choose-an-imagesharp-package) and
[Licensing](./licensing.md#the-two-imagesharp-packages) explain the choice.

`PdfPinata.Skia` and `PdfPinata.HarfBuzz` call native code, so the browser needs WebAssembly builds
of SkiaSharp and HarfBuzzSharp. To use them:

1. Add `SkiaSharp.NativeAssets.WebAssembly`, `HarfBuzzSharp.NativeAssets.WebAssembly`, or both. Use
   the same version as the managed SkiaSharp or HarfBuzzSharp package. To see that version, run
   `dotnet list package --include-transitive`.
2. Install the `wasm-tools` workload: `dotnet workload install wasm-tools`.
3. Build again. The build links the native library into the .NET runtime that the browser
   downloads, which makes the download several megabytes larger.

SkiaSharp's own documentation for Blazor WebAssembly covers these steps in more detail. PdfPinata
adds nothing to them.

**Without a shaper, some scripts are drawn wrong.** `PdfPinata.HarfBuzz` is the only shaper. Without
it, each character maps to one glyph. The core still puts Hebrew and Arabic in right-to-left order,
but Arabic letters do not join, Indic scripts are not reordered, and no text gets kerning or
ligatures. Latin, Greek and Cyrillic text is correct. If your documents need Arabic or an Indic
script, you need HarfBuzz and its native steps. See
[International text](../fonts-and-text/international-text.md#shape-complex-scripts-with-harfbuzz).

## Register the backend in Program.cs

Set the backend properties once, in `Program.cs`, before `RunAsync`. No component should set them,
because they apply to the whole application and a different font resolver cannot be set after the
first font exists.

Put the font files in `wwwroot/fonts`, then fetch them in `Program.cs`:

```csharp
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using PdfPinata.Fonts;
using PdfPinata.Utils;
using PinataLayout.DocumentObjectModel.Shapes;
using SixLabors.ImageSharp.PixelFormats;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");
builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

using var http = new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) };

var fonts = new InMemoryFontResolver();
foreach (string file in new[]
{
    "LiberationSans-Regular.ttf", "LiberationSans-Bold.ttf",
    "LiberationSans-Italic.ttf", "LiberationSans-BoldItalic.ttf",
})
{
    fonts.AddFont(await http.GetByteArrayAsync("fonts/" + file));
}
fonts.AddAlias("Arial", "Liberation Sans");

GlobalFontSettings.FontResolver = fonts;
ImageSource.ImageSourceImpl = new ImageSharpImageSource<Rgba32>();
GlobalFontSettings.GlyphOutlineProvider = new ImageSharpGlyphOutlineProvider();

await builder.Build().RunAsync();
```

`InMemoryFontResolver` is in the core package, in namespace `PdfPinata.Utils`, so it works with
either backend. The three ImageSharp types are in `PdfPinata.Utils` too, in both ImageSharp
packages. `Rgba32` comes from ImageSharp itself, through the package. `ImageSource` is in
`PinataLayout.DocumentObjectModel.Shapes`, although it ships in the core package.

You need `GlyphOutlineProvider` only for `XGraphicsPath.AddString` and for drop caps in
`XTextFormatter`. [Installation](../installation.md#register-the-backend-at-startup) says what each
property does.

## Fonts

**Do not register `ImageSharpFontResolver` or `SkiaFontResolver` in the browser.** Both look for the
fonts installed on the machine, and they know only Windows, macOS and Linux. In the browser, the
first font they resolve throws:

```text
NotImplementedException: FontResolver not implemented for this platform (PdfPinata.Utils.FontResolverBase.cs).
```

`InMemoryFontResolver` takes the bytes of each font from you and never reads a file:

- **`AddFont(byte[])`** reads the family name and the style from the font itself, so you do not name
  each file. A `.ttc` or `.otc` collection adds every face it holds. The resolver keeps a copy of
  the bytes.
- **`AddFont(Stream)`** reads a stream to its end, for a font embedded as a resource in your
  assembly. Do not give it a stream from `HttpClient`: a browser cannot read that stream
  synchronously, and `AddFont` throws. Fetch the bytes with `await http.GetByteArrayAsync(...)` and
  call `AddFont(byte[])`, as the example does.
- **`AddAlias("Arial", "Liberation Sans")`** answers a request for one family with another. Use it
  when code or a document asks for a font that you do not ship. Liberation Sans has the same widths
  as Arial, so lines wrap at the same words. An alias is not followed through another alias.
- **`DefaultFontName`** is the family of the first font you add, unless you set it. PinataLayout's
  `Normal` style uses it, so with the example above, a document is set in Liberation Sans.
- **An unknown family gets the default family**, in the style asked for, and nothing tells you. Set
  `NullIfFontNotFound = true` to make an unknown family fail instead.
- **Add every font before the first document.** The library asks about each family and style once
  and keeps the answer, so a font added later does not change a family already asked for.

With no fonts added, the first font, or `new Document()`, throws:

```text
InvalidOperationException: No fonts have been added to the InMemoryFontResolver. Call AddFont with at least one font before a document uses one - in a browser there are no system fonts to fall back on.
```

Each font is a download for the user. Add only the faces your documents use. If a family has no bold
or italic face, PdfPinata simulates it. See
[Simulated bold and italic](../fonts-and-text/fonts.md#simulated-bold-and-italic).

## Save and download

`PdfDocument.Save(path)` works, but the file goes to a file system in the browser's memory, and the
user cannot get it. Save to a `MemoryStream` and give it to JavaScript to download.

Add this function to `wwwroot/index.html`, after the Blazor script, or to a script file of your own:

```html
<script>
  window.downloadFileFromStream = async (fileName, contentStreamReference) => {
    const arrayBuffer = await contentStreamReference.arrayBuffer();
    const blob = new Blob([arrayBuffer], { type: 'application/pdf' });
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = fileName;
    anchor.click();
    anchor.remove();
    setTimeout(() => URL.revokeObjectURL(url), 60000);
  };
</script>
```

Do not revoke the URL immediately after `click()`. Some browsers start the download
asynchronously, and if the URL is already revoked, the download fails. The function above revokes
it after one minute.

Then render the document and send the stream from a component:

```razor
@using PdfPinata.Pdf
@using PinataLayout.DocumentObjectModel
@using PinataLayout.Rendering
@inject IJSRuntime JS

<button @onclick="DownloadAsync">Download PDF</button>

@code {
    private async Task DownloadAsync()
    {
        Document document = new Document();
        document.AddSection().AddParagraph("Made in the browser.");

        PdfDocumentRenderer renderer = new PdfDocumentRenderer(true) { Document = document };
        renderer.RenderDocument();

        using MemoryStream stream = new MemoryStream();
        renderer.PdfDocument.Save(stream);   // leaves the stream open, at position 0

        using DotNetStreamReference streamReference = new DotNetStreamReference(stream);
        await JS.InvokeVoidAsync("downloadFileFromStream", "document.pdf", streamReference);
    }
}
```

`Save(Stream)` does not close the stream, and it sets the position back to 0, so the stream is ready
to read.

## Open files and load images

The same applies to input: a method that takes a path reads the file system in memory, not the
user's disk. Use the overload that takes a stream or bytes:

| Instead of | Use |
|---|---|
| `PdfReader.Open(path, ...)` | `PdfReader.Open(stream, ...)` |
| `XPdfForm.FromFile(path)` | `XPdfForm.FromStream(stream)` |
| `XImage.FromFile(path)` | `XImage.FromStream(() => new MemoryStream(bytes))` |
| `ImageSource.FromFile(path)` | `ImageSource.FromBinary(name, () => bytes)` or `ImageSource.FromStream(name, () => stream)` |

`PdfReader.Open` needs a stream that can seek. A file the user picks with `InputFile` comes as a
stream that cannot seek and cannot be read synchronously, so copy it into a `MemoryStream` first:

```csharp
private async Task OnFileChosen(InputFileChangeEventArgs e)
{
    using MemoryStream buffer = new MemoryStream();
    await e.File.OpenReadStream(maxAllowedSize: 20 * 1024 * 1024).CopyToAsync(buffer);
    buffer.Position = 0;

    using PdfDocument document = PdfReader.Open(buffer, PdfDocumentOpenMode.Import);
    pageCount = document.PageCount;
}
```

`OpenReadStream` refuses a file larger than 500 KB unless you pass `maxAllowedSize`.

To put an image from `wwwroot` into a PinataLayout document, fetch its bytes and pass an image
source to `AddImage`:

```csharp
byte[] logo = await Http.GetByteArrayAsync("images/logo.png");
section.AddImage(ImageSource.FromBinary("logo.png", () => logo));
```

## Encrypted documents

.NET in the browser has no AES. A document encrypted with AES-128 (revision 4 of the standard
security handler) or AES-256 (revisions 5 and 6) does not open. `PdfReader.Open` throws
`NotSupportedException`, with the runtime's `PlatformNotSupportedException` as its
`InnerException`. For an AES-256 document, the message is:

```text
AES-256 encryption is not supported on this platform, the browser (.NET on WebAssembly): its .NET runtime provides no AES. This document is encrypted with AES-256, so it cannot be opened here. Open it on a server or desktop instead, or use a copy that is not encrypted or is encrypted with RC4, which works on every platform.
```

For an AES-128 document, it says `AES-128`. The message is written for the user, so you can show it
as it is:

```csharp
try
{
    using PdfDocument document = PdfReader.Open(buffer, password, PdfDocumentOpenMode.Import);
    // ...
}
catch (NotSupportedException ex)
{
    errorMessage = ex.Message;
}
```

The error can come before PdfPinata checks the password, so a document with AES fails with this
message even when the password is wrong.

RC4 works in the browser. Documents encrypted with 40-bit or 128-bit RC4 open, and PdfPinata writes
only RC4, so [protecting a document](../standards/encryption.md#protect-a-document) and saving it
work as on any other platform.

## Digital signatures

`PdfPinata.Signing` does not run in the browser. It needs CMS, X.509 certificates and RSA from
`System.Security.Cryptography.Pkcs`, and .NET on WebAssembly does not have them. A private key in
the browser would also be visible to anyone who uses the page.

Build the document in the browser and sign it on a server. The browser sends the unsigned PDF, and
the server signs it and sends it back:

1. In the browser, render the document and save it to a `MemoryStream`.
2. Send the bytes to your server with `HttpClient.PostAsync`.
3. On the server, pass the bytes to `PdfSigner.Sign(input, output, signer)` with a signer:
   `Pkcs7Signer` from `PdfPinata.Signing`, or your own `IPdfSigner` for a hardware security module
   or a signing service.
4. Return the signed bytes, and download them in the browser as in
   [Save and download](#save-and-download).

In the browser:

```csharp
using MemoryStream unsigned = new MemoryStream();
renderer.PdfDocument.Save(unsigned);

using ByteArrayContent content = new ByteArrayContent(unsigned.ToArray());
content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
using HttpResponseMessage response = await Http.PostAsync("api/sign", content);
response.EnsureSuccessStatusCode();

using MemoryStream signed = new MemoryStream(await response.Content.ReadAsByteArrayAsync());
using DotNetStreamReference streamReference = new DotNetStreamReference(signed);
await JS.InvokeVoidAsync("downloadFileFromStream", "signed.pdf", streamReference);
```

On the server, for example in an ASP.NET Core minimal API:

```csharp
app.MapPost("/api/sign", async (HttpRequest request) =>
{
    using MemoryStream unsigned = new MemoryStream();
    await request.Body.CopyToAsync(unsigned);
    unsigned.Position = 0;

    using MemoryStream signed = new MemoryStream();
    PdfSigner.Sign(unsigned, signed, signer, new PdfSignatureOptions { Reason = "Approved" });
    return Results.File(signed.ToArray(), "application/pdf");
});
```

Here `signer` is a `Pkcs7Signer` that the server creates at startup from its certificate. Check who
calls the endpoint before you sign: anyone who can post to it can get a document signed with your
key.

The whole `PdfSigner.Sign` call runs on the server. It cannot run in the browser with an `IPdfSigner`
that sends only the hash to the server: `PdfSigner.Sign` calls `IPdfSigner.Sign` synchronously while
it writes the file, and .NET in the browser cannot wait synchronously for an HTTP response.
[Digital signatures](../standards/digital-signatures.md) covers signers, certificates and options.

## Performance

**Generating a document blocks the page.** Blazor WebAssembly runs your code on the browser's one UI
thread, and PdfPinata's work is synchronous. While a document renders, the page does not respond and
does not redraw. To show that work has started, set a busy flag and give Blazor a chance to render
it before you start:

```csharp
busy = true;
await Task.Delay(1);   // lets Blazor render the busy state
try
{
    // render and save the document
}
finally
{
    busy = false;
}
```

**Compile ahead of time for speed.** Without AOT compilation, the browser runs .NET code through an
interpreter. Ahead-of-time compilation makes code that does a lot of work, such as layout and
compression, much faster. Turn it on in the project file:

```xml
<PropertyGroup>
  <RunAOTCompilation>true</RunAOTCompilation>
</PropertyGroup>
```

AOT compilation needs the `wasm-tools` workload. It applies when you publish, not when you build,
it makes publishing slower, and it makes the download larger.

**Generate large documents somewhere else.** For long documents, or documents with many images,
generate on a server and download the result. Or run the .NET code in a Web Worker, so that the page
stays responsive while the document renders. A Web Worker is a separate .NET runtime, so it needs
its own font resolver and backend, registered when the worker starts.

## Trimming

Blazor WebAssembly trims the application when you publish. The PdfPinata packages that the browser
can use are marked trimmable: `PdfPinata`, `PdfPinata.ImageSharp4`, `PdfPinata.ImageSharp`,
`PdfPinata.Charting`, `PdfPinata.EInvoice`, `PinataLayout.DocumentObjectModel` and
`PinataLayout.Rendering`.

A Blazor publish hides trim warnings by default. To see them for your own application, publish with
`-p:SuppressTrimAnalysisWarnings=false`.

The repository's CI publishes the sample app trimmed, with `PdfPinata.ImageSharp4`, and fails if
the trimmer warns about code in a PdfPinata or PinataLayout assembly. The trimmer analyses only the
code an app can reach, so this covers what the sample uses: text, tables, images, charts, saving,
and opening a document. You will still see warnings from ASP.NET Core itself. ImageSharp 4 gives
none for the sample. ImageSharp 2.1, with `PdfPinata.ImageSharp`, gives one.

## Things to know

- **Register everything in `Program.cs`.** The backend properties are static and apply to the whole
  application. A component that sets them can run after the first font exists, and then setting a
  different font resolver throws.
- **`XImage.FromStream` and `ImageSource.FromStream` take a function**, not a stream. Give them a
  function that opens a stream over bytes you already hold, such as `() => new MemoryStream(bytes)`.
  The function cannot fetch the image, because it is called synchronously.
- **Ship the font licence.** The Liberation fonts use the SIL Open Font License, which requires the
  licence to go with the font files. Put it in `wwwroot/fonts` beside them.

## See it in action

The repository's [Blazor WebAssembly sample](https://github.com/PinataLabs/PdfPinata/tree/main/src/SampleBlazorWasm)
does everything on this page: it registers `PdfPinata.ImageSharp4` and Liberation Sans from `wwwroot`, builds a
document with text, a table, an image and a chart, and downloads it.
