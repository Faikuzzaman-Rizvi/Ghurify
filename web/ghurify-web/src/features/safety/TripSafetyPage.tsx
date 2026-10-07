import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useParams } from 'react-router';
import { ArrowLeft, MapPinCheck, Phone, Siren } from 'lucide-react';

import { ApiError, asNumber } from '@/api/client';
import { Dialog } from '@/components/Dialog';
import {
  cardClass,
  inputClass,
  primaryButtonClass,
  secondaryButtonClass,
  TextAreaField,
  TextField,
} from '@/components/Field';
import { EmptyState, ErrorState } from '@/components/States';
import { PageBanner } from '@/components/ui/PageBanner';
import { useAuthStore } from '@/features/auth/authStore';
import { useTrip } from '@/features/trips/useTrips';
import { errorText } from '@/lib/errors';
import { formatCount, toLanguage } from '@/lib/format';
import {
  currentPosition,
  mapLink,
  safetyApi,
  type CheckInView,
  type SosRaisedView,
} from './safetyApi';
import {
  useCheckIns,
  useCompleteCheckIn,
  useImSafe,
  useRaiseSos,
  useScheduleCheckIn,
} from './useSafety';

/** How often an open SOS sends the phone's position to the desk. */
const positionEveryMs = 60_000;

/**
 * Safety for one trip: the SOS button, and the trip's check-ins. For the host and the travellers
 * on it; the API refuses everyone else.
 */
export function TripSafetyPage() {
  const { t } = useTranslation();
  const { id } = useParams();
  const tripId = Number(id);
  const userId = useAuthStore((state) => state.user?.id);
  const trip = useTrip(tripId);
  const isHost = trip.data !== undefined && asNumber(trip.data.host.id) === userId;

  return (
    <>
      <PageBanner
        compact
        slug={trip.data?.destination.slug ?? 'sundarbans'}
        kind={trip.data?.destination.kind ?? 'Forest'}
        eyebrow={
          <Link
            to={`/trips/${tripId}`}
            className="inline-flex items-center gap-1.5 hover:text-white"
          >
            <ArrowLeft aria-hidden="true" className="h-3.5 w-3.5" />
            {trip.data?.title ?? t('safety.backToTrip')}
          </Link>
        }
        title={t('safety.title')}
      >
        <p className="mt-3 max-w-xl text-white/80">{t('safety.subtitle')}</p>
      </PageBanner>

      <div className="container-page relative z-10 -mt-14 grid items-start gap-8 pb-8 lg:grid-cols-[22rem_1fr]">
        <SosPanel tripId={tripId} />
        <CheckInsPanel tripId={tripId} isHost={isHost} />
      </div>
    </>
  );
}

function SosPanel({ tripId }: { tripId: number }) {
  const { t, i18n } = useTranslation();
  const language = toLanguage(i18n.language);
  const raise = useRaiseSos(tripId);
  const safe = useImSafe();
  const [asking, setAsking] = useState(false);
  const [message, setMessage] = useState('');
  const [raised, setRaised] = useState<SosRaisedView | null>(null);

  const sosId = raised ? asNumber(raised.sosId) : null;

  // While the SOS is open, keep the desk's map up to date. A failed fix is skipped, not fatal.
  useEffect(() => {
    if (sosId === null) return;
    const timer = window.setInterval(() => {
      currentPosition()
        .then((position) => safetyApi.updateSosLocation(sosId, position))
        .catch(() => undefined);
    }, positionEveryMs);
    return () => window.clearInterval(timer);
  }, [sosId]);

  const raiseError = raise.error
    ? raise.error instanceof ApiError
      ? errorText(raise.error, t)
      : t('safety.sos.noPosition')
    : null;

  return (
    <section
      aria-labelledby="sos-title"
      className={`${cardClass} flex flex-col gap-4 ring-2! ring-jamdani/30! lg:sticky lg:top-24`}
    >
      <h2 id="sos-title" className="flex items-center gap-3 text-xl font-semibold">
        <span className="flex h-11 w-11 items-center justify-center rounded-xl bg-jamdani text-white">
          <Siren aria-hidden="true" className="h-6 w-6" />
        </span>
        {t('safety.sos.title')}
      </h2>

      {!raised && (
        <>
          <p className="text-deep/80">{t('safety.sos.explain')}</p>
          <button
            type="button"
            onClick={() => setAsking(true)}
            className="inline-flex min-h-16 items-center justify-center rounded-2xl bg-jamdani px-6 font-display text-xl font-bold text-white shadow-[0_10px_30px_rgba(163,48,92,0.35)] transition hover:brightness-110 focus-visible:outline-none focus-visible:ring-4 focus-visible:ring-jamdani/40 active:scale-[0.98]"
          >
            {t('safety.sos.button')}
          </button>
        </>
      )}

      {raised && (
        <div role="status" className="flex flex-col gap-3">
          <p className="text-lg font-bold text-jamdani">{t('safety.sos.sent')}</p>
          <p className="text-deep">
            {raised.emergencyContactTexted
              ? t('safety.sos.contactTexted')
              : t('safety.sos.contactNotTexted')}
          </p>
          {raised.nearestHelp.length > 0 && (
            <div>
              <h3 className="font-semibold text-deep">{t('safety.sos.nearestHelp')}</h3>
              <ul className="mt-2 flex flex-col gap-2">
                {raised.nearestHelp.map((point) => (
                  <li
                    key={`${point.name}-${point.distanceMeters}`}
                    className="rounded-xl bg-mist p-4"
                  >
                    <p className="font-semibold text-deep">
                      {language === 'bn' ? point.nameBn : point.name}
                    </p>
                    <p className="text-sm text-deep/70">
                      {t('safety.sos.distance', {
                        km: formatCount(
                          Math.max(1, Math.round(Number(point.distanceMeters) / 1000)),
                          language,
                        ),
                      })}
                      {' · '}
                      <a
                        href={mapLink(point.latitude, point.longitude)}
                        target="_blank"
                        rel="noreferrer"
                        className="text-hill underline"
                      >
                        {t('safety.sos.map')}
                      </a>
                      {point.phone && (
                        <>
                          {' · '}
                          <a href={`tel:${point.phone}`} className="text-hill underline">
                            {point.phone}
                          </a>
                        </>
                      )}
                    </p>
                  </li>
                ))}
              </ul>
              <p className="mt-2 text-xs text-deep/60">{t('safety.sos.helpApproximate')}</p>
            </div>
          )}
          <button
            type="button"
            className={secondaryButtonClass}
            disabled={safe.isPending || safe.isSuccess}
            onClick={() => safe.mutate(sosId!)}
          >
            {safe.isSuccess ? t('safety.sos.markedSafe') : t('safety.sos.imSafe')}
          </button>
          {safe.isError && (
            <p role="alert" className="text-sm text-jamdani">
              {errorText(safe.error, t)}
            </p>
          )}
        </div>
      )}

      <a
        href="tel:999"
        className="inline-flex items-center justify-center gap-2 rounded-xl border border-jamdani/30 py-3 font-semibold text-jamdani transition hover:bg-jamdani/5"
      >
        <Phone aria-hidden="true" className="h-4 w-4" />
        {t('safety.sos.call999')}
      </a>

      <Dialog open={asking} title={t('safety.sos.confirmTitle')} onClose={() => setAsking(false)}>
        <form
          className="flex flex-col gap-3"
          onSubmit={(event) => {
            event.preventDefault();
            raise.mutate(message.trim() || null, {
              onSuccess: (view) => {
                setRaised(view);
                setAsking(false);
              },
            });
          }}
        >
          <p className="text-deep/80">{t('safety.sos.confirmText')}</p>
          <TextAreaField
            id="sos-message"
            label={t('safety.sos.message')}
            value={message}
            maxLength={500}
            rows={2}
            onChange={(event) => setMessage(event.target.value)}
          />
          {raiseError && (
            <p role="alert" className="text-sm text-jamdani">
              {raiseError}
            </p>
          )}
          <div className="flex justify-end gap-2">
            <button type="button" className={secondaryButtonClass} onClick={() => setAsking(false)}>
              {t('common.cancel')}
            </button>
            <button
              type="submit"
              disabled={raise.isPending}
              className="inline-flex items-center justify-center rounded-full bg-jamdani px-5 py-2.5 font-bold text-white disabled:opacity-60"
            >
              {raise.isPending ? t('safety.sos.sending') : t('safety.sos.send')}
            </button>
          </div>
        </form>
      </Dialog>
    </section>
  );
}

function CheckInsPanel({ tripId, isHost }: { tripId: number; isHost: boolean }) {
  const { t } = useTranslation();
  const checkIns = useCheckIns(tripId);

  return (
    <section aria-labelledby="checkins-title" className={`${cardClass} flex flex-col gap-4`}>
      <div className="flex items-start gap-3">
        <span className="flex h-11 w-11 shrink-0 items-center justify-center rounded-xl bg-hill text-white">
          <MapPinCheck aria-hidden="true" className="h-6 w-6" />
        </span>
        <div>
          <h2 id="checkins-title" className="text-xl font-semibold">
            {t('safety.checkIns.title')}
          </h2>
          <p className="mt-1 text-sm text-deep/70">{t('safety.checkIns.explain')}</p>
        </div>
      </div>

      {checkIns.isPending && (
        <div role="status" className="h-20 animate-pulse rounded-2xl bg-hill/10">
          <span className="sr-only">{t('common.loading')}</span>
        </div>
      )}
      {checkIns.isError && (
        <ErrorState
          message={errorText(checkIns.error, t)}
          onRetry={() => void checkIns.refetch()}
        />
      )}
      {checkIns.data?.length === 0 && (
        <EmptyState
          title={t('safety.checkIns.empty')}
          {...(isHost ? { hint: t('safety.checkIns.emptyHost') } : {})}
        />
      )}
      {checkIns.data && checkIns.data.length > 0 && (
        <ul className="flex flex-col gap-2">
          {checkIns.data.map((checkIn) => (
            <li key={String(checkIn.id)}>
              <CheckInRow checkIn={checkIn} tripId={tripId} />
            </li>
          ))}
        </ul>
      )}

      {isHost && <ScheduleForm tripId={tripId} />}
    </section>
  );
}

function CheckInRow({ checkIn, tripId }: { checkIn: CheckInView; tripId: number }) {
  const { t, i18n } = useTranslation();
  const complete = useCompleteCheckIn(tripId);
  const due = new Intl.DateTimeFormat(i18n.language, {
    dateStyle: 'medium',
    timeStyle: 'short',
    timeZone: 'Asia/Dhaka',
  }).format(new Date(checkIn.dueAt));

  return (
    <div className="flex flex-wrap items-center justify-between gap-3 rounded-xl bg-mist p-4">
      <div>
        <p className="font-semibold text-deep">{checkIn.label}</p>
        <p className="text-sm text-deep/70">
          {t('safety.checkIns.due', { time: due })} ·{' '}
          {t(`safety.checkIns.status.${checkIn.status}`)}
          {checkIn.checkedInBy ? ` · ${checkIn.checkedInBy}` : ''}
        </p>
      </div>
      {checkIn.status !== 'Done' && (
        <button
          type="button"
          className={primaryButtonClass}
          disabled={complete.isPending}
          onClick={() => complete.mutate({ checkInId: asNumber(checkIn.id), note: null })}
        >
          {t('safety.checkIns.done')}
        </button>
      )}
      {complete.isError && (
        <p role="alert" className="w-full text-sm text-jamdani">
          {errorText(complete.error, t)}
        </p>
      )}
    </div>
  );
}

function ScheduleForm({ tripId }: { tripId: number }) {
  const { t } = useTranslation();
  const schedule = useScheduleCheckIn(tripId);
  const [label, setLabel] = useState('');
  const [dueAt, setDueAt] = useState('');

  return (
    <form
      className="flex flex-col gap-3 border-t border-hill/10 pt-4"
      onSubmit={(event) => {
        event.preventDefault();
        // datetime-local has no zone: it is read as the phone's local time, which for a trip in
        // Bangladesh is Dhaka time.
        schedule.mutate(
          { label: label.trim(), dueAt: new Date(dueAt).toISOString() },
          {
            onSuccess: () => {
              setLabel('');
              setDueAt('');
            },
          },
        );
      }}
    >
      <h3 className="font-semibold text-deep">{t('safety.checkIns.scheduleTitle')}</h3>
      <TextField
        id="checkin-label"
        label={t('safety.checkIns.label')}
        value={label}
        maxLength={150}
        onChange={(event) => setLabel(event.target.value)}
      />
      <div className="flex flex-col gap-1">
        <label htmlFor="checkin-due" className="text-sm font-medium text-deep">
          {t('safety.checkIns.dueAt')}
        </label>
        <input
          id="checkin-due"
          type="datetime-local"
          value={dueAt}
          onChange={(event) => setDueAt(event.target.value)}
          className={inputClass}
        />
      </div>
      {schedule.isError && (
        <p role="alert" className="text-sm text-jamdani">
          {errorText(schedule.error, t)}
        </p>
      )}
      <button
        type="submit"
        className={`${primaryButtonClass} self-start`}
        disabled={schedule.isPending || label.trim() === '' || dueAt === ''}
      >
        {t('safety.checkIns.schedule')}
      </button>
    </form>
  );
}
