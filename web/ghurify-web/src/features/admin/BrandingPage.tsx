import { useTranslation } from 'react-i18next';

import { cardClass, primaryButtonClass, secondaryButtonClass } from '@/components/Field';
import { ErrorState } from '@/components/States';
import { errorText } from '@/lib/errors';
import { SettingField } from './SettingField';
import { SiteAssetField } from './SiteAssetField';
import { useSettingsForm } from './useSettingsForm';

/** The groups this screen owns: the words the site says about itself, and how to reach it. */
const groups = ['Identity', 'Contact', 'Social'] as const;

/**
 * What the site is called, what it says about itself, how to reach it, and its logo and icons.
 *
 * Every field here is read by the live site — the header, the tab title, the footer, the link
 * preview — so changing one is visible on the next page load with no release and no deploy.
 */
export function BrandingPage() {
  const { t } = useTranslation();
  const form = useSettingsForm(groups);

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

  const readOnly = !form.settings.data.editableGroups.includes('Identity');

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
          {t('admin.branding.title')}
        </h2>
        <p className="mt-1 max-w-2xl text-sm text-deep/70">{t('admin.branding.lead')}</p>
      </header>

      {readOnly && (
        <p className="rounded-xl bg-turmeric/10 px-4 py-3 text-sm font-medium text-ochre">
          {t('admin.settings.readOnly')}
        </p>
      )}

      {groups.map((group) => (
        <section key={group} className={`${cardClass} flex flex-col gap-4`}>
          <div>
            <h3 className="font-display text-lg font-semibold text-deep">
              {t(`admin.settingGroups.${group}`)}
            </h3>
            <p className="mt-0.5 text-sm text-deep/60">{t(`admin.settingGroupHints.${group}`)}</p>
          </div>

          <div className="grid gap-4 sm:grid-cols-2">
            {form.mine
              .filter((setting) => setting.group === group)
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
        </section>
      ))}

      <section className={`${cardClass} flex flex-col gap-4`}>
        <div>
          <h3 className="font-display text-lg font-semibold text-deep">
            {t('admin.branding.images')}
          </h3>
          <p className="mt-0.5 text-sm text-deep/60">{t('admin.branding.imagesHint')}</p>
        </div>

        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {form.settings.data.assets.map((asset) => (
            <SiteAssetField key={asset.kind} asset={asset} disabled={readOnly} />
          ))}
        </div>
      </section>

      {form.save.isError && (
        <p role="alert" className={`${cardClass} text-sm font-medium text-jamdani`}>
          {errorText(form.save.error, t)}
        </p>
      )}

      {/* Sticky, because the colour and branding forms are long and the save button should never
          be somewhere you have to go looking for. */}
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
          disabled={!form.dirty || form.save.isPending || readOnly}
          className={`${primaryButtonClass} disabled:opacity-60`}
        >
          {form.save.isPending ? t('common.saving') : t('common.save')}
        </button>
      </div>
    </form>
  );
}
