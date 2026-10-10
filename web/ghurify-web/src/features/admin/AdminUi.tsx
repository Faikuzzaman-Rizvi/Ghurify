import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { ChevronLeft, ChevronRight, Search, type LucideIcon } from 'lucide-react';

import { inputClass } from '@/components/Field';
import { formatCount, toLanguage } from '@/lib/format';
import { tonePill, toneDot, toneTile, type Tone } from './adminStyles';

/**
 * The head of every admin section: a small eyebrow, the title (each page's h2; the portal is the
 * h1), an optional line saying what the section is for, and the section's main actions on the
 * right. One shape everywhere, so moving between sections never moves the controls.
 */
export function AdminPageHeader({
  eyebrow,
  title,
  description,
  actions,
}: {
  eyebrow?: string | undefined;
  title: string;
  description?: ReactNode;
  actions?: ReactNode;
}) {
  return (
    <header className="flex flex-wrap items-end justify-between gap-x-6 gap-y-4">
      <div className="min-w-0 max-w-3xl">
        {eyebrow && (
          <p className="mb-1.5 text-xs font-bold uppercase tracking-[0.18em] text-ochre [:lang(bn)_&]:tracking-normal">
            {eyebrow}
          </p>
        )}
        <h2 className="font-display text-2xl font-semibold leading-tight text-deep sm:text-[1.75rem]">
          {title}
        </h2>
        {description && (
          <p className="mt-1.5 text-sm leading-relaxed text-deep/65 sm:text-[0.95rem]">
            {description}
          </p>
        )}
      </div>
      {actions && <div className="flex flex-wrap items-center gap-2">{actions}</div>}
    </header>
  );
}

/** One figure in a row of summary tiles above a section. */
export interface SummaryItem {
  label: string;
  value: ReactNode;
  icon: LucideIcon;
  tone?: Tone;
}

/** A row of small figures: how much there is, and how much of it needs somebody. */
export function SummaryTiles({ items }: { items: readonly SummaryItem[] }) {
  return (
    <dl className="grid grid-cols-2 gap-3 lg:grid-cols-[repeat(auto-fit,minmax(13rem,1fr))]">
      {items.map(({ label, value, icon: Icon, tone = 'neutral' }) => (
        <div
          key={label}
          className="flex items-center gap-3 rounded-2xl bg-white/85 p-4 shadow-[0_4px_18px_rgba(15,42,31,0.05)] ring-1 ring-hill/10 backdrop-blur-sm"
        >
          <span
            aria-hidden="true"
            className={`flex h-11 w-11 shrink-0 items-center justify-center rounded-xl ${toneTile[tone]}`}
          >
            <Icon className="h-5 w-5" />
          </span>
          <div className="flex min-w-0 flex-col-reverse">
            <dt className="text-xs leading-snug text-deep/60 sm:text-sm">{label}</dt>
            <dd className="font-display text-xl font-semibold leading-tight text-deep">{value}</dd>
          </div>
        </div>
      ))}
    </dl>
  );
}

/** One choice in a row of filter chips. */
export interface FilterOption<T extends string> {
  value: T;
  label: string;
  count?: number | undefined;
  tone?: Tone | undefined;
}

/**
 * A row of filter chips with counts. Toggle buttons rather than radios: each says whether it is
 * on, and the row is labelled as one group for screen readers.
 */
export function FilterChips<T extends string>({
  label,
  options,
  value,
  onChange,
}: {
  label: string;
  options: readonly FilterOption<T>[];
  value: T;
  onChange: (value: T) => void;
}) {
  return (
    <div role="group" aria-label={label} className="flex flex-wrap gap-2">
      {options.map((option) => (
        <ToggleChip
          key={option.value}
          pressed={option.value === value}
          onClick={() => onChange(option.value)}
          tone={option.tone}
          count={option.count}
        >
          {option.label}
        </ToggleChip>
      ))}
    </div>
  );
}

/** One chip that is on or off: a filter choice, or a switch such as "not checked only". */
export function ToggleChip({
  pressed,
  onClick,
  tone,
  count,
  children,
}: {
  pressed: boolean;
  onClick: () => void;
  tone?: Tone | undefined;
  count?: number | undefined;
  children: ReactNode;
}) {
  const { i18n } = useTranslation();

  return (
    <button
      type="button"
      aria-pressed={pressed}
      onClick={onClick}
      className={`inline-flex items-center gap-2 rounded-full px-4 py-2 text-sm font-semibold transition focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-turmeric focus-visible:ring-offset-2 ${
        pressed
          ? 'bg-deep text-white shadow-[0_6px_16px_rgba(15,42,31,0.2)]'
          : 'bg-white text-deep/75 ring-1 ring-hill/12 hover:text-deep hover:ring-hill/30'
      }`}
    >
      {tone && <span aria-hidden="true" className={`h-2 w-2 rounded-full ${toneDot[tone]}`} />}
      {children}
      {count !== undefined && (
        <span
          className={`min-w-6 rounded-full px-1.5 py-0.5 text-center text-xs tabular-nums ${
            pressed ? 'bg-white/15 text-white' : 'bg-mist text-deep/70'
          }`}
        >
          {formatCount(count, toLanguage(i18n.language))}
        </span>
      )}
    </button>
  );
}

/** A small rounded status label, with a dot or icon so the colour is never the only signal. */
export function StatusPill({
  tone,
  children,
  icon: Icon,
  onPhoto = false,
  className = '',
}: {
  tone: Tone;
  children: ReactNode;
  icon?: LucideIcon | undefined;
  /** Over a photo the tinted ground would let the picture through, so it turns solid white. */
  onPhoto?: boolean;
  className?: string;
}) {
  return (
    <span
      className={`inline-flex items-center gap-1.5 rounded-full px-2.5 py-1 text-xs font-semibold ring-1 ${tonePill[tone]} ${
        onPhoto ? 'bg-white! shadow-sm ring-white/60!' : ''
      } ${className}`}
    >
      {Icon ? (
        <Icon aria-hidden="true" className="h-3.5 w-3.5" />
      ) : (
        <span aria-hidden="true" className={`h-1.5 w-1.5 rounded-full ${toneDot[tone]}`} />
      )}
      {children}
    </span>
  );
}

/** Placeholder cards while a grid loads, in the grid's own shape. */
export function CardGridSkeleton({
  count = 6,
  className = 'h-72',
}: {
  count?: number;
  className?: string;
}) {
  const { t } = useTranslation();

  return (
    <div role="status" className="grid gap-5 sm:grid-cols-2 xl:grid-cols-3">
      <span className="sr-only">{t('common.loading')}</span>
      {Array.from({ length: count }, (_, index) => (
        <div
          key={index}
          aria-hidden="true"
          className={`animate-pulse rounded-3xl bg-hill/8 ${className}`}
        />
      ))}
    </div>
  );
}

/**
 * The search bar above a list: a text box with a magnifier, any filters passed as children, and
 * the search button, on one raised strip. Searching only on submit keeps every keystroke from
 * becoming a request.
 */
export function AdminSearchBar({
  id,
  label,
  placeholder,
  value,
  onChange,
  onSubmit,
  submitLabel,
  children,
}: {
  id: string;
  label: string;
  placeholder: string;
  value: string;
  onChange: (value: string) => void;
  onSubmit: () => void;
  submitLabel: string;
  children?: ReactNode;
}) {
  return (
    // Raised above what follows: the frosted glass makes the bar a layer of its own, and an
    // open status list inside it would otherwise be painted under the cards below.
    <form
      role="search"
      className="relative z-20 flex flex-col gap-2 rounded-3xl bg-white/85 p-2 shadow-[0_4px_18px_rgba(15,42,31,0.05)] ring-1 ring-hill/10 backdrop-blur-sm sm:flex-row"
      onSubmit={(event) => {
        event.preventDefault();
        onSubmit();
      }}
    >
      <label htmlFor={id} className="sr-only">
        {label}
      </label>
      <div className="relative min-w-0 flex-1">
        <Search
          aria-hidden="true"
          className="pointer-events-none absolute left-4 top-1/2 h-4.5 w-4.5 -translate-y-1/2 text-deep/40"
        />
        <input
          id={id}
          type="search"
          value={value}
          placeholder={placeholder}
          onChange={(event) => onChange(event.target.value)}
          className={`${inputClass} border-transparent bg-mist/60 pl-11 shadow-none hover:border-hill/20`}
        />
      </div>
      {children}
      <button
        type="submit"
        className="inline-flex items-center justify-center gap-2 rounded-full bg-hill px-6 py-3 font-semibold text-white shadow-sm transition hover:bg-deep focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-turmeric focus-visible:ring-offset-2"
      >
        <Search aria-hidden="true" className="h-4 w-4" />
        {submitLabel}
      </button>
    </form>
  );
}

/** Previous and next under a paged list, with where in the list this page is. */
export function AdminPager({
  page,
  pageSize,
  total,
  onPage,
}: {
  page: number;
  pageSize: number;
  total: number;
  onPage: (page: number) => void;
}) {
  const { t } = useTranslation();
  if (total <= pageSize) return null;

  const first = (page - 1) * pageSize + 1;
  const last = Math.min(total, page * pageSize);
  const pagerButton =
    'inline-flex items-center gap-1.5 rounded-full border border-hill/15 bg-white px-4 py-2 text-sm font-semibold text-deep transition hover:bg-mist disabled:pointer-events-none disabled:opacity-40';

  return (
    <nav
      className="flex flex-wrap items-center justify-between gap-3"
      aria-label={t('common.pagination')}
    >
      <button
        type="button"
        disabled={page <= 1}
        onClick={() => onPage(page - 1)}
        className={pagerButton}
      >
        <ChevronLeft aria-hidden="true" className="h-4 w-4" />
        {t('common.previous')}
      </button>
      <p className="text-sm text-deep/60">{t('admin.ui.showing', { first, last, total })}</p>
      <button
        type="button"
        disabled={page * pageSize >= total}
        onClick={() => onPage(page + 1)}
        className={pagerButton}
      >
        {t('common.next')}
        <ChevronRight aria-hidden="true" className="h-4 w-4" />
      </button>
    </nav>
  );
}

/** Placeholder rows while a list panel loads. */
export function ListSkeleton({ rows = 5 }: { rows?: number }) {
  const { t } = useTranslation();

  return (
    <div role="status" className="flex flex-col gap-2">
      <span className="sr-only">{t('common.loading')}</span>
      {Array.from({ length: rows }, (_, index) => (
        <div key={index} aria-hidden="true" className="h-20 animate-pulse rounded-2xl bg-hill/8" />
      ))}
    </div>
  );
}
