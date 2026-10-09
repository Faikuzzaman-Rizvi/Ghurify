import { apiGet } from '@/api/client';
import type { components } from '@/api/schema';

/** All taken from the generated OpenAPI types. Regenerate with `npm run gen:api`. */
export type SiteConfig = components['schemas']['SiteConfig'];
export type SiteThemeConfig = components['schemas']['SiteThemeConfig'];
export type SiteAssetLink = components['schemas']['SiteAssetLink'];
export type SiteSocialLink = components['schemas']['SiteSocialLink'];

/**
 * The site's own configuration. Anonymous, because the header and the footer are painted before
 * anybody has signed in.
 */
export const siteApi = {
  config: (signal?: AbortSignal) =>
    apiGet<SiteConfig>('/api/v1/site/config', {
      ...(signal ? { signal } : {}),
      // No token: this is the same answer for everybody, and asking for one would delay the
      // first paint behind the session being restored.
      authenticated: false,
    }),
};

/** One asset by kind, or undefined if this build does not know that kind. */
export function assetUrl(config: SiteConfig | undefined, kind: string): string | undefined {
  const asset = config?.assets.find((candidate) => candidate.kind === kind);
  return asset && asset.url.length > 0 ? asset.url : undefined;
}
