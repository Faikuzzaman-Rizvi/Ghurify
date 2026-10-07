import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';

import { asNumber } from '@/api/client';
import { cardClass } from '@/components/Field';
import { formatDateRange, formatDateTime, formatMoney, toLanguage } from '@/lib/format';
import { CopyButton, PaymentMethod, PaymentStatusBadge } from './PaymentParts';
import type { PaymentDetail } from './paymentsApi';

/** Failure codes the API records; anything else is the gateway's own words, shown as sent. */
const knownFailures = new Set([
  'declined',
  'cancelled_by_traveler',
  'gateway_unavailable',
  'amount_mismatch',
]);

/**
 * Everything about one payment, as a receipt: what was charged and how, every reference to quote,
 * when each step happened (in Dhaka time), the booking it paid for and every refund against it.
 * Used by the traveller's receipt page and the admin desk, which adds its own sections around it.
 */
export function PaymentReceipt({
  payment,
  tripLink,
}: {
  payment: PaymentDetail;
  tripLink: string;
}) {
  const { t, i18n } = useTranslation();
  const language = toLanguage(i18n.language);
  const money = (value: number | string) => formatMoney(value, language);
  const when = (instant: string | null) => (instant ? formatDateTime(instant, language) : '—');

  const paid = payment.paidAmount ?? null;
  const refunded = asNumber(payment.refunded);
  const failure = payment.failureReason
    ? knownFailures.has(payment.failureReason)
      ? t(`receipt.failure.${payment.failureReason}`)
      : payment.failureReason
    : null;

  return (
    <div className="flex flex-col gap-6">
      <section className={`${cardClass} flex flex-col items-center gap-3 text-center`}>
        <PaymentStatusBadge status={payment.status} />
        <p className="text-sm text-deep/60">
          {paid !== null ? t('receipt.amountPaid') : t('receipt.amountDue')}
        </p>
        <p className="font-display text-4xl font-bold text-hill">{money(paid ?? payment.total)}</p>
        <p className="text-sm text-deep/70">
          <PaymentMethod
            type={payment.methodType}
            name={payment.methodName}
            last4={payment.accountLast4}
          />
        </p>
        {failure && (
          <p role="note" className="rounded-xl bg-jamdani/10 px-4 py-2 text-sm text-jamdani">
            {failure}
          </p>
        )}
      </section>

      <div className="grid gap-6 md:grid-cols-2">
        <ReceiptSection title={t('receipt.sections.transaction')}>
          <Row label={t('receipt.fields.transactionId')}>
            <Reference value={payment.transactionRef} label={t('receipt.fields.transactionId')} />
          </Row>
          <Row label={t('receipt.fields.gatewayTxnId')}>
            {payment.providerTxnId ? (
              <Reference value={payment.providerTxnId} label={t('receipt.fields.gatewayTxnId')} />
            ) : (
              '—'
            )}
          </Row>
          <Row label={t('receipt.fields.validationId')}>
            <span className="break-all font-mono text-sm">{payment.validationId ?? '—'}</span>
          </Row>
          <Row label={t('receipt.fields.gateway')}>
            {t(`receipt.gateway.${payment.provider}`, { defaultValue: payment.provider })}
          </Row>
          <Row label={t('receipt.fields.status')}>
            <PaymentStatusBadge status={payment.status} />
          </Row>
        </ReceiptSection>

        <ReceiptSection title={t('receipt.sections.method')}>
          <Row label={t('receipt.fields.method')}>
            <PaymentMethod
              type={payment.methodType}
              name={payment.methodName}
              last4={payment.accountLast4}
            />
          </Row>
          <Row label={t('receipt.fields.methodType')}>
            {payment.methodType ? t(`paymentHistory.method.${payment.methodType}`) : '—'}
          </Row>
          <Row label={t('receipt.fields.issuer')}>{payment.issuer ?? '—'}</Row>
          <Row label={t('receipt.fields.currency')}>{payment.currency}</Row>
        </ReceiptSection>

        <ReceiptSection title={t('receipt.sections.amounts')}>
          <Row label={t('receipt.fields.price')}>{money(payment.amount)}</Row>
          <Row label={t('receipt.fields.fee')}>{money(payment.fee)}</Row>
          <Row label={t('receipt.fields.total')} strong>
            {money(payment.total)}
          </Row>
          <Row label={t('receipt.fields.paid')}>{paid !== null ? money(paid) : '—'}</Row>
          <Row label={t('receipt.fields.refunded')}>{money(refunded)}</Row>
          <Row label={t('receipt.fields.net')} strong>
            {money(payment.netPaid ?? 0)}
          </Row>
        </ReceiptSection>

        <ReceiptSection title={t('receipt.sections.dates')}>
          <Row label={t('receipt.fields.started')}>{when(payment.created)}</Row>
          <Row label={t('receipt.fields.completed')}>{when(payment.completedOn)}</Row>
          <Row label={t('receipt.fields.paidAtGateway')}>{when(payment.gatewayPaidOn)}</Row>
          <p className="pt-1 text-xs text-deep/50">{t('receipt.dhakaTime')}</p>
        </ReceiptSection>
      </div>

      <ReceiptSection title={t('receipt.sections.booking')}>
        <Row label={t('receipt.fields.trip')}>
          <Link
            to={tripLink}
            className="font-semibold text-hill underline-offset-2 hover:underline"
          >
            {payment.tripTitle}
          </Link>
        </Row>
        <Row label={t('receipt.fields.dates')}>
          {formatDateRange(payment.startDate, payment.endDate, language)}
        </Row>
        <Row label={t('receipt.fields.host')}>{payment.hostName ?? '—'}</Row>
        <Row label={t('receipt.fields.booking')}>
          {t('receipt.bookingNumber', { id: asNumber(payment.bookingId) })} ·{' '}
          {t(`bookings.status.${payment.bookingStatus}`)}
        </Row>
      </ReceiptSection>

      <ReceiptSection title={t('receipt.sections.refunds')}>
        {payment.refunds.length === 0 ? (
          <p className="text-sm text-deep/60">{t('receipt.noRefunds')}</p>
        ) : (
          <ul className="flex flex-col gap-3">
            {payment.refunds.map((refund) => (
              <li key={String(refund.id)} className="rounded-xl bg-mist p-4 text-sm">
                <p className="flex flex-wrap items-center justify-between gap-2">
                  <span className="font-semibold text-deep">{money(refund.amount)}</span>
                  <span
                    className={`rounded-full px-2.5 py-0.5 text-xs font-semibold ${
                      refund.status === 'Succeeded'
                        ? 'bg-emerald-50 text-emerald-800'
                        : 'bg-turmeric/15 text-ochre'
                    }`}
                  >
                    {t(`refunds.status.${refund.status}`)}
                  </span>
                </p>
                <p className="mt-1 text-deep/70">
                  {t(`refunds.reason.${refund.reason}`)} · {when(refund.created)}
                  {refund.completedOn ? ` → ${when(refund.completedOn)}` : ''}
                </p>
                {refund.providerRefundRef && (
                  <p className="mt-1 break-all font-mono text-xs text-deep/60">
                    {t('receipt.fields.refundRef')}: {refund.providerRefundRef}
                  </p>
                )}
                {asNumber(refund.shortfall) > 0 && (
                  <p className="mt-1 text-ochre">
                    {t('receipt.shortfall', { amount: money(refund.shortfall) })}
                  </p>
                )}
              </li>
            ))}
          </ul>
        )}
      </ReceiptSection>
    </div>
  );
}

export function ReceiptSection({ title, children }: { title: string; children: ReactNode }) {
  return (
    <section className={`${cardClass} break-inside-avoid`}>
      <h2 className="mb-3 text-base font-semibold text-deep">{title}</h2>
      <dl className="flex flex-col divide-y divide-hill/10">{children}</dl>
    </section>
  );
}

export function Row({
  label,
  children,
  strong = false,
}: {
  label: string;
  children: ReactNode;
  strong?: boolean;
}) {
  return (
    <div className="flex flex-wrap items-center justify-between gap-x-4 gap-y-1 py-2.5 text-sm">
      <dt className="text-deep/60">{label}</dt>
      <dd className={`min-w-0 text-right ${strong ? 'font-bold text-deep' : 'text-deep'}`}>
        {children}
      </dd>
    </div>
  );
}

function Reference({ value, label }: { value: string; label: string }) {
  return (
    <span className="inline-flex max-w-full items-center gap-1">
      <span className="break-all font-mono text-sm">{value}</span>
      <CopyButton value={value} label={label} />
    </span>
  );
}
