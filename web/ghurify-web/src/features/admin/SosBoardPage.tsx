import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';

import { asNumber } from '@/api/client';
import { createHubConnection } from '@/api/realtime';
import { cardClass, primaryButtonClass, secondaryButtonClass } from '@/components/Field';
import { EmptyState, ErrorState } from '@/components/States';
import { mapLink } from '@/features/safety/safetyApi';
import { errorText } from '@/lib/errors';
import { adminApi, type SosBoardItem } from './adminApi';

type Live = 'connecting' | 'live' | 'offline';

/**
 * Live board for the safety desk. Joins /hubs/safety; every SOS, moved position or missed
 * check-in pushed there refreshes the lists at once. The lists also poll, in case the hub drops.
 */
export function SosBoardPage() {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const [includeResolved, setIncludeResolved] = useState(false);
  const [live, setLive] = useState<Live>('connecting');

  const board = useQuery({
    queryKey: ['admin', 'sos', includeResolved],
    queryFn: ({ signal }) => adminApi.sosBoard(includeResolved, signal),
    refetchInterval: 30_000,
  });

  const missed = useQuery({
    queryKey: ['admin', 'missed-check-ins'],
    queryFn: ({ signal }) => adminApi.missedCheckIns(signal),
    refetchInterval: 60_000,
  });

  useEffect(() => {
    const connection = createHubConnection('/hubs/safety');
    const join = () =>
      connection.invoke('JoinDesk').then(
        () => setLive('live'),
        () => setLive('offline'),
      );

    connection.on('sos', () => {
      void queryClient.invalidateQueries({ queryKey: ['admin', 'sos'] });
      void queryClient.invalidateQueries({ queryKey: ['admin', 'dashboard'] });
    });
    connection.on('checkInMissed', () => {
      void queryClient.invalidateQueries({ queryKey: ['admin', 'missed-check-ins'] });
    });
    connection.onreconnecting(() => setLive('connecting'));
    // A reconnect is a new connection: it has to join the desk again.
    connection.onreconnected(() => void join());
    connection.onclose(() => setLive('offline'));

    connection.start().then(join, () => setLive('offline'));

    return () => {
      void connection.stop();
    };
  }, [queryClient]);

  return (
    <div className="flex flex-col gap-6">
      <header className="flex flex-wrap items-center justify-between gap-3">
        <h2 className="font-display text-2xl font-semibold text-deep sm:text-[1.75rem]">
          {t('admin.sos.title')}
        </h2>
        <div className="flex items-center gap-3">
          <span
            role="status"
            className={`rounded-full px-2.5 py-0.5 text-xs font-semibold ${
              live === 'live' ? 'bg-emerald-50 text-emerald-800' : 'bg-mist text-deep'
            }`}
          >
            {t(`admin.sos.live.${live}`)}
          </span>
          <label className="flex items-center gap-2 text-sm text-deep">
            <input
              type="checkbox"
              checked={includeResolved}
              onChange={(event) => setIncludeResolved(event.target.checked)}
            />
            {t('admin.sos.showResolved')}
          </label>
        </div>
      </header>

      <section aria-labelledby="sos-list" className="flex flex-col gap-3">
        <h3 id="sos-list" className="sr-only">
          {t('admin.sos.title')}
        </h3>
        {board.isPending && (
          <div role="status" className="h-32 animate-pulse rounded-2xl bg-hill/10">
            <span className="sr-only">{t('common.loading')}</span>
          </div>
        )}
        {board.isError && (
          <ErrorState message={errorText(board.error, t)} onRetry={() => void board.refetch()} />
        )}
        {board.data?.length === 0 && <EmptyState title={t('admin.sos.empty')} />}
        {board.data && board.data.length > 0 && (
          <ul className="flex flex-col gap-3">
            {board.data.map((sos) => (
              <li key={String(sos.id)}>
                <SosCard sos={sos} />
              </li>
            ))}
          </ul>
        )}
      </section>

      <section aria-labelledby="missed-title" className="flex flex-col gap-3">
        <h3 id="missed-title" className="text-lg font-bold text-deep">
          {t('admin.sos.missedTitle')}
        </h3>
        {missed.isPending && (
          <div role="status" className="h-20 animate-pulse rounded-2xl bg-hill/10">
            <span className="sr-only">{t('common.loading')}</span>
          </div>
        )}
        {missed.isError && (
          <ErrorState message={errorText(missed.error, t)} onRetry={() => void missed.refetch()} />
        )}
        {missed.data?.length === 0 && (
          <p className="text-sm text-deep/70">{t('admin.sos.noMissed')}</p>
        )}
        {missed.data && missed.data.length > 0 && (
          <ul className="flex flex-col gap-2">
            {missed.data.map((item) => (
              <li key={String(item.checkInId)} className={`${cardClass} !p-4`}>
                <p className="font-semibold text-deep">{item.label}</p>
                <Link
                  to={`/trips/${asNumber(item.tripId)}`}
                  className="text-sm text-hill underline"
                >
                  {item.tripTitle}
                </Link>
              </li>
            ))}
          </ul>
        )}
      </section>
    </div>
  );
}

function SosCard({ sos }: { sos: SosBoardItem }) {
  const { t, i18n } = useTranslation();
  const queryClient = useQueryClient();
  const id = asNumber(sos.id);

  const change = useMutation({
    mutationFn: (to: 'acknowledge' | 'resolve') =>
      to === 'acknowledge' ? adminApi.acknowledgeSos(id) : adminApi.resolveSos(id),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['admin', 'sos'] });
      void queryClient.invalidateQueries({ queryKey: ['admin', 'dashboard'] });
    },
  });

  const time = (iso: string) =>
    new Intl.DateTimeFormat(i18n.language, { timeStyle: 'short', timeZone: 'Asia/Dhaka' }).format(
      new Date(iso),
    );

  return (
    <article
      className={`${cardClass} ${sos.status === 'Open' ? 'ring-2 ring-jamdani' : ''} flex flex-col gap-2`}
    >
      <div className="flex flex-wrap items-baseline justify-between gap-2">
        <p className="text-lg font-bold text-deep">
          {sos.userName ?? t('admin.noName')}{' '}
          <span className="text-sm font-normal text-deep/70">· {sos.tripTitle}</span>
        </p>
        <span className="rounded-full bg-jamdani/10 px-2 py-0.5 text-xs font-semibold text-jamdani">
          {t(`admin.sos.status.${sos.status}`)}
        </span>
      </div>
      {sos.message && <p className="text-deep">“{sos.message}”</p>}
      <p className="text-sm text-deep/70">
        {t('admin.sos.raisedAt', { time: time(sos.created) })} ·{' '}
        {t('admin.sos.lastSeen', { time: time(sos.lastSeenOn) })}
      </p>
      <dl className="grid gap-1 text-sm text-deep sm:grid-cols-2">
        {sos.userPhone && (
          <div>
            <dt className="inline text-deep/60">{t('admin.sos.traveller')}: </dt>
            <dd className="inline">
              <a href={`tel:${sos.userPhone}`} className="text-hill underline">
                {sos.userPhone}
              </a>
            </dd>
          </div>
        )}
        {sos.hostPhone && (
          <div>
            <dt className="inline text-deep/60">
              {t('admin.sos.host', { name: sos.hostName ?? '' })}:{' '}
            </dt>
            <dd className="inline">
              <a href={`tel:${sos.hostPhone}`} className="text-hill underline">
                {sos.hostPhone}
              </a>
            </dd>
          </div>
        )}
      </dl>
      <div className="flex flex-wrap gap-2">
        <a
          href={mapLink(sos.latitude, sos.longitude)}
          target="_blank"
          rel="noreferrer"
          className={secondaryButtonClass}
        >
          {t('admin.sos.map')}
        </a>
        {sos.status === 'Open' && (
          <button
            type="button"
            className={primaryButtonClass}
            disabled={change.isPending}
            onClick={() => change.mutate('acknowledge')}
          >
            {t('admin.sos.acknowledge')}
          </button>
        )}
        {sos.status !== 'Resolved' && (
          <button
            type="button"
            className={secondaryButtonClass}
            disabled={change.isPending}
            onClick={() => change.mutate('resolve')}
          >
            {t('admin.sos.resolve')}
          </button>
        )}
      </div>
      {change.isError && (
        <p role="alert" className="text-sm text-jamdani">
          {errorText(change.error, t)}
        </p>
      )}
    </article>
  );
}
