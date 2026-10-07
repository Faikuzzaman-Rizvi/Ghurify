import { useTranslation } from 'react-i18next';
import { Link, useParams } from 'react-router';
import { ArrowLeft, Printer } from 'lucide-react';

import { asNumber } from '@/api/client';
import { secondaryButtonClass } from '@/components/Field';
import { ErrorState } from '@/components/States';
import { errorText } from '@/lib/errors';
import { PaymentReceipt } from './PaymentReceipt';
import { usePaymentReceipt } from './usePayments';

/**
 * One of the traveller's payments as a receipt they can keep: every amount, reference and date,
 * printable. Someone else's payment comes back "not found" from the API.
 */
export function PaymentReceiptPage() {
  const { t } = useTranslation();
  const id = Number(useParams().id);
  const receipt = usePaymentReceipt(id);

  return (
    <div className="bg-mist pb-16 pt-28 print:bg-white print:pt-0">
      <div className="container-page flex max-w-4xl flex-col gap-6">
        <header className="flex flex-wrap items-end justify-between gap-4">
          <div>
            <Link
              to="/me/payments"
              className="inline-flex items-center gap-1.5 text-sm font-semibold text-hill hover:underline print:hidden"
            >
              <ArrowLeft aria-hidden="true" className="h-4 w-4" />
              {t('receipt.back')}
            </Link>
            <p className="mt-3 text-xs font-semibold uppercase tracking-widest text-ochre">
              {t('app.name')}
            </p>
            <h1 className="font-display text-3xl font-bold text-deep">{t('receipt.title')}</h1>
            {receipt.data && (
              <p className="mt-1 font-mono text-sm text-deep/60">
                {t('receipt.number', { id: asNumber(receipt.data.id) })}
              </p>
            )}
          </div>
          {receipt.data && (
            <button
              type="button"
              className={`${secondaryButtonClass} print:hidden`}
              onClick={() => window.print()}
            >
              <Printer aria-hidden="true" className="h-4 w-4" />
              {t('receipt.print')}
            </button>
          )}
        </header>

        {receipt.isPending && (
          <div role="status" className="flex flex-col gap-4">
            <span className="sr-only">{t('common.loading')}</span>
            {[0, 1, 2].map((block) => (
              <div
                key={block}
                aria-hidden="true"
                className="h-40 animate-pulse rounded-2xl bg-hill/10"
              />
            ))}
          </div>
        )}
        {receipt.isError && (
          <ErrorState
            message={errorText(receipt.error, t)}
            onRetry={() => void receipt.refetch()}
          />
        )}
        {receipt.data && (
          <PaymentReceipt
            payment={receipt.data}
            tripLink={`/trips/${asNumber(receipt.data.tripId)}`}
          />
        )}
      </div>
    </div>
  );
}
