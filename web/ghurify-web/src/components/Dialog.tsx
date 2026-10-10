import { useEffect, useId, useRef, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { X } from 'lucide-react';

/**
 * A modal built on the native &lt;dialog&gt; element, which traps focus and closes on Escape by
 * itself. `open` is controlled by the caller.
 */
export function Dialog({
  open,
  title,
  onClose,
  children,
}: {
  open: boolean;
  title: string;
  onClose: () => void;
  children: ReactNode;
}) {
  const { t } = useTranslation();
  const ref = useRef<HTMLDialogElement>(null);
  const titleId = useId();

  useEffect(() => {
    const dialog = ref.current;
    if (!dialog) return;

    // jsdom has no showModal; fall back to the open attribute there.
    if (open && !dialog.open) {
      if (typeof dialog.showModal === 'function') dialog.showModal();
      else dialog.setAttribute('open', '');
    } else if (!open && dialog.open) {
      if (typeof dialog.close === 'function') dialog.close();
      else dialog.removeAttribute('open');
    }
  }, [open]);

  return (
    <dialog
      ref={ref}
      aria-labelledby={titleId}
      onClose={onClose}
      onCancel={onClose}
      className="m-auto flex max-h-[min(44rem,calc(100dvh-3rem))] w-[min(32rem,calc(100vw-2rem))] flex-col overflow-hidden rounded-2xl bg-white p-0 shadow-2xl backdrop:bg-night/60 backdrop:backdrop-blur-sm"
    >
      {open && (
        <>
          <div className="flex shrink-0 items-start justify-between gap-4 px-6 pt-6">
            <h2 id={titleId} className="text-xl font-bold text-deep">
              {title}
            </h2>
            <button
              type="button"
              onClick={onClose}
              aria-label={t('common.close')}
              className="rounded-full p-1.5 text-deep/60 transition hover:bg-mist hover:text-deep"
            >
              <X aria-hidden="true" className="h-5 w-5" />
            </button>
          </div>
          {/* Only this part scrolls, and only when the form is genuinely taller than the
              window: the dialog itself no longer grows a scrollbar for a popover inside it. */}
          <div className="min-h-0 flex-1 overflow-y-auto overscroll-contain px-6 pb-6 pt-4">
            {children}
          </div>
        </>
      )}
    </dialog>
  );
}
