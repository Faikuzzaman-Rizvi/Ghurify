import { useEffect } from 'react';
import { useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';

import { toLanguage } from '@/lib/format';
import { applyIdentity, applyTheme } from './applySiteConfig';
import { siteApi, type SiteConfig } from './siteApi';

/** One key, so the panel can refetch exactly what the whole app reads. */
export const siteConfigKey = ['site', 'config'] as const;

/**
 * The site's configuration.
 *
 * Shared by everything that renders the brand — the header, the footer, the logo — through one
 * query, so it is fetched once per load however many components ask for it. Kept fresh for a
 * minute: a change made in the panel should reach an open tab without a reload, but the site's
 * name does not need polling.
 */
export function useSiteConfig() {
  return useQuery({
    queryKey: siteConfigKey,
    queryFn: ({ signal }) => siteApi.config(signal),
    staleTime: 60_000,
    // The site has to render even if this call fails: every reader falls back to what the app
    // shipped with, so there is nothing to retry for and nothing to show an error for.
    retry: 1,
  });
}

/**
 * Loads the configuration once and applies it to the document: the colours, the fonts, the tab
 * title and the icons. Mounted at the root of the app, above the router.
 *
 * Until it has loaded, the page shows what the app shipped with — the stylesheet's own tokens
 * and index.html's title and icon — so nothing flashes blank.
 */
export function useAppliedSiteConfig(): { config: SiteConfig | undefined; isPending: boolean } {
  const { data: config, isPending } = useSiteConfig();
  const { i18n } = useTranslation();
  const language = toLanguage(i18n.language);

  useEffect(() => {
    if (config) {
      applyTheme(config.theme);
    }
  }, [config]);

  // The name and description are per language, so this re-runs when somebody switches.
  useEffect(() => {
    if (config) {
      applyIdentity(config, language);
    }
  }, [config, language]);

  return { config, isPending };
}

/** The site's name in the reader's language, falling back to what the app shipped with. */
export function useSiteName(): string {
  const { data: config } = useSiteConfig();
  const { t, i18n } = useTranslation();

  if (!config) {
    return t('app.name');
  }

  return toLanguage(i18n.language) === 'bn' ? config.identity.nameBn : config.identity.name;
}
