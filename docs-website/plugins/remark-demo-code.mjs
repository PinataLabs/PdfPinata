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

/** The keys this plugin reads from a code block's meta string. */
const ownKeys = ['demo', 'snippet'];

/**
 * Splits a code block's meta string into this plugin's own `demo=Name` and `snippet=name`, and the
 * rest, which Docusaurus reads. Neither value can contain a space, so splitting on spaces is enough.
 * Plain string work rather than a regular expression, so that a long meta string cannot make the
 * match backtrack.
 */
function splitMeta(meta) {
  const own = {};
  const rest = [];
  for (const word of (meta ?? '').split(' ').filter(Boolean)) {
    const equals = word.indexOf('=');
    const key = equals > 0 ? word.slice(0, equals) : '';
    if (ownKeys.includes(key)) {
      own[key] = word.slice(equals + 1).replaceAll('"', '');
    } else {
      rest.push(word);
    }
  }
  return {own, rest: rest.join(' ')};
}

function visit(node, callback) {
  callback(node);
  for (const child of node.children ?? []) visit(child, callback);
}

export default function remarkDemoCode() {
  return (tree, file) => {
    visit(tree, (node) => {
      if (node.type !== 'code') return;
      const {own, rest} = splitMeta(node.meta);
      if (!own.demo) return;

      try {
        node.value = own.snippet ? demoSnippet(own.demo, own.snippet) : demoExample(own.demo);
      } catch (error) {
        throw new Error(`${file.path}: ${error.message}`);
      }

      const title = rest.includes('title=') ? '' : ` title="${demoSourcePath(own.demo)}"`;
      node.meta = `${rest}${title}`.trim();
    });
  };
}
