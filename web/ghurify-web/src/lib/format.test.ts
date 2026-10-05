import { describe, expect, it } from 'vitest';
import { formatCount, formatDateRange, formatMoney, toLanguage, tripDays } from './format';

describe('format', () => {
  it('writes money as Tk in English and ৳ with Bangla digits in Bangla', () => {
    expect(formatMoney(6800, 'en')).toBe('Tk 6,800');
    expect(formatMoney('6800.00', 'en')).toBe('Tk 6,800');
    expect(formatMoney(6800, 'bn')).toBe('৳৬,৮০০');
  });

  it('groups large amounts by lakh, as they are written in Bangladesh', () => {
    expect(formatMoney(150000, 'en')).toBe('Tk 1,50,000');
  });

  it('writes counts in the reader’s script', () => {
    expect(formatCount(12, 'en')).toBe('12');
    expect(formatCount(12, 'bn')).toBe('১২');
  });

  it('keeps a trip date on its own day whatever the browser time zone', () => {
    expect(formatDateRange('2026-10-14', '2026-10-16', 'en')).toBe('14 Oct – 16 Oct');
  });

  it('counts both the first and last day of a trip', () => {
    expect(tripDays('2026-10-14', '2026-10-16')).toBe(3);
    expect(tripDays('2026-10-14', '2026-10-14')).toBe(1);
  });

  it('treats every English variant as English and everything else as Bangla', () => {
    expect(toLanguage('en-US')).toBe('en');
    expect(toLanguage('bn')).toBe('bn');
    expect(toLanguage(undefined)).toBe('bn');
  });
});
