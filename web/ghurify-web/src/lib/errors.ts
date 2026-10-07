import type { TFunction } from 'i18next';
import { ApiError } from '@/api/client';

/**
 * The text to show for a failed request. The API sends a stable `code` with every expected
 * failure (`nid_in_use`, `seat_taken`); when there is a translation for it under `errors.*`,
 * that wins, so the message is in the reader's language. Otherwise a generic line by status.
 */
export function errorText(error: unknown, t: TFunction): string {
  if (error instanceof ApiError) {
    if (error.code) {
      const key = `errors.${error.code}`;
      const translated = t(key);
      if (translated !== key) {
        return translated;
      }
    }

    switch (error.status) {
      case 400:
        return t('errors.invalid');
      case 401:
        return t('errors.signedOut');
      case 403:
        return t('errors.forbidden');
      case 404:
        return t('errors.notFound');
      case 409:
        return t('errors.conflict');
      case 429:
        return t('errors.tooMany');
    }
  }

  return t('common.error');
}
