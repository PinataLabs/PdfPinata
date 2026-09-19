// Fills code blocks from the SampleApp's demos, so the code the site shows is code that compiles and
// that the demo smoke test runs.
//
//   ```csharp demo=Protect snippet=encrypt
//   ```
//
// takes the lines between "// docs:begin encrypt" and "// docs:end encrypt" in ProtectDemo.cs.
// Leaving out `snippet` takes the demo's whole `#region example`, as `SampleApp run` prints it.
// Whatever the block contains is replaced. A demo or an excerpt that cannot be found fails the
// build, naming the page and the file.

import {demoExample, demoSnippet, demoSourcePath} from './demo-source.mjs';

/** Parses `key=value` and `key="value with spaces"` pairs out of a code block's meta string. */
function parseMeta(meta) {
  const pairs = {};
  for (const match of (meta ?? '').matchAll(/(\w+)=(?:"([^"]*)"|(\S+))/g)) {
    pairs[match[1]] = match[2] ?? match[3];
  }
  return pairs;
}

/** Removes the keys this plugin consumes from the meta, leaving the ones Docusaurus reads. */
function stripMeta(meta) {
  return (meta ?? '').replace(/\b(demo|snippet)=(?:"[^"]*"|\S+)/g, '').trim();
}

function visit(node, callback) {
  callback(node);
  for (const child of node.children ?? []) visit(child, callback);
}

export default function remarkDemoCode() {
  return (tree, file) => {
    visit(tree, (node) => {
      if (node.type !== 'code') return;
      const meta = parseMeta(node.meta);
      if (!meta.demo) return;

      try {
        node.value = meta.snippet ? demoSnippet(meta.demo, meta.snippet) : demoExample(meta.demo);
      } catch (error) {
        throw new Error(`${file.path}: ${error.message}`);
      }

      const rest = stripMeta(node.meta);
      const title = /\btitle=/.test(rest) ? '' : ` title="${demoSourcePath(meta.demo)}"`;
      node.meta = `${rest}${title}`.trim();
    });
  };
}
