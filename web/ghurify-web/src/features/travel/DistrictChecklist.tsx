import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ArrowLeft, Check, MapPinned, Plus, Search } from 'lucide-react';

import { cardClass, primaryButtonClass } from '@/components/Field';
import { Photo } from '@/components/ui/Photo';
import { formatCount, formatFullDate, toLanguage } from '@/lib/format';
import { districtBySlug, districtCount, districtsByDivision } from './districts';
import type { VisitedPlace } from './travelApi';

/**
 * The 64 districts by division, the visited ones filled in: the reference beside the map, and its
 * keyboard-friendly twin. A visited district opens what is there; one not visited yet starts
 * adding a place in it.
 */
export function DistrictChecklist({
  visited,
  selected,
  onOpen,
  onAdd,
}: {
  /** Visited district slug -> its places. */
  visited: ReadonlyMap<string, readonly VisitedPlace[]>;
  selected: string | null;
  onOpen: (slug: string) => void;
  onAdd: (slug: string) => void;
}) {
  const { t, i18n } = useTranslation();
  const language = toLanguage(i18n.language);
  const [query, setQuery] = useState('');
  const collator = new Intl.Collator(language === 'bn' ? 'bn' : 'en');
  const needle = query.trim().toLocaleLowerCase();

  const name = (slug: string) => t(`district.${slug}`);
  const matches = (slug: string, division: string) =>
    !needle ||
    [
      t(`district.${slug}`, { lng: 'en' }),
      t(`district.${slug}`, { lng: 'bn' }),
      t(`division.${division}`, { lng: 'en' }),
      t(`division.${division}`, { lng: 'bn' }),
    ].some((text) => text.toLocaleLowerCase().includes(needle));

  const groups = districtsByDivision
    .map(({ division, districts }) => ({
      division,
      total: districts.length,
      reached: districts.filter((shape) => visited.has(shape.slug)).length,
      shown: districts
        .filter((shape) => matches(shape.slug, division))
        .sort((a, b) => collator.compare(name(a.slug), name(b.slug))),
    }))
    .filter((group) => group.shown.length > 0);

  return (
    <section aria-labelledby="districts-heading" className={cardClass}>
      <div className="flex items-center justify-between gap-3">
        <h2 id="districts-heading" className="text-lg font-semibold">
          {t('travel.districts.title')}
        </h2>
        <p className="rounded-full bg-hill/10 px-3 py-1 text-sm font-bold text-hill">
          {formatCount(visited.size, language)} / {formatCount(districtCount, language)}
        </p>
      </div>
      <p className="mt-1 text-sm text-deep/65">{t('travel.districts.hint')}</p>

      <label className="relative mt-4 block">
        <span className="sr-only">{t('travel.districts.search')}</span>
        <Search
          aria-hidden="true"
          className="pointer-events-none absolute left-3.5 top-1/2 h-4 w-4 -translate-y-1/2 text-deep/45"
        />
        <input
          type="search"
          value={query}
          onChange={(event) => setQuery(event.target.value)}
          placeholder={t('travel.districts.searchPlaceholder')}
          className="w-full rounded-2xl border border-hill/15 bg-mist/60 py-2.5 pl-10 pr-4 text-sm text-deep placeholder:text-deep/45 focus:border-hill focus:bg-white focus:outline-none focus:ring-2 focus:ring-hill/20"
        />
      </label>

      <div className="mt-2 flex flex-col divide-y divide-hill/10 lg:max-h-[30rem] lg:overflow-y-auto lg:pr-1">
        {groups.length === 0 && (
          <p className="py-4 text-sm text-deep/60">{t('travel.districts.noMatch')}</p>
        )}
        {groups.map((group) => (
          <div key={group.division} className="py-3.5">
            <h3 className="flex items-center gap-2 text-sm font-semibold text-deep">
              {t('travel.districts.division', { division: t(`division.${group.division}`) })}
              <span className="text-xs font-semibold text-deep/50">
                {formatCount(group.reached, language)}/{formatCount(group.total, language)}
              </span>
              <span
                aria-hidden="true"
                className="ml-auto h-1.5 w-16 overflow-hidden rounded-full bg-mist"
              >
                <span
                  className="block h-full rounded-full bg-linear-to-r from-hill to-turmeric transition-[width] duration-700"
                  style={{ width: `${(group.reached / group.total) * 100}%` }}
                />
              </span>
            </h3>
            <ul className="mt-2.5 flex flex-wrap gap-1.5">
              {group.shown.map((shape) => {
                const places = visited.get(shape.slug);
                const active = shape.slug === selected;
                return (
                  <li key={shape.slug}>
                    {places ? (
                      <button
                        type="button"
                        aria-pressed={active}
                        aria-label={t('travel.districts.openVisited', {
                          district: name(shape.slug),
                          count: places.length,
                          n: formatCount(places.length, language),
                        })}
                        onClick={() => onOpen(shape.slug)}
                        className={`inline-flex items-center gap-1.5 rounded-full px-3 py-1.5 text-xs font-semibold shadow-sm transition ${
                          active
                            ? 'bg-deep text-white ring-2 ring-turmeric ring-offset-1'
                            : 'bg-hill text-white hover:bg-deep'
                        }`}
                      >
                        <Check aria-hidden="true" className="h-3 w-3" />
                        {name(shape.slug)}
                      </button>
                    ) : (
                      <button
                        type="button"
                        aria-label={t('travel.districts.addIn', { district: name(shape.slug) })}
                        onClick={() => onAdd(shape.slug)}
                        className="group inline-flex items-center gap-1 rounded-full bg-sand px-3 py-1.5 text-xs font-medium text-deep/75 ring-1 ring-deep/5 transition hover:bg-turmeric/20 hover:text-deep"
                      >
                        {name(shape.slug)}
                        <Plus
                          aria-hidden="true"
                          className="h-3 w-3 opacity-0 transition group-hover:opacity-100 group-focus-visible:opacity-100"
                        />
                      </button>
                    )}
                  </li>
                );
              })}
            </ul>
          </div>
        ))}
      </div>
    </section>
  );
}

/** One district, chosen on the map or in the list: the places there, and adding another. */
export function DistrictDetails({
  slug,
  places,
  onBack,
  onOpenPlace,
  onAdd,
}: {
  slug: string;
  places: readonly VisitedPlace[];
  onBack: () => void;
  onOpenPlace: (key: string) => void;
  onAdd: () => void;
}) {
  const { t, i18n } = useTranslation();
  const language = toLanguage(i18n.language);
  const shape = districtBySlug.get(slug);
  const district = t(`district.${slug}`);
  const withPhoto = places.find((place) => place.destinationSlug);
  // Their own photo from there, before any stock photo of a destination.
  const cover = coverOf(places);

  return (
    <article aria-label={district} className={`${cardClass} overflow-hidden p-0!`}>
      <header className="relative isolate overflow-hidden bg-linear-to-br from-hill to-night px-5 pb-5 pt-14 text-white">
        {(cover || withPhoto?.destinationSlug) && (
          <>
            {cover ? (
              <img
                src={cover}
                alt=""
                className="absolute inset-0 -z-10 h-full w-full object-cover opacity-70"
              />
            ) : (
              <Photo
                slug={withPhoto!.destinationSlug!}
                kind={withPhoto!.kind ?? 'Hills'}
                cut="card"
                decorative
                className="absolute! inset-0 -z-10 opacity-60"
              />
            )}
            <div
              aria-hidden="true"
              className="absolute inset-0 -z-10 bg-linear-to-t from-night/90 to-night/20"
            />
          </>
        )}
        <button
          type="button"
          onClick={onBack}
          className="absolute left-3 top-3 inline-flex items-center gap-1 rounded-full bg-white/90 px-3 py-1.5 text-xs font-semibold text-deep shadow transition hover:bg-white"
        >
          <ArrowLeft aria-hidden="true" className="h-3.5 w-3.5" />
          {t('travel.details.back')}
        </button>
        <p className="eyebrow text-dusk!">
          {shape && t('travel.districts.division', { division: t(`division.${shape.division}`) })}
        </p>
        <h2 className="font-display text-2xl font-bold text-white!">{district}</h2>
        <p className="mt-1 text-sm text-white/80">
          {places.length > 0
            ? t('travel.districts.places', {
                count: places.length,
                n: formatCount(places.length, language),
              })
            : t('travel.districts.notYet')}
        </p>
      </header>

      <div className="flex flex-col gap-3 p-5">
        {places.length > 0 && (
          <ul className="flex flex-col gap-1">
            {places.map((place) => (
              <li key={place.key}>
                <button
                  type="button"
                  onClick={() => onOpenPlace(place.key)}
                  className="flex w-full items-center gap-3 rounded-xl p-2 text-left transition hover:bg-mist"
                >
                  {coverOf([place]) ? (
                    <img
                      src={coverOf([place])}
                      alt=""
                      loading="lazy"
                      className="h-11 w-11 shrink-0 rounded-lg object-cover"
                    />
                  ) : place.destinationSlug ? (
                    <Photo
                      slug={place.destinationSlug}
                      kind={place.kind ?? 'Hills'}
                      cut="card"
                      decorative
                      className="h-11 w-11 shrink-0 rounded-lg"
                    />
                  ) : (
                    <span className="flex h-11 w-11 shrink-0 items-center justify-center rounded-lg bg-turmeric/25 text-ochre">
                      <MapPinned aria-hidden="true" className="h-5 w-5" />
                    </span>
                  )}
                  <span className="min-w-0">
                    <span className="block truncate text-sm font-semibold text-deep">
                      {language === 'bn' && place.nameBn ? place.nameBn : place.name}
                    </span>
                    <span className="block truncate text-xs text-deep/60">
                      {t('travel.districts.lastVisit', {
                        date: formatFullDate(place.lastVisitedOn, language),
                      })}
                    </span>
                  </span>
                </button>
              </li>
            ))}
          </ul>
        )}
        <button type="button" className={`${primaryButtonClass} self-start`} onClick={onAdd}>
          <Plus aria-hidden="true" className="h-4 w-4" />
          {t('travel.districts.addHere', { district })}
        </button>
      </div>
    </article>
  );
}

/** The first photo they put on a visit to any of these places. */
function coverOf(places: readonly VisitedPlace[]): string | undefined {
  for (const place of places) {
    const own = place.photos.find((photo) => photo.kind === 'Image' && photo.visitId !== null);
    if (own) return own.url;
  }
  return undefined;
}
