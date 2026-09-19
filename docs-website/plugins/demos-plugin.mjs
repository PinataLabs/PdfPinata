// Feeds the demo gallery: every demo in the SampleApp's registry, whether its PDF and thumbnail have
// been rendered into static/demos, and which guide pages name it in their `demos:` front matter.
//
// The PDFs are rendered by `pnpm run demos`, which needs .NET and Ghostscript. Without them the
// gallery shows placeholders and this plugin warns. With DOCS_REQUIRE_DEMOS=1 set, as the docs
// workflow sets it, a missing PDF or thumbnail fails the build instead, so a deploy can never
// publish an empty gallery.

import fs from 'node:fs';
import path from 'node:path';
import {listDemos} from './demo-source.mjs';

/** Every Markdown file under `dir`, recursively. */
function markdownFiles(dir) {
  return fs.readdirSync(dir, {withFileTypes: true}).flatMap((entry) => {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) return markdownFiles(full);
    return /\.mdx?$/.test(entry.name) ? [full] : [];
  });
}

/**
 * Reads one-line `key: value` fields out of a page's front matter. Plain string work rather than a
 * regular expression, so that a long line cannot make the match backtrack.
 */
function frontMatterField(frontMatter) {
  const lines = frontMatter.split('\n');
  return (name) => lines.find((line) => line.startsWith(`${name}:`))?.slice(name.length + 1).trim();
}

/** Removes one pair of matching quotes from around a YAML scalar. */
function unquote(value) {
  const quoted = value.length >= 2 && (value[0] === '"' || value[0] === "'") && value.at(-1) === value[0];
  return quoted ? value.slice(1, -1) : value;
}

/** Maps each demo name to the guide pages whose front matter lists it under `demos:`. */
function guidesByDemo(docsDir) {
  const guides = {};
  for (const file of markdownFiles(docsDir)) {
    const text = fs.readFileSync(file, 'utf8').replace(/\r\n/g, '\n');
    const front = text.match(/^---\n([\s\S]*?)\n---/);
    if (!front) continue;
    const field = frontMatterField(front[1]);
    const demos = field('demos');
    if (!demos?.startsWith('[') || !demos.endsWith(']')) continue;
    const title = unquote(field('title') ?? '');
    const id = path.relative(docsDir, file).replace(/\\/g, '/').replace(/\.mdx?$/, '');
    for (const name of demos.slice(1, -1).split(',').map((s) => s.trim()).filter(Boolean)) {
      (guides[name] ??= []).push({title: title || id, to: `/${id}`});
    }
  }
  return guides;
}

export default function demosPlugin(context) {
  const staticDemos = path.join(context.siteDir, 'static', 'demos');
  const docsDir = path.join(context.siteDir, 'docs');

  return {
    name: 'pdfpinata-demos',

    async loadContent() {
      const guides = guidesByDemo(docsDir);
      const demos = listDemos().map((demo) => ({
        ...demo,
        hasPdf: fs.existsSync(path.join(staticDemos, `${demo.name}.pdf`)),
        hasThumbnail: fs.existsSync(path.join(staticDemos, `${demo.name}.png`)),
        guides: guides[demo.name] ?? [],
      }));

      const known = new Set(demos.map((d) => d.name));
      const unknown = Object.keys(guides).filter((name) => !known.has(name));
      if (unknown.length > 0) {
        throw new Error(`Guide pages name demos that do not exist: ${unknown.join(', ')}.`);
      }

      const missing = demos.filter((d) => !d.hasPdf || !d.hasThumbnail).map((d) => d.name);
      if (missing.length > 0) {
        const message =
          `${missing.length} of ${demos.length} demos have no rendered PDF or thumbnail in static/demos ` +
          `(${missing.join(', ')}). Run "pnpm run demos" to render them.`;
        if (process.env.DOCS_REQUIRE_DEMOS === '1') throw new Error(message);
        console.warn(`[pdfpinata-demos] ${message} The gallery will show placeholders.`);
      }

      return demos;
    },

    async contentLoaded({content, actions}) {
      actions.setGlobalData({demos: content});
    },
  };
}
