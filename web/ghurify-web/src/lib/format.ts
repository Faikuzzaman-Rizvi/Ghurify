/**
 * The one place money and dates are turned into text, so every screen shows them the same way:
 * `Tk 6,800` in English, `৳৬,৮০০` in Bangla, and calendar dates as they fall in Dhaka.
 */

export type Language = 'bn' | 'en';

/** Normalises i18next's language ("en-US", "bn") to the two the app supports. */
export function toLanguage(language: string | undefined): Language {
  return language?.startsWith('en') ? 'en' : 'bn';
}

const moneyFormatters: Record<Language, Intl.NumberFormat> = {
  // en-IN groups by lakh (1,00,000), which is how amounts are written in Bangladesh.
  en: new Intl.NumberFormat('en-IN', { maximumFractionDigits: 0 }),
  bn: new Intl.NumberFormat('bn-BD', { maximumFractionDigits: 0 }),
};

/** `Tk 6,800` (en) or `৳৬,৮০০` (bn). The API sends decimals as number or string. */
export function formatMoney(amount: number | string, language: Language): string {
  const value = typeof amount === 'number' ? amount : Number(amount);
  const digits = moneyFormatters[language].format(value);
  return language === 'en' ? `Tk ${digits}` : `৳${digits}`;
}

/** Digits in the reader's script: 12 or ১২. */
export function formatCount(value: number | string, language: Language): string {
  return moneyFormatters[language].format(typeof value === 'number' ? value : Number(value));
}

/**
 * A trip date arrives as "2026-10-14": a calendar day in Bangladesh, not an instant. It is read
 * as UTC midnight and formatted in UTC so no browser time zone can shift it to the day before.
 */
function parseDate(isoDate: string): Date {
  const [year = 1970, month = 1, day = 1] = isoDate.split('-').map(Number);
  return new Date(Date.UTC(year, month - 1, day));
}

const locale: Record<Language, string> = { en: 'en-GB', bn: 'bn-BD' };

export function formatDate(isoDate: string, language: Language): string {
  return new Intl.DateTimeFormat(locale[language], {
    weekday: 'short',
    day: 'numeric',
    month: 'short',
    timeZone: 'UTC',
  }).format(parseDate(isoDate));
}

export function formatDateRange(start: string, end: string, language: Language): string {
  if (start === end) {
    return formatDate(start, language);
  }

  const format = new Intl.DateTimeFormat(locale[language], {
    day: 'numeric',
    month: 'short',
    timeZone: 'UTC',
  });

  return `${format.format(parseDate(start))} – ${format.format(parseDate(end))}`;
}

/** "12 Mar 2026": a day in someone's travel history, where the year matters. */
export function formatFullDate(isoDate: string, language: Language): string {
  return new Intl.DateTimeFormat(locale[language], {
    day: 'numeric',
    month: 'short',
    year: 'numeric',
    timeZone: 'UTC',
  }).format(parseDate(isoDate));
}

/** "October 2026": for how long someone has been a member. */
export function formatMonthYear(isoDate: string, language: Language): string {
  return new Intl.DateTimeFormat(locale[language], {
    month: 'long',
    year: 'numeric',
    timeZone: 'UTC',
  }).format(parseDate(isoDate));
}

/** Days a trip lasts, counting both the first and the last. */
export function tripDays(start: string, end: string): number {
  const millisecondsPerDay = 86_400_000;
  return (
    Math.round((parseDate(end).getTime() - parseDate(start).getTime()) / millisecondsPerDay) + 1
  );
}

/**
 * An instant from the API ("2026-10-07T09:30:00Z") as the date and time it was in Dhaka:
 * `7 Oct 2026, 15:30` (en) or its Bangla equivalent. Money moves are shown to the minute.
 */
export function formatDateTime(instant: string, language: Language): string {
  return new Intl.DateTimeFormat(locale[language], {
    dateStyle: 'medium',
    timeStyle: 'short',
    timeZone: 'Asia/Dhaka',
  }).format(new Date(instant));
}

/** Today's date in Dhaka as "YYYY-MM-DD", for date inputs. */
export function todayInDhaka(): string {
  return new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Dhaka' }).format(new Date());
}
