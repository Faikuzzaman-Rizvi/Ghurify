import { useTranslation } from 'react-i18next';
import { ExternalLink } from 'lucide-react';
import { PageBanner } from '@/components/ui/PageBanner';
import { Photo } from '@/components/ui/Photo';
import { photoCredits } from '@/lib/photos';

/**
 * Who took the photos. They are Creative Commons licensed, which requires naming the author,
 * linking the licence and the source; every photo in /public/photos is listed from the one
 * table in lib/photos, so this page cannot fall out of date.
 */
export function CreditsPage() {
  const { t } = useTranslation();

  return (
    <>
      <PageBanner
        slug="tanguar-haor"
        kind="Wetland"
        eyebrow={t('credits.eyebrow')}
        titleKey="credits.title"
      >
        <p className="mt-4 max-w-2xl text-white/85">{t('credits.body')}</p>
      </PageBanner>

      <div className="container-page py-12 lg:py-16">
        <ul className="grid gap-6 sm:grid-cols-2 lg:grid-cols-3">
          {photoCredits.map((photo) => (
            <li
              key={photo.slug}
              className="overflow-hidden rounded-2xl bg-white shadow-sm ring-1 ring-hill/10"
            >
              <Photo
                slug={photo.slug}
                kind="Hills"
                cut="card"
                decorative
                className="aspect-video"
              />
              <div className="p-5 text-sm">
                <p className="font-display font-semibold text-deep">{t(`photo.${photo.slug}`)}</p>
                <p className="mt-1">
                  {t('credits.by', { author: photo.author })} ·{' '}
                  <a
                    href={photo.licenseUrl}
                    target="_blank"
                    rel="noopener noreferrer license"
                    className="text-hill underline-offset-4 hover:underline"
                  >
                    {photo.license}
                  </a>
                </p>
                <a
                  href={photo.source}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="mt-2 inline-flex items-center gap-1 text-hill underline-offset-4 hover:underline"
                >
                  {t('credits.source')}
                  <ExternalLink aria-hidden="true" className="h-3.5 w-3.5" />
                  <span className="sr-only">{t('credits.newTab')}</span>
                </a>
              </div>
            </li>
          ))}
        </ul>
      </div>
    </>
  );
}
