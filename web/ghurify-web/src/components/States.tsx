import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { Luggage } from 'lucide-react';

/** Placeholder cards while a grid loads, so the layout does not jump when data arrives. */
export function CardSkeletons({
  count = 3,
  tall = false,
  itemClassName = '',
}: {
  count?: number;
  tall?: boolean;
  /** Sizing for each placeholder when the parent is not a grid (a swipeable row). */
  itemClassName?: string;
}) {
  const { t } = useTranslation();

  return (
    <div role="status" className="contents">
      <span className="sr-only">{t('common.loading')}</span>
      {Array.from({ length: count }, (_, index) => (
        <div
          key={index}
          aria-hidden="true"
          className={`animate-pulse rounded-2xl bg-hill/10 ${tall ? 'aspect-4/5 min-h-80' : 'h-56'} ${itemClassName}`}
        />
      ))}
    </div>
  );
}

/** A whole screen on its way: one loaded on demand (the map page) arriving over the network. */
export function PageLoading() {
  const { t } = useTranslation();

  return (
    <div
      role="status"
      className="container-page flex min-h-[60vh] items-center justify-center py-24"
    >
      <span
        aria-hidden="true"
        className="h-10 w-10 animate-spin rounded-full border-4 border-hill/15 border-t-hill"
      />
      <span className="sr-only">{t('common.loading')}</span>
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
    <div className="col-span-full flex flex-col items-center gap-3 rounded-2xl border-2 border-dashed border-hill/15 bg-white px-6 py-14 text-center">
      <span
        className="flex h-16 w-16 items-center justify-center rounded-full bg-mist text-hill"
        aria-hidden="true"
      >
        <Luggage className="h-8 w-8" />
      </span>
      <p className="font-display text-lg font-semibold text-deep">{title}</p>
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
