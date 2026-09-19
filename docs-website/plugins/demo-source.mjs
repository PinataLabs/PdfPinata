// Reads the SampleApp's demos straight out of their C# source, so that the site never keeps a copy
// of code that could drift from the code that compiles. Shared by the remark plugin that quotes
// excerpts, the plugin that feeds the demo gallery, and the script that renders the demo PDFs.

import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));

/** The repository root, two levels above this file. */
export const repoRoot = path.resolve(here, '..', '..');

/** Where the demos are written. */
export const demosDir = path.join(repoRoot, 'src', 'SampleApp', 'Demos');

/** The registry whose order is the order the demos are meant to be read in. */
const registryFile = path.join(repoRoot, 'src', 'SampleApp', 'Infrastructure', 'DemoRegistry.cs');

/** Marks the part of a demo the SampleApp prints; mirrors DemoSource.BeginMarker and EndMarker. */
const regionBegin = '#region example';
const regionEnd = '#endregion';

/** Begins every excerpt marker; mirrors DemoSource.SnippetMarkerPrefix. */
const snippetPrefix = '// docs:';

/** The path of a demo's source file, relative to the repository root, with forward slashes. */
export function demoSourcePath(name) {
  return `src/SampleApp/Demos/${name}Demo.cs`;
}

/** The whole source of the demo called `name`, or throws naming the file it looked for. */
export function readDemo(name) {
  const file = path.join(demosDir, `${name}Demo.cs`);
  if (!fs.existsSync(file)) {
    throw new Error(`There is no demo called "${name}": ${file} does not exist.`);
  }
  return fs.readFileSync(file, 'utf8').replace(/\r\n/g, '\n');
}

/** Removes the common indent and the blank lines at either end, as DemoSource.ExampleFrom does. */
function dedent(lines) {
  const body = [...lines];
  while (body.length > 0 && body[0].trim() === '') body.shift();
  while (body.length > 0 && body[body.length - 1].trim() === '') body.pop();
  const indents = body.filter((l) => l.trim() !== '').map((l) => l.length - l.trimStart().length);
  const indent = indents.length > 0 ? Math.min(...indents) : 0;
  return body.map((l) => (l.length >= indent ? l.slice(indent) : l.trimStart())).join('\n');
}

const isMarker = (line) => line.trimStart().startsWith(snippetPrefix);

/**
 * The `#region example` of a demo, without the excerpt markers - exactly what `SampleApp run`
 * prints above the demo's output.
 */
export function demoExample(name) {
  const lines = readDemo(name).split('\n');
  const begin = lines.findIndex((l) => l.trim() === regionBegin);
  const end = lines.findIndex((l, i) => i > begin && l.trim().startsWith(regionEnd));
  if (begin < 0 || end < 0) {
    throw new Error(`${demoSourcePath(name)} has no "${regionBegin}" region.`);
  }
  return dedent(lines.slice(begin + 1, end).filter((l) => !isMarker(l)));
}

/**
 * The lines between `// docs:begin <snippet>` and `// docs:end <snippet>` in a demo, dedented and
 * without any excerpt markers nested inside.
 */
export function demoSnippet(name, snippet) {
  const lines = readDemo(name).split('\n');
  const begin = lines.findIndex((l) => l.trim() === `${snippetPrefix}begin ${snippet}`);
  const end = lines.findIndex((l, i) => i > begin && l.trim() === `${snippetPrefix}end ${snippet}`);
  if (begin < 0 || end < 0) {
    throw new Error(
      `${demoSourcePath(name)} has no excerpt "${snippet}": expected a line "${snippetPrefix}begin ${snippet}" ` +
        `and, after it, a line "${snippetPrefix}end ${snippet}".`,
    );
  }
  const text = dedent(lines.slice(begin + 1, end).filter((l) => !isMarker(l)));
  if (text === '') {
    throw new Error(`The excerpt "${snippet}" in ${demoSourcePath(name)} is empty.`);
  }
  return text;
}

/** Reads `public override string <member> => "..."` out of a demo, or null. */
function stringMember(source, member) {
  const match = source.match(new RegExp(`override\\s+string\\??\\s+${member}\\s*=>\\s*"((?:[^"\\\\]|\\\\.)*)"`));
  return match ? match[1].replace(/\\"/g, '"') : null;
}

/** Reads `OpenPassword => SomeConstant;` and resolves the constant, or null for no password. */
function openPassword(source) {
  const literal = stringMember(source, 'OpenPassword');
  if (literal !== null) return literal;
  const named = source.match(/override\s+string\??\s+OpenPassword\s*=>\s*(\w+)\s*;/);
  if (!named) return null;
  const constant = source.match(new RegExp(`const\\s+string\\s+${named[1]}\\s*=\\s*"([^"]*)"`));
  return constant ? constant[1] : null;
}

/**
 * Every demo in registry order, with its name, one-line summary and the password its output is
 * encrypted with (null for all but one).
 */
export function listDemos() {
  const registry = fs.readFileSync(registryFile, 'utf8');
  const classes = [...registry.matchAll(/new\s+(\w+)Demo\(\)/g)].map((m) => m[1]);
  if (classes.length === 0) {
    throw new Error(`Found no demos in ${registryFile}; has its layout changed?`);
  }
  return classes.map((cls) => {
    const source = readDemo(cls);
    const name = stringMember(source, 'Name');
    const summary = stringMember(source, 'Summary');
    if (name === null || summary === null) {
      throw new Error(`Could not read the Name and Summary of ${demoSourcePath(cls)}.`);
    }
    return {name, summary, source: demoSourcePath(cls), password: openPassword(source)};
  });
}
