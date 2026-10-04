import { useTranslation } from 'react-i18next';
import { useHealth } from '@/hooks/useHealth';
import { LanguageToggle } from '@/components/LanguageToggle';

/**
 * Landing screen for the skeleton. It exists to prove the whole chain works:
 * React -> Vite proxy -> API -> SQL Server.
 */
export function HomePage() {
  const { t } = useTranslation();
  const { data, isPending, isError, refetch, isFetching } = useHealth();

  return (
    <main className="mx-auto flex min-h-screen max-w-2xl flex-col gap-8 px-4 py-10">
      <header className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
        <div>
          <h1 className="text-3xl font-bold text-hill">{t('app.name')}</h1>
          <p className="mt-1 text-deep/70">{t('app.tagline')}</p>
        </div>
        <LanguageToggle />
      </header>

      <section
        aria-live="polite"
        className="rounded-xl border border-hill/15 bg-white p-6 shadow-sm"
      >
        <h2 className="text-lg font-semibold text-deep">{t('health.title')}</h2>

        {isPending ? (
          <p className="mt-3 text-deep/70">{t('health.checking')}</p>
        ) : isError ? (
          <div className="mt-3 flex flex-col items-start gap-3">
            <p className="font-medium text-jamdani">{t('health.unhealthy')}</p>
            <button
              type="button"
              onClick={() => void refetch()}
              disabled={isFetching}
              className="rounded-md bg-hill px-4 py-2 text-white transition hover:bg-deep disabled:opacity-60"
            >
              {isFetching ? t('common.loading') : t('health.retry')}
            </button>
          </div>
        ) : (
          <div className="mt-3 flex flex-col gap-1">
            <p className="text-xl font-semibold text-hill">{t('health.healthy')}</p>
            <p className="text-sm text-deep/70">
              {t('health.databaseLabel')}: {data.databaseStatus}
            </p>
          </div>
        )}
      </section>
    </main>
  );
}
