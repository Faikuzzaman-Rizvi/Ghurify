import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router';
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

/** The hero search. Submitting opens the explore page with the choices in the query string. */
export function TripSearchForm() {
  const { t, i18n } = useTranslation();
  const language = toLanguage(i18n.language);
  const navigate = useNavigate();
  const { data: destinations } = useDestinations();

  const { register, handleSubmit } = useForm<SearchForm>({
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

  const fieldClass =
    'w-full rounded-2xl border border-hill/20 bg-mist px-3 py-2.5 text-base text-deep focus:border-hill focus:bg-white';

  return (
    <form
      onSubmit={(event) => void onSubmit(event)}
      className="grid gap-3 sm:grid-cols-[1.4fr_1fr_1fr_auto] sm:items-end"
    >
      <div className="flex flex-col gap-1">
        <label htmlFor="search-destination" className="text-xs font-semibold text-deep/70">
          {t('home.destinationLabel')}
        </label>
        <select id="search-destination" className={fieldClass} {...register('destination')}>
          <option value="">{t('home.anyDestination')}</option>
          {destinations?.map((destination) => (
            <option key={destination.slug} value={destination.slug}>
              {language === 'bn' ? destination.nameBn : destination.name}
            </option>
          ))}
        </select>
      </div>

      <div className="flex flex-col gap-1">
        <label htmlFor="search-from" className="text-xs font-semibold text-deep/70">
          {t('home.dateLabel')}
        </label>
        <input
          id="search-from"
          type="date"
          min={todayInDhaka()}
          className={fieldClass}
          {...register('from')}
        />
      </div>

      <div className="flex flex-col gap-1">
        <label htmlFor="search-group" className="text-xs font-semibold text-deep/70">
          {t('home.groupLabel')}
        </label>
        <select id="search-group" className={fieldClass} {...register('groupType')}>
          <option value="">{t('home.anyGroup')}</option>
          {groupTypes.map((groupType) => (
            <option key={groupType} value={groupType}>
              {t(`groupType.${groupType}`)}
            </option>
          ))}
        </select>
      </div>

      <button
        type="submit"
        className="rounded-2xl bg-turmeric px-6 py-3 font-display text-lg font-bold text-deep shadow-sm transition hover:bg-dusk"
      >
        {t('home.search')}
      </button>
    </form>
  );
}
