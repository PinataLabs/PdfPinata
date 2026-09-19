# PdfPinata documentation site

The PdfPinata documentation site, built with [Docusaurus](https://docusaurus.io/).
Published to GitHub Pages at <https://pinatalabs.github.io/PdfPinata/>.

## Local development

This project uses [pnpm](https://pnpm.io/). The version is pinned by the
`packageManager` field in [`package.json`](package.json), so `corepack enable` is
enough to get the right one.

```sh
pnpm install
pnpm start
```

`pnpm start` serves the site at <http://localhost:3000/PdfPinata/> with hot reload.

## Build

```sh
pnpm run demos      # build the SampleApp, render every demo PDF and thumbnail into static/demos
pnpm run build      # static output in ./build
pnpm run serve      # serve the built output locally
pnpm run typecheck  # type check the config, sidebars and components
```

`pnpm run demos` needs the .NET SDK that `global.json` names, the .NET 8 runtime, and Ghostscript
(`gs`, or `gswin64c` on Windows) on `PATH`. Without them it warns and stops, and the site still
builds with placeholders in the demo gallery. The output in `static/demos` is git-ignored: it is
rebuilt from the code on every deploy. Set `DOCS_REQUIRE_DEMOS=1`, as the workflow does, to make a
missing tool or a missing PDF an error.

## Content

Documentation pages live in [`docs/`](docs) as Markdown. Ordering is controlled by
[`sidebars.ts`](sidebars.ts).

The site runs in **docs-only mode** — `docs/overview.md` has `slug: /` and is served
as the site root, so there is no separate landing page under `src/pages`.

### Code comes from the demos

Code blocks are filled from the SampleApp's demos at build time, so the code the site shows is code
that compiles and that `DemoSmokeTests` runs:

````md
```csharp demo=Protect snippet=encrypt
```
````

takes the lines between `// docs:begin encrypt` and `// docs:end encrypt` in
`src/SampleApp/Demos/ProtectDemo.cs`. Leave out `snippet` to take the demo's whole
`#region example`. A demo or an excerpt that cannot be found fails the build. The SampleApp leaves
the marker lines out of the source it prints. See
[`plugins/remark-demo-code.mjs`](plugins/remark-demo-code.mjs).

A page lists the demos it explains in its front matter, as `demos: [Protect]`. The demo gallery
([`docs/demos.mdx`](docs/demos.mdx)) reads the demos from the SampleApp's `DemoRegistry` and links
each one to the pages that name it. See [`plugins/demos-plugin.mjs`](plugins/demos-plugin.mjs).

## Publishing

[`.github/workflows/docs.yml`](../.github/workflows/docs.yml) builds the site on pull requests
that touch `docs-website/` or the SampleApp, and builds and deploys it on every push to `main`
that touches the site or the code. Pull requests build the site (and type check) without deploying.

One-time repository setup: **Settings → Pages → Build and deployment → Source: GitHub Actions**.
