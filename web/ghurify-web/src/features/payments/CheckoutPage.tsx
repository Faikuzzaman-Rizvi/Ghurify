import { useRef } from 'react';
import { useMutation, useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { Link, useParams } from 'react-router';
import { CalendarDays, LockKeyhole, ShieldCheck, UserRound } from 'lucide-react';

import { asNumber } from '@/api/client';
import { accentButtonClass, cardClass, primaryButtonClass } from '@/components/Field';
import { EmptyState, ErrorState } from '@/components/States';
import { PageBanner } from '@/components/ui/PageBanner';
import { HoldCountdown } from '@/features/bookings/HoldCountdown';
import { errorText } from '@/lib/errors';
import { formatDateRange, formatMoney, toLanguage } from '@/lib/format';
import { newIdempotencyKey, paymentsApi } from './paymentsApi';

/**
 * Checkout: the trip price, the service fee and the total, shown before the traveller is sent to
 * the gateway. The money is held in escrow, and the page says so.
 */
export function CheckoutPage() {
  const { t, i18n } = useTranslation();
  const language = toLanguage(i18n.language);
  const { id } = useParams();
  const bookingId = Number(id);
  // One key per visit to this page: retrying the button replays the same attempt.
  const key = useRef(newIdempotencyKey());

  const checkout = useQuery({
    queryKey: ['checkout', bookingId],
    queryFn: ({ signal }) => paymentsApi.checkout(bookingId, signal),
    enabled: Number.isFinite(bookingId) && bookingId > 0,
  });

  const pay = useMutation({
    mutationFn: () => paymentsApi.start(bookingId, key.current),
    onSuccess: (started) => window.location.assign(started.redirectUrl),
    // A failed attempt is over on the server; the next press must start a new one.
    onError: () => {
      key.current = newIdempotencyKey();
    },
  });

  const banner = (
    <PageBanner
      compact
      slug="coxs-bazar"
      kind="Beach"
      eyebrow={t('checkout.eyebrow')}
      title={t('checkout.title')}
    />
  );

  if (checkout.isPending) {
    return (
      <>
        {banner}
        <div role="status" className="container-page relative z-10 -mt-14 pb-8">
          <span className="sr-only">{t('common.loading')}</span>
          <div aria-hidden="true" className="h-80 animate-pulse rounded-2xl bg-hill/10" />
        </div>
      </>
    );
  }

  if (checkout.isError) {
    return (
      <>
        {banner}
        <div className="container-page relative z-10 -mt-14 pb-8 *:max-w-3xl">
          <ErrorState
            message={errorText(checkout.error, t)}
            onRetry={() => void checkout.refetch()}
          />
        </div>
      </>
    );
  }

  const booking = checkout.data;

  if (booking.status !== 'Held') {
    return (
      <>
        {banner}
        <div className="container-page relative z-10 -mt-14 pb-8 *:max-w-3xl">
          <EmptyState
            title={
              booking.status === 'Confirmed' ? t('checkout.alreadyPaid') : t('checkout.notPayable')
            }
            action={
              <Link to="/me/trips" className={`${primaryButtonClass} mt-2`}>
                {t('nav.myTrips')}
              </Link>
            }
          />
        </div>
      </>
    );
  }

  return (
    <>
      {banner}
      <div className="container-page relative z-10 -mt-14 grid items-start gap-8 pb-8 lg:grid-cols-[1fr_24rem]">
        <div className="flex min-w-0 flex-col gap-6">
          <section className={cardClass}>
            <p className="eyebrow">{t('checkout.yourTrip')}</p>
            <h2 className="mt-2 text-2xl font-semibold">{booking.tripTitle}</h2>
            <p className="mt-3 flex flex-wrap items-center gap-x-5 gap-y-1 text-sm text-deep/70">
              <span className="inline-flex items-center gap-1.5">
                <CalendarDays aria-hidden="true" className="h-4 w-4 text-ochre" />
                {formatDateRange(booking.startDate, booking.endDate, language)}
              </span>
              {booking.hostName && (
                <span className="inline-flex items-center gap-1.5">
                  <UserRound aria-hidden="true" className="h-4 w-4 text-ochre" />
                  {t('trips.hostedBy', { name: booking.hostName })}
                </span>
              )}
            </p>
          </section>

          <section className={cardClass}>
            <h2 className="flex items-center gap-2 text-lg font-semibold">
              <ShieldCheck aria-hidden="true" className="h-5 w-5 text-hill" />
              {t('checkout.nextTitle')}
            </h2>
            <ol className="mt-5 flex flex-col gap-4">
              {(['pay', 'held', 'released'] as const).map((step, index) => (
                <li key={step} className="flex gap-3">
                  <span className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-hill text-xs font-bold text-white">
                    {index + 1}
                  </span>
                  <p className="text-sm leading-relaxed text-deep/80">
                    {t(`checkout.next.${step}`)}
                  </p>
                </li>
              ))}
            </ol>
          </section>
        </div>

        <aside className={`${cardClass} flex flex-col gap-5 p-0! lg:sticky lg:top-24`}>
          <div className="rounded-t-2xl bg-hill px-6 py-5 text-white">
            <p className="text-sm text-white/80">{t('checkout.total')}</p>
            <p className="font-display text-4xl font-bold">
              {formatMoney(booking.total, language)}
            </p>
          </div>

          <dl className="space-y-3 px-6 text-sm">
            <div className="flex justify-between">
              <dt className="text-deep/70">{t('checkout.price')}</dt>
              <dd className="font-semibold text-deep">{formatMoney(booking.amount, language)}</dd>
            </div>
            <div className="flex justify-between">
              <dt className="text-deep/70">{t('checkout.fee')}</dt>
              <dd className="font-semibold text-deep">{formatMoney(booking.fee, language)}</dd>
            </div>
          </dl>

          <div className="mx-6 rounded-xl bg-turmeric/15 p-4 ring-1 ring-turmeric/40">
            <HoldCountdown expiresAt={booking.holdExpiresAt} />
          </div>

          <div className="flex flex-col gap-4 px-6 pb-6">
            {pay.isError && (
              <p role="alert" className="text-sm text-jamdani">
                {errorText(pay.error, t)}
              </p>
            )}

            <button
              type="button"
              className={`${accentButtonClass} w-full py-4 text-lg`}
              disabled={pay.isPending || pay.isSuccess}
              onClick={() => pay.mutate()}
            >
              <LockKeyhole aria-hidden="true" className="h-5 w-5" />
              {pay.isPending || pay.isSuccess
                ? t('checkout.redirecting')
                : t('checkout.pay', { amount: formatMoney(asNumber(booking.total), language) })}
            </button>

            <p className="flex gap-2 text-xs leading-relaxed text-deep/70">
              <ShieldCheck aria-hidden="true" className="h-4 w-4 shrink-0 text-hill" />
              {t('checkout.escrow')}
            </p>
          </div>
        </aside>
      </div>
    </>
  );
}
