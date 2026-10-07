import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  Building2,
  Check,
  Copy,
  CreditCard,
  Landmark,
  Smartphone,
  type LucideIcon,
} from 'lucide-react';

import type { PaymentMethodType, PaymentStatus } from './paymentsApi';

const statusTone: Record<PaymentStatus, string> = {
  Created: 'bg-mist text-deep/70',
  Pending: 'bg-turmeric/15 text-ochre',
  Succeeded: 'bg-emerald-50 text-emerald-800',
  Failed: 'bg-jamdani/10 text-jamdani',
  Expired: 'bg-mist text-deep/60',
};

/** Where a payment attempt stands, as a coloured pill. */
export function PaymentStatusBadge({ status }: { status: PaymentStatus | null }) {
  const { t } = useTranslation();

  // The API always sends a status; the generated type allows null only because the enum is shared.
  if (!status) return null;

  return (
    <span
      className={`inline-block rounded-full px-2.5 py-0.5 text-xs font-semibold ${statusTone[status]}`}
    >
      {t(`paymentHistory.status.${status}`)}
    </span>
  );
}

const methodIcon: Record<PaymentMethodType, LucideIcon> = {
  Card: CreditCard,
  MobileBanking: Smartphone,
  InternetBanking: Landmark,
  Other: Building2,
};

/**
 * How it was paid: "bKash •••• 7788". Before a payment succeeds the gateway has not said, so it
 * reads "Not paid".
 */
export function PaymentMethod({
  type,
  name,
  last4,
}: {
  type: PaymentMethodType | null;
  name: string | null;
  last4: string | null;
}) {
  const { t } = useTranslation();

  if (!type) {
    return <span className="text-deep/50">{t('paymentHistory.method.none')}</span>;
  }

  const Icon = methodIcon[type];
  return (
    <span className="inline-flex items-center gap-1.5">
      <Icon aria-hidden="true" className="h-4 w-4 shrink-0 text-hill" />
      <span>{name ?? t(`paymentHistory.method.${type}`)}</span>
      {last4 && (
        <span className="font-mono text-deep/60">
          <span className="sr-only">{t('paymentHistory.endingIn')}</span>
          <span aria-hidden="true">•••• </span>
          {last4}
        </span>
      )}
    </span>
  );
}

/**
 * Copies a reference (a transaction ID to quote to support or a bank) and says so for a moment.
 * Where the clipboard is unavailable the reference is still on screen to select by hand.
 */
export function CopyButton({ value, label }: { value: string; label: string }) {
  const { t } = useTranslation();
  const [copied, setCopied] = useState(false);

  useEffect(() => {
    if (!copied) return;
    const timer = window.setTimeout(() => setCopied(false), 2000);
    return () => window.clearTimeout(timer);
  }, [copied]);

  return (
    <button
      type="button"
      onClick={() => {
        void navigator.clipboard
          ?.writeText(value)
          .then(() => setCopied(true))
          .catch(() => undefined);
      }}
      className="inline-flex shrink-0 items-center gap-1 rounded-full px-2 py-1 text-xs font-semibold text-hill transition hover:bg-hill/10 print:hidden"
      aria-label={t('paymentHistory.copyLabel', { label })}
    >
      {copied ? (
        <Check aria-hidden="true" className="h-3.5 w-3.5" />
      ) : (
        <Copy aria-hidden="true" className="h-3.5 w-3.5" />
      )}
      <span aria-live="polite">
        {copied ? t('paymentHistory.copied') : t('paymentHistory.copy')}
      </span>
    </button>
  );
}
