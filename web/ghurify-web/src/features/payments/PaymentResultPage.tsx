import { useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { Link, useSearchParams } from 'react-router';
import { LoaderCircle, MessageCircle, PartyPopper, RotateCcw, TriangleAlert } from 'lucide-react';

import { accentButtonClass, cardClass, secondaryButtonClass } from '@/components/Field';
import { useHeroUnderHeader } from '@/components/ui/headerStore';
import { Photo } from '@/components/ui/Photo';
import { paymentsApi } from './paymentsApi';

const look = {
  paid: { icon: PartyPopper, tone: 'bg-emerald-100 text-emerald-700', photo: 'saint-martins' },
  failed: { icon: TriangleAlert, tone: 'bg-jamdani/10 text-jamdani', photo: 'sundarbans' },
  waiting: { icon: LoaderCircle, tone: 'bg-turmeric/20 text-ochre', photo: 'sajek' },
} as const;

/**
 * Where the traveller lands after the gateway. The URL only says which booking; whether the
 * payment went through is read from the API, which only believes the gateway's verified callback.
 * While the confirmation is on its way, the page checks again every few seconds.
 */
export function PaymentResultPage() {
  const { t } = useTranslation();
  const [params] = useSearchParams();
  const bookingId = Number(params.get('booking'));
  const said = params.get('outcome');
  useHeroUnderHeader();

  const checkout = useQuery({
    queryKey: ['checkout', bookingId, 'result'],
    queryFn: ({ signal }) => paymentsApi.checkout(bookingId, signal),
    enabled: Number.isFinite(bookingId) && bookingId > 0,
    refetchInterval: (query) => {
      const data = query.state.data;
      const settled =
        data?.status === 'Confirmed' ||
        data?.latestPaymentStatus === 'Failed' ||
        data?.latestPaymentStatus === 'Expired' ||
        data?.status === 'Cancelled';
      return settled ? false : 3_000;
    },
  });

  const status = checkout.data?.status;
  const payment = checkout.data?.latestPaymentStatus;

  const state: 'paid' | 'failed' | 'waiting' =
    status === 'Confirmed'
      ? 'paid'
      : payment === 'Failed' || payment === 'Expired' || status === 'Cancelled' || said === 'cancel'
        ? 'failed'
        : 'waiting';
  const { icon: Icon, tone, photo } = look[state];

  return (
    <section className="relative isolate flex min-h-svh items-center justify-center overflow-hidden bg-night px-4 pb-16 pt-28">
      <Photo slug={photo} kind="Hills" cut="wide" decorative className="absolute! inset-0 -z-10" />
      <div aria-hidden="true" className="absolute inset-0 -z-10 bg-night/70" />

      <div
        className={`${cardClass} flex w-full max-w-md animate-rise flex-col items-center gap-4 text-center`}
        role="status"
      >
        <span className={`flex h-20 w-20 items-center justify-center rounded-full ${tone}`}>
          <Icon
            aria-hidden="true"
            className={`h-10 w-10 ${state === 'waiting' ? 'animate-spin' : ''}`}
          />
        </span>
        <h1 className="text-2xl font-bold">{t(`paymentResult.${state}.title`)}</h1>
        <p className="text-deep/70">{t(`paymentResult.${state}.body`)}</p>

        <div className="mt-2 flex w-full flex-col gap-3 sm:flex-row sm:justify-center">
          {state === 'paid' && checkout.data && (
            <Link to={`/trips/${checkout.data.tripId}/chat`} className={accentButtonClass}>
              <MessageCircle aria-hidden="true" className="h-4 w-4" />
              {t('chat.open')}
            </Link>
          )}
          {state === 'failed' && checkout.data?.status === 'Held' && (
            <Link to={`/bookings/${bookingId}/checkout`} className={accentButtonClass}>
              <RotateCcw aria-hidden="true" className="h-4 w-4" />
              {t('paymentResult.tryAgain')}
            </Link>
          )}
          <Link to="/me/trips" className={secondaryButtonClass}>
            {t('nav.myTrips')}
          </Link>
        </div>
      </div>
    </section>
  );
}
