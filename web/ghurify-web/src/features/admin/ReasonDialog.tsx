import { useState, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';

import { Dialog } from '@/components/Dialog';
import {
  dangerButtonClass,
  primaryButtonClass,
  secondaryButtonClass,
  TextAreaField,
} from '@/components/Field';
import { errorText } from '@/lib/errors';

/**
 * Asks why before an admin action runs. Every admin change is audited with this reason, so the
 * confirm button stays disabled until one is written.
 */
export function ReasonDialog({
  open,
  title,
  description,
  confirmLabel,
  danger = false,
  pending,
  error,
  onConfirm,
  onClose,
}: {
  open: boolean;
  title: string;
  description?: ReactNode;
  confirmLabel: string;
  danger?: boolean;
  pending: boolean;
  error: unknown;
  onConfirm: (reason: string) => void;
  onClose: () => void;
}) {
  const { t } = useTranslation();
  const [reason, setReason] = useState('');

  return (
    <Dialog
      open={open}
      title={title}
      onClose={() => {
        setReason('');
        onClose();
      }}
    >
      <form
        className="flex flex-col gap-3"
        onSubmit={(event) => {
          event.preventDefault();
          onConfirm(reason.trim());
        }}
      >
        {description && <div className="text-sm text-deep/80">{description}</div>}
        <TextAreaField
          id="admin-reason"
          label={t('admin.reason.label')}
          hint={t('admin.reason.hint')}
          value={reason}
          maxLength={300}
          rows={3}
          onChange={(event) => setReason(event.target.value)}
        />
        {error ? (
          <p role="alert" className="text-sm text-jamdani">
            {errorText(error, t)}
          </p>
        ) : null}
        <div className="flex justify-end gap-2">
          <button type="button" className={secondaryButtonClass} onClick={onClose}>
            {t('common.cancel')}
          </button>
          <button
            type="submit"
            className={danger ? dangerButtonClass : primaryButtonClass}
            disabled={pending || reason.trim() === ''}
          >
            {pending ? t('common.saving') : confirmLabel}
          </button>
        </div>
      </form>
    </Dialog>
  );
}
