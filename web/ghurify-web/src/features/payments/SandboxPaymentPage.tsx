import { useState } from 'react';
import { useMutation, useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { useNavigate, useSearchParams } from 'react-router';
import { CreditCard, FlaskConical } from 'lucide-react';

import { asNumber } from '@/api/client';
import { cardClass, dangerButtonClass, primaryButtonClass } from '@/components/Field';
import { ErrorState } from '@/components/States';
import { errorText } from '@/lib/errors';
import { formatMoney, toLanguage } from '@/lib/format';
import { paymentsApi, sandboxMethods, type SandboxMethod } from './paymentsApi';

/**
 * The sandbox gateway's "payment page", used in development and demos instead of SSLCommerz. The
 * choice goes through the API's real callback handling, so everything after it is the real flow.
 * The API only offers this when the fake gateway is configured, never in production.
 */
export function SandboxPaymentPage() {
  const { t, i18n } = useTranslation();
  const language = toLanguage(i18n.language);
  const [params] = useSearchParams();
  const navigate = useNavigate();
  const reference = params.get('ref') ?? '';

  const payment = useQuery({
    queryKey: ['sandbox', reference],
    queryFn: ({ signal }) => paymentsApi.sandbox(reference, signal),
    enabled: reference.length > 0,
  });

  const [method, setMethod] = useState<SandboxMethod>('bkash');

  const complete = useMutation({
    mutationFn: (succeed: boolean) => paymentsApi.completeSandbox(reference, succeed, method),
    onSuccess: (result, succeed) =>
      void navigate(
        `/payments/result?booking=${asNumber(result.bookingId ?? payment.data?.bookingId ?? 0)}&outcome=${
          succeed ? 'success' : 'fail'
        }`,
      ),
  });

  return (
    <div className="flex min-h-[calc(100svh-4.5rem)] items-center justify-center bg-mist px-4 py-12">
      <section className={`${cardClass} flex w-full max-w-md flex-col gap-5 overflow-hidden p-0!`}>
        {/* Striped like tape, so nobody mistakes it for a real bank page. */}
        <p className="flex items-center justify-center gap-2 bg-[repeating-linear-gradient(-45deg,var(--color-turmeric),var(--color-turmeric)_12px,var(--color-dusk)_12px,var(--color-dusk)_24px)] py-2.5 text-xs font-bold uppercase tracking-widest text-night">
          <FlaskConical aria-hidden="true" className="h-4 w-4" />
          {t('sandbox.badge')}
        </p>

        <div className="flex flex-col gap-5 px-6 pb-6 text-center sm:px-8 sm:pb-8">
          <span className="mx-auto flex h-14 w-14 items-center justify-center rounded-2xl bg-hill/10 text-hill">
            <CreditCard aria-hidden="true" className="h-7 w-7" />
          </span>
          <div>
            <h1 className="text-2xl font-bold">{t('sandbox.title')}</h1>
            <p className="mt-2 text-sm text-deep/70">{t('sandbox.intro')}</p>
          </div>

          {payment.isPending && reference && (
            <p role="status" className="text-sm text-deep/60">
              {t('common.loading')}
            </p>
          )}
          {payment.isError && (
            <ErrorState
              message={errorText(payment.error, t)}
              onRetry={() => void payment.refetch()}
            />
          )}
          {payment.data && (
            <p className="rounded-xl bg-mist py-4 font-display text-4xl font-bold text-hill">
              {formatMoney(payment.data.total, language)}
            </p>
          )}

          {payment.data && (
            <fieldset className="text-left">
              <legend className="mb-2 text-sm font-semibold text-deep">
                {t('sandbox.methodLabel')}
              </legend>
              <div className="grid grid-cols-2 gap-2">
                {sandboxMethods.map((option) => (
                  <label
                    key={option}
                    className={`flex cursor-pointer items-center gap-2 rounded-xl border px-3 py-2.5 text-sm font-medium transition has-focus-visible:outline-2 has-focus-visible:outline-turmeric ${
                      method === option
                        ? 'border-hill bg-hill/10 text-hill'
                        : 'border-hill/15 text-deep hover:bg-mist'
                    }`}
                  >
                    <input
                      type="radio"
                      name="sandbox-method"
                      value={option}
                      checked={method === option}
                      onChange={() => setMethod(option)}
                      className="accent-hill"
                    />
                    {t(`sandbox.methods.${option}`)}
                  </label>
                ))}
              </div>
            </fieldset>
          )}

          {complete.isError && (
            <p role="alert" className="text-sm text-jamdani">
              {errorText(complete.error, t)}
            </p>
          )}

          <div className="flex flex-col gap-3">
            <button
              type="button"
              className={`${primaryButtonClass} py-3.5`}
              disabled={!payment.data || complete.isPending}
              onClick={() => complete.mutate(true)}
            >
              {t('sandbox.pay')}
            </button>
            <button
              type="button"
              className={dangerButtonClass}
              disabled={!payment.data || complete.isPending}
              onClick={() => complete.mutate(false)}
            >
              {t('sandbox.fail')}
            </button>
          </div>
        </div>
      </section>
    </div>
  );
}
