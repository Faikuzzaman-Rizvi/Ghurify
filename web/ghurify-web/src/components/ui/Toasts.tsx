import { useTranslation } from 'react-i18next';
import { CircleAlert, CircleCheck, X } from 'lucide-react';

import { useToastStore } from './toastStore';

/**
 * Where the short confirmations appear: bottom of the screen, above everything, out of the
 * way of the page's own content. Announced politely, so a screen reader hears the
 * confirmation without losing the reader's place, and each one can be dismissed early.
 */
export function Toasts() {
  const { t } = useTranslation();
  const toasts = useToastStore((state) => state.toasts);
  const dismiss = useToastStore((state) => state.dismiss);

  return (
    <div
      role="status"
      aria-live="polite"
      className="pointer-events-none fixed inset-x-0 bottom-4 z-[70] flex flex-col items-center gap-2 px-4 print:hidden"
    >
      {toasts.map((toast) => (
        <div
          key={toast.id}
          className={`pointer-events-auto flex max-w-[min(28rem,100%)] animate-rise items-start gap-3 rounded-2xl px-4 py-3 text-sm font-medium shadow-[0_18px_50px_rgba(15,42,31,0.28)] ${
            toast.tone === 'error' ? 'bg-jamdani text-white' : 'bg-deep text-white'
          }`}
        >
          {toast.tone === 'error' ? (
            <CircleAlert aria-hidden="true" className="mt-0.5 h-4.5 w-4.5 shrink-0 text-white" />
          ) : (
            <CircleCheck aria-hidden="true" className="mt-0.5 h-4.5 w-4.5 shrink-0 text-dusk" />
          )}
          <p className="min-w-0 flex-1">{toast.message}</p>
          <button
            type="button"
            onClick={() => dismiss(toast.id)}
            aria-label={t('common.close')}
            className="-mr-1 shrink-0 rounded-full p-1 text-white/70 transition hover:bg-white/15 hover:text-white"
          >
            <X aria-hidden="true" className="h-4 w-4" />
          </button>
        </div>
      ))}
    </div>
  );
}
