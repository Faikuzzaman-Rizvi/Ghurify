import { useEffect, useMemo, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import { BadgeCheck, Siren, TriangleAlert } from 'lucide-react';

import { cardClass, primaryButtonClass, secondaryButtonClass } from '@/components/Field';
import { ErrorState } from '@/components/States';
import { errorText } from '@/lib/errors';
import { contrast, contrastProblems } from './contrast';
import { SettingField } from './SettingField';
import { useSettingsForm } from './useSettingsForm';

const groups = ['Theme'] as const;

/**
 * The site's colours and type.
 *
 * The preview below the pickers is the point of this screen. It is painted with the colours as
 * they would be, in the same combinations the real site uses, so a choice that cannot be read is
 * visible before it is saved rather than after. The same pairs are checked numerically beside
 * it, and the API refuses the save if any of them fall below what WCAG AA asks for — because the
 * person who makes the site unreadable may be the only one who could put it back.
 */
export function ThemePage() {
  const { t } = useTranslation();
  const form = useSettingsForm(groups);

  const colours = useMemo(() => {
    const picked: Record<string, string> = {};
    for (const [key, value] of Object.entries(form.previewValues)) {
      if (key.startsWith('theme.colour.')) {
        picked[key.slice('theme.colour.'.length)] = value;
      }
    }
    return picked;
  }, [form.previewValues]);

  const problems = useMemo(() => contrastProblems(colours), [colours]);

  if (form.settings.isPending) {
    return (
      <div role="status" className="h-96 animate-pulse rounded-2xl bg-hill/10">
        <span className="sr-only">{t('common.loading')}</span>
      </div>
    );
  }

  if (form.settings.isError) {
    return (
      <ErrorState
        message={errorText(form.settings.error, t)}
        onRetry={() => void form.settings.refetch()}
      />
    );
  }

  const { data } = form.settings;
  const readOnly = !data.editableGroups.includes('Theme');

  return (
    <form
      className="flex flex-col gap-5"
      onSubmit={(event) => {
        event.preventDefault();
        form.submit();
      }}
    >
      <header>
        <h2 className="font-display text-2xl font-semibold text-deep sm:text-[1.75rem]">
          {t('admin.theme.title')}
        </h2>
        <p className="mt-1 max-w-2xl text-sm text-deep/70">{t('admin.theme.lead')}</p>
      </header>

      {readOnly && (
        <p className="rounded-xl bg-turmeric/10 px-4 py-3 text-sm font-medium text-ochre">
          {t('admin.settings.readOnly')}
        </p>
      )}

      <div className="grid gap-5 lg:grid-cols-2">
        <section className={`${cardClass} flex flex-col gap-4`}>
          <div>
            <h3 className="font-display text-lg font-semibold text-deep">
              {t('admin.theme.colours')}
            </h3>
            <p className="mt-0.5 text-sm text-deep/60">{t('admin.theme.coloursHint')}</p>
          </div>

          <div className="grid gap-4 sm:grid-cols-2">
            {form.mine
              .filter((setting) => setting.kind === 'Colour')
              .map((setting) => (
                <SettingField
                  key={setting.key}
                  setting={setting}
                  value={form.valueOf(setting)}
                  disabled={!form.editable(setting)}
                  onChange={(value) => form.set(setting.key, value)}
                  onReset={() => form.reset(setting)}
                />
              ))}
          </div>

          <div>
            <h3 className="font-display text-lg font-semibold text-deep">
              {t('admin.theme.fonts')}
            </h3>
            <p className="mt-0.5 text-sm text-deep/60">{t('admin.theme.fontsHint')}</p>
          </div>

          <div className="grid gap-4 sm:grid-cols-2">
            {form.mine
              .filter((setting) => setting.kind === 'Font')
              .map((setting) => (
                <SettingField
                  key={setting.key}
                  setting={setting}
                  value={form.valueOf(setting)}
                  fonts={
                    setting.key.endsWith('display') ? data.displayFonts : data.bodyFonts
                  }
                  disabled={!form.editable(setting)}
                  onChange={(value) => form.set(setting.key, value)}
                  onReset={() => form.reset(setting)}
                />
              ))}
          </div>
        </section>

        <div className="flex flex-col gap-5">
          <ThemePreview colours={colours} />
          <ContrastReport problems={problems} />
        </div>
      </div>

      {form.save.isError && (
        <p role="alert" className={`${cardClass} text-sm font-medium text-jamdani`}>
          {errorText(form.save.error, t)}
        </p>
      )}

      <div className="sticky bottom-0 -mx-4 flex flex-wrap items-center justify-end gap-2 border-t border-hill/10 bg-white/90 px-4 py-3 backdrop-blur sm:-mx-6 sm:px-6">
        {form.dirty && (
          <p className="mr-auto text-sm font-medium text-ochre">{t('admin.settings.unsaved')}</p>
        )}
        <button
          type="button"
          onClick={form.discard}
          disabled={!form.dirty}
          className={`${secondaryButtonClass} disabled:opacity-50`}
        >
          {t('admin.settings.discard')}
        </button>
        <button
          type="submit"
          // The API refuses an unreadable theme anyway; stopping it here saves the round trip
          // and says why in the same place the colours are being chosen.
          disabled={!form.dirty || form.save.isPending || readOnly || problems.length > 0}
          className={`${primaryButtonClass} disabled:opacity-60`}
        >
          {form.save.isPending ? t('common.saving') : t('common.save')}
        </button>
      </div>
    </form>
  );
}

/**
 * The site in miniature, painted with the colours being chosen.
 *
 * Scoped to this one element rather than the whole document, so the admin portal around it stays
 * readable while a bad combination is being tried. That matters: a preview that recoloured the
 * page would also recolour the controls needed to undo it.
 */
function ThemePreview({ colours }: { colours: Record<string, string> }) {
  const { t } = useTranslation();
  const scope = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const element = scope.current;
    if (!element) return;

    for (const [token, value] of Object.entries(colours)) {
      // Only a real colour; a half-typed hex would leave the preview mid-edit.
      if (/^#[0-9a-fA-F]{6}$/.test(value)) {
        element.style.setProperty(`--color-${token}`, value);
      }
    }
  }, [colours]);

  return (
    <section className={`${cardClass} flex flex-col gap-3`}>
      <h3 className="font-display text-lg font-semibold text-deep">{t('admin.theme.preview')}</h3>

      <div ref={scope} className="overflow-hidden rounded-xl border border-hill/15">
        <div className="bg-white p-4">
          <p className="font-display text-lg font-bold text-deep">{t('admin.theme.sampleTitle')}</p>
          <p className="mt-1 text-sm text-deep/70">{t('admin.theme.sampleBody')}</p>

          <div className="mt-3 flex flex-wrap items-center gap-2">
            <span className="rounded-full bg-hill px-4 py-2 text-sm font-semibold text-white">
              {t('admin.theme.samplePrimary')}
            </span>
            <span className="rounded-full bg-turmeric px-3 py-1.5 text-xs font-bold text-night">
              {t('admin.theme.sampleBadge')}
            </span>
            <span className="text-sm font-semibold text-ochre">
              {t('admin.theme.sampleAccent')}
            </span>
            <span className="flex items-center gap-1 text-sm font-medium text-jamdani">
              <TriangleAlert aria-hidden="true" className="h-4 w-4" />
              {t('admin.theme.sampleWarning')}
            </span>
          </div>
        </div>

        <div className="bg-mist p-4">
          <p className="font-display font-semibold text-deep">{t('admin.theme.sampleSection')}</p>
          <p className="mt-1 flex items-center gap-1.5 text-sm text-hill">
            <BadgeCheck aria-hidden="true" className="h-4 w-4" />
            {t('admin.theme.sampleLink')}
          </p>
        </div>

        <div className="flex items-center gap-2 bg-night p-4 text-sm text-white">
          <Siren aria-hidden="true" className="h-4 w-4 text-turmeric" />
          {t('admin.theme.sampleFooter')}
        </div>
      </div>
    </section>
  );
}

/** The same pairs the API checks, with their measured ratios, so a refusal is never a surprise. */
function ContrastReport({ problems }: { problems: ReturnType<typeof contrastProblems> }) {
  const { t } = useTranslation();

  if (problems.length === 0) {
    return (
      <p className="flex items-center gap-2 rounded-xl bg-emerald-50 px-4 py-3 text-sm font-medium text-emerald-800">
        <BadgeCheck aria-hidden="true" className="h-5 w-5 shrink-0" />
        {t('admin.theme.contrastOk')}
      </p>
    );
  }

  return (
    <div className="flex flex-col gap-2 rounded-xl bg-jamdani/10 px-4 py-3">
      <p className="flex items-center gap-2 text-sm font-semibold text-jamdani">
        <TriangleAlert aria-hidden="true" className="h-5 w-5 shrink-0" />
        {t('admin.theme.contrastProblems', { count: problems.length })}
      </p>
      <ul className="flex flex-col gap-1 text-sm text-deep/80">
        {problems.map((problem) => (
          <li key={`${problem.token}-${problem.where}`}>
            {t('admin.theme.contrastLine', {
              what: t(`admin.theme.where.${problem.where}`),
              ratio: contrast.format(problem.ratio),
              minimum: contrast.format(problem.minimum),
            })}
          </li>
        ))}
      </ul>
    </div>
  );
}
