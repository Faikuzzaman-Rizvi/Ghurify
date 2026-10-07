import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';
import {
  CalendarDays,
  Check,
  Compass,
  Flag,
  MapPin,
  MessageCircle,
  ReceiptText,
  ShieldAlert,
  Star,
  Undo2,
  XCircle,
} from 'lucide-react';

import { asNumber } from '@/api/client';
import {
  accentButtonClass,
  cardClass,
  dangerButtonClass,
  primaryButtonClass,
  secondaryButtonClass,
} from '@/components/Field';
import { CardSkeletons, EmptyState, ErrorState } from '@/components/States';
import { PageBanner } from '@/components/ui/PageBanner';
import { Photo } from '@/components/ui/Photo';
import { useChatUnread } from '@/features/chat/useChat';
import { ReportDialog } from '@/features/safety/ReportDialog';
import { CancelBookingDialog } from '@/features/payments/CancelBookingDialog';
import { paymentsApi } from '@/features/payments/paymentsApi';
import { useAuthStore } from '@/features/auth/authStore';
import { errorText } from '@/lib/errors';
import { formatDateRange, formatMoney, toLanguage } from '@/lib/format';
import type { MyTripBooking } from './bookingsApi';
import { HoldCountdown } from './HoldCountdown';
import { useCancelRequest, useMyBookings } from './useBookings';

const journey = ['requested', 'approved', 'paid', 'travelled'] as const;

/** How far along a booking is, or null when it ended without travelling. */
function journeyStep(booking: MyTripBooking): number | null {
  if (booking.bookingStatus === 'Confirmed') return booking.tripStatus === 'Completed' ? 3 : 2;
  if (booking.bookingStatus === 'Held') return 1;
  if (booking.requestStatus === 'Pending') return 0;
  return null;
}

/** Still to come, as opposed to travelled or ended. */
function isUpcoming(booking: MyTripBooking): boolean {
  const step = journeyStep(booking);
  return step !== null && step < 3;
}

type Filter = 'upcoming' | 'past';

/** The traveller's trips: what they asked for, what is held, paid, or over. */
export function MyTripsPage() {
  const { t } = useTranslation();
  const bookings = useMyBookings();
  const [filter, setFilter] = useState<Filter | null>(null);

  const all = bookings.data ?? [];
  const counts = {
    upcoming: all.filter(isUpcoming).length,
    past: all.filter((booking) => !isUpcoming(booking)).length,
  };
  // Until the traveller picks one: with nothing upcoming, open on the past, not an empty list.
  const active: Filter = filter ?? (counts.upcoming === 0 && counts.past > 0 ? 'past' : 'upcoming');
  const shown = all.filter((booking) => (active === 'upcoming') === isUpcoming(booking));

  return (
    <>
      <PageBanner
        compact
        slug="saint-martins"
        kind="Island"
        eyebrow={t('myTrips.eyebrow')}
        titleKey="myTrips.titleAccent"
        aside={
          <Link to="/trips" className={accentButtonClass}>
            <Compass aria-hidden="true" className="h-4 w-4" />
            {t('nav.explore')}
          </Link>
        }
      >
        <p className="mt-3 max-w-xl text-white/80">{t('myTrips.subtitle')}</p>
      </PageBanner>

      <div className="container-page relative z-10 -mt-14 grid items-start gap-8 pb-8 lg:grid-cols-[1fr_20rem]">
        <section aria-labelledby="bookings-heading" className="flex min-w-0 flex-col gap-6">
          <h2 id="bookings-heading" className="sr-only">
            {t('myTrips.title')}
          </h2>

          {bookings.isPending && (
            <div className="grid gap-6">
              <CardSkeletons count={2} />
            </div>
          )}
          {bookings.isError && (
            <ErrorState
              message={errorText(bookings.error, t)}
              onRetry={() => void bookings.refetch()}
            />
          )}
          {bookings.data?.length === 0 && (
            <EmptyState
              title={t('myTrips.empty')}
              hint={t('myTrips.emptyHint')}
              action={
                <Link to="/trips" className={`${primaryButtonClass} mt-2`}>
                  {t('nav.explore')}
                </Link>
              }
            />
          )}

          {all.length > 0 && (
            <>
              <div
                role="group"
                aria-label={t('myTrips.filter')}
                className="flex w-fit gap-1 rounded-full bg-white p-1 shadow-sm ring-1 ring-hill/10"
              >
                {(['upcoming', 'past'] as const).map((key) => (
                  <button
                    key={key}
                    type="button"
                    aria-pressed={active === key}
                    onClick={() => setFilter(key)}
                    className={`flex items-center gap-2 rounded-full px-5 py-2 text-sm font-semibold transition ${
                      active === key ? 'bg-hill text-white' : 'text-deep/70 hover:text-deep'
                    }`}
                  >
                    {t(`myTrips.tabs.${key}`)}
                    <span
                      className={`rounded-full px-2 py-0.5 text-xs ${
                        active === key ? 'bg-white/20' : 'bg-mist'
                      }`}
                    >
                      {counts[key]}
                    </span>
                  </button>
                ))}
              </div>

              {shown.length === 0 ? (
                <p className="rounded-2xl border-2 border-dashed border-hill/15 px-6 py-10 text-center text-deep/60">
                  {t('myTrips.noneHere')}
                </p>
              ) : (
                <ul className="flex flex-col gap-6">
                  {shown.map((booking) => (
                    <li key={String(booking.requestId)}>
                      <BookingCard booking={booking} />
                    </li>
                  ))}
                </ul>
              )}
            </>
          )}
        </section>

        <aside className="flex flex-col gap-6 lg:sticky lg:top-24">
          <RefundsCard />
          <div className={cardClass}>
            <h2 className="flex items-center gap-2 text-lg font-semibold">
              <ShieldAlert aria-hidden="true" className="h-5 w-5 text-hill" />
              {t('myTrips.safeTitle')}
            </h2>
            <ul className="mt-4 space-y-3 text-sm text-deep/75">
              {(['escrow', 'sos', 'refund'] as const).map((key) => (
                <li key={key} className="flex gap-2">
                  <Check aria-hidden="true" className="mt-0.5 h-4 w-4 shrink-0 text-hill" />
                  {t(`myTrips.safe.${key}`)}
                </li>
              ))}
            </ul>
          </div>
        </aside>
      </div>
    </>
  );
}

/** Money on its way back, and where each refund stands with the gateway. */
function RefundsCard() {
  const { t, i18n } = useTranslation();
  const language = toLanguage(i18n.language);
  const user = useAuthStore((state) => state.user?.id ?? 'anonymous');
  const refunds = useQuery({
    queryKey: ['me', user, 'refunds'],
    queryFn: ({ signal }) => paymentsApi.refunds(signal),
  });

  if (!refunds.data || refunds.data.length === 0) {
    return null;
  }

  return (
    <section aria-labelledby="refunds-title" className={cardClass}>
      <h2 id="refunds-title" className="flex items-center gap-2 text-lg font-semibold">
        <ReceiptText aria-hidden="true" className="h-5 w-5 text-hill" />
        {t('refunds.title')}
      </h2>
      <ul className="mt-4 divide-y divide-hill/10">
        {refunds.data.map((refund) => (
          <li key={String(refund.id)} className="py-3 text-sm first:pt-0 last:pb-0">
            <p className="font-semibold text-deep">{refund.tripTitle}</p>
            <p className="mt-0.5 flex flex-wrap justify-between gap-2 text-deep/70">
              <span>{t(`refunds.reason.${refund.reason}`)}</span>
              <span className="font-semibold text-hill">
                {formatMoney(refund.amount, language)}
              </span>
            </p>
            <p className="mt-1 text-xs text-deep/60">{t(`refunds.status.${refund.status}`)}</p>
          </li>
        ))}
      </ul>
    </section>
  );
}

function BookingCard({ booking }: { booking: MyTripBooking }) {
  const { t, i18n } = useTranslation();
  const language = toLanguage(i18n.language);
  const cancel = useCancelRequest();
  const [cancelling, setCancelling] = useState(false);
  const [disputing, setDisputing] = useState(false);
  const { data: chats } = useChatUnread();
  const tripId = asNumber(booking.tripId);
  const unread = asNumber(chats?.find((chat) => asNumber(chat.tripId) === tripId)?.unread ?? 0);
  const status = booking.bookingStatus ?? booking.requestStatus;
  const label = booking.bookingStatus
    ? t(`bookings.status.${booking.bookingStatus}`)
    : t(`requests.status.${booking.requestStatus}`);
  const canWithdraw =
    booking.requestStatus === 'Pending' ||
    (booking.requestStatus === 'Approved' && booking.bookingStatus === 'Held');
  const step = journeyStep(booking);
  const confirmed = booking.bookingStatus === 'Confirmed';
  const travelled = booking.tripStatus === 'Completed';

  const badge =
    status === 'Confirmed'
      ? 'bg-emerald-500 text-white'
      : status === 'Held'
        ? 'bg-turmeric text-night'
        : status === 'Pending'
          ? 'bg-white text-deep'
          : 'bg-deep/80 text-white';

  return (
    <article className={`${cardClass} overflow-hidden p-0! sm:flex`}>
      <div className="relative h-40 shrink-0 sm:h-auto sm:w-52">
        <Photo
          slug={booking.destinationSlug}
          kind={booking.destinationKind}
          cut="card"
          decorative
          className="absolute! inset-0"
        />
        <span
          className={`absolute left-3 top-3 rounded-full px-3 py-1 text-xs font-semibold shadow-sm ${badge}`}
        >
          {label}
        </span>
      </div>

      <div className="flex min-w-0 flex-1 flex-col gap-4 p-5 sm:p-6">
        <div>
          <Link
            to={`/trips/${tripId}`}
            className="font-display text-lg font-semibold text-deep hover:text-hill"
          >
            {booking.title}
          </Link>
          <p className="mt-1.5 flex flex-wrap items-center gap-x-4 gap-y-1 text-sm text-deep/70">
            <span className="inline-flex items-center gap-1.5">
              <MapPin aria-hidden="true" className="h-4 w-4 text-ochre" />
              {language === 'bn' ? booking.destinationNameBn : booking.destinationName}
            </span>
            <span className="inline-flex items-center gap-1.5">
              <CalendarDays aria-hidden="true" className="h-4 w-4 text-ochre" />
              {formatDateRange(booking.startDate, booking.endDate, language)}
            </span>
          </p>
        </div>

        {step !== null && (
          <ol className="flex items-center" aria-label={t('myTrips.progress')}>
            {journey.map((name, index) => {
              const done = index <= step;
              return (
                <li key={name} className="flex flex-1 items-center last:flex-none">
                  <span className="flex flex-col items-center gap-1">
                    <span
                      className={`flex h-6 w-6 items-center justify-center rounded-full text-[0.65rem] font-bold ${
                        done ? 'bg-hill text-white' : 'bg-mist text-deep/40'
                      }`}
                    >
                      {done ? <Check aria-hidden="true" className="h-3.5 w-3.5" /> : index + 1}
                    </span>
                    <span
                      className={`whitespace-nowrap text-[0.7rem] ${
                        done ? 'font-semibold text-deep' : 'text-deep/50'
                      }`}
                    >
                      {t(`myTrips.journey.${name}`)}
                      <span className="sr-only">
                        {' '}
                        ({done ? t('verification.stepDone') : t('verification.stepTodo')})
                      </span>
                    </span>
                  </span>
                  {index < journey.length - 1 && (
                    <span
                      aria-hidden="true"
                      className={`mx-1 mb-5 h-0.5 flex-1 rounded-full ${index < step ? 'bg-hill' : 'bg-mist'}`}
                    />
                  )}
                </li>
              );
            })}
          </ol>
        )}

        {booking.bookingStatus === 'Held' && booking.holdExpiresAt && (
          <div className="flex flex-wrap items-center justify-between gap-3 rounded-xl bg-turmeric/15 p-4 ring-1 ring-turmeric/40">
            <HoldCountdown expiresAt={booking.holdExpiresAt} />
            <Link
              to={`/bookings/${asNumber(booking.bookingId ?? 0)}/checkout`}
              className={accentButtonClass}
            >
              {t('bookings.payNow', { amount: formatMoney(booking.amount ?? 0, language) })}
            </Link>
          </div>
        )}

        <div className="flex flex-wrap gap-2 border-t border-hill/10 pt-4 *:px-4! *:py-2! *:text-sm empty:hidden">
          {confirmed && (
            <Link to={`/trips/${tripId}/chat`} className={secondaryButtonClass}>
              <MessageCircle aria-hidden="true" className="h-4 w-4" />
              {t('chat.open')}
              {unread > 0 && (
                <span className="rounded-full bg-jamdani px-2 text-xs font-bold text-white">
                  {t('chat.unread', { count: unread })}
                </span>
              )}
            </Link>
          )}
          {confirmed && !travelled && (
            <Link to={`/trips/${tripId}/safety`} className={secondaryButtonClass}>
              <ShieldAlert aria-hidden="true" className="h-4 w-4" />
              {t('safety.open')}
            </Link>
          )}
          {confirmed && travelled && (
            <Link to={`/trips/${tripId}/review`} className={primaryButtonClass}>
              <Star aria-hidden="true" className="h-4 w-4" />
              {t('reviews.write')}
            </Link>
          )}
          {confirmed && !travelled && (
            <button type="button" className={dangerButtonClass} onClick={() => setCancelling(true)}>
              <XCircle aria-hidden="true" className="h-4 w-4" />
              {t('cancel.open')}
            </button>
          )}
          {canWithdraw && (
            <button
              type="button"
              className={dangerButtonClass}
              disabled={cancel.isPending}
              onClick={() => cancel.mutate(asNumber(booking.requestId))}
            >
              <Undo2 aria-hidden="true" className="h-4 w-4" />
              {t('myTrips.withdraw')}
            </button>
          )}
        </div>

        {booking.bookingId &&
          (booking.bookingStatus === 'Confirmed' || booking.bookingStatus === 'Refunded') && (
            <button
              type="button"
              onClick={() => setDisputing(true)}
              className="inline-flex items-center gap-1.5 self-start text-sm text-deep/60 transition hover:text-jamdani"
            >
              <Flag aria-hidden="true" className="h-4 w-4" />
              {t('report.open.Dispute')}
            </button>
          )}
        {booking.bookingId && disputing && (
          <ReportDialog
            kind="Dispute"
            targetId={asNumber(booking.bookingId)}
            open
            onClose={() => setDisputing(false)}
          />
        )}
        {booking.bookingId && (
          <CancelBookingDialog
            bookingId={asNumber(booking.bookingId)}
            open={cancelling}
            onClose={() => setCancelling(false)}
          />
        )}
        {cancel.isError && (
          <p role="alert" className="text-sm text-jamdani">
            {errorText(cancel.error, t)}
          </p>
        )}
      </div>
    </article>
  );
}
