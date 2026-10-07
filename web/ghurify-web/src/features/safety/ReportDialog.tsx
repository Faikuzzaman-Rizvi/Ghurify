import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { Dialog } from '@/components/Dialog';
import {
  primaryButtonClass,
  secondaryButtonClass,
  SelectField,
  TextAreaField,
} from '@/components/Field';
import { errorText } from '@/lib/errors';
import type { ReportKind, ReportReason } from './safetyApi';
import { useFileReport } from './useSafety';

const reasons: readonly ReportReason[] = [
  'Harassment',
  'Fraud',
  'Unsafe',
  'Inappropriate',
  'Payment',
  'Other',
];

/**
 * Report a person, a story or a trip, or (kind Dispute, target a booking) raise a dispute. Goes
 * to the moderators; the person reported is not told who reported them.
 */
export function ReportDialog({
  kind,
  targetId,
  open,
  onClose,
}: {
  kind: ReportKind;
  targetId: number;
  open: boolean;
  onClose: () => void;
}) {
  const { t } = useTranslation();
  const file = useFileReport();
  const [reason, setReason] = useState<ReportReason>(kind === 'Dispute' ? 'Payment' : 'Harassment');
  const [details, setDetails] = useState('');

  const close = () => {
    file.reset();
    setDetails('');
    onClose();
  };

  return (
    <Dialog
      open={open}
      title={kind === 'Dispute' ? t('report.disputeTitle') : t(`report.title.${kind}`)}
      onClose={close}
    >
      {file.isSuccess ? (
        <div className="flex flex-col gap-3">
          <p role="status" className="text-deep">
            {kind === 'Dispute' ? t('report.disputeSent') : t('report.sent')}
          </p>
          <button type="button" className={`${primaryButtonClass} self-end`} onClick={close}>
            {t('common.close')}
          </button>
        </div>
      ) : (
        <form
          className="flex flex-col gap-3"
          onSubmit={(event) => {
            event.preventDefault();
            file.mutate({ kind, targetId, reason, details: details.trim() || null });
          }}
        >
          <SelectField
            id={`report-reason-${kind}-${targetId}`}
            label={t('report.reason')}
            value={reason}
            onChange={(event) => setReason(event.target.value as ReportReason)}
          >
            {reasons.map((option) => (
              <option key={option} value={option}>
                {t(`admin.reports.reasons.${option}`)}
              </option>
            ))}
          </SelectField>
          <TextAreaField
            id={`report-details-${kind}-${targetId}`}
            label={t('report.details')}
            hint={kind === 'Dispute' ? t('report.disputeHint') : t('report.detailsHint')}
            value={details}
            maxLength={1000}
            onChange={(event) => setDetails(event.target.value)}
          />
          {file.isError && (
            <p role="alert" className="text-sm text-jamdani">
              {errorText(file.error, t)}
            </p>
          )}
          <div className="flex justify-end gap-2">
            <button type="button" className={secondaryButtonClass} onClick={close}>
              {t('common.cancel')}
            </button>
            <button type="submit" className={primaryButtonClass} disabled={file.isPending}>
              {file.isPending ? t('common.saving') : t('report.send')}
            </button>
          </div>
        </form>
      )}
    </Dialog>
  );
}
