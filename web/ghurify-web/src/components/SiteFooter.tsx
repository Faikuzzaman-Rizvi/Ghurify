import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';
import { useHealth } from '@/hooks/useHealth';
import { Logo } from './Logo';

/** Brand, links, and a small live status light that proves the whole chain is up. */
export function SiteFooter() {
  const { t } = useTranslation();

  return (
    <footer className="mt-20 bg-deep text-white/80">
      <div className="mx-auto grid max-w-6xl gap-8 px-4 py-12 sm:grid-cols-[2fr_1fr_1fr]">
        <div>
          <Logo inverted />
          <p className="mt-3 max-w-sm text-sm">{t('footer.tagline')}</p>
        </div>

        <div>
          <h2 className="font-display text-lg font-bold text-white">{t('footer.explore')}</h2>
          <ul className="mt-2 space-y-1 text-sm">
            <li>
              <Link to="/trips" className="hover:text-turmeric">
                {t('nav.explore')}
              </Link>
            </li>
            <li>
              <Link to="/#destinations" className="hover:text-turmeric">
                {t('nav.destinations')}
              </Link>
            </li>
            <li>
              <Link to="/#safety" className="hover:text-turmeric">
                {t('nav.safety')}
              </Link>
            </li>
          </ul>
        </div>

        <SystemStatus />
      </div>

      <p className="border-t border-white/10 px-4 py-4 text-center text-xs text-white/60">
        {t('footer.rights', { year: new Date().getFullYear() })}
      </p>
    </footer>
  );
}

/**
 * The health check from Sprint 1, kept as a footer light: React -> Vite proxy -> API ->
 * SQL Server, answered live.
 */
export function SystemStatus() {
  const { t } = useTranslation();
  const { data, isPending, isError, refetch, isFetching } = useHealth();

  return (
    <section aria-live="polite">
      <h2 className="font-display text-lg font-bold text-white">{t('health.title')}</h2>

      {isPending ? (
        <p className="mt-2 text-sm">{t('health.checking')}</p>
      ) : isError ? (
        <div className="mt-2 flex flex-col items-start gap-2">
          <p className="flex items-center gap-2 text-sm font-medium text-white">
            <span className="h-2.5 w-2.5 rounded-full bg-jamdani" aria-hidden="true" />
            {t('health.unhealthy')}
          </p>
          <button
            type="button"
            onClick={() => void refetch()}
            disabled={isFetching}
            className="rounded-full bg-white/10 px-3 py-1 text-sm text-white transition hover:bg-white/20 disabled:opacity-60"
          >
            {isFetching ? t('common.loading') : t('health.retry')}
          </button>
        </div>
      ) : (
        <div className="mt-2 text-sm">
          <p className="flex items-center gap-2 font-medium text-white">
            <span className="relative flex h-2.5 w-2.5" aria-hidden="true">
              <span className="absolute inline-flex h-full w-full animate-ping rounded-full bg-emerald-400 opacity-60" />
              <span className="relative inline-flex h-2.5 w-2.5 rounded-full bg-emerald-400" />
            </span>
            {t('health.healthy')}
          </p>
          <p className="mt-1 text-white/70">
            {t('health.databaseLabel')}: {data.databaseStatus}
          </p>
        </div>
      )}
    </section>
  );
}
