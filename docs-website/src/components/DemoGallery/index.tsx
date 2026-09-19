import Link from '@docusaurus/Link';
import useBrokenLinks from '@docusaurus/useBrokenLinks';
import {usePluginData} from '@docusaurus/useGlobalData';
import useBaseUrl from '@docusaurus/useBaseUrl';
import type {ReactNode} from 'react';
import styles from './styles.module.css';

const repoBlob = 'https://github.com/PinataLabs/PdfPinata/blob/main/';

function DemoCard({demo}: {demo: Demo}): ReactNode {
  const pdf = useBaseUrl(`/demos/${demo.name}.pdf`);
  const thumbnail = useBaseUrl(`/demos/${demo.name}.png`);
  // Guide pages link to a demo as /demos#protect. Declaring the anchor lets the broken-link
  // checker, which only sees headings by itself, tell a real one from a typo.
  const anchor = demo.name.toLowerCase();
  useBrokenLinks().collectAnchor(anchor);

  return (
    <article className={styles.card} id={anchor}>
      {demo.hasThumbnail ? (
        <a href={demo.hasPdf ? pdf : undefined} target="_blank" rel="noopener" className={styles.thumbnail}>
          <img src={thumbnail} alt={`The first page of the ${demo.name} demo`} loading="lazy" />
        </a>
      ) : (
        <div className={`${styles.thumbnail} ${styles.placeholder}`}>
          <span>Run <code>pnpm run demos</code> to render this preview</span>
        </div>
      )}
      <div className={styles.body}>
        <h3 className={styles.title}>{demo.name}</h3>
        <p className={styles.summary}>{demo.summary}</p>
        {demo.password && (
          <p className={styles.password}>
            Open it with the password <code>{demo.password}</code>.
          </p>
        )}
        <ul className={styles.links}>
          {demo.hasPdf && (
            <li>
              <a href={pdf} target="_blank" rel="noopener">PDF</a>
            </li>
          )}
          <li>
            <a href={repoBlob + demo.source} target="_blank" rel="noopener">Source</a>
          </li>
          {demo.guides.map((guide) => (
            <li key={guide.to}>
              <Link to={guide.to}>{guide.title}</Link>
            </li>
          ))}
        </ul>
      </div>
    </article>
  );
}

/** Every SampleApp demo, in the order the SampleApp lists them. */
export default function DemoGallery(): ReactNode {
  const {demos} = usePluginData('pdfpinata-demos') as {demos: Demo[]};
  return (
    <div className={styles.grid}>
      {demos.map((demo) => (
        <DemoCard key={demo.name} demo={demo} />
      ))}
    </div>
  );
}
