---
title: Contributing
description: How to build and test PdfPinata, run the demos, work on this documentation site, and send a pull request.
---

Bug reports, fixes and new features are welcome. To report a bug or suggest a feature, open an
issue on [GitHub](https://github.com/PinataLabs/PdfPinata/issues). A short program or a sample PDF
that shows the problem makes a report much faster to act on.

## Build and test

You need the .NET SDK version named in `global.json` (10.0.100 or a later feature band). The test
project also runs on .NET 8, so install the .NET 8 runtime too. Every project and the solution are
under `src/`, so name the solution in each command; a bare `dotnet test` at the repository root
finds nothing to run.

```powershell
dotnet build src/PdfPinata.slnx
dotnet test src/PdfPinata.slnx
dotnet test src/PdfPinata.slnx -f net10.0                                   # one target framework
dotnet test src/PdfPinata.slnx --filter "FullyQualifiedName~CLexerTests"    # one test class
```

Some tests turn PDFs into images and compare them with reference images. They need Ghostscript. On
Windows it comes from a NuGet package and needs no setup. On Linux and macOS, install it with the
package manager (`apt-get install ghostscript` or `brew install ghostscript`). Tests that compare
against reference images skip themselves when Ghostscript cannot draw on the machine. CI runs on
Linux, which is the reference platform for rendering.

:::warning Judge a test run by its exit code
When Ghostscript fails inside the test host, the whole process can end. `dotnet test` then prints
"Test host process crashed" and a `Passed!` line with a total lower than the number of tests. That
run did not pass. Check the exit code, and compare the total with `dotnet test --list-tests`. Add
`--blame-crash` to find the test that did not finish, and run again before you believe it.
:::

## Run the demos

`src/SampleApp` is a command-line app with one demo per feature. The same demos fill the
[demo gallery](../demos.mdx) and the code on these pages.

```powershell
dotnet run --project src/SampleApp -- list                           # what each demo shows
dotnet run --project src/SampleApp -- run                            # every demo, into ./output
dotnet run --project src/SampleApp -- run --example Fonts Text       # only the demos named
dotnet run --project src/SampleApp -- run --no-code --output out     # no source, another folder
```

A test runs every demo, so a demo that throws or changes its page count fails the build. To add a
demo, add a class to `src/SampleApp/Demos/` and list it in `DemoRegistry`. The code between
`#region example` and `#endregion` is what the app prints and what the site quotes. A demo must not
register a backend itself, and its fonts and images are embedded resources.

## Work on the documentation site

The site is Docusaurus, in `docs-website/`. You need Node.js 20 or later and pnpm.

```powershell
cd docs-website
pnpm install
pnpm run demos      # build the SampleApp and render each demo's PDF and thumbnail
pnpm start          # serve the site locally, reloading on change
```

`pnpm run demos` needs the .NET SDK and Ghostscript (`gs`, or `gswin64c` on Windows). It writes into
`static/demos`, which is not committed. Without those tools it prints a warning and the gallery
shows placeholders; the rest of the site still works.

### Quote code from the demos

Pages do not keep their own copies of code. A code block that names a demo is filled from that demo's
source when the site builds. With no `snippet`, it gets the demo's whole `#region example`. With a
`snippet`, it gets one excerpt:

````md
```csharp demo=Protect snippet=encrypt
```
````

Leave the body of the block empty; the build replaces whatever is there. The block gets the demo's
file path as its title.

To create the excerpt, add marker comments around the lines inside the demo's `#region example`:

```csharp
        // docs:begin encrypt
        document.SecuritySettings.UserPassword = "user";
        // docs:end encrypt
```

Each marker is on a line of its own, indented like the code around it. Names are kebab-case and
unique within the file. The SampleApp leaves the marker lines out when it prints a demo's source.
Add only marker lines; do not change the demo's code to suit the page. If a page names a demo or
an excerpt that does not exist, the build fails and names the page and the file.

A page lists the demos it explains in its front matter, as `demos: [Protect, Signing]`. The gallery
uses that list to link each demo to its pages.

## Pull requests

- Write commit subjects in plain prose, in the imperative, and describe the change in behaviour, for
  example "Read a hex string by the digits in it rather than by what follows them". Do not put a
  Conventional Commits prefix on a commit subject, except `chore:` or `refactor:` for pure
  housekeeping. Use the body to say what was wrong and why the new way is right.
- Give the pull request title a Conventional Commits prefix: `feat:`, `fix:`, `perf:`, `docs:`,
  `chore:`, `refactor:`, `test:` and so on, with `!` for a breaking change (`feat!:`). The prefix sets
  the pull request's label, and the label decides the release-note section and the version bump.
- Add a test for a fix or a feature. The tests for most of the library are in
  `src/PdfPinata.Test`.
- Keep the final newline at the end of each file; `.editorconfig` asks for it.

Package versions come from git tags, so do not add a `<Version>` to a project file.

## Design notes

`docs/specs/` in the repository holds a design note for each larger feature of this fork: what was
built, what was left out on purpose, and why. Read the note for an area before you extend it. They
are on [GitHub](https://github.com/PinataLabs/PdfPinata/tree/main/docs/specs).
