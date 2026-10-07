import { useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { Link, useParams } from 'react-router';
import { ArrowLeft, ShieldAlert, ShieldCheck } from 'lucide-react';

import { asNumber } from '@/api/client';
import { ErrorState } from '@/components/States';
import { PaymentReceipt, ReceiptSection, Row } from '@/features/payments/PaymentReceipt';
import { errorText } from '@/lib/errors';
import { formatDateTime, formatMoney, toLanguage } from '@/lib/format';
import { adminApi } from './adminApi';

/**
 * One payment in full for the admin desk: the traveller's receipt, plus who paid and who hosts,
 * what the gateway settled to us after its charge and its risk check, and every callback it sent.
 */
export function AdminPaymentDetailPage() {
  const { t, i18n } = useTranslation();
  const language = toLanguage(i18n.language);
  const money = (value: number | string) => formatMoney(value, language);
  const id = Number(useParams().id);

  const detail = useQuery({
    queryKey: ['admin', 'payment', id],
    queryFn: ({ signal }) => adminApi.payment(id, signal),
    enabled: Number.isFinite(id) && id > 0,
    retry: false,
  });

  const data = detail.data;
  const paid = data?.payment.paidAmount;
  const gatewayCharge =
    data?.storeAmount != null && paid != null ? asNumber(paid) - asNumber(data.storeAmount) : null;

  return (
    <div className="flex flex-col gap-4">
      <Link
        to="/admin/payments"
        className="inline-flex items-center gap-1.5 text-sm font-semibold text-hill hover:underline"
      >
        <ArrowLeft aria-hidden="true" className="h-4 w-4" />
        {t('admin.payments.back')}
      </Link>
      <h2 className="font-display text-2xl font-semibold text-deep sm:text-[1.75rem]">
        {t('admin.payments.detailTitle', { id })}
      </h2>

      {detail.isPending && (
        <div role="status" className="h-60 animate-pulse rounded-2xl bg-hill/10">
          <span className="sr-only">{t('common.loading')}</span>
        </div>
      )}
      {detail.isError && (
        <ErrorState message={errorText(detail.error, t)} onRetry={() => void detail.refetch()} />
      )}

      {data && (
        <>
          <div className="grid gap-6 md:grid-cols-2">
            <ReceiptSection title={t('admin.payments.parties')}>
              <Row label={t('admin.payments.payer')}>
                <Link
                  to={`/admin/users/${asNumber(data.travellerId)}`}
                  className="text-hill underline"
                >
                  {data.travellerName ?? t('admin.noName')}
                </Link>
              </Row>
              <Row label={t('admin.payments.email')}>
                <span className="break-all">{data.travellerEmail}</span>
              </Row>
              <Row label={t('admin.payments.host')}>
                <Link to={`/admin/users/${asNumber(data.hostId)}`} className="text-hill underline">
                  {data.payment.hostName ?? t('admin.noName')}
                </Link>
              </Row>
              <Row label={t('receipt.fields.booking')}>
                <Link
                  to={`/admin/bookings?q=${asNumber(data.payment.bookingId)}`}
                  className="text-hill underline"
                >
                  {t('receipt.bookingNumber', { id: asNumber(data.payment.bookingId) })}
                </Link>
              </Row>
            </ReceiptSection>

            <ReceiptSection title={t('admin.payments.settlement')}>
              <Row label={t('receipt.fields.paid')}>{paid != null ? money(paid) : '—'}</Row>
              <Row label={t('admin.payments.gatewayCharge')}>
                {gatewayCharge != null ? money(gatewayCharge) : '—'}
              </Row>
              <Row label={t('admin.payments.storeAmount')} strong>
                {data.storeAmount != null ? money(data.storeAmount) : '—'}
              </Row>
              <Row label={t('admin.payments.risk')}>
                {data.riskFlagged == null ? (
                  t('admin.payments.riskUnknown')
                ) : data.riskFlagged ? (
                  <span className="inline-flex items-center gap-1 font-semibold text-jamdani">
                    <ShieldAlert aria-hidden="true" className="h-4 w-4" />
                    {t('admin.payments.riskFlagged')}
                  </span>
                ) : (
                  <span className="inline-flex items-center gap-1 text-emerald-800">
                    <ShieldCheck aria-hidden="true" className="h-4 w-4" />
                    {t('admin.payments.riskClear')}
                  </span>
                )}
              </Row>
            </ReceiptSection>
          </div>

          <PaymentReceipt
            payment={data.payment}
            tripLink={`/trips/${asNumber(data.payment.tripId)}`}
          />

          <ReceiptSection title={t('admin.payments.callbacks')}>
            {data.callbacks.length === 0 ? (
              <p className="text-sm text-deep/60">{t('admin.payments.noCallbacks')}</p>
            ) : (
              <ul className="flex flex-col gap-2 text-sm">
                {data.callbacks.map((callback) => (
                  <li key={String(callback.id)} className="rounded-xl bg-mist p-3">
                    <p
                      className={`font-semibold ${callback.signatureValid ? 'text-deep' : 'text-jamdani'}`}
                    >
                      {callback.signatureValid
                        ? t('admin.payments.signatureValid')
                        : t('admin.payments.signatureInvalid')}
                      {callback.outcome ? ` · ${callback.outcome}` : ''}
                    </p>
                    <p className="break-all text-deep/70">
                      <span className="font-mono">{callback.eventId}</span> ·{' '}
                      {formatDateTime(callback.received, language)} ·{' '}
                      {callback.processedOn
                        ? t('admin.payments.processed', {
                            when: formatDateTime(callback.processedOn, language),
                          })
                        : t('admin.payments.notProcessed')}
                    </p>
                  </li>
                ))}
              </ul>
            )}
          </ReceiptSection>
        </>
      )}
    </div>
  );
}
