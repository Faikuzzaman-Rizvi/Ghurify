import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { History, RotateCcw } from 'lucide-react';

import { inputClass } from '@/components/Field';
import { Select } from '@/components/ui/Select';
import { adminApi, type SettingView } from './adminApi';

/**
 * One setting, edited.
 *
 * The control follows the setting's kind, which the API declares — a colour gets a swatch and a
 * text box side by side, a font gets the list of families the backend will actually load, and
 * everything else gets a text field of the right length. So a setting added to the backend
 * catalogue appears here, correctly edited, without a frontend release.
 *
 * Each field carries two small affordances the panel would be frustrating without: what it would
 * go back to, and what it used to be.
 */
export function SettingField({
  setting,
  value,
  fonts,
  disabled,
  onChange,
  onReset,
}: {
  setting: SettingView;
  value: string;
  /** The families on offer, for a font setting. */
  fonts?: readonly string[];
  disabled: boolean;
  onChange: (value: string) => void;
  onReset: () => void;
}) {
  const { t } = useTranslation();
  const [showHistory, setShowHistory] = useState(false);
  const id = `setting-${setting.key.replace(/\./g, '-')}`;

  const label = t(`admin.settingKeys.${setting.key}`, { defaultValue: setting.key });
  const hint = t(`admin.settingHints.${setting.key}`, { defaultValue: '' });

  return (
    <div className="flex min-w-0 flex-col gap-1.5">
      <div className="flex items-baseline justify-between gap-2">
        <label htmlFor={id} className="text-sm font-semibold text-deep">
          {label}
          {setting.required && <span aria-hidden="true" className="ml-1 text-jamdani">*</span>}
        </label>

        <div className="flex items-center gap-1">
          {setting.isCustom && !disabled && (
            <button
              type="button"
              onClick={onReset}
              className="flex items-center gap-1 rounded-full px-2 py-0.5 text-xs font-medium text-deep/60 transition hover:bg-mist hover:text-deep"
            >
              <RotateCcw aria-hidden="true" className="h-3 w-3" />
              {t('admin.settings.reset')}
            </button>
          )}
          {setting.isCustom && (
            <button
              type="button"
              onClick={() => setShowHistory((open) => !open)}
              aria-expanded={showHistory}
              className="flex items-center gap-1 rounded-full px-2 py-0.5 text-xs font-medium text-deep/60 transition hover:bg-mist hover:text-deep"
            >
              <History aria-hidden="true" className="h-3 w-3" />
              {t('admin.settings.history')}
            </button>
          )}
        </div>
      </div>

      {setting.kind === 'Colour' ? (
        <ColourInput id={id} value={value} disabled={disabled} onChange={onChange} />
      ) : setting.kind === 'Font' ? (
        <Select
          id={id}
          value={value}
          onChange={onChange}
          disabled={disabled}
          options={(fonts ?? []).map((family) => ({ value: family, label: family }))}
          buttonClassName={`${inputClass} cursor-pointer`}
        />
      ) : setting.kind === 'LongText' ? (
        <textarea
          id={id}
          rows={2}
          value={value}
          disabled={disabled}
          maxLength={Number(setting.maxLength)}
          onChange={(event) => onChange(event.target.value)}
          className={`${inputClass} resize-y disabled:bg-mist disabled:text-deep/50`}
        />
      ) : (
        <input
          id={id}
          type={inputTypeFor(setting.kind)}
          value={value}
          disabled={disabled}
          maxLength={Number(setting.maxLength)}
          placeholder={setting.default}
          onChange={(event) => onChange(event.target.value)}
          className={`${inputClass} disabled:bg-mist disabled:text-deep/50`}
        />
      )}

      {hint && <p className="text-xs text-deep/60">{hint}</p>}

      {!setting.isCustom && setting.default.length > 0 && setting.kind !== 'Colour' && (
        <p className="text-xs text-deep/50">{t('admin.settings.usingDefault')}</p>
      )}

      {showHistory && <SettingHistory settingKey={setting.key} />}
    </div>
  );
}

/** A swatch and the hex beside it, each editable and kept in step with the other. */
function ColourInput({
  id,
  value,
  disabled,
  onChange,
}: {
  id: string;
  value: string;
  disabled: boolean;
  onChange: (value: string) => void;
}) {
  const { t } = useTranslation();
  // The native picker only understands #rrggbb; a half-typed value must not reach it.
  const valid = /^#[0-9a-fA-F]{6}$/.test(value);

  return (
    <div className="flex items-center gap-2">
      <input
        type="color"
        value={valid ? value : '#000000'}
        disabled={disabled}
        aria-label={t('admin.settings.pickColour')}
        onChange={(event) => onChange(event.target.value)}
        className="h-11 w-14 shrink-0 cursor-pointer rounded-xl border border-hill/20 bg-white p-1 disabled:cursor-default"
      />
      <input
        id={id}
        value={value}
        disabled={disabled}
        maxLength={7}
        spellCheck={false}
        onChange={(event) => onChange(event.target.value.toLowerCase())}
        className={`${inputClass} font-mono disabled:bg-mist disabled:text-deep/50`}
      />
    </div>
  );
}

/** What this setting used to be, and who changed it. */
function SettingHistory({ settingKey }: { settingKey: string }) {
  const { t, i18n } = useTranslation();

  const history = useQuery({
    queryKey: ['admin', 'settings', 'history', settingKey],
    queryFn: ({ signal }) => adminApi.settingHistory(settingKey, signal),
  });

  const when = (iso: string) =>
    new Intl.DateTimeFormat(i18n.language, {
      dateStyle: 'medium',
      timeStyle: 'short',
      timeZone: 'Asia/Dhaka',
    }).format(new Date(iso));

  if (history.isPending) {
    return (
      <p role="status" className="text-xs text-deep/50">
        {t('common.loading')}
      </p>
    );
  }

  if (history.isError || history.data.length === 0) {
    return <p className="text-xs text-deep/50">{t('admin.settings.noHistory')}</p>;
  }

  return (
    <ol className="flex flex-col gap-1 rounded-xl bg-mist p-2.5 text-xs text-deep/70">
      {history.data.map((change) => (
        <li key={String(change.id)}>
          <span className="line-through">{change.oldValue ?? t('admin.settings.theDefault')}</span>
          {' → '}
          <span className="font-medium text-deep">
            {change.newValue ?? t('admin.settings.theDefault')}
          </span>
          <span className="text-deep/50">
            {' · '}
            {change.changedByName ?? t('admin.noName')} · {when(change.created)}
          </span>
        </li>
      ))}
    </ol>
  );
}

function inputTypeFor(kind: SettingView['kind']): string {
  switch (kind) {
    case 'EmailAddress':
      return 'email';
    case 'Url':
      return 'url';
    case 'Phone':
      return 'tel';
    default:
      return 'text';
  }
}
