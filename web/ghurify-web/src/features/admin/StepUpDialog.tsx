import { useState, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { ShieldCheck } from 'lucide-react';

import { Dialog } from '@/components/Dialog';
import { inputClass } from '@/components/Field';
import { errorText } from '@/lib/errors';
import { adminApi } from './adminApi';
import { setStepUpReceipt } from './stepUp';

/**
 * Asks for the signed-in admin's own password, stores the receipt, and releases the action that
 * was waiting on it. Opened by <see cref="useStepUp" />, never rendered directly.
 */
export function StepUpDialog({
  onCancel,
  onConfirmed,
}: {
  onCancel: () => void;
  onConfirmed: () => Promise<void>;
}) {
  const { t } = useTranslation();
  const [password, setPassword] = useState('');
  const [busy, setBusy] = useState(false);
  const [problem, setProblem] = useState<ReactNode>(null);

  async function submit() {
    setBusy(true);
    setProblem(null);

    try {
      const receipt = await adminApi.stepUp(password);
      setStepUpReceipt(receipt.token, receipt.expiresOn);
      setPassword('');
      await onConfirmed();
    } catch (error) {
      setProblem(errorText(error, t));
    } finally {
      setBusy(false);
    }
  }

  return (
    <Dialog open title={t('admin.stepUp.title')} onClose={onCancel}>
      <form
        className="flex flex-col gap-4"
        onSubmit={(event) => {
          event.preventDefault();
          void submit();
        }}
      >
        <p className="flex gap-2 text-sm text-deep/70">
          <ShieldCheck aria-hidden="true" className="mt-0.5 h-5 w-5 shrink-0 text-hill" />
          {t('admin.stepUp.lead')}
        </p>

        <div className="flex flex-col gap-1.5">
          <label htmlFor="step-up-password" className="text-sm font-semibold text-deep">
            {t('admin.stepUp.password')}
          </label>
          <input
            id="step-up-password"
            type="password"
            autoComplete="current-password"
            // The dialog has one job and opens in answer to a click, so the field it exists for
            // takes the focus.
            autoFocus
            required
            value={password}
            onChange={(event) => setPassword(event.target.value)}
            className={inputClass}
          />
        </div>

        {problem && (
          <p role="alert" className="text-sm font-medium text-jamdani">
            {problem}
          </p>
        )}

        <div className="flex flex-wrap justify-end gap-2">
          <button
            type="button"
            onClick={onCancel}
            className="rounded-full px-5 py-2.5 font-semibold text-deep/70 transition hover:bg-mist hover:text-deep"
          >
            {t('common.cancel')}
          </button>
          <button
            type="submit"
            disabled={busy || password.length === 0}
            className="rounded-full bg-hill px-6 py-2.5 font-semibold text-white shadow-sm transition hover:bg-deep disabled:opacity-60"
          >
            {busy ? t('common.saving') : t('admin.stepUp.confirm')}
          </button>
        </div>
      </form>
    </Dialog>
  );
}
