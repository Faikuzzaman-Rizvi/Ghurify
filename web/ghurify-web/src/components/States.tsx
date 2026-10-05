import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';

/** Placeholder cards while a grid loads, so the layout does not jump when data arrives. */
export function CardSkeletons({ count = 3, tall = false }: { count?: number; tall?: boolean }) {
  const { t } = useTranslation();

  return (
    <div role="status" className="contents">
      <span className="sr-only">{t('common.loading')}</span>
      {Array.from({ length: count }, (_, index) => (
        <div
          key={index}
          aria-hidden="true"
          className={`animate-pulse rounded-3xl bg-hill/10 ${tall ? 'h-80' : 'h-56'}`}
        />
      ))}
    </div>
  );
}

/** Nothing to show, with a way forward. */
export function EmptyState({
  title,
  hint,
  action,
}: {
  title: string;
  hint?: string;
  action?: ReactNode;
}) {
  return (
    <div className="col-span-full flex flex-col items-center gap-2 rounded-3xl border-2 border-dashed border-hill/20 bg-white/60 px-6 py-12 text-center">
      <span className="text-4xl" aria-hidden="true">
        🧳
      </span>
      <p className="font-display text-lg font-bold text-deep">{title}</p>
      {hint && <p className="max-w-md text-sm text-deep/70">{hint}</p>}
      {action}
    </div>
  );
}

/** A failed load, with a retry. */
export function ErrorState({ message, onRetry }: { message: string; onRetry: () => void }) {
  const { t } = useTranslation();

  return (
    <div
      role="alert"
      className="col-span-full flex flex-col items-center gap-3 rounded-3xl bg-jamdani/5 px-6 py-10 text-center ring-1 ring-jamdani/20"
    >
      <p className="font-medium text-jamdani">{message}</p>
      <button
        type="button"
        onClick={onRetry}
        className="rounded-full bg-hill px-4 py-2 text-sm font-medium text-white transition hover:bg-deep"
      >
        {t('common.retry')}
      </button>
    </div>
  );
}
