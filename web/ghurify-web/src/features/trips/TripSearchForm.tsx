import { Controller, useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router';
import { CalendarDays, MapPin, Search, Users } from 'lucide-react';
import { Select } from '@/components/ui/Select';
import { todayInDhaka, toLanguage } from '@/lib/format';
import { groupTypes } from './tripsApi';
import { useDestinations } from './useTrips';

/** Mirrors the backend validator: every field optional, slugs and group types from fixed sets. */
const searchSchema = z.object({
  destination: z.string().regex(/^[a-z0-9-]*$/),
  from: z.string(),
  groupType: z.enum(['', ...groupTypes]),
});

type SearchForm = z.infer<typeof searchSchema>;

// The fields drop their own outline; the whole cell shows focus instead, with a turmeric bar.
const cellClass =
  'relative flex flex-col gap-0.5 px-5 py-3 transition hover:bg-mist/70 focus-within:bg-mist focus-within:shadow-[inset_0_-3px_0_var(--color-turmeric)]';
const labelClass =
  'flex items-center gap-1.5 text-xs font-semibold uppercase tracking-wide text-deep/60 [:lang(bn)_&]:tracking-normal';
const valueClass = 'font-display text-base font-semibold text-deep';

/**
 * The search bar: one white strip of where / when / who with the action docked at the end,
 * stacking on narrow screens. Submitting opens the explore page with the choices in the
 * query string.
 */
export function TripSearchForm() {
  const { t, i18n } = useTranslation();
  const language = toLanguage(i18n.language);
  const navigate = useNavigate();
  const { data: destinations } = useDestinations();

  const { register, handleSubmit, control } = useForm<SearchForm>({
    resolver: zodResolver(searchSchema),
    defaultValues: { destination: '', from: '', groupType: '' },
  });

  const onSubmit = handleSubmit((values) => {
    const params = new URLSearchParams();
    if (values.destination) params.set('destination', values.destination);
    if (values.from) params.set('from', values.from);
    if (values.groupType) params.set('groupType', values.groupType);

    const query = params.toString();
    void navigate(query ? `/trips?${query}` : '/trips');
  });

  const destinationOptions = [
    { value: '', label: t('home.anyDestination') },
    ...(destinations ?? []).map((destination) => ({
      value: destination.slug,
      label: language === 'bn' ? destination.nameBn : destination.name,
      hint: language === 'bn' ? destination.divisionBn : destination.division,
    })),
  ];
  const groupOptions = [
    { value: '', label: t('home.anyGroup') },
    ...groupTypes.map((groupType) => ({ value: groupType, label: t(`groupType.${groupType}`) })),
  ];

  return (
    // No overflow-hidden here: it would clip the dropdowns. The corners are rounded per cell.
    <form
      onSubmit={(event) => void onSubmit(event)}
      className="grid rounded-2xl bg-white text-left shadow-2xl ring-1 ring-night/5 md:grid-cols-[1.3fr_1fr_1fr_auto] md:divide-x md:divide-hill/10"
    >
      <div
        className={`${cellClass} rounded-t-2xl border-b border-hill/10 md:rounded-l-2xl md:rounded-tr-none md:border-b-0`}
      >
        <label htmlFor="search-destination" className={labelClass}>
          <MapPin aria-hidden="true" className="h-4 w-4 text-ochre" />
          {t('home.destinationLabel')}
        </label>
        <Controller
          control={control}
          name="destination"
          render={({ field }) => (
            <Select
              id="search-destination"
              value={field.value}
              onChange={field.onChange}
              options={destinationOptions}
              buttonClassName={valueClass}
            />
          )}
        />
      </div>

      <div className={`${cellClass} border-b border-hill/10 md:border-b-0`}>
        <label htmlFor="search-from" className={labelClass}>
          <CalendarDays aria-hidden="true" className="h-4 w-4 text-ochre" />
          {t('home.dateLabel')}
        </label>
        <input
          id="search-from"
          type="date"
          min={todayInDhaka()}
          className={`w-full cursor-pointer bg-transparent outline-none ${valueClass}`}
          {...register('from')}
        />
      </div>

      <div className={cellClass}>
        <label htmlFor="search-group" className={labelClass}>
          <Users aria-hidden="true" className="h-4 w-4 text-ochre" />
          {t('home.groupLabel')}
        </label>
        <Controller
          control={control}
          name="groupType"
          render={({ field }) => (
            <Select
              id="search-group"
              value={field.value}
              onChange={field.onChange}
              options={groupOptions}
              buttonClassName={valueClass}
            />
          )}
        />
      </div>

      <button
        type="submit"
        className="flex items-center justify-center gap-2 rounded-b-2xl bg-turmeric px-8 py-4 font-display text-lg font-semibold text-night transition hover:bg-dusk focus-visible:-outline-offset-4 md:rounded-r-2xl md:rounded-bl-none"
      >
        <Search aria-hidden="true" className="h-5 w-5" />
        {t('home.search')}
      </button>
    </form>
  );
}
