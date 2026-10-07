import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';

import { apiGet, apiPost } from '@/api/client';
import type { components } from '@/api/schema';
import { Dialog } from '@/components/Dialog';
import { dangerButtonClass, secondaryButtonClass } from '@/components/Field';
import { errorText } from '@/lib/errors';
import { formatCount, formatMoney, toLanguage } from '@/lib/format';

type CancellationQuote = components['schemas']['CancellationQuote'];

/**
 * Cancelling a paid seat: shows exactly what the refund rules give back today before the traveller
 * confirms, so nobody is surprised by the amount.
 */
export function CancelBookingDialog({
  bookingId,
  open,
  onClose,
}: {
  bookingId: number;
  open: boolean;
  onClose: () => void;
}) {
  const { t, i18n } = useTranslation();
  const language = toLanguage(i18n.language);
  const queryClient = useQueryClient();

  const quote = useQuery({
    queryKey: ['cancellation', bookingId],
    queryFn: ({ signal }) =>
      apiGet<CancellationQuote>(`/api/v1/bookings/${bookingId}/cancellation`, { signal }),
    enabled: open,
  });

  const cancel = useMutation({
    mutationFn: () => apiPost<CancellationQuote>(`/api/v1/bookings/${bookingId}/cancel`),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['me'] });
      onClose();
    },
  });

  return (
    <Dialog open={open} onClose={onClose} title={t('cancel.title')}>
      {quote.isPending && <p role="status">{t('common.loading')}</p>}
      {quote.isError && (
        <p role="alert" className="text-sm text-jamdani">
          {errorText(quote.error, t)}
        </p>
      )}
      {quote.data && (
        <div className="flex flex-col gap-3">
          <p className="text-sm text-deep/80">
            {t('cancel.days', {
              count: Number(quote.data.daysBeforeDeparture),
              n: formatCount(quote.data.daysBeforeDeparture, language),
            })}
          </p>
          <p className="rounded-2xl bg-mist p-3 text-deep">
            {t(`cancel.rule.${quote.data.rule}`, {
              refund: formatMoney(quote.data.refund, language),
            })}
          </p>
          {cancel.isError && (
            <p role="alert" className="text-sm text-jamdani">
              {errorText(cancel.error, t)}
            </p>
          )}
          <div className="flex justify-end gap-2">
            <button type="button" className={secondaryButtonClass} onClick={onClose}>
              {t('cancel.keep')}
            </button>
            <button
              type="button"
              className={dangerButtonClass}
              disabled={!quote.data.canCancel || cancel.isPending}
              onClick={() => cancel.mutate()}
            >
              {t('cancel.confirm')}
            </button>
          </div>
        </div>
      )}
    </Dialog>
  );
}
