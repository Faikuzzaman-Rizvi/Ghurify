import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';
import {
  CircleCheck,
  Clock,
  ExternalLink,
  Hand,
  MapPinned,
  Phone,
  Siren,
  TimerOff,
  type LucideIcon,
} from 'lucide-react';

import { asNumber } from '@/api/client';
import { createHubConnection, runHubConnection } from '@/api/realtime';
import { EmptyState, ErrorState } from '@/components/States';
import { mapLink } from '@/features/safety/safetyApi';
import { errorText } from '@/lib/errors';
import { adminApi, type SosBoardItem } from './adminApi';
import { AdminPageHeader, ListSkeleton, ToggleChip } from './AdminUi';
import {
  adminPanelClass,
  adminRowClass,
  smallPrimaryButtonClass,
  smallSecondaryButtonClass,
} from './adminStyles';

type Live = 'connecting' | 'live' | 'offline';

/** How each state of an alert is drawn: the band across the top of its card. */
const band: Record<string, { className: string; icon: LucideIcon }> = {
  Open: { className: 'bg-linear-to-r from-jamdani to-[#c0436f] text-white', icon: Siren },
  Acknowledged: { className: 'bg-linear-to-r from-turmeric to-dusk text-night', icon: Hand },
  Resolved: { className: 'bg-emerald-50 text-emerald-800', icon: CircleCheck },
};

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

    return runHubConnection(connection, {
      onStarted: () => void join(),
      onFailed: () => setLive('offline'),
    });
  }, [queryClient]);

  return (
    <div className="flex flex-col gap-8">
      <AdminPageHeader
        eyebrow={t('admin.groups.safety')}
        title={t('admin.sos.title')}
        description={t('admin.sos.lead')}
        actions={
          <>
            <span
              role="status"
              className={`inline-flex items-center gap-2 rounded-full px-3.5 py-2 text-sm font-semibold ring-1 ${
                live === 'live'
                  ? 'bg-emerald-50 text-emerald-800 ring-emerald-600/15'
                  : live === 'offline'
                    ? 'bg-jamdani/5 text-jamdani ring-jamdani/20'
                    : 'bg-white text-deep/70 ring-hill/15'
              }`}
            >
              <span aria-hidden="true" className="relative flex h-2.5 w-2.5">
                {live === 'live' && (
                  <span className="absolute inset-0 animate-ping rounded-full bg-emerald-500/60 motion-reduce:animate-none" />
                )}
                <span
                  className={`relative h-2.5 w-2.5 rounded-full ${
                    live === 'live'
                      ? 'bg-emerald-500'
                      : live === 'offline'
                        ? 'bg-jamdani'
                        : 'bg-deep/30'
                  }`}
                />
              </span>
              {t(`admin.sos.live.${live}`)}
            </span>
            <ToggleChip
              pressed={includeResolved}
              onClick={() => setIncludeResolved((current) => !current)}
            >
              {t('admin.sos.showResolved')}
            </ToggleChip>
          </>
        }
      />

      <section aria-labelledby="sos-list" className="flex flex-col gap-4">
        <h3 id="sos-list" className="sr-only">
          {t('admin.sos.title')}
        </h3>
        {board.isPending && <ListSkeleton rows={2} />}
        {board.isError && (
          <ErrorState message={errorText(board.error, t)} onRetry={() => void board.refetch()} />
        )}
        {board.data?.length === 0 && <EmptyState title={t('admin.sos.empty')} />}
        {board.data && board.data.length > 0 && (
          <ul className="grid gap-5 lg:grid-cols-2">
            {board.data.map((sos) => (
              <li key={String(sos.id)} className="flex">
                <SosCard sos={sos} />
              </li>
            ))}
          </ul>
        )}
      </section>

      <section aria-labelledby="missed-title" className="flex flex-col gap-3">
        <h3
          id="missed-title"
          className="flex items-center gap-2 font-display text-lg font-semibold text-deep"
        >
          <TimerOff aria-hidden="true" className="h-5 w-5 text-turmeric" />
          {t('admin.sos.missedTitle')}
        </h3>
        {missed.isPending && <ListSkeleton rows={1} />}
        {missed.isError && (
          <ErrorState message={errorText(missed.error, t)} onRetry={() => void missed.refetch()} />
        )}
        {missed.data?.length === 0 && (
          <p className="flex items-center gap-2.5 rounded-2xl bg-white/80 px-5 py-4 text-sm text-deep/70 ring-1 ring-hill/10">
            <CircleCheck aria-hidden="true" className="h-5 w-5 text-emerald-600" />
            {t('admin.sos.noMissed')}
          </p>
        )}
        {missed.data && missed.data.length > 0 && (
          <ul className={adminPanelClass}>
            {missed.data.map((item) => (
              <li
                key={String(item.checkInId)}
                className={`${adminRowClass} flex items-center gap-4 px-5 py-4`}
              >
                <span
                  aria-hidden="true"
                  className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-turmeric/15 text-ochre"
                >
                  <Clock className="h-5 w-5" />
                </span>
                <div className="min-w-0">
                  <p className="font-semibold text-deep">{item.label}</p>
                  <Link
                    to={`/trips/${asNumber(item.tripId)}`}
                    className="text-sm font-medium text-hill underline-offset-4 hover:underline"
                  >
                    {item.tripTitle}
                  </Link>
                </div>
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
  const { className: bandClass, icon: BandIcon } = band[sos.status] ?? band.Open!;

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
      className={`flex w-full flex-col overflow-hidden rounded-3xl bg-white shadow-[0_8px_30px_rgba(15,42,31,0.08)] ring-1 ${
        sos.status === 'Open'
          ? 'shadow-[0_12px_40px_rgba(163,48,92,0.18)] ring-jamdani/40'
          : 'ring-hill/10'
      }`}
    >
      <div className={`flex items-center justify-between gap-3 px-5 py-3 ${bandClass}`}>
        <span className="flex items-center gap-2 text-sm font-bold">
          <BandIcon
            aria-hidden="true"
            className={`h-5 w-5 ${sos.status === 'Open' ? 'animate-pulse motion-reduce:animate-none' : ''}`}
          />
          {t(`admin.sos.status.${sos.status}`)}
        </span>
        <span className="flex items-center gap-1.5 text-xs font-semibold opacity-90">
          <Clock aria-hidden="true" className="h-3.5 w-3.5" />
          {t('admin.sos.raisedAt', { time: time(sos.created) })}
        </span>
      </div>

      <div className="flex flex-1 flex-col gap-4 p-5">
        <div>
          <h4 className="font-display text-xl font-semibold text-deep">
            {sos.userName ?? t('admin.noName')}
          </h4>
          <p className="text-sm text-deep/65">{sos.tripTitle}</p>
        </div>

        {sos.message && (
          <blockquote className="rounded-2xl border-l-4 border-jamdani/60 bg-jamdani/5 px-4 py-3 text-deep">
            “{sos.message}”
          </blockquote>
        )}

        <p className="flex items-center gap-2 text-sm text-deep/65">
          <MapPinned aria-hidden="true" className="h-4 w-4 text-hill" />
          {t('admin.sos.lastSeen', { time: time(sos.lastSeenOn) })}
        </p>

        {(sos.userPhone || sos.hostPhone) && (
          <dl className="grid gap-2 sm:grid-cols-2">
            {sos.userPhone && <CallTile label={t('admin.sos.traveller')} phone={sos.userPhone} />}
            {sos.hostPhone && (
              <CallTile
                label={t('admin.sos.host', { name: sos.hostName ?? '' })}
                phone={sos.hostPhone}
              />
            )}
          </dl>
        )}

        <div className="mt-auto flex flex-wrap gap-2 border-t border-hill/8 pt-4">
          {sos.status === 'Open' && (
            <button
              type="button"
              className={smallPrimaryButtonClass}
              disabled={change.isPending}
              onClick={() => change.mutate('acknowledge')}
            >
              <Hand aria-hidden="true" className="h-4 w-4" />
              {t('admin.sos.acknowledge')}
            </button>
          )}
          <a
            href={mapLink(sos.latitude, sos.longitude)}
            target="_blank"
            rel="noreferrer"
            className={smallSecondaryButtonClass}
          >
            <ExternalLink aria-hidden="true" className="h-4 w-4" />
            {t('admin.sos.map')}
          </a>
          {sos.status !== 'Resolved' && (
            <button
              type="button"
              className={smallSecondaryButtonClass}
              disabled={change.isPending}
              onClick={() => change.mutate('resolve')}
            >
              <CircleCheck aria-hidden="true" className="h-4 w-4" />
              {t('admin.sos.resolve')}
            </button>
          )}
        </div>
        {change.isError && (
          <p role="alert" className="text-sm text-jamdani">
            {errorText(change.error, t)}
          </p>
        )}
      </div>
    </article>
  );
}

/** Somebody to ring about an alert: who they are, and their number as a call link. */
function CallTile({ label, phone }: { label: string; phone: string }) {
  return (
    <div className="flex items-center gap-3 rounded-2xl bg-mist px-3.5 py-3 ring-1 ring-hill/8">
      <span
        aria-hidden="true"
        className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl bg-white text-hill shadow-sm"
      >
        <Phone className="h-4 w-4" />
      </span>
      <div className="min-w-0">
        <dt className="text-xs text-deep/60">{label}</dt>
        <dd>
          <a
            href={`tel:${phone}`}
            className="font-semibold text-hill underline-offset-4 hover:underline"
          >
            {phone}
          </a>
        </dd>
      </div>
    </div>
  );
}
