import { useTranslation } from 'react-i18next';
import { Link, useParams } from 'react-router';
import { ArrowLeft, Check, Inbox, Quote, Users, X } from 'lucide-react';

import { asNumber } from '@/api/client';
import { cardClass, dangerButtonClass, primaryButtonClass } from '@/components/Field';
import { EmptyState, ErrorState } from '@/components/States';
import { PageBanner } from '@/components/ui/PageBanner';
import { VerificationBadge } from '@/components/VerificationBadge';
import { Avatar } from '@/features/feed/PostCard';
import { useTrip } from '@/features/trips/useTrips';
import { errorText } from '@/lib/errors';
import { formatCount, toLanguage } from '@/lib/format';
import type { JoinRequestForHost } from './bookingsApi';
import { HoldCountdown } from './HoldCountdown';
import { useApproveRequest, useDeclineRequest, useTripRequests } from './useBookings';

/** The host's view of who wants to come: approve (holding a seat) or decline. */
export function ManageRequestsPage() {
  const { t, i18n } = useTranslation();
  const language = toLanguage(i18n.language);
  const { id } = useParams();
  const tripId = Number(id);
  const trip = useTrip(tripId);
  const requests = useTripRequests(tripId);

  const list = requests.data ?? [];
  const waiting = list.filter((request) => request.status === 'Pending').length;
  const approved = list.filter((request) => request.status === 'Approved').length;

  return (
    <>
      <PageBanner
        compact
        slug={trip.data?.destination.slug ?? 'rangamati'}
        kind={trip.data?.destination.kind ?? 'Lake'}
        eyebrow={
          <Link to="/host/trips" className="inline-flex items-center gap-1.5 hover:text-white">
            <ArrowLeft aria-hidden="true" className="h-3.5 w-3.5" />
            {t('wizard.backToTrips')}
          </Link>
        }
        title={t('requests.title')}
      >
        {trip.data && (
          <p className="mt-3 text-white/85">
            {trip.data.title} ·{' '}
            {t('trip.seatsOf', {
              left: formatCount(trip.data.seatsLeft, language),
              total: formatCount(trip.data.seats, language),
            })}
          </p>
        )}
      </PageBanner>

      <div className="container-page relative z-10 -mt-14 flex flex-col gap-6 pb-8 *:max-w-4xl">
        {list.length > 0 && (
          <ul className="grid grid-cols-2 gap-4">
            {[
              { key: 'waiting', icon: Inbox, value: waiting, hot: waiting > 0 },
              { key: 'approved', icon: Users, value: approved, hot: false },
            ].map(({ key, icon: Icon, value, hot }) => (
              <li
                key={key}
                className={`${cardClass} flex items-center gap-4 p-5! ${hot ? 'ring-2! ring-turmeric!' : ''}`}
              >
                <span
                  className={`flex h-12 w-12 shrink-0 items-center justify-center rounded-xl ${
                    hot ? 'bg-turmeric text-night' : 'bg-hill/10 text-hill'
                  }`}
                >
                  <Icon aria-hidden="true" className="h-6 w-6" />
                </span>
                <span>
                  <span className="block font-display text-2xl font-bold text-deep">
                    {formatCount(value, language)}
                  </span>
                  <span className="block text-sm text-deep/60">{t(`requests.counts.${key}`)}</span>
                </span>
              </li>
            ))}
          </ul>
        )}

        {requests.isPending && (
          <div role="status" className="h-40 animate-pulse rounded-2xl bg-hill/10">
            <span className="sr-only">{t('common.loading')}</span>
          </div>
        )}
        {requests.isError && (
          <ErrorState
            message={errorText(requests.error, t)}
            onRetry={() => void requests.refetch()}
          />
        )}
        {requests.data?.length === 0 && (
          <EmptyState title={t('requests.empty')} hint={t('requests.emptyHint')} />
        )}
        {list.length > 0 && (
          <ul className="flex flex-col gap-4">
            {list.map((request) => (
              <li key={String(request.id)}>
                <RequestCard request={request} />
              </li>
            ))}
          </ul>
        )}
      </div>
    </>
  );
}

const statusTone: Record<string, string> = {
  Pending: 'bg-turmeric/20 text-ochre',
  Approved: 'bg-emerald-50 text-emerald-800',
  Held: 'bg-turmeric/20 text-ochre',
  Confirmed: 'bg-emerald-50 text-emerald-800',
};

function RequestCard({ request }: { request: JoinRequestForHost }) {
  const { t } = useTranslation();
  const approve = useApproveRequest();
  const decline = useDeclineRequest();
  const id = asNumber(request.id);
  const busy = approve.isPending || decline.isPending;
  const error = approve.error ?? decline.error;
  const statusKey = request.bookingStatus ?? request.status;

  return (
    <article
      className={`${cardClass} flex flex-col gap-4 ${request.status === 'Pending' ? 'ring-turmeric/50!' : ''}`}
    >
      <div className="flex flex-wrap items-center gap-4">
        <Avatar name={request.displayName} userId={request.userId} />
        <div className="min-w-0 flex-1">
          <p className="flex flex-wrap items-center gap-2 font-display font-semibold text-deep">
            {request.displayName ?? t('admin.noName')}
            <VerificationBadge level={request.verifiedLevel} />
          </p>
          {request.gender && request.gender !== 'Unspecified' && (
            <p className="text-sm text-deep/60">{t(`gender.${request.gender}`)}</p>
          )}
        </div>
        <span
          className={`rounded-full px-3 py-1 text-xs font-semibold ${statusTone[statusKey ?? ''] ?? 'bg-mist text-deep'}`}
        >
          {request.bookingStatus
            ? t(`bookings.status.${request.bookingStatus}`)
            : t(`requests.status.${request.status}`)}
        </span>
      </div>

      {request.message && (
        <blockquote className="flex gap-3 rounded-xl bg-mist p-4 text-sm leading-relaxed text-deep/85">
          <Quote aria-hidden="true" className="h-5 w-5 shrink-0 text-turmeric" />
          <p>{request.message}</p>
        </blockquote>
      )}
      {request.bookingStatus === 'Held' && request.holdExpiresAt && (
        <p className="rounded-xl bg-turmeric/10 px-4 py-3">
          <HoldCountdown expiresAt={request.holdExpiresAt} />
        </p>
      )}

      {request.status === 'Pending' && (
        <div className="flex flex-wrap gap-2 border-t border-hill/10 pt-4">
          <button
            type="button"
            className={primaryButtonClass}
            disabled={busy}
            onClick={() => approve.mutate(id)}
          >
            <Check aria-hidden="true" className="h-4 w-4" />
            {t('requests.approve')}
          </button>
          <button
            type="button"
            className={dangerButtonClass}
            disabled={busy}
            onClick={() => decline.mutate(id)}
          >
            <X aria-hidden="true" className="h-4 w-4" />
            {t('requests.decline')}
          </button>
        </div>
      )}
      {error && (
        <p role="alert" className="text-sm text-jamdani">
          {errorText(error, t)}
        </p>
      )}
    </article>
  );
}
