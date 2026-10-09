import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { siteConfigKey } from '@/features/site/useSiteConfig';
import { adminApi, type SettingView, type SettingsView } from './adminApi';

/**
 * The shared machinery behind the branding and theme screens: load every setting, hold the
 * edits, and save the ones that actually changed.
 *
 * Only changed keys are sent. The API treats an absent key as "leave it alone" and an empty
 * value as "back to the shipped default", so a screen can reset one field without touching its
 * neighbours, and saving an untouched form is a no-op all the way down to the database.
 */
export function useSettingsForm(groups: readonly SettingsView['settings'][number]['group'][]) {
  const queryClient = useQueryClient();
  const [edits, setEdits] = useState<Record<string, string>>({});

  const settings = useQuery({
    queryKey: ['admin', 'settings'],
    queryFn: ({ signal }) => adminApi.settings(signal),
  });

  const save = useMutation({
    mutationFn: (changes: Record<string, string>) => adminApi.saveSettings(changes),
    onSuccess: () => {
      setEdits({});
      // The panel's own copy, and the configuration the whole app renders from.
      void queryClient.invalidateQueries({ queryKey: ['admin', 'settings'] });
      void queryClient.invalidateQueries({ queryKey: siteConfigKey });
    },
  });

  /** The settings these screens care about, in catalogue order. */
  const mine = useMemo(
    () => (settings.data?.settings ?? []).filter((setting) => groups.includes(setting.group)),
    [settings.data, groups],
  );

  /** What each field shows: the edit if there is one, otherwise what is in force. */
  const valueOf = (setting: SettingView) => edits[setting.key] ?? setting.value;

  /** Every value in force, with the edits over the top: what a live preview renders. */
  const previewValues = useMemo(() => {
    const values: Record<string, string> = {};
    for (const setting of settings.data?.settings ?? []) {
      values[setting.key] = edits[setting.key] ?? setting.value;
    }
    return values;
  }, [settings.data, edits]);

  const set = (key: string, value: string) =>
    setEdits((current) => ({ ...current, [key]: value }));

  /** Puts one field back to the shipped default, which is an empty value to the API. */
  const reset = (setting: SettingView) => set(setting.key, '');

  const dirty = Object.keys(edits).some(
    (key) => edits[key] !== settings.data?.settings.find((setting) => setting.key === key)?.value,
  );

  const editable = (setting: SettingView) =>
    (settings.data?.editableGroups ?? []).includes(setting.group);

  return {
    settings,
    mine,
    valueOf,
    previewValues,
    set,
    reset,
    discard: () => setEdits({}),
    dirty,
    editable,
    save,
    submit: () => save.mutate(edits),
  };
}
