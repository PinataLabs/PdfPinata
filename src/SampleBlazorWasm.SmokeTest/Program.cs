using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using PdfPinata.Pdf.IO;

namespace SampleBlazorWasm.SmokeTest;

/// <summary>
/// Serves the published Blazor WebAssembly sample, drives it in headless Chromium and checks the PDF
/// it downloads. Returns non-zero if any check fails, so the step running it is the gate.
/// </summary>
/// <remarks>
/// <para>
/// Everything the sample builds is covered by PdfPinata.Test, which links its <c>SampleDocument</c>
/// and runs it on the desktop runtime. What only a browser shows is whether that code runs on .NET for
/// WebAssembly, trimmed, with the fonts and the image fetched over HTTP: a type the trimmer removed, a
/// platform API the browser lacks, a seam registered too late. Each of those surfaces as Generate
/// failing, which the sample reports as <c>Failed: ...</c> in its status line and no download.
/// </para>
/// <para>
/// So Generate is clicked while waiting both for the download and for a sign of failure - that status
/// line, or Blazor's own error banner for an exception nothing caught - and whichever comes first
/// decides. Waiting for the download alone would also fail, but only by timing out, and the log would
/// say how long it waited rather than what went wrong.
/// </para>
/// <para>
/// To run it by hand, as the workflow does:
/// <code>
/// dotnet publish src/SampleBlazorWasm -c Release -o artifacts/blazor-wasm
/// dotnet build src/SampleBlazorWasm.SmokeTest -c Release
/// pwsh src/SampleBlazorWasm.SmokeTest/bin/Release/net10.0/playwright.ps1 install chromium
/// dotnet run --project src/SampleBlazorWasm.SmokeTest -c Release --no-build -- artifacts/blazor-wasm/wwwroot
/// </code>
/// The second argument, if given, is where the download, a screenshot and the console log go;
/// <c>artifacts/blazor-wasm-smoke</c> otherwise.
/// </para>
/// </remarks>
public static class Program
{
    /// <summary>
    /// The page count of the sample's document, <c>SampleDocument.PageCount</c>. Written out rather
    /// than linked, so that this project references the library alone and not the sample's layout code.
    /// </summary>
    private const int ExpectedPageCount = 1;

    /// <summary>The name the sample saves its download under, <c>SampleDocument.FileName</c>.</summary>
    private const string ExpectedFileName = "pdfpinata-blazor.pdf";

    /// <summary>The start of the message PdfPinata gives for an AES document where there is no AES.</summary>
    private const string ExpectedAesMessage = "AES-256 encryption is not supported on this platform";

    // The first load fetches the runtime and the app, about 10 MB uncompressed, and the document is
    // laid out by the WebAssembly interpreter. Both take seconds locally; these allow for a slow runner.
    private const float BootTimeout = 120_000;
    private const float ActionTimeout = 120_000;

    private static readonly List<string> failures = [];

    public static async Task<int> Main(string[] args)
    {
        if (args.Length is < 1 or > 2)
        {
            Console.Error.WriteLine("Usage: SampleBlazorWasm.SmokeTest <published wwwroot> [output directory]");
            return 2;
        }

        var root = Path.GetFullPath(args[0]);
        var output = Path.GetFullPath(args.Length > 1 ? args[1] : Path.Combine("artifacts", "blazor-wasm-smoke"));
        if (!File.Exists(Path.Combine(root, "index.html")))
        {
            Console.Error.WriteLine($"No index.html in {root}: pass the wwwroot folder of a published SampleBlazorWasm.");
            return 2;
        }

        Directory.CreateDirectory(output);
        Console.WriteLine("Blazor WebAssembly sample smoke test");
        Console.WriteLine($"Serving {root}");

        await using var server = Serve(root);
        await server.StartAsync();
        var url = server.Urls.Single();
        Console.WriteLine($"Listening on {url}");

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync();
        var page = await browser.NewPageAsync(new() { AcceptDownloads = true });

        var console = new List<string>();
        page.Console += (_, message) =>
        {
            console.Add($"[{message.Type}] {message.Text}");
            if (message.Type == "error")
                Fail($"the page logged an error: {message.Text}");
        };
        page.PageError += (_, error) =>
        {
            console.Add($"[pageerror] {error}");
            Fail($"the page threw: {error}");
        };

        try
        {
            await page.GotoAsync(url, new() { Timeout = BootTimeout });
            // The buttons exist only once Blazor has started and replaced "Loading...".
            await page.Locator("#generate").WaitForAsync(new() { Timeout = BootTimeout });
            Console.WriteLine("The app started.");

            // An exception nothing caught leaves both buttons disabled, so the AES check would only
            // add a second timeout to the log.
            if (await CheckGenerate(page, output))
                await CheckOpenAes(page);
        }
        catch (Exception ex) when (ex is PlaywrightException or TimeoutException)
        {
            Fail($"{ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            await page.ScreenshotAsync(new() { Path = Path.Combine(output, "page.png"), FullPage = true });
            await File.WriteAllLinesAsync(Path.Combine(output, "console.log"), console);
            await server.StopAsync();
        }

        Console.WriteLine($"Screenshot, console log and download are in {output}");
        if (failures.Count > 0)
        {
            Console.WriteLine($"FAILED: {failures.Count} check(s)");
            return 1;
        }

        Console.WriteLine("All checks passed.");
        return 0;
    }

    /// <summary>Clicks Generate and checks the download. Answers false if Generate failed.</summary>
    private static async Task<bool> CheckGenerate(IPage page, string output)
    {
        var download = page.WaitForDownloadAsync(new() { Timeout = ActionTimeout });
        var failed = page.WaitForFunctionAsync(
            """
            () => (document.getElementById('generate-status')?.textContent ?? '').startsWith('Failed')
                || getComputedStyle(document.getElementById('blazor-error-ui')).display !== 'none'
            """,
            null,
            new() { Timeout = ActionTimeout });

        await page.Locator("#generate").ClickAsync();
        Console.WriteLine("Clicked Generate.");

        var first = await Task.WhenAny(download, failed);
        if (first == failed && failed.IsCompletedSuccessfully)
        {
            var status = await page.Locator("#generate-status").TextContentAsync();
            var banner = await page.Locator("#blazor-error-ui").IsVisibleAsync();
            Fail($"Generate failed. Status: \"{status}\"{(banner ? "; Blazor's error banner is showing" : "")}.");
            return false;
        }

        var file = await download;
        Check($"the download is named {ExpectedFileName}", file.SuggestedFilename == ExpectedFileName, file.SuggestedFilename);
        var path = Path.Combine(output, file.SuggestedFilename);
        await file.SaveAsAsync(path);
        Console.WriteLine($"Downloaded {new FileInfo(path).Length:N0} bytes to {path}");

        try
        {
            using var document = PdfReader.Open(path, PdfDocumentOpenMode.Import);
            Check($"the download opens with PdfReader and has {ExpectedPageCount} page(s)",
                document.PageCount == ExpectedPageCount, $"{document.PageCount} page(s)");
        }
        catch (Exception ex)
        {
            Fail($"PdfReader could not open the download: {ex.GetType().Name}: {ex.Message}");
        }

        await page.WaitForFunctionAsync(
            "() => (document.getElementById('generate-status')?.textContent ?? '').startsWith('Generated ')",
            null,
            new() { Timeout = ActionTimeout });
        Console.WriteLine($"Status: {await page.Locator("#generate-status").TextContentAsync()}");
        return true;
    }

    /// <summary>
    /// The browser has no AES, and the sample shows the message PdfPinata gives for it. The desktop
    /// tests can only pretend AES is missing; this is the one place the real platform says so.
    /// </summary>
    private static async Task CheckOpenAes(IPage page)
    {
        await page.Locator("#open-aes").ClickAsync();
        await page.WaitForFunctionAsync(
            """
            () => {
                const text = document.getElementById('aes-message')?.textContent ?? '';
                return text !== '' && text !== 'Opening...';
            }
            """,
            null,
            new() { Timeout = ActionTimeout });

        var message = await page.Locator("#aes-message").TextContentAsync() ?? "";
        Check("opening the AES-256 document gives PdfPinata's message for a platform without AES",
            message.StartsWith(ExpectedAesMessage, StringComparison.Ordinal), message);
    }

    /// <summary>
    /// Kestrel on a free loopback port, serving the published wwwroot at the site root, because the
    /// app's base href is "/". ServeUnknownFileTypes serves anything the content-type map does not
    /// name; nothing negotiates the precompressed .br and .gz copies, which is fine for a smoke test.
    /// </summary>
    private static WebApplication Serve(string root)
    {
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions { ContentRootPath = root });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.SetMinimumLevel(LogLevel.Warning);

        var app = builder.Build();
        var files = new PhysicalFileProvider(root);
        app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = files });
        app.UseStaticFiles(new StaticFileOptions { FileProvider = files, ServeUnknownFileTypes = true });
        return app;
    }

    private static void Check(string description, bool passed, string? actual = null)
    {
        if (passed)
            Console.WriteLine($"  ok: {description}");
        else
            Fail(actual is null ? description : $"{description} (got: {actual})");
    }

    private static void Fail(string message)
    {
        lock (failures)
            failures.Add(message);
        Console.WriteLine($"  FAIL: {message}");
    }
}
