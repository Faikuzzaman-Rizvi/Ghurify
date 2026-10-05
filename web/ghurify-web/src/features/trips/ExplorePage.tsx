import { useTranslation } from 'react-i18next';
import { useSearchParams } from 'react-router';
import { asNumber } from '@/api/client';
import { CardSkeletons, EmptyState, ErrorState } from '@/components/States';
import { formatCount, formatMoney, toLanguage } from '@/lib/format';
import { TripCard } from './TripCard';
import { groupTypes, tripSorts, type GroupType, type TripFilters, type TripSort } from './tripsApi';
import { useDestinations, useTripSearch } from './useTrips';

const pageSize = 12;
const budgets = [5000, 7500, 10000, 15000];

/**
 * Every live trip, filtered. The query string is the source of truth, so a filtered search
 * can be bookmarked, shared, and survives a reload.
 */
export function ExplorePage() {
  const { t, i18n } = useTranslation();
  const language = toLanguage(i18n.language);
  const [params, setParams] = useSearchParams();
  const { data: destinations } = useDestinations();

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

  const total = data ? asNumber(data.totalCount) : 0;
  const page = filters.page ?? 1;
  const pages = Math.max(1, Math.ceil(total / pageSize));
  const hasFilters = ['destination', 'groupType', 'maxPrice', 'from'].some((key) =>
    params.has(key),
  );

  const selectClass =
    'w-full rounded-2xl border border-hill/20 bg-white px-3 py-2 text-sm text-deep focus:border-hill';

  return (
    <div className="mx-auto max-w-6xl px-4 pt-10">
      <header className="max-w-2xl">
        <h1 className="text-4xl font-extrabold text-deep">{t('trips.exploreTitle')}</h1>
        <p className="mt-2 text-deep/70">{t('trips.exploreSubtitle')}</p>
      </header>

      <section
        aria-label={t('trips.filtersLabel')}
        className="sticky top-[4.5rem] z-30 mt-6 grid gap-3 rounded-3xl bg-white/95 p-4 shadow-md ring-1 ring-hill/10 backdrop-blur sm:grid-cols-4"
      >
        <div className="flex flex-col gap-1">
          <label htmlFor="filter-destination" className="text-xs font-semibold text-deep/70">
            {t('home.destinationLabel')}
          </label>
          <select
            id="filter-destination"
            className={selectClass}
            value={filters.destination ?? ''}
            onChange={(event) => update('destination', event.target.value)}
          >
            <option value="">{t('home.anyDestination')}</option>
            {destinations?.map((destination) => (
              <option key={destination.slug} value={destination.slug}>
                {language === 'bn' ? destination.nameBn : destination.name}
              </option>
            ))}
          </select>
        </div>

        <div className="flex flex-col gap-1">
          <label htmlFor="filter-group" className="text-xs font-semibold text-deep/70">
            {t('home.groupLabel')}
          </label>
          <select
            id="filter-group"
            className={selectClass}
            value={filters.groupType ?? ''}
            onChange={(event) => update('groupType', event.target.value)}
          >
            <option value="">{t('home.anyGroup')}</option>
            {groupTypes.map((groupType) => (
              <option key={groupType} value={groupType}>
                {t(`groupType.${groupType}`)}
              </option>
            ))}
          </select>
        </div>

        <div className="flex flex-col gap-1">
          <label htmlFor="filter-budget" className="text-xs font-semibold text-deep/70">
            {t('trips.maxPriceLabel')}
          </label>
          <select
            id="filter-budget"
            className={selectClass}
            value={filters.maxPrice ?? ''}
            onChange={(event) => update('maxPrice', event.target.value)}
          >
            <option value="">{t('trips.anyPrice')}</option>
            {budgets.map((budget) => (
              <option key={budget} value={budget}>
                {t('trips.upTo', { price: formatMoney(budget, language) })}
              </option>
            ))}
          </select>
        </div>

        <div className="flex flex-col gap-1">
          <label htmlFor="filter-sort" className="text-xs font-semibold text-deep/70">
            {t('trips.sortLabel')}
          </label>
          <select
            id="filter-sort"
            className={selectClass}
            value={filters.sort ?? 'Soonest'}
            onChange={(event) =>
              update('sort', event.target.value === 'Soonest' ? '' : event.target.value)
            }
          >
            {tripSorts.map((sort) => (
              <option key={sort} value={sort}>
                {t(`trips.sort.${sort}`)}
              </option>
            ))}
          </select>
        </div>
      </section>

      <div className="mt-6 flex items-center justify-between gap-4" aria-live="polite">
        <p className="text-sm font-medium text-deep/70">
          {data && t('trips.results', { count: total, n: formatCount(total, language) })}
        </p>
        {hasFilters && (
          <button
            type="button"
            onClick={() => setParams(new URLSearchParams())}
            className="text-sm font-medium text-hill underline-offset-4 hover:underline"
          >
            {t('trips.clearFilters')}
          </button>
        )}
      </div>

      <div
        className={`mt-4 grid gap-6 transition-opacity sm:grid-cols-2 lg:grid-cols-3 ${
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
              hasFilters ? (
                <button
                  type="button"
                  onClick={() => setParams(new URLSearchParams())}
                  className="mt-2 rounded-full bg-hill px-4 py-2 text-sm font-medium text-white transition hover:bg-deep"
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

      {pages > 1 && (
        <nav
          aria-label={t('trips.pagination')}
          className="mt-8 flex items-center justify-center gap-3"
        >
          <button
            type="button"
            disabled={page <= 1}
            onClick={() => update('page', String(page - 1))}
            className="rounded-full border border-hill/25 px-4 py-2 text-sm text-deep transition hover:bg-hill/10 disabled:opacity-40"
          >
            ← {t('trips.previous')}
          </button>
          <span className="text-sm text-deep/70">
            {t('trips.pageOf', {
              page: formatCount(page, language),
              pages: formatCount(pages, language),
            })}
          </span>
          <button
            type="button"
            disabled={page >= pages}
            onClick={() => update('page', String(page + 1))}
            className="rounded-full border border-hill/25 px-4 py-2 text-sm text-deep transition hover:bg-hill/10 disabled:opacity-40"
          >
            {t('trips.next')} →
          </button>
        </nav>
      )}
    </div>
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
    pageSize,
  };
}
