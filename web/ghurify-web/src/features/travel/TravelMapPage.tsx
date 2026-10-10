import { lazy, Suspense, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';
import {
  CalendarClock,
  CalendarDays,
  Compass,
  Download,
  Eye,
  EyeOff,
  Flag,
  Landmark,
  Map as MapIcon,
  MapPinned,
  Plus,
  Route,
  Shield,
} from 'lucide-react';

import { asNumber } from '@/api/client';
import { Avatar } from '@/components/Avatar';
import {
  accentButtonClass,
  cardClass,
  primaryButtonClass,
  secondaryButtonClass,
} from '@/components/Field';
import { ErrorState } from '@/components/States';
import { PageBanner } from '@/components/ui/PageBanner';
import { Photo } from '@/components/ui/Photo';
import { useAuthStore } from '@/features/auth/authStore';
import { avatarUrl } from '@/features/auth/profileApi';
import { useSignedInIdentity } from '@/features/auth/useAccount';
import { errorText } from '@/lib/errors';
import { formatCount, formatDateRange, formatFullDate, toLanguage } from '@/lib/format';
import { AddPlaceDialog } from './AddPlaceDialog';
import { DistrictChecklist, DistrictDetails } from './DistrictChecklist';
import { DistrictMap, TravelPoster, type DistrictPin } from './DistrictMap';
import { districtBySlug, districtCount, placesByDistrict } from './districts';
import { mapThemes, themeById, type MapTheme } from './mapThemes';
import { PlaceDetails } from './PlaceDetails';
import { downloadPoster, type PosterFormat } from './posterExport';
import {
  divisions,
  pointOf,
  upcomingKey,
  type TravelSummary,
  type UpcomingTrip,
  type VisitedPlace,
} from './travelApi';
import { useMyTravelMap, useTravelMapSharing } from './useTravelMap';

// Leaflet and its tiles only load for those who open the street map.
const TravelMapCanvas = lazy(() => import('./TravelMapCanvas'));

type View = 'districts' | 'street';

/** A per-browser choice (the view, the theme, names on or off), kept between visits. */
function useRemembered<T extends string>(key: string, fallback: T, allowed: readonly T[]) {
  const [value, setValue] = useState<T>(() => {
    try {
      const stored = localStorage.getItem(`ghurify.travelMap.${key}`);
      return allowed.find((item) => item === stored) ?? fallback;
    } catch {
      return fallback;
    }
  });
  const remember = (next: T) => {
    setValue(next);
    try {
      localStorage.setItem(`ghurify.travelMap.${key}`, next);
    } catch {
      // Private windows may refuse; the choice still holds for this visit.
    }
  };
  return [value, remember] as const;
}

const districtKey = (slug: string) => `district:${slug}`;

/**
 * The signed-in person's travel map. By default Bangladesh's 64 districts, the ones they have
 * been to filled in, as a poster they can style and download; or the street map with every place
 * as a pin. Ghurify trips put themselves there when they end; other places they add, from the
 * button, the map or the district list. Beside it: the districts, their history, their trips to
 * come, and whether the map is on their public profile.
 */
export function TravelMapPage() {
  const { t, i18n } = useTranslation();
  const language = toLanguage(i18n.language);
  const map = useMyTravelMap();
  const identity = useSignedInIdentity();
  const [selected, setSelected] = useState<string | null>(null);
  const [adding, setAdding] = useState<{ district: string | null } | null>(null);
  const [justAdded, setJustAdded] = useState<number | null>(null);
  const [view, setView] = useRemembered<View>('view', 'districts', ['districts', 'street']);
  const [themeId, setThemeId] = useRemembered<MapTheme['id']>(
    'theme',
    'hill',
    mapThemes.map((theme) => theme.id),
  );
  const [labels, setLabels] = useRemembered<'on' | 'off'>('labels', 'on', ['on', 'off']);
  const [posterName, setPosterName] = useState<string | null>(null);
  const [showPhoto, setShowPhoto] = useState(true);
  const panel = useRef<HTMLDivElement>(null);

  const places = useMemo(() => map.data?.places ?? [], [map.data]);
  const upcoming = map.data?.upcoming ?? [];
  const theme = themeById(themeId);
  const byDistrict = useMemo(() => placesByDistrict(places), [places]);
  const visitedCounts = useMemo(
    () => new Map([...byDistrict].map(([slug, items]) => [slug, items.length])),
    [byDistrict],
  );
  const divisionsReached = new Set(
    [...byDistrict.keys()].map((slug) => districtBySlug.get(slug)?.division),
  ).size;

  const placeName = (place: VisitedPlace) =>
    language === 'bn' && place.nameBn ? place.nameBn : place.name;
  const pins: DistrictPin[] = [
    ...places.flatMap((place): DistrictPin[] => {
      const point = pointOf(place);
      return point
        ? [
            {
              key: place.key,
              latitude: point[0],
              longitude: point[1],
              tone: place.visits.some((visit) => visit.source === 'Trip') ? 'trip' : 'added',
              name: placeName(place),
            },
          ]
        : [];
    }),
    ...upcoming.map((trip): DistrictPin => ({
      key: upcomingKey(trip),
      latitude: Number(trip.latitude),
      longitude: Number(trip.longitude),
      tone: 'upcoming',
      name: `${language === 'bn' ? trip.destinationNameBn : trip.destinationName} · ${trip.title}`,
    })),
  ];

  // A place just added: open its pin once the map has it (it may join a pin already there).
  if (justAdded !== null) {
    const found = places.find((place) =>
      place.visits.some((visit) => asNumber(visit.id) === justAdded),
    );
    if (found) {
      setJustAdded(null);
      setSelected(found.key);
    }
  }

  const selectedPlace = places.find((place) => place.key === selected) ?? null;
  const selectedTrip = upcoming.find((trip) => upcomingKey(trip) === selected) ?? null;
  const selectedDistrict = selected?.startsWith('district:') ? selected.slice(9) : null;
  const name = posterName ?? map.data?.displayName ?? identity.name;
  const hasPhoto = identity.avatarVersion !== null && identity.userId !== 0;

  /** From the map: on a phone the details are below it, so bring them into view. */
  const selectFromMap = (key: string) => {
    setSelected(key);
    if (!window.matchMedia?.('(min-width: 1024px)').matches) {
      window.requestAnimationFrame(() =>
        panel.current?.scrollIntoView?.({ behavior: 'smooth', block: 'start' }),
      );
    }
  };

  const banner = (
    <PageBanner
      compact
      slug="rangamati"
      kind="Lake"
      eyebrow={t('travel.eyebrow')}
      titleKey="travel.titleAccent"
      aside={
        <button
          type="button"
          className={accentButtonClass}
          onClick={() => setAdding({ district: null })}
        >
          <Plus aria-hidden="true" className="h-4 w-4" />
          {t('travel.addPlace')}
        </button>
      }
    >
      <p className="mt-3 max-w-xl text-white/80">{t('travel.subtitle')}</p>
      {map.data && <SummaryStrip summary={map.data.summary} districts={byDistrict.size} />}
    </PageBanner>
  );

  if (map.isError) {
    return (
      <>
        {banner}
        <div className="container-page relative z-10 -mt-14 pb-10">
          <ErrorState message={errorText(map.error, t)} onRetry={() => void map.refetch()} />
        </div>
      </>
    );
  }

  const empty = map.isSuccess && places.length === 0 && upcoming.length === 0;
  const loading = (
    <div role="status" className="h-full min-h-104 w-full animate-pulse rounded-3xl bg-hill/10">
      <span className="sr-only">{t('common.loading')}</span>
    </div>
  );

  return (
    <>
      {banner}

      <div className="container-page relative z-10 -mt-14 grid grid-cols-[minmax(0,1fr)] items-start gap-6 pb-10 lg:grid-cols-[minmax(0,1fr)_24rem]">
        <section
          aria-labelledby="travel-map-heading"
          className={`${cardClass} overflow-hidden p-0!`}
        >
          <div className="flex flex-wrap items-center justify-between gap-3 border-b border-hill/10 px-4 py-3 sm:px-5">
            <h2 id="travel-map-heading" className="font-display text-lg font-semibold">
              {t('travel.map.title')}
            </h2>
            <ViewSwitch view={view} onChange={setView} />
          </div>

          {view === 'districts' ? (
            <div className="relative p-3 sm:p-5">
              {map.isPending ? (
                loading
              ) : (
                <TravelPoster
                  theme={theme}
                  name={name}
                  visitedDistricts={byDistrict.size}
                  divisionsReached={divisionsReached}
                  photo={
                    hasPhoto && showPhoto ? (
                      <Avatar
                        userId={identity.userId}
                        name={name}
                        version={identity.avatarVersion}
                        size="md"
                        className="ring-4 ring-white/70"
                      />
                    ) : undefined
                  }
                >
                  <DistrictMap
                    theme={theme}
                    visited={visitedCounts}
                    pins={pins}
                    selectedDistrict={selectedDistrict}
                    selectedPin={selected}
                    showLabels={labels === 'on'}
                    onSelectDistrict={(slug) => selectFromMap(districtKey(slug))}
                    onSelectPin={selectFromMap}
                  />
                </TravelPoster>
              )}
              {empty && <EmptyMap onAdd={() => setAdding({ district: null })} />}
            </div>
          ) : (
            <div className="relative h-104 sm:h-136">
              {map.isPending ? (
                loading
              ) : (
                <Suspense fallback={loading}>
                  <TravelMapCanvas
                    places={places}
                    upcoming={upcoming}
                    selectedKey={selected}
                    onSelect={selectFromMap}
                  />
                </Suspense>
              )}
              {empty && <EmptyMap onAdd={() => setAdding({ district: null })} />}
            </div>
          )}
          <Legend view={view} />

          {view === 'districts' && map.data && (
            <PosterControls
              themeId={theme.id}
              onTheme={setThemeId}
              name={name}
              onName={setPosterName}
              labels={labels === 'on'}
              onLabels={(on) => setLabels(on ? 'on' : 'off')}
              photo={hasPhoto ? showPhoto : null}
              onPhoto={setShowPhoto}
              poster={{
                theme,
                visited: new Set(byDistrict.keys()),
                progress: byDistrict.size / districtCount,
                pins: pins.filter((pin) => pin.tone !== 'upcoming'),
                showLabels: labels === 'on',
                photoUrl:
                  hasPhoto && showPhoto ? avatarUrl(identity.userId, identity.avatarVersion) : null,
                text: {
                  eyebrow: t('travel.poster.eyebrow'),
                  title: name ? t('travel.poster.title', { name }) : t('travel.poster.titleMine'),
                  count: formatCount(byDistrict.size, language),
                  total: formatCount(districtCount, language),
                  explored: t('travel.poster.explored', {
                    percent: formatCount(
                      Math.round((byDistrict.size / districtCount) * 100),
                      language,
                    ),
                  }),
                  counts: t('travel.poster.counts', {
                    count: byDistrict.size,
                    districts: formatCount(byDistrict.size, language),
                    divisions: formatCount(divisionsReached, language),
                    totalDivisions: formatCount(divisions.length, language),
                  }),
                  brand: t('app.name'),
                  tagline: t('travel.poster.tagline'),
                  districtName: (slug) => t(`district.${slug}`),
                },
              }}
            />
          )}
        </section>

        <div ref={panel} className="flex min-w-0 scroll-mt-24 flex-col gap-5 lg:sticky lg:top-24">
          {selectedPlace ? (
            <PlaceDetails place={selectedPlace} editable onBack={() => setSelected(null)} />
          ) : selectedTrip ? (
            <ComingUpDetails trip={selectedTrip} onBack={() => setSelected(null)} />
          ) : selectedDistrict ? (
            <DistrictDetails
              slug={selectedDistrict}
              places={byDistrict.get(selectedDistrict) ?? []}
              onBack={() => setSelected(null)}
              onOpenPlace={setSelected}
              onAdd={() => setAdding({ district: selectedDistrict })}
            />
          ) : (
            <>
              {map.data && (
                <DistrictChecklist
                  visited={byDistrict}
                  selected={selectedDistrict}
                  onOpen={(slug) => setSelected(districtKey(slug))}
                  onAdd={(slug) => setAdding({ district: slug })}
                />
              )}
              <History
                places={places}
                upcoming={upcoming}
                loading={map.isPending}
                onSelect={setSelected}
              />
            </>
          )}
          {map.data && <SharingCard shared={map.data.shared} userId={asNumber(map.data.userId)} />}
        </div>
      </div>

      {adding && (
        <AddPlaceDialog
          open
          district={adding.district}
          onClose={() => setAdding(null)}
          onAdded={(visitId) => {
            setAdding(null);
            setJustAdded(visitId);
          }}
        />
      )}
    </>
  );
}

/** Districts or the street map. */
function ViewSwitch({ view, onChange }: { view: View; onChange: (view: View) => void }) {
  const { t } = useTranslation();
  const options = [
    { value: 'districts' as const, label: t('travel.view.districts'), icon: Landmark },
    { value: 'street' as const, label: t('travel.view.street'), icon: MapIcon },
  ];

  return (
    <div
      role="group"
      aria-label={t('travel.view.label')}
      className="flex gap-1 rounded-full bg-mist p-1"
    >
      {options.map(({ value, label, icon: Icon }) => (
        <button
          key={value}
          type="button"
          aria-pressed={view === value}
          onClick={() => onChange(value)}
          className={`inline-flex items-center gap-1.5 rounded-full px-3 py-1.5 text-xs font-semibold transition ${
            view === value ? 'bg-hill text-white shadow' : 'text-deep/70 hover:text-deep'
          }`}
        >
          <Icon aria-hidden="true" className="h-3.5 w-3.5" />
          {label}
        </button>
      ))}
    </div>
  );
}

/** The poster's look (theme, name, names on the map, photo) and saving it as a file. */
function PosterControls({
  themeId,
  onTheme,
  name,
  onName,
  labels,
  onLabels,
  photo,
  onPhoto,
  poster,
}: {
  themeId: MapTheme['id'];
  onTheme: (id: MapTheme['id']) => void;
  name: string;
  onName: (name: string) => void;
  labels: boolean;
  onLabels: (on: boolean) => void;
  /** Whether their photo shows; null when they have none. */
  photo: boolean | null;
  onPhoto: (on: boolean) => void;
  poster: Parameters<typeof downloadPoster>[0];
}) {
  const { t } = useTranslation();
  const [busy, setBusy] = useState<PosterFormat | null>(null);
  const [failed, setFailed] = useState(false);

  const save = async (format: PosterFormat) => {
    setBusy(format);
    setFailed(false);
    try {
      await downloadPoster(poster, format, 'ghurify-travel-map');
    } catch {
      setFailed(true);
    } finally {
      setBusy(null);
    }
  };

  return (
    <div className="flex flex-col gap-5 border-t border-hill/10 px-4 py-5 sm:px-5">
      <fieldset>
        <legend className="text-sm font-semibold text-deep">{t('travel.poster.style')}</legend>
        <div className="mt-3 flex flex-wrap items-center gap-x-6 gap-y-4">
          <div
            role="radiogroup"
            aria-label={t('travel.poster.theme')}
            className="flex items-center gap-2"
          >
            <span aria-hidden="true" className="mr-1 text-xs text-deep/60">
              {t('travel.poster.theme')}
            </span>
            {mapThemes.map((item) => (
              <button
                key={item.id}
                type="button"
                role="radio"
                aria-checked={item.id === themeId}
                aria-label={t(`travel.poster.themes.${item.id}`)}
                title={t(`travel.poster.themes.${item.id}`)}
                onClick={() => onTheme(item.id)}
                className={`relative h-9 w-9 overflow-hidden rounded-full ring-offset-2 transition hover:scale-105 ${
                  item.id === themeId ? 'ring-2 ring-hill' : 'ring-1 ring-deep/15'
                }`}
                style={{ background: item.paper }}
              >
                <span
                  aria-hidden="true"
                  className="absolute bottom-0 right-0 h-5 w-5 rounded-tl-full"
                  style={{
                    background: `linear-gradient(135deg, ${item.visited[0]}, ${item.visited[1]})`,
                  }}
                />
              </button>
            ))}
          </div>

          <label className="flex min-w-48 flex-1 flex-col gap-1 text-xs text-deep/60">
            {t('travel.poster.name')}
            <input
              type="text"
              value={name}
              maxLength={40}
              placeholder={t('travel.poster.namePlaceholder')}
              onChange={(event) => onName(event.target.value)}
              className="rounded-xl border border-hill/15 bg-mist/60 px-3 py-2 text-sm text-deep placeholder:text-deep/45 focus:border-hill focus:bg-white focus:outline-none focus:ring-2 focus:ring-hill/20"
            />
          </label>

          <div className="flex flex-wrap gap-4 text-sm text-deep">
            <label className="flex items-center gap-2">
              <input
                type="checkbox"
                checked={labels}
                onChange={(event) => onLabels(event.target.checked)}
                className="h-4 w-4 accent-hill"
              />
              {t('travel.poster.labels')}
            </label>
            {photo !== null && (
              <label className="flex items-center gap-2">
                <input
                  type="checkbox"
                  checked={photo}
                  onChange={(event) => onPhoto(event.target.checked)}
                  className="h-4 w-4 accent-hill"
                />
                {t('travel.poster.photo')}
              </label>
            )}
          </div>
        </div>
      </fieldset>

      <div>
        <h3 className="text-sm font-semibold text-deep">{t('travel.poster.download')}</h3>
        <p className="mt-0.5 text-xs text-deep/60">{t('travel.poster.downloadHint')}</p>
        <div className="mt-3 grid grid-cols-3 gap-2">
          {(['png', 'jpg', 'pdf'] as const).map((format) => (
            <button
              key={format}
              type="button"
              disabled={busy !== null}
              onClick={() => void save(format)}
              className={`${secondaryButtonClass} justify-center`}
            >
              <Download aria-hidden="true" className="h-4 w-4" />
              {busy === format ? t('travel.poster.downloading') : format.toUpperCase()}
            </button>
          ))}
        </div>
        {failed && (
          <p role="alert" className="mt-2 text-sm text-jamdani">
            {t('travel.poster.downloadError')}
          </p>
        )}
      </div>
    </div>
  );
}

/** Places, districts and divisions reached, Ghurify trips and their days: on the banner. */
function SummaryStrip({ summary, districts }: { summary: TravelSummary; districts: number }) {
  const { t, i18n } = useTranslation();
  const language = toLanguage(i18n.language);
  const stats = [
    {
      icon: MapPinned,
      value: formatCount(summary.places, language),
      label: t('travel.summary.places', { count: asNumber(summary.places) }),
    },
    {
      icon: Landmark,
      value: `${formatCount(districts, language)}/${formatCount(districtCount, language)}`,
      label: t('travel.summary.districts'),
    },
    {
      icon: Flag,
      value: `${formatCount(summary.divisions, language)}/${formatCount(8, language)}`,
      label: t('travel.summary.divisions'),
    },
    {
      icon: Route,
      value: formatCount(summary.trips, language),
      label: t('travel.summary.trips', { count: asNumber(summary.trips) }),
    },
    {
      icon: CalendarDays,
      value: formatCount(summary.tripDays, language),
      label: t('travel.summary.days', { count: asNumber(summary.tripDays) }),
    },
  ];

  return (
    <dl className="mt-5 grid max-w-3xl grid-cols-2 gap-2 sm:grid-cols-5">
      {stats.map(({ icon: Icon, value, label }, index) => (
        <div
          key={label}
          className={`rounded-2xl bg-night/55 px-3 py-3 ring-1 ring-white/25 backdrop-blur-sm ${
            index === 0 ? 'max-sm:col-span-2' : ''
          }`}
        >
          {/* The longest label ("Days on trips") has to wrap inside a fifth of the row
              rather than run past its edge, so the icon keeps its width and the words break. */}
          <dt className="flex items-start gap-1.5 text-xs leading-tight text-white/90">
            <Icon aria-hidden="true" className="mt-px h-3.5 w-3.5 shrink-0 text-dusk" />
            <span className="min-w-0">{label}</span>
          </dt>
          <dd className="mt-1 font-display text-2xl font-bold leading-none text-white">{value}</dd>
        </div>
      ))}
    </dl>
  );
}

/** What the colours mean. */
function Legend({ view }: { view: View }) {
  const { t } = useTranslation();
  const items = [
    ...(view === 'districts'
      ? [
          {
            swatch: 'rounded-sm! rotate-0! bg-linear-to-b from-[#2f7a57] to-deep',
            label: t('travel.legend.district'),
          },
        ]
      : []),
    { swatch: 'bg-hill', label: t('travel.legend.trip') },
    { swatch: 'bg-turmeric', label: t('travel.legend.added') },
    {
      swatch: 'border-2 border-dashed border-jamdani bg-white',
      label: t('travel.legend.upcoming'),
    },
  ];

  return (
    <div className="border-t border-hill/10 px-5 py-3 text-xs text-deep/70">
      <ul className="flex flex-wrap gap-x-5 gap-y-2">
        {items.map((item) => (
          <li key={item.label} className="flex items-center gap-2">
            <span
              aria-hidden="true"
              className={`h-3 w-3 -rotate-45 rounded-full rounded-bl-none ${item.swatch}`}
            />
            {item.label}
          </li>
        ))}
      </ul>
      {/* The boundaries' licence (CC BY 3.0 IGO) asks for this credit. */}
      {view === 'districts' && (
        <p className="mt-2 text-[0.7rem] text-deep/45">{t('travel.districts.credit')}</p>
      )}
    </div>
  );
}

/** Nothing on the map yet: the two ways to fill it. */
function EmptyMap({ onAdd }: { onAdd: () => void }) {
  const { t } = useTranslation();

  return (
    <div className="pointer-events-none absolute inset-0 z-[500] flex items-center justify-center p-4">
      <div className="pointer-events-auto max-w-sm rounded-3xl bg-white/95 p-6 text-center shadow-xl ring-1 ring-hill/10 backdrop-blur">
        <span className="mx-auto flex h-14 w-14 items-center justify-center rounded-full bg-mist text-hill">
          <MapIcon aria-hidden="true" className="h-7 w-7" />
        </span>
        <p className="mt-3 font-display text-lg font-semibold text-deep">
          {t('travel.empty.title')}
        </p>
        <p className="mt-1 text-sm text-deep/70">{t('travel.empty.body')}</p>
        <div className="mt-4 flex flex-wrap justify-center gap-2">
          <button type="button" className={primaryButtonClass} onClick={onAdd}>
            <Plus aria-hidden="true" className="h-4 w-4" />
            {t('travel.addPlace')}
          </button>
          <Link to="/trips" className={secondaryButtonClass}>
            <Compass aria-hidden="true" className="h-4 w-4" />
            {t('nav.explore')}
          </Link>
        </div>
      </div>
    </div>
  );
}

/** Trips to come, then every visit by year, newest first. Each opens its pin. */
function History({
  places,
  upcoming,
  loading,
  onSelect,
}: {
  places: readonly VisitedPlace[];
  upcoming: readonly UpcomingTrip[];
  loading: boolean;
  onSelect: (key: string) => void;
}) {
  const { t, i18n } = useTranslation();
  const language = toLanguage(i18n.language);

  const entries = places
    .flatMap((place) =>
      place.visits.map((visit) => ({
        placeKey: place.key,
        name: language === 'bn' && place.nameBn ? place.nameBn : place.name,
        slug: place.destinationSlug,
        kind: place.kind,
        // Their own photo from that visit, when they put one on it.
        cover: place.photos.find(
          (photo) =>
            photo.kind === 'Image' &&
            photo.visitId !== null &&
            asNumber(photo.visitId) === asNumber(visit.id),
        )?.url,
        visit,
      })),
    )
    .sort((a, b) => b.visit.visitedOn.localeCompare(a.visit.visitedOn));

  const years = [...new Set(entries.map((entry) => entry.visit.visitedOn.slice(0, 4)))];

  return (
    <section aria-labelledby="history-heading" className={cardClass}>
      <h2 id="history-heading" className="text-lg font-semibold">
        {t('travel.history.title')}
      </h2>

      {loading && (
        <div role="status" className="mt-4 h-40 animate-pulse rounded-xl bg-hill/10">
          <span className="sr-only">{t('common.loading')}</span>
        </div>
      )}

      {upcoming.length > 0 && (
        <div className="mt-4">
          <h3 className="eyebrow">{t('travel.history.comingUp')}</h3>
          <ul className="mt-2 flex flex-col gap-2">
            {upcoming.map((trip) => (
              <li key={String(trip.tripId)}>
                <button
                  type="button"
                  onClick={() => onSelect(upcomingKey(trip))}
                  className="flex w-full items-center gap-3 rounded-xl border-2 border-dashed border-jamdani/40 p-2.5 text-left transition hover:bg-jamdani/5"
                >
                  <CalendarClock aria-hidden="true" className="h-5 w-5 shrink-0 text-jamdani" />
                  <span className="min-w-0">
                    <span className="block truncate text-sm font-semibold text-deep">
                      {language === 'bn' ? trip.destinationNameBn : trip.destinationName}
                    </span>
                    <span className="block truncate text-xs text-deep/60">
                      {formatDateRange(trip.startDate, trip.endDate, language)} · {trip.title}
                    </span>
                  </span>
                </button>
              </li>
            ))}
          </ul>
        </div>
      )}

      {!loading && entries.length === 0 && (
        <p className="mt-3 text-sm text-deep/60">{t('travel.history.empty')}</p>
      )}

      {years.map((year) => (
        <div key={year} className="mt-5">
          <h3 className="eyebrow">{formatCount(Number(year), language).replace(/[,٬]/g, '')}</h3>
          <ol className="mt-2 flex flex-col gap-1 border-l-2 border-hill/15 pl-3">
            {entries
              .filter((entry) => entry.visit.visitedOn.startsWith(year))
              .map((entry) => (
                <li key={String(entry.visit.id)} className="relative">
                  <span
                    aria-hidden="true"
                    className={`absolute -left-[1.15rem] top-4 h-2.5 w-2.5 rounded-full ring-2 ring-white ${
                      entry.visit.source === 'Trip' ? 'bg-hill' : 'bg-turmeric'
                    }`}
                  />
                  <button
                    type="button"
                    onClick={() => onSelect(entry.placeKey)}
                    className="flex w-full items-center gap-3 rounded-xl p-2 text-left transition hover:bg-mist"
                  >
                    {entry.cover ? (
                      <img
                        src={entry.cover}
                        alt=""
                        loading="lazy"
                        className="h-11 w-11 shrink-0 rounded-lg object-cover"
                      />
                    ) : entry.slug ? (
                      <Photo
                        slug={entry.slug}
                        kind={entry.kind ?? 'Hills'}
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
                        {entry.name}
                      </span>
                      <span className="block truncate text-xs text-deep/60">
                        {formatFullDate(entry.visit.visitedOn, language)}
                        {entry.visit.trip ? ` · ${entry.visit.trip.title}` : ''}
                      </span>
                    </span>
                  </button>
                </li>
              ))}
          </ol>
        </div>
      ))}
    </section>
  );
}

/** A trip still to come, opened from its dashed pin. */
function ComingUpDetails({ trip, onBack }: { trip: UpcomingTrip; onBack: () => void }) {
  const { t, i18n } = useTranslation();
  const language = toLanguage(i18n.language);
  const id = asNumber(trip.tripId);
  const place = language === 'bn' ? trip.destinationNameBn : trip.destinationName;

  return (
    <article className={`${cardClass} flex flex-col gap-4 overflow-hidden p-0!`} aria-label={place}>
      <header className="relative isolate h-36 overflow-hidden">
        <Photo
          slug={trip.destinationSlug}
          kind="Hills"
          cut="card"
          decorative
          className="absolute! inset-0 -z-10"
        />
        <div
          aria-hidden="true"
          className="absolute inset-0 -z-10 bg-linear-to-t from-night/85 to-night/10"
        />
        <button
          type="button"
          onClick={onBack}
          className="absolute left-3 top-3 rounded-full bg-white/90 px-3 py-1.5 text-xs font-semibold text-deep shadow transition hover:bg-white"
        >
          ← {t('travel.details.back')}
        </button>
        <div className="absolute inset-x-4 bottom-3">
          <p className="eyebrow text-dusk!">{t('travel.history.comingUp')}</p>
          <h2 className="font-display text-2xl font-bold text-white!">{place}</h2>
        </div>
      </header>
      <div className="flex flex-col gap-3 px-5 pb-5 text-sm">
        <Link to={`/trips/${id}`} className="font-semibold text-hill hover:underline">
          {trip.title}
        </Link>
        <p className="flex items-center gap-1.5 text-deep/70">
          <CalendarDays aria-hidden="true" className="h-4 w-4 text-hill" />
          {formatDateRange(trip.startDate, trip.endDate, language)} ·{' '}
          {trip.asHost ? t('travel.comingUp.hosting') : t('travel.comingUp.going')}
        </p>
        <p className="text-deep/60">{t('travel.comingUp.note')}</p>
        <div className="flex flex-wrap gap-2">
          <Link to={`/trips/${id}/safety`} className={secondaryButtonClass}>
            <Shield aria-hidden="true" className="h-4 w-4" />
            {t('travel.comingUp.safety')}
          </Link>
          <Link to={`/trips/${id}/chat`} className={secondaryButtonClass}>
            {t('chat.open')}
          </Link>
        </div>
      </div>
    </article>
  );
}

/** Whether the map shows on the public profile, and what that shows. */
function SharingCard({ shared, userId }: { shared: boolean; userId: number }) {
  const { t } = useTranslation();
  const sharing = useTravelMapSharing();
  const signedInId = useAuthStore((state) => state.user?.id);

  return (
    <section aria-labelledby="sharing-heading" className={cardClass}>
      <div className="flex items-start justify-between gap-4">
        <div>
          <h2 id="sharing-heading" className="flex items-center gap-2 text-base font-semibold">
            {shared ? (
              <Eye aria-hidden="true" className="h-4 w-4 text-hill" />
            ) : (
              <EyeOff aria-hidden="true" className="h-4 w-4 text-deep/50" />
            )}
            {t('travel.sharing.title')}
          </h2>
          <p className="mt-1 text-sm text-deep/70">{t('travel.sharing.body')}</p>
        </div>
        <button
          type="button"
          role="switch"
          aria-checked={shared}
          aria-labelledby="sharing-heading"
          disabled={sharing.isPending}
          onClick={() => sharing.mutate(!shared)}
          className={`relative h-7 w-12 shrink-0 rounded-full transition disabled:opacity-60 ${
            shared ? 'bg-hill' : 'bg-deep/20'
          }`}
        >
          <span
            aria-hidden="true"
            className={`absolute top-1 h-5 w-5 rounded-full bg-white shadow transition-[left] ${
              shared ? 'left-6' : 'left-1'
            }`}
          />
        </button>
      </div>
      {sharing.isError && (
        <p role="alert" className="mt-2 text-sm text-jamdani">
          {errorText(sharing.error, t)}
        </p>
      )}
      {shared && signedInId === userId && (
        <Link
          to={`/users/${userId}`}
          className="mt-3 inline-block text-sm font-semibold text-hill hover:underline"
        >
          {t('travel.sharing.see')}
        </Link>
      )}
    </section>
  );
}
