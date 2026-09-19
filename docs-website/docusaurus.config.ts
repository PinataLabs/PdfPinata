import type * as Preset from '@docusaurus/preset-classic';
import type {Config} from '@docusaurus/types';
import {themes as prismThemes} from 'prism-react-renderer';
import demosPlugin from './plugins/demos-plugin.mjs';
import remarkDemoCode from './plugins/remark-demo-code.mjs';

const organizationName = 'PinataLabs';
const projectName = 'PdfPinata';
const repoUrl = `https://github.com/${organizationName}/${projectName}`;

const config: Config = {
  title: 'PdfPinata',
  tagline: 'Create, draw, lay out and change PDF files from .NET',
  favicon: 'img/favicon.png',

  // Published to GitHub Pages at https://pinatalabs.github.io/PdfPinata/
  url: `https://${organizationName.toLowerCase()}.github.io`,
  baseUrl: `/${projectName}/`,
  organizationName,
  projectName,
  trailingSlash: false,

  onBrokenLinks: 'throw',
  onBrokenAnchors: 'throw',
  markdown: {
    hooks: {
      onBrokenMarkdownLinks: 'throw',
    },
  },

  i18n: {
    defaultLocale: 'en',
    locales: ['en'],
  },

  presets: [
    [
      'classic',
      {
        docs: {
          // Docs-only mode: the docs are served from the site root.
          routeBasePath: '/',
          sidebarPath: './sidebars.ts',
          editUrl: `${repoUrl}/tree/main/docs-website/`,
          // Code blocks written ```csharp demo=Name snippet=excerpt are filled from the SampleApp's
          // demos at build time. See plugins/remark-demo-code.mjs.
          remarkPlugins: [remarkDemoCode],
        },
        blog: false,
        theme: {
          customCss: './src/css/custom.css',
        },
      } satisfies Preset.Options,
    ],
  ],

  plugins: [demosPlugin],

  themes: [
    [
      '@easyops-cn/docusaurus-search-local',
      {
        // Offline search: the index is built at compile time and served as
        // static assets, so it works on GitHub Pages with no search backend.
        hashed: true,
        // The site runs in docs-only mode (docs.routeBasePath === '/'), so the
        // indexer has to be pointed at the root too — it defaults to '/docs'
        // and would otherwise index nothing.
        docsRouteBasePath: '/',
        indexBlog: false,
        highlightSearchTermsOnTargetPage: true,
      },
    ],
  ],

  themeConfig: {
    image: 'img/logo.jpg',
    colorMode: {
      respectPrefersColorScheme: true,
    },
    navbar: {
      title: 'PdfPinata',
      logo: {
        alt: 'PdfPinata logo',
        src: 'img/icon.png',
      },
      items: [
        {
          type: 'docSidebar',
          sidebarId: 'docsSidebar',
          position: 'left',
          label: 'Docs',
        },
        {to: '/demos', label: 'Demos', position: 'left'},
        {
          href: 'https://www.nuget.org/packages/PdfPinata',
          label: 'NuGet',
          position: 'right',
        },
        {
          href: repoUrl,
          label: 'GitHub',
          position: 'right',
        },
      ],
    },
    footer: {
      style: 'dark',
      links: [
        {
          title: 'Docs',
          items: [
            {label: 'Overview', to: '/'},
            {label: 'Installation', to: '/installation'},
            {label: 'Getting Started', to: '/getting-started'},
            {label: 'Demos', to: '/demos'},
          ],
        },
        {
          title: 'Packages',
          items: [
            {label: 'PdfPinata', href: 'https://www.nuget.org/packages/PdfPinata'},
            {label: 'PdfPinata.Skia', href: 'https://www.nuget.org/packages/PdfPinata.Skia'},
            {label: 'PinataLayout.Rendering', href: 'https://www.nuget.org/packages/PinataLayout.Rendering'},
          ],
        },
        {
          title: 'More',
          items: [
            {label: 'GitHub', href: repoUrl},
            {label: 'Issues', href: `${repoUrl}/issues`},
            {label: 'Changelog', href: `${repoUrl}/blob/main/CHANGELOG.md`},
          ],
        },
      ],
      copyright: `Copyright © ${new Date().getFullYear()} PdfPinata contributors. MIT licensed.`,
    },
    prism: {
      theme: prismThemes.github,
      darkTheme: prismThemes.dracula,
      additionalLanguages: ['csharp', 'bash', 'powershell', 'json'],
    },
  } satisfies Preset.ThemeConfig,
};

export default config;
