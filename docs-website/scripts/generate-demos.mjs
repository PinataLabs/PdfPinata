// Renders every SampleApp demo into static/demos: the PDF itself, and a PNG of its first page for the
// gallery. Run with `pnpm run demos`.
//
// It needs the .NET SDK (the one global.json names, plus the .NET 8 runtime the SampleApp targets)
// and Ghostscript (`gs`, or `gswin64c` on Windows). Without them it warns and stops, and the site
// still builds, with placeholders in the gallery. With DOCS_REQUIRE_DEMOS=1 set, as the docs
// workflow sets it, a missing tool is an error instead.

import {spawnSync} from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import {listDemos, repoRoot} from '../plugins/demo-source.mjs';

const siteDir = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const outDir = path.join(siteDir, 'static', 'demos');
const required = process.env.DOCS_REQUIRE_DEMOS === '1';

/** Stops with a warning, or with an error when the demos are required. */
function giveUp(message) {
  if (required) {
    console.error(`error: ${message}`);
    process.exit(1);
  }
  console.warn(`warning: ${message} The gallery will show placeholders.`);
  process.exit(0);
}

/** True when `command` can be started at all. */
function exists(command) {
  const probe = spawnSync(command, ['--version'], {stdio: 'ignore', shell: false});
  return !probe.error;
}

if (!exists('dotnet')) giveUp('dotnet was not found on PATH, so the demos cannot be built.');

const gs = ['gs', 'gswin64c', 'gswin32c'].find(exists);
if (!gs) giveUp('Ghostscript (gs, or gswin64c on Windows) was not found on PATH, so no thumbnails can be drawn.');

fs.rmSync(outDir, {recursive: true, force: true});
fs.mkdirSync(outDir, {recursive: true});

// MinVer wants the git history to version the build, and the docs workflow checks out shallowly.
// The demos do not care what version they were built as.
console.log('Building and running the SampleApp...');
const run = spawnSync(
  'dotnet',
  [
    'run',
    '--project', path.join(repoRoot, 'src', 'SampleApp', 'SampleApp.csproj'),
    '--configuration', 'Release',
    '-p:MinVerSkip=true',
    '--',
    'run', '--no-code', '--output', outDir,
  ],
  {stdio: 'inherit'},
);
if (run.status !== 0) {
  console.error(`error: the SampleApp exited with ${run.status ?? run.error}; see its output above.`);
  process.exit(1);
}

let failed = 0;
for (const demo of listDemos()) {
  const pdf = path.join(outDir, `${demo.name}.pdf`);
  const png = path.join(outDir, `${demo.name}.png`);
  if (!fs.existsSync(pdf)) {
    console.error(`error: the SampleApp wrote no ${demo.name}.pdf.`);
    failed++;
    continue;
  }

  const args = [
    '-q', '-dSAFER', '-dBATCH', '-dNOPAUSE',
    '-sDEVICE=png16m', '-r72', '-dTextAlphaBits=4', '-dGraphicsAlphaBits=4',
    '-dFirstPage=1', '-dLastPage=1',
    ...(demo.password ? [`-sPDFPassword=${demo.password}`] : []),
    `-sOutputFile=${png}`, pdf,
  ];
  const draw = spawnSync(gs, args, {stdio: 'inherit'});
  if (draw.status !== 0 || !fs.existsSync(png)) {
    console.error(`error: Ghostscript could not draw the first page of ${demo.name}.pdf.`);
    failed++;
    continue;
  }
  console.log(`  ${demo.name}`);
}

if (failed > 0) process.exit(1);
console.log(`Rendered ${listDemos().length} demos into ${path.relative(siteDir, outDir)}.`);
