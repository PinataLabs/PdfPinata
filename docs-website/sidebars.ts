import type {SidebarsConfig} from '@docusaurus/plugin-content-docs';

// The order here is the order a newcomer should read in: set up, draw a page, lay out a document,
// then everything done to a PDF that already exists, then the standards a PDF can claim.
const sidebars: SidebarsConfig = {
  docsSidebar: [
    {
      type: 'category',
      label: 'Start here',
      collapsed: false,
      items: ['overview', 'installation', 'getting-started'],
    },
    {
      type: 'category',
      label: 'Fonts and text',
      collapsed: false,
      items: [
        'fonts-and-text/fonts',
        'fonts-and-text/international-text',
        'fonts-and-text/unicode-and-embedding',
      ],
    },
    {
      type: 'category',
      label: 'Drawing with XGraphics',
      collapsed: false,
      items: [
        'drawing/pages-and-orientation',
        'drawing/text',
        'drawing/text-layout',
        'drawing/shapes-pens-and-brushes',
        'drawing/images',
        'drawing/barcodes',
        'drawing/forms-stamps-and-imposition',
      ],
    },
    {
      type: 'category',
      label: 'Layout with PinataLayout',
      collapsed: false,
      items: [
        'layout/documents-sections-and-styles',
        'layout/paragraphs-and-layout',
        'layout/tables',
        'layout/structure-and-cross-references',
        'layout/footnotes',
        'layout/charts',
        'layout/advanced-layout',
        'layout/ddl',
      ],
    },
    {
      type: 'category',
      label: 'Working with existing PDFs',
      collapsed: false,
      items: [
        'existing-pdfs/opening-documents',
        'existing-pdfs/merge-split-and-assemble',
        'existing-pdfs/page-resizing-and-bleed',
        'existing-pdfs/text-extraction',
        'existing-pdfs/reading-content-streams',
        'existing-pdfs/pdf-objects',
        'existing-pdfs/incremental-saving',
        'existing-pdfs/compression',
      ],
    },
    {
      type: 'category',
      label: 'Interactive features',
      collapsed: false,
      items: [
        'interactive/forms',
        'interactive/annotations',
        'interactive/bookmarks-and-outlines',
        'interactive/navigation-and-viewer-preferences',
      ],
    },
    {
      type: 'category',
      label: 'Standards and security',
      collapsed: false,
      items: [
        'standards/pdf-a',
        'standards/accessibility',
        'standards/digital-signatures',
        'standards/encryption',
        'standards/e-invoicing',
      ],
    },
    'demos',
    {
      type: 'category',
      label: 'Reference',
      collapsed: false,
      items: [
        'reference/migrating',
        'reference/platforms-and-deployment',
        'reference/troubleshooting',
        'reference/faq',
        'reference/licensing',
        'reference/contributing',
        {
          type: 'link',
          label: 'Changelog',
          href: 'https://github.com/PinataLabs/PdfPinata/blob/main/CHANGELOG.md',
        },
      ],
    },
  ],
};

export default sidebars;
