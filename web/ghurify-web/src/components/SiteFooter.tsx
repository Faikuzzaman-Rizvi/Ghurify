import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';
import { BadgeCheck, LockKeyhole, Siren, type LucideIcon } from 'lucide-react';
import { useHealth } from '@/hooks/useHealth';
import { Logo } from './Logo';

const promises: { key: string; icon: LucideIcon }[] = [
  { key: 'verified', icon: BadgeCheck },
  { key: 'escrow', icon: LockKeyhole },
  { key: 'sos', icon: Siren },
];

/** The promise strip, brand and links, and a small live status light. */
export function SiteFooter() {
  const { t } = useTranslation();

  const exploreLinks = [
    { to: '/trips', label: t('nav.explore') },
    { to: '/feed', label: t('nav.stories') },
    { to: '/#destinations', label: t('nav.destinations') },
    { to: '/#safety', label: t('nav.safety') },
  ];
  const travellerLinks = [
    { to: '/me/trips', label: t('nav.myTrips') },
    { to: '/host/trips', label: t('footer.hostATrip') },
    { to: '/account', label: t('account.title') },
  ];

  return (
    <footer className="mt-auto bg-night text-white/75">
      <div className="container-page pt-16">
        <ul className="grid gap-px overflow-hidden rounded-2xl border border-white/10 bg-white/10 sm:grid-cols-3">
          {promises.map(({ key, icon: Icon }) => (
            <li key={key} className="flex items-center gap-4 bg-night p-5">
              <span className="flex h-12 w-12 shrink-0 items-center justify-center rounded-xl bg-turmeric text-night">
                <Icon aria-hidden="true" className="h-6 w-6" />
              </span>
              <span>
                <span className="block font-display font-semibold text-white">
                  {t(`home.safety.${key}.title`)}
                </span>
                <span className="text-sm">{t(`footer.promise.${key}`)}</span>
              </span>
            </li>
          ))}
        </ul>

        <div className="grid gap-10 py-14 sm:grid-cols-2 lg:grid-cols-[1.6fr_1fr_1fr_1.2fr]">
          <div>
            <Logo inverted />
            <p className="mt-4 max-w-sm leading-relaxed">{t('footer.tagline')}</p>
          </div>

          <FooterLinks title={t('footer.explore')} links={exploreLinks} />
          <FooterLinks title={t('footer.travellers')} links={travellerLinks} />
          <SystemStatus />
        </div>
      </div>

      <div className="border-t border-white/10">
        <div className="container-page flex flex-col items-center justify-between gap-2 py-5 text-sm text-white/60 sm:flex-row">
          <p>{t('footer.rights', { year: new Date().getFullYear() })}</p>
          <Link to="/credits" className="transition hover:text-dusk">
            {t('credits.link')}
          </Link>
        </div>
      </div>
    </footer>
  );
}

function FooterLinks({ title, links }: { title: string; links: { to: string; label: string }[] }) {
  return (
    <div>
      <h2 className="text-lg font-semibold text-white!">{title}</h2>
      <ul className="mt-4 space-y-2.5">
        {links.map((link) => (
          <li key={link.to}>
            <Link to={link.to} className="transition hover:text-dusk">
              {link.label}
            </Link>
          </li>
        ))}
      </ul>
    </div>
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
      <h2 className="text-lg font-semibold text-white!">{t('health.title')}</h2>

      {isPending ? (
        <p className="mt-4 text-sm">{t('health.checking')}</p>
      ) : isError ? (
        <div className="mt-4 flex flex-col items-start gap-2">
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
        <div className="mt-4 text-sm">
          <p className="flex items-center gap-2 font-medium text-white">
            <span className="relative flex h-2.5 w-2.5" aria-hidden="true">
              <span className="absolute inline-flex h-full w-full animate-ping rounded-full bg-emerald-400 opacity-60" />
              <span className="relative inline-flex h-2.5 w-2.5 rounded-full bg-emerald-400" />
            </span>
            {t('health.healthy')}
          </p>
          <p className="mt-1 text-white/60">
            {t('health.databaseLabel')}: {data.databaseStatus}
          </p>
        </div>
      )}
    </section>
  );
}
