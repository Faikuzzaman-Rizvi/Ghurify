import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import {
  CircleCheck,
  CircleDashed,
  Compass,
  ExternalLink,
  Hospital,
  MapPin,
  Pencil,
  Phone,
  PhoneOff,
  Plus,
  Shield,
  type LucideIcon,
} from 'lucide-react';

import { asNumber } from '@/api/client';
import { Dialog } from '@/components/Dialog';
import {
  primaryButtonClass,
  secondaryButtonClass,
  SelectField,
  TextField,
} from '@/components/Field';
import { EmptyState, ErrorState } from '@/components/States';
import { useDestinations } from '@/features/trips/useTrips';
import { mapLink } from '@/features/safety/safetyApi';
import { errorText } from '@/lib/errors';
import { formatCount, formatDate, toLanguage, type Language } from '@/lib/format';
import { adminApi, type EmergencyPointView } from './adminApi';
import {
  AdminPageHeader,
  CardGridSkeleton,
  FilterChips,
  StatusPill,
  SummaryTiles,
  ToggleChip,
} from './AdminUi';
import { adminCardClass, smallSecondaryButtonClass, toneTile, type Tone } from './adminStyles';

const kinds = [1, 2, 3] as const;
type Kind = (typeof kinds)[number];

const kindIcon: Record<Kind, LucideIcon> = { 1: Shield, 2: Hospital, 3: Compass };
const kindTone: Record<Kind, Tone> = { 1: 'info', 2: 'bad', 3: 'warn' };

type KindFilter = 'all' | `${Kind}`;

const kindOf = (point: EmergencyPointView): Kind => {
  const kind = asNumber(point.kind);
  return kind === 2 || kind === 3 ? kind : 1;
};

/**
 * The police stations and hospitals offered with every SOS. Seeded positions are approximate;
 * the desk corrects them, adds phone numbers, and marks each one checked.
 *
 * The points are grouped by the destination they serve, because that is how the desk works
 * through them: one place at a time, making sure each has a police station and a hospital
 * that answer the phone.
 */
export function EmergencyPointsPage() {
  const { t, i18n } = useTranslation();
  const language = toLanguage(i18n.language);
  const [editing, setEditing] = useState<EmergencyPointView | 'new' | null>(null);
  const [kind, setKind] = useState<KindFilter>('all');
  const [uncheckedOnly, setUncheckedOnly] = useState(false);

  const points = useQuery({
    queryKey: ['admin', 'emergency-points'],
    queryFn: ({ signal }) => adminApi.emergencyPoints(signal),
  });

  const all = points.data ?? [];
  const unchecked = all.filter((point) => !point.checkedOn).length;
  const withoutPhone = all.filter((point) => !point.phone).length;
  const shown = all.filter(
    (point) =>
      (kind === 'all' || String(kindOf(point)) === kind) && (!uncheckedOnly || !point.checkedOn),
  );
  const groups = groupByDestination(shown, t('admin.points.noDestinationGroup'));

  return (
    <div className="flex flex-col gap-6">
      <AdminPageHeader
        eyebrow={t('admin.groups.safety')}
        title={t('admin.points.title')}
        description={t('admin.points.lead')}
        actions={
          <button type="button" className={primaryButtonClass} onClick={() => setEditing('new')}>
            <Plus aria-hidden="true" className="h-4 w-4" />
            {t('admin.points.add')}
          </button>
        }
      />

      {points.data && all.length > 0 && (
        <>
          <SummaryTiles
            items={[
              {
                label: t('admin.points.stats.total'),
                value: formatCount(all.length, language),
                icon: MapPin,
              },
              {
                label: t('admin.points.stats.checked'),
                value: formatCount(all.length - unchecked, language),
                icon: CircleCheck,
                tone: 'good',
              },
              {
                label: t('admin.points.stats.unchecked'),
                value: formatCount(unchecked, language),
                icon: CircleDashed,
                tone: unchecked > 0 ? 'warn' : 'neutral',
              },
              {
                label: t('admin.points.stats.noPhone'),
                value: formatCount(withoutPhone, language),
                icon: PhoneOff,
                tone: withoutPhone > 0 ? 'bad' : 'neutral',
              },
            ]}
          />

          <div className="flex flex-wrap items-center justify-between gap-3">
            <FilterChips
              label={t('admin.points.kind')}
              value={kind}
              onChange={setKind}
              options={[
                { value: 'all', label: t('admin.ui.all'), count: all.length },
                ...kinds.map((option) => ({
                  value: `${option}` as const,
                  label: t(`admin.points.kinds.${option}`),
                  count: all.filter((point) => kindOf(point) === option).length,
                  tone: kindTone[option],
                })),
              ]}
            />
            <ToggleChip
              pressed={uncheckedOnly}
              onClick={() => setUncheckedOnly((current) => !current)}
              tone={unchecked > 0 ? 'warn' : 'good'}
            >
              {t('admin.points.unchecked', { count: unchecked })}
            </ToggleChip>
          </div>
        </>
      )}

      {points.isPending && <CardGridSkeleton className="h-60" />}
      {points.isError && (
        <ErrorState message={errorText(points.error, t)} onRetry={() => void points.refetch()} />
      )}
      {points.data?.length === 0 && <EmptyState title={t('admin.points.empty')} />}
      {all.length > 0 && shown.length === 0 && <EmptyState title={t('admin.points.noMatches')} />}

      {groups.map((group) => (
        <section
          key={group.key}
          aria-labelledby={`points-${group.key}`}
          className="flex flex-col gap-3"
        >
          <h3
            id={`points-${group.key}`}
            className="flex items-center gap-2 font-display text-lg font-semibold text-deep"
          >
            <MapPin aria-hidden="true" className="h-4 w-4 text-turmeric" />
            {group.name}
            <span className="rounded-full bg-mist px-2 py-0.5 text-xs font-semibold text-deep/60 ring-1 ring-hill/10">
              {formatCount(group.points.length, language)}
            </span>
          </h3>
          <ul className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
            {group.points.map((point) => (
              <li key={String(point.id)} className="flex">
                <PointCard point={point} language={language} onEdit={() => setEditing(point)} />
              </li>
            ))}
          </ul>
        </section>
      ))}

      {editing && (
        <PointDialog
          key={editing === 'new' ? 'new' : String(editing.id)}
          point={editing === 'new' ? null : editing}
          onClose={() => setEditing(null)}
        />
      )}
    </div>
  );
}

/** The points in the order the list gives them, gathered under the destination each serves. */
function groupByDestination(points: readonly EmergencyPointView[], unassigned: string) {
  const groups = new Map<string, { key: string; name: string; points: EmergencyPointView[] }>();
  for (const point of points) {
    const key = point.destinationSlug ?? 'none';
    const group = groups.get(key) ?? {
      key,
      name: point.destinationName ?? unassigned,
      points: [],
    };
    group.points.push(point);
    groups.set(key, group);
  }
  // Points that serve no destination go last.
  return [...groups.values()].sort((a, b) => Number(a.key === 'none') - Number(b.key === 'none'));
}

/** One police station or hospital: what it is, how to reach it, and whether it was checked. */
function PointCard({
  point,
  language,
  onEdit,
}: {
  point: EmergencyPointView;
  language: Language;
  onEdit: () => void;
}) {
  const { t } = useTranslation();
  const kind = kindOf(point);
  const Icon = kindIcon[kind];
  const name = language === 'bn' ? point.nameBn : point.name;
  const otherName = language === 'bn' ? point.name : point.nameBn;
  const latitude = asNumber(point.latitude);
  const longitude = asNumber(point.longitude);

  return (
    <article className={`${adminCardClass} p-5`}>
      <div className="flex items-start gap-3.5">
        <span
          aria-hidden="true"
          className={`flex h-12 w-12 shrink-0 items-center justify-center rounded-2xl ${toneTile[kindTone[kind]]}`}
        >
          <Icon className="h-6 w-6" />
        </span>
        <div className="min-w-0">
          <p className="text-xs font-bold uppercase tracking-wider text-deep/50 [:lang(bn)_&]:tracking-normal">
            {t(`admin.points.kinds.${kind}`)}
          </p>
          <h4 className="mt-0.5 font-display text-base font-semibold leading-snug text-deep">
            {name}
          </h4>
          {otherName && otherName !== name && (
            <p lang={language === 'bn' ? 'en' : 'bn'} className="text-sm text-deep/55">
              {otherName}
            </p>
          )}
        </div>
      </div>

      <dl className="mb-5 mt-4 flex flex-col gap-2 text-sm">
        <div className="flex items-center gap-2.5">
          <dt className="sr-only">{t('admin.points.phone')}</dt>
          {point.phone ? (
            <Phone aria-hidden="true" className="h-4 w-4 shrink-0 text-hill" />
          ) : (
            <PhoneOff aria-hidden="true" className="h-4 w-4 shrink-0 text-jamdani/70" />
          )}
          <dd>
            {point.phone ? (
              <a
                href={`tel:${point.phone}`}
                className="font-semibold text-hill underline-offset-4 hover:underline"
              >
                {point.phone}
              </a>
            ) : (
              <span className="text-jamdani/80">{t('admin.points.noPhone')}</span>
            )}
          </dd>
        </div>
        <div className="flex items-center gap-2.5">
          <dt className="sr-only">{t('admin.points.position')}</dt>
          <MapPin aria-hidden="true" className="h-4 w-4 shrink-0 text-hill" />
          <dd className="flex flex-wrap items-center gap-x-2">
            <span className="font-mono text-xs text-deep/60">
              {latitude.toFixed(4)}, {longitude.toFixed(4)}
            </span>
            <a
              href={mapLink(point.latitude, point.longitude)}
              target="_blank"
              rel="noreferrer"
              className="inline-flex items-center gap-1 font-semibold text-hill underline-offset-4 hover:underline"
            >
              {t('admin.sos.map')}
              <ExternalLink aria-hidden="true" className="h-3.5 w-3.5" />
            </a>
          </dd>
        </div>
      </dl>

      <div className="mt-auto flex flex-wrap items-center justify-between gap-2 border-t border-hill/8 pt-4">
        {point.checkedOn ? (
          <StatusPill tone="good" icon={CircleCheck}>
            {t('admin.points.checkedOn', {
              name: point.checkedBy ?? '',
              date: formatDate(point.checkedOn, language),
            })}
          </StatusPill>
        ) : (
          <StatusPill tone="warn" icon={CircleDashed}>
            {t('admin.points.notChecked')}
          </StatusPill>
        )}
        <button type="button" className={smallSecondaryButtonClass} onClick={onEdit}>
          <Pencil aria-hidden="true" className="h-4 w-4" />
          {t('admin.points.edit')}
        </button>
      </div>
    </article>
  );
}

function PointDialog({
  point,
  onClose,
}: {
  point: EmergencyPointView | null;
  onClose: () => void;
}) {
  const { t, i18n } = useTranslation();
  const language = toLanguage(i18n.language);
  const queryClient = useQueryClient();
  const destinations = useDestinations();
  const [form, setForm] = useState({
    destinationSlug: point?.destinationSlug ?? '',
    kind: point ? asNumber(point.kind) : 1,
    name: point?.name ?? '',
    nameBn: point?.nameBn ?? '',
    phone: point?.phone ?? '',
    latitude: point ? String(point.latitude) : '',
    longitude: point ? String(point.longitude) : '',
    checked: point ? point.checkedOn !== null : false,
  });

  const save = useMutation({
    mutationFn: () =>
      adminApi.saveEmergencyPoint({
        id: point ? asNumber(point.id) : null,
        destinationSlug: form.destinationSlug || null,
        kind: form.kind,
        name: form.name,
        nameBn: form.nameBn,
        phone: form.phone.trim() || null,
        latitude: Number(form.latitude),
        longitude: Number(form.longitude),
        checked: form.checked,
      }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['admin', 'emergency-points'] });
      onClose();
    },
  });

  const set = (key: keyof typeof form) => (event: { target: { value: string } }) =>
    setForm((current) => ({ ...current, [key]: event.target.value }));

  return (
    <Dialog
      open
      title={point ? t('admin.points.editTitle') : t('admin.points.add')}
      onClose={onClose}
    >
      <form
        className="grid gap-3 sm:grid-cols-2"
        onSubmit={(event) => {
          event.preventDefault();
          save.mutate();
        }}
      >
        <SelectField
          id="point-kind"
          label={t('admin.points.kind')}
          value={form.kind}
          onChange={(event) =>
            setForm((current) => ({ ...current, kind: Number(event.target.value) }))
          }
        >
          {kinds.map((kind) => (
            <option key={kind} value={kind}>
              {t(`admin.points.kinds.${kind}`)}
            </option>
          ))}
        </SelectField>
        <SelectField
          id="point-destination"
          label={t('admin.points.destination')}
          value={form.destinationSlug}
          onChange={set('destinationSlug')}
        >
          <option value="">{t('admin.points.noDestination')}</option>
          {(destinations.data ?? []).map((destination) => (
            <option key={destination.slug} value={destination.slug}>
              {language === 'bn' ? destination.nameBn : destination.name}
            </option>
          ))}
        </SelectField>
        <TextField
          id="point-name"
          label={t('admin.points.name')}
          value={form.name}
          maxLength={150}
          onChange={set('name')}
        />
        <TextField
          id="point-name-bn"
          label={t('admin.points.nameBn')}
          value={form.nameBn}
          maxLength={150}
          lang="bn"
          onChange={set('nameBn')}
        />
        <TextField
          id="point-phone"
          label={t('admin.points.phone')}
          value={form.phone}
          inputMode="tel"
          maxLength={20}
          onChange={set('phone')}
        />
        <div className="grid grid-cols-2 gap-2">
          <TextField
            id="point-lat"
            label={t('admin.points.latitude')}
            value={form.latitude}
            inputMode="decimal"
            onChange={set('latitude')}
          />
          <TextField
            id="point-long"
            label={t('admin.points.longitude')}
            value={form.longitude}
            inputMode="decimal"
            onChange={set('longitude')}
          />
        </div>
        <label className="flex items-start gap-2.5 rounded-2xl bg-mist p-3.5 text-sm text-deep ring-1 ring-hill/10 sm:col-span-2">
          <input
            type="checkbox"
            checked={form.checked}
            onChange={(event) =>
              setForm((current) => ({ ...current, checked: event.target.checked }))
            }
            className="mt-1 accent-hill"
          />
          {t('admin.points.confirmChecked')}
        </label>
        {save.isError && (
          <p role="alert" className="text-sm text-jamdani sm:col-span-2">
            {errorText(save.error, t)}
          </p>
        )}
        <div className="flex justify-end gap-2 sm:col-span-2">
          <button type="button" className={secondaryButtonClass} onClick={onClose}>
            {t('common.cancel')}
          </button>
          <button type="submit" className={primaryButtonClass} disabled={save.isPending}>
            {save.isPending ? t('common.saving') : t('common.save')}
          </button>
        </div>
      </form>
    </Dialog>
  );
}
