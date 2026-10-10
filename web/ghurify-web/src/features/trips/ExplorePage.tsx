import { useCallback, useEffect, useRef, useState, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { useSearchParams } from 'react-router';
import { SlidersHorizontal, X } from 'lucide-react';
import { asNumber } from '@/api/client';
import { CardSkeletons, EmptyState, ErrorState } from '@/components/States';
import { PageBanner } from '@/components/ui/PageBanner';
import { Pagination } from '@/components/ui/Pagination';
import { DatePicker } from '@/components/ui/DatePicker';
import { Select } from '@/components/ui/Select';
import { formatCount, formatMoney, todayInDhaka, toLanguage } from '@/lib/format';
import { TripCard } from './TripCard';
import {
  groupTypes,
  tripSorts,
  type DestinationKind,
  type GroupType,
  type TripFilters,
  type TripSort,
} from './tripsApi';
import { useDestinations, useTripSearch } from './useTrips';

const pageSize = 12;
const budgets = [5000, 7500, 10000, 15000];
const filterKeys = ['destination', 'groupType', 'maxPrice', 'from', 'verifiedHostsOnly'] as const;

/**
 * Every live trip, filtered. The query string is the source of truth, so a filtered search
 * can be bookmarked, shared, and survives a reload. Filters sit in a sidebar on wide screens
 * and in a bottom sheet on phones; it is the same panel either way.
 */
export function ExplorePage() {
  const { t, i18n } = useTranslation();
  const language = toLanguage(i18n.language);
  const [params, setParams] = useSearchParams();
  const { data: destinations } = useDestinations();
  const [sheetOpen, setSheetOpen] = useState(false);
  const closeSheet = useCallback(() => setSheetOpen(false), []);

  const filters = readFilters(params);
  const { data, isPending, isError, isFetching, refetch } = useTripSearch(filters);

  function update(key: keyof TripFilters, value: string) {
    const next = new URLSearchParams(params);
    if (value) {
      next.set(key, value);
    } else {
      next.delete(key);
    }
    // Any filter change starts again from the first page.
    if (key !== 'page') {
      next.delete('page');
    }
    setParams(next);
  }

  function clearFilters() {
    const next = new URLSearchParams();
    const sort = params.get('sort');
    if (sort) next.set('sort', sort);
    setParams(next);
  }

  const total = data ? asNumber(data.totalCount) : 0;
  const page = filters.page ?? 1;
  const pages = Math.max(1, Math.ceil(total / pageSize));
  const activeCount = filterKeys.filter((key) => filters[key] !== undefined).length;

  // The banner shows the chosen destination; with none chosen, a river scene.
  const chosen = destinations?.find((destination) => destination.slug === filters.destination);
  const banner: { slug: string; kind: DestinationKind } = chosen
    ? { slug: chosen.slug, kind: chosen.kind }
    : { slug: 'sylhet', kind: 'River' };

  return (
    <>
      <PageBanner
        slug={banner.slug}
        kind={banner.kind}
        eyebrow={t('trips.exploreEyebrow')}
        titleKey="trips.exploreTitle"
      >
        <p className="mt-4 max-w-2xl text-white/85 sm:text-lg">{t('trips.exploreSubtitle')}</p>
      </PageBanner>

      <div className="container-page grid gap-8 py-12 lg:grid-cols-[18.5rem_1fr] lg:gap-10 lg:py-16">
        <FilterPanel
          open={sheetOpen}
          onClose={closeSheet}
          total={data ? total : undefined}
          activeCount={activeCount}
          onClear={clearFilters}
        >
          <div className="flex flex-col gap-2">
            <label htmlFor="filter-destination" className="text-sm font-semibold text-deep">
              {t('home.destinationLabel')}
            </label>
            <Select
              id="filter-destination"
              value={filters.destination ?? ''}
              onChange={(value) => update('destination', value)}
              options={[
                { value: '', label: t('home.anyDestination') },
                ...(destinations ?? []).map((destination) => ({
                  value: destination.slug,
                  label: language === 'bn' ? destination.nameBn : destination.name,
                  hint: language === 'bn' ? destination.divisionBn : destination.division,
                })),
              ]}
              buttonClassName="rounded-xl border border-hill/15 bg-white px-4 py-3 text-deep transition hover:border-hill/30 focus-visible:border-hill focus-visible:ring-4 focus-visible:ring-hill/10"
            />
          </div>

          <div className="flex flex-col gap-2">
            <label htmlFor="filter-from" className="text-sm font-semibold text-deep">
              {t('home.dateLabel')}
            </label>
            <DatePicker
              id="filter-from"
              min={todayInDhaka()}
              clearable
              placeholder={t('datePicker.anyDate')}
              value={filters.from ?? ''}
              onChange={(value) => update('from', value)}
              inputClassName="rounded-xl border border-hill/15 bg-white px-4 py-3 text-deep outline-none transition hover:border-hill/30 focus:border-hill focus:ring-4 focus:ring-hill/10"
            />
          </div>

          <ChipGroup
            legend={t('home.groupLabel')}
            name="groupType"
            value={filters.groupType ?? ''}
            onChange={(value) => update('groupType', value)}
            options={[
              { value: '', label: t('home.anyGroup') },
              ...groupTypes.map((groupType) => ({
                value: groupType,
                label: t(`groupType.${groupType}`),
              })),
            ]}
          />

          <ChipGroup
            legend={t('trips.maxPriceLabel')}
            name="maxPrice"
            value={filters.maxPrice ? String(filters.maxPrice) : ''}
            onChange={(value) => update('maxPrice', value)}
            options={[
              { value: '', label: t('trips.anyPrice') },
              ...budgets.map((budget) => ({
                value: String(budget),
                label: t('trips.upTo', { price: formatMoney(budget, language) }),
              })),
            ]}
          />

          <label className="flex cursor-pointer items-start gap-3 rounded-xl bg-white p-4 text-sm font-medium text-deep ring-1 ring-hill/10 transition hover:ring-hill/30">
            <input
              type="checkbox"
              className="mt-0.5 h-4 w-4 shrink-0 accent-hill"
              checked={filters.verifiedHostsOnly === true}
              onChange={(event) => update('verifiedHostsOnly', event.target.checked ? 'true' : '')}
            />
            {t('trips.verifiedHostsOnly')}
          </label>
        </FilterPanel>

        <section aria-labelledby="results-heading" className="min-w-0">
          <h2 id="results-heading" className="sr-only">
            {t('trips.resultsHeading')}
          </h2>

          <div className="flex flex-wrap items-center justify-between gap-3 rounded-2xl bg-mist px-4 py-3">
            <div className="flex items-center gap-3" aria-live="polite">
              <button
                type="button"
                onClick={() => setSheetOpen(true)}
                className="inline-flex items-center gap-2 rounded-full bg-white px-4 py-2 text-sm font-semibold text-deep shadow-sm ring-1 ring-hill/10 lg:hidden"
              >
                <SlidersHorizontal aria-hidden="true" className="h-4 w-4 text-hill" />
                {t('trips.filtersButton')}
                {activeCount > 0 && (
                  <span className="flex h-5 min-w-5 items-center justify-center rounded-full bg-hill px-1 text-xs text-white">
                    {formatCount(activeCount, language)}
                  </span>
                )}
              </button>
              <p className="text-sm font-semibold text-deep">
                {data && t('trips.results', { count: total, n: formatCount(total, language) })}
              </p>
            </div>

            <div className="flex items-center gap-2">
              <label htmlFor="filter-sort" className="text-sm text-deep/70">
                {t('trips.sortLabel')}
              </label>
              <Select
                id="filter-sort"
                align="right"
                value={filters.sort ?? 'Soonest'}
                onChange={(value) => update('sort', value === 'Soonest' ? '' : value)}
                options={tripSorts.map((sort) => ({ value: sort, label: t(`trips.sort.${sort}`) }))}
                buttonClassName="rounded-full border border-hill/15 bg-white py-2 pl-4 pr-3 text-sm font-semibold text-deep transition hover:border-hill/30 focus-visible:border-hill focus-visible:ring-4 focus-visible:ring-hill/10"
              />
            </div>
          </div>

          <div
            className={`mt-6 grid gap-6 transition-opacity sm:grid-cols-2 xl:grid-cols-3 ${
              isFetching && !isPending ? 'opacity-60' : ''
            }`}
          >
            {isPending ? (
              <CardSkeletons count={6} tall />
            ) : isError ? (
              <ErrorState message={t('trips.loadError')} onRetry={() => void refetch()} />
            ) : data.items.length === 0 ? (
              <EmptyState
                title={t('trips.empty')}
                hint={t('trips.emptyHint')}
                action={
                  activeCount > 0 ? (
                    <button
                      type="button"
                      onClick={clearFilters}
                      className="mt-2 rounded-full bg-hill px-5 py-2.5 text-sm font-semibold text-white transition hover:bg-deep"
                    >
                      {t('trips.clearFilters')}
                    </button>
                  ) : undefined
                }
              />
            ) : (
              data.items.map((trip) => <TripCard key={String(trip.id)} trip={trip} />)
            )}
          </div>

          <Pagination
            page={page}
            pages={pages}
            onChange={(next) => {
              update('page', next > 1 ? String(next) : '');
              window.scrollTo({ top: 0, behavior: 'smooth' });
            }}
            label={t('trips.pagination')}
          />
        </section>
      </div>
    </>
  );
}

/**
 * The filters. One element in the page: a sticky sidebar from `lg`, and below that a bottom
 * sheet opened from the toolbar (Escape or the backdrop closes it). Filters apply as they
 * change, so the sheet's main button just shows the results.
 */
function FilterPanel({
  open,
  onClose,
  total,
  activeCount,
  onClear,
  children,
}: {
  open: boolean;
  onClose: () => void;
  total: number | undefined;
  activeCount: number;
  onClear: () => void;
  children: ReactNode;
}) {
  const { t, i18n } = useTranslation();
  const language = toLanguage(i18n.language);
  const headingRef = useRef<HTMLHeadingElement>(null);

  // Focus moves into the sheet once, as it opens.
  useEffect(() => {
    if (open) headingRef.current?.focus();
  }, [open]);

  useEffect(() => {
    if (!open) return;
    const onKey = (event: KeyboardEvent) => {
      if (event.key === 'Escape') onClose();
    };
    document.addEventListener('keydown', onKey);
    // The page behind the sheet should not scroll with it.
    const overflow = document.body.style.overflow;
    document.body.style.overflow = 'hidden';
    return () => {
      document.removeEventListener('keydown', onKey);
      document.body.style.overflow = overflow;
    };
  }, [open, onClose]);

  return (
    <>
      {open && (
        <div
          aria-hidden="true"
          onClick={onClose}
          className="fixed inset-0 z-40 animate-fade-in bg-night/60 backdrop-blur-sm lg:hidden"
        />
      )}
      <aside
        aria-labelledby="filters-heading"
        className={`${
          open
            ? 'fixed inset-x-0 bottom-0 z-50 flex max-h-[88dvh] animate-rise flex-col rounded-t-3xl bg-white shadow-2xl'
            : 'hidden'
        } lg:sticky lg:top-24 lg:z-auto lg:flex lg:max-h-none lg:animate-none lg:flex-col lg:self-start lg:rounded-2xl lg:bg-mist lg:shadow-none`}
      >
        <div className="flex items-center justify-between gap-3 border-b border-hill/10 px-5 py-4 lg:rounded-t-2xl lg:border-0 lg:bg-hill lg:py-5">
          <h2
            id="filters-heading"
            ref={headingRef}
            tabIndex={-1}
            className="text-lg font-semibold outline-none lg:text-white!"
          >
            {t('trips.filtersLabel')}
          </h2>
          {activeCount > 0 && (
            <button
              type="button"
              onClick={onClear}
              className="text-sm font-medium text-hill underline-offset-4 hover:underline lg:text-dusk"
            >
              {t('trips.clearFilters')}
            </button>
          )}
          <button
            type="button"
            onClick={onClose}
            aria-label={t('common.close')}
            className="rounded-full p-1.5 text-deep transition hover:bg-hill/10 lg:hidden"
          >
            <X aria-hidden="true" className="h-5 w-5" />
          </button>
        </div>

        {/* The sheet scrolls on a phone; the sidebar must not, or it clips the destination
            list as it opens. */}
        <div className="flex flex-col gap-6 overflow-y-auto p-5 lg:overflow-visible">
          {children}
        </div>

        <div className="border-t border-hill/10 p-4 lg:hidden">
          <button
            type="button"
            onClick={onClose}
            className="w-full rounded-full bg-turmeric px-5 py-3 font-semibold text-night transition hover:bg-dusk"
          >
            {total === undefined
              ? t('trips.showResults')
              : t('trips.showCount', { count: total, n: formatCount(total, language) })}
          </button>
        </div>
      </aside>
    </>
  );
}

/** A single choice shown as chips: real radio buttons, styled, so arrow keys work. */
function ChipGroup({
  legend,
  name,
  value,
  onChange,
  options,
}: {
  legend: string;
  name: string;
  value: string;
  onChange: (value: string) => void;
  options: { value: string; label: string }[];
}) {
  return (
    <fieldset>
      <legend className="mb-2 text-sm font-semibold text-deep">{legend}</legend>
      <div className="flex flex-wrap gap-2">
        {options.map((option) => (
          <label key={option.value || 'any'} className="cursor-pointer">
            <input
              type="radio"
              name={name}
              value={option.value}
              checked={value === option.value}
              onChange={() => onChange(option.value)}
              className="peer sr-only"
            />
            <span className="inline-block rounded-full border border-hill/15 bg-white px-3.5 py-1.5 text-sm text-deep transition peer-checked:border-hill peer-checked:bg-hill peer-checked:text-white peer-focus-visible:outline-2 peer-focus-visible:outline-offset-2 peer-focus-visible:outline-turmeric hover:border-hill/40">
              {option.label}
            </span>
          </label>
        ))}
      </div>
    </fieldset>
  );
}

/** Reads the filters from the query string, dropping anything malformed rather than failing. */
function readFilters(params: URLSearchParams): TripFilters {
  const groupType = params.get('groupType');
  const sort = params.get('sort');
  const maxPrice = Number(params.get('maxPrice'));
  const page = Number(params.get('page'));

  return {
    destination: params.get('destination') ?? undefined,
    from: params.get('from') ?? undefined,
    groupType: groupTypes.includes(groupType as GroupType) ? (groupType as GroupType) : undefined,
    maxPrice: maxPrice > 0 ? maxPrice : undefined,
    sort: tripSorts.includes(sort as TripSort) ? (sort as TripSort) : undefined,
    page: Number.isInteger(page) && page > 1 ? page : undefined,
    verifiedHostsOnly: params.get('verifiedHostsOnly') === 'true' ? true : undefined,
    pageSize,
  };
}
