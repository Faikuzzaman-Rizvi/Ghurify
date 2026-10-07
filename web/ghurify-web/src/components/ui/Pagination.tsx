import { useTranslation } from 'react-i18next';
import { ChevronLeft, ChevronRight } from 'lucide-react';
import { formatCount, toLanguage } from '@/lib/format';
import { pageWindow } from './pageWindow';

const circle =
  'flex h-10 w-10 items-center justify-center rounded-full text-sm font-medium transition';

/** Numbered page circles with previous and next. */
export function Pagination({
  page,
  pages,
  onChange,
  label,
}: {
  page: number;
  pages: number;
  onChange: (page: number) => void;
  label: string;
}) {
  const { t, i18n } = useTranslation();
  const language = toLanguage(i18n.language);

  if (pages <= 1) return null;

  return (
    <nav aria-label={label} className="mt-12 flex items-center justify-center">
      <ul className="flex items-center gap-2">
        <li>
          <button
            type="button"
            disabled={page <= 1}
            onClick={() => onChange(page - 1)}
            aria-label={t('trips.previous')}
            className={`${circle} border border-hill/15 text-deep hover:bg-hill hover:text-white disabled:pointer-events-none disabled:opacity-35`}
          >
            <ChevronLeft aria-hidden="true" className="h-4 w-4" />
          </button>
        </li>
        {pageWindow(page, pages).map((entry, index) =>
          entry === 'gap' ? (
            <li key={`gap-${index}`} aria-hidden="true" className="px-1 text-deep/50">
              …
            </li>
          ) : (
            <li key={entry}>
              <button
                type="button"
                onClick={() => onChange(entry)}
                aria-current={entry === page ? 'page' : undefined}
                aria-label={t('trips.pageNumber', { n: formatCount(entry, language) })}
                className={`${circle} ${
                  entry === page
                    ? 'bg-hill text-white shadow-md'
                    : 'border border-hill/15 text-deep hover:bg-hill/10'
                }`}
              >
                {formatCount(entry, language)}
              </button>
            </li>
          ),
        )}
        <li>
          <button
            type="button"
            disabled={page >= pages}
            onClick={() => onChange(page + 1)}
            aria-label={t('trips.next')}
            className={`${circle} border border-hill/15 text-deep hover:bg-hill hover:text-white disabled:pointer-events-none disabled:opacity-35`}
          >
            <ChevronRight aria-hidden="true" className="h-4 w-4" />
          </button>
        </li>
      </ul>
    </nav>
  );
}
