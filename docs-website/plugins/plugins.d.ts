// Types for the plain JavaScript plugins, so that `pnpm run typecheck` can check the config that
// imports them.

declare module '*/demos-plugin.mjs' {
  import type {Plugin, LoadContext} from '@docusaurus/types';
  const demosPlugin: (context: LoadContext) => Plugin<Demo[]>;
  export default demosPlugin;
}

declare module '*/remark-demo-code.mjs' {
  const remarkDemoCode: () => (tree: unknown, file: {path: string}) => void;
  export default remarkDemoCode;
}

/** One SampleApp demo, as the demos plugin hands it to the gallery. */
interface Demo {
  name: string;
  summary: string;
  source: string;
  password: string | null;
  hasPdf: boolean;
  hasThumbnail: boolean;
  guides: {title: string; to: string}[];
}
