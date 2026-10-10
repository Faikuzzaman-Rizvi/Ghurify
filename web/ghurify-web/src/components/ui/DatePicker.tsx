import { useEffect, useId, useMemo, useRef, useState, type KeyboardEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { CalendarDays, ChevronLeft, ChevronRight, X } from 'lucide-react';
import { todayInDhaka, toLanguage } from '@/lib/format';
import { usePopoverPosition } from './usePopoverPosition';

/**
 * A calendar date picker that replaces the browser's native one (an unstyleable grey popup
 * with mm/dd/yyyy). Values are calendar days as "YYYY-MM-DD" strings, the same as a native
 * date input, so forms and the API are unchanged.
 *
 * It stays a real text field: people can type a date (2026-10-14 or 14/10/2026) as well as
 * pick one, and screen readers and tests address it by its label. Clicking it, or Alt+Down,
 * opens the calendar: arrow keys move by day and week, Page Up/Down by month, Home/End to the
 * week's ends, Enter picks, Escape closes. Clicking the month title steps out to a month and
 * then a year view, so a birth year is two clicks away rather than hundreds.
 */
export interface DatePickerProps {
  id: string;
  /** "YYYY-MM-DD", or '' for none. */
  value: string;
  onChange: (value: string) => void;
  onBlur?: (() => void) | undefined;
  min?: string | undefined;
  max?: string | undefined;
  placeholder?: string | undefined;
  /** Another date to draw as the far end of a range (a trip's first day, for its last day). */
  rangeWith?: string | undefined;
  /** Classes for the text field: lets each place style it like its neighbours. */
  inputClassName?: string;
  className?: string;
  invalid?: boolean;
  describedBy?: string | undefined;
  /** Show a clear button inside the field when it has a value. */
  clearable?: boolean;
}

type View = 'days' | 'months' | 'years';

const iso = /^(\d{4})-(\d{2})-(\d{2})$/;
const dmy = /^(\d{1,2})[/.-](\d{1,2})[/.-](\d{4})$/;

/** Parses what someone typed into "YYYY-MM-DD", or null when it is not a real date. */
function parseTyped(text: string): string | null {
  const trimmed = text.trim();
  let year: number, month: number, day: number;
  const a = iso.exec(trimmed);
  const b = dmy.exec(trimmed);
  if (a) [year, month, day] = [Number(a[1]), Number(a[2]), Number(a[3])];
  else if (b) [day, month, year] = [Number(b[1]), Number(b[2]), Number(b[3])];
  else return null;
  const date = new Date(Date.UTC(year, month - 1, day));
  if (date.getUTCFullYear() !== year || date.getUTCMonth() !== month - 1) return null;
  return toIso(year, month - 1, day);
}

function toIso(year: number, monthIndex: number, day: number): string {
  return `${year}-${String(monthIndex + 1).padStart(2, '0')}-${String(day).padStart(2, '0')}`;
}

function parts(value: string): [number, number, number] {
  const [y = 1970, m = 1, d = 1] = value.split('-').map(Number);
  return [y, m - 1, d];
}

function addDays(value: string, days: number): string {
  const [y, m, d] = parts(value);
  const date = new Date(Date.UTC(y, m, d + days));
  return toIso(date.getUTCFullYear(), date.getUTCMonth(), date.getUTCDate());
}

function addMonths(value: string, months: number): string {
  const [y, m, d] = parts(value);
  const target = new Date(Date.UTC(y, m + months, 1));
  const lastDay = new Date(Date.UTC(target.getUTCFullYear(), target.getUTCMonth() + 1, 0));
  return toIso(target.getUTCFullYear(), target.getUTCMonth(), Math.min(d, lastDay.getUTCDate()));
}

function clamp(value: string, min?: string, max?: string): string {
  if (min && value < min) return min;
  if (max && value > max) return max;
  return value;
}

export function DatePicker({
  id,
  value,
  onChange,
  onBlur,
  min,
  max,
  placeholder,
  rangeWith,
  inputClassName = '',
  className = '',
  invalid = false,
  describedBy,
  clearable = false,
}: DatePickerProps) {
  const { t, i18n } = useTranslation();
  const language = toLanguage(i18n.language);
  const locale = language === 'bn' ? 'bn-BD' : 'en-GB';
  const dialogId = useId();
  const rootRef = useRef<HTMLDivElement>(null);
  const gridRef = useRef<HTMLDivElement>(null);
  const today = todayInDhaka();

  const [open, setOpen] = useState(false);
  const [view, setView] = useState<View>('days');
  // The day with keyboard focus inside the calendar; also decides which month is shown.
  const [focused, setFocused] = useState(() => clamp(value || today, min, max));
  const [text, setText] = useState<string | null>(null);

  // Against the window: inside a dialog the calendar then opens upwards rather than over the
  // buttons at its foot, and it never lengthens whatever is scrolling behind it.
  const { anchorRef: fieldRef, panelRef: calendarRef } = usePopoverPosition<
    HTMLDivElement,
    HTMLDivElement
  >(open, { maxHeight: 440 });

  const formats = useMemo(
    () => ({
      field: new Intl.DateTimeFormat(locale, {
        day: 'numeric',
        month: 'short',
        year: 'numeric',
        timeZone: 'UTC',
      }),
      full: new Intl.DateTimeFormat(locale, {
        weekday: 'long',
        day: 'numeric',
        month: 'long',
        year: 'numeric',
        timeZone: 'UTC',
      }),
      monthYear: new Intl.DateTimeFormat(locale, {
        month: 'long',
        year: 'numeric',
        timeZone: 'UTC',
      }),
      month: new Intl.DateTimeFormat(locale, { month: 'short', timeZone: 'UTC' }),
      weekday: new Intl.DateTimeFormat(locale, { weekday: 'narrow', timeZone: 'UTC' }),
      number: new Intl.NumberFormat(locale, { useGrouping: false }),
    }),
    [locale],
  );

  const asDate = (day: string) => {
    const [y, m, d] = parts(day);
    return new Date(Date.UTC(y, m, d));
  };
  const display = value ? formats.field.format(asDate(value)) : '';

  // Close on a click anywhere else.
  useEffect(() => {
    if (!open) return;
    function onPointer(event: PointerEvent) {
      if (!rootRef.current?.contains(event.target as Node)) setOpen(false);
    }
    document.addEventListener('pointerdown', onPointer);
    return () => document.removeEventListener('pointerdown', onPointer);
  }, [open]);

  // Keep keyboard focus on the focused day while the calendar is driven by the keyboard.
  useEffect(() => {
    if (!open || view !== 'days') return;
    const active = gridRef.current?.querySelector<HTMLButtonElement>(`[data-day="${focused}"]`);
    if (active && gridRef.current?.contains(document.activeElement)) active.focus();
  }, [open, view, focused]);

  function openCalendar() {
    setFocused(clamp(value || today, min, max));
    setView('days');
    setOpen(true);
  }

  function choose(day: string) {
    onChange(day);
    setText(null);
    setOpen(false);
    rootRef.current?.querySelector<HTMLInputElement>('input')?.focus();
  }

  const outOfRange = (day: string) =>
    (min !== undefined && day < min) || (max !== undefined && day > max);

  function onFieldKeyDown(event: KeyboardEvent<HTMLInputElement>) {
    if ((event.altKey && event.key === 'ArrowDown') || (open && event.key === 'ArrowDown')) {
      event.preventDefault();
      // Move focus into the grid so the arrow keys pick days: now if it is showing, else as
      // soon as it has rendered.
      const focusDay = () =>
        gridRef.current?.querySelector<HTMLButtonElement>('[tabindex="0"]')?.focus();
      if (open) focusDay();
      else {
        openCalendar();
        window.setTimeout(focusDay, 0);
      }
    } else if (event.key === 'Escape' && open) {
      event.preventDefault();
      setOpen(false);
    } else if (event.key === 'Enter') {
      const parsed = text === null ? null : parseTyped(text);
      if (parsed && !outOfRange(parsed)) {
        event.preventDefault();
        choose(parsed);
      }
    }
  }

  function onGridKeyDown(event: KeyboardEvent<HTMLDivElement>) {
    const moves: Record<string, () => string> = {
      ArrowLeft: () => addDays(focused, -1),
      ArrowRight: () => addDays(focused, 1),
      ArrowUp: () => addDays(focused, -7),
      ArrowDown: () => addDays(focused, 7),
      PageUp: () => addMonths(focused, event.shiftKey ? -12 : -1),
      PageDown: () => addMonths(focused, event.shiftKey ? 12 : 1),
      Home: () => addDays(focused, -asDate(focused).getUTCDay()),
      End: () => addDays(focused, 6 - asDate(focused).getUTCDay()),
    };
    const move = moves[event.key];
    if (move) {
      event.preventDefault();
      setFocused(clamp(move(), min, max));
    } else if (event.key === 'Escape') {
      event.preventDefault();
      setOpen(false);
      rootRef.current?.querySelector<HTMLInputElement>('input')?.focus();
    }
  }

  const [year, monthIndex] = parts(focused);
  const first = new Date(Date.UTC(year, monthIndex, 1));
  const gridStart = addDays(toIso(year, monthIndex, 1), -first.getUTCDay());
  const days = Array.from({ length: 42 }, (_, index) => addDays(gridStart, index));
  // A week of weekday initials, starting on Sunday (4 Jan 1970 was a Sunday).
  const weekdays = Array.from({ length: 7 }, (_, index) =>
    formats.weekday.format(new Date(Date.UTC(1970, 0, 4 + index))),
  );
  const rangeLow = value && rangeWith ? (value < rangeWith ? value : rangeWith) : null;
  const rangeHigh = value && rangeWith ? (value < rangeWith ? rangeWith : value) : null;
  const yearPage = Math.floor(year / 12) * 12;
  const minYear = min ? parts(min)[0] : -Infinity;
  const maxYear = max ? parts(max)[0] : Infinity;

  const canPrev =
    view === 'days'
      ? !min || toIso(year, monthIndex, 1) > min
      : view === 'months'
        ? year > minYear
        : yearPage > minYear;
  const canNext =
    view === 'days'
      ? !max || addMonths(toIso(year, monthIndex, 1), 1) <= max
      : view === 'months'
        ? year < maxYear
        : yearPage + 11 < maxYear;

  function step(direction: 1 | -1) {
    if (view === 'days') setFocused(clamp(addMonths(focused, direction), min, max));
    else if (view === 'months') setFocused(clamp(addMonths(focused, 12 * direction), min, max));
    else setFocused(clamp(addMonths(focused, 144 * direction), min, max));
  }

  return (
    <div ref={rootRef} className={`relative ${className}`}>
      <div ref={fieldRef} className="relative">
        <input
          id={id}
          type="text"
          inputMode="numeric"
          autoComplete="off"
          role="combobox"
          aria-haspopup="dialog"
          aria-expanded={open}
          aria-controls={dialogId}
          aria-invalid={invalid ? 'true' : 'false'}
          aria-describedby={describedBy}
          placeholder={placeholder ?? t('datePicker.placeholder')}
          value={text ?? display}
          onChange={(event) => {
            setText(event.target.value);
            const parsed = parseTyped(event.target.value);
            if (parsed && !outOfRange(parsed)) onChange(parsed);
            if (event.target.value === '') onChange('');
          }}
          onClick={() => !open && openCalendar()}
          onKeyDown={onFieldKeyDown}
          onBlur={() => {
            setText(null);
            onBlur?.();
          }}
          className={`w-full cursor-pointer pr-16 ${inputClassName}`}
        />
        <span className="absolute inset-y-0 right-3 flex items-center gap-1">
          {clearable && value && (
            <button
              type="button"
              onClick={() => onChange('')}
              aria-label={t('datePicker.clear')}
              className="rounded-full p-1 text-deep/40 transition hover:bg-mist hover:text-deep"
            >
              <X aria-hidden="true" className="h-3.5 w-3.5" />
            </button>
          )}
          <button
            type="button"
            tabIndex={-1}
            aria-hidden="true"
            onClick={() => (open ? setOpen(false) : openCalendar())}
            className="rounded-full p-1 text-hill transition hover:bg-mist"
          >
            <CalendarDays className="h-4.5 w-4.5" />
          </button>
        </span>
      </div>

      {open && (
        <div
          id={dialogId}
          ref={calendarRef}
          role="dialog"
          aria-label={t('datePicker.label')}
          className="fixed z-50 w-[min(20rem,calc(100vw-2rem))] animate-menu-in overflow-y-auto overscroll-contain rounded-2xl bg-white p-3 text-left shadow-[0_24px_60px_rgba(15,42,31,0.22)] ring-1 ring-hill/10 sm:p-4"
        >
          <div className="mb-3 flex items-center justify-between gap-2">
            <button
              type="button"
              onClick={() => step(-1)}
              disabled={!canPrev}
              aria-label={t('datePicker.previous')}
              className="flex h-9 w-9 items-center justify-center rounded-full text-deep transition hover:bg-mist disabled:pointer-events-none disabled:opacity-25"
            >
              <ChevronLeft aria-hidden="true" className="h-4.5 w-4.5" />
            </button>
            <button
              type="button"
              onClick={() =>
                setView(view === 'days' ? 'months' : view === 'months' ? 'years' : 'days')
              }
              aria-live="polite"
              className="rounded-full px-3 py-1.5 font-display text-[0.95rem] font-semibold text-deep transition hover:bg-mist"
            >
              {view === 'days'
                ? formats.monthYear.format(first)
                : view === 'months'
                  ? formats.number.format(year)
                  : `${formats.number.format(yearPage)} – ${formats.number.format(yearPage + 11)}`}
            </button>
            <button
              type="button"
              onClick={() => step(1)}
              disabled={!canNext}
              aria-label={t('datePicker.next')}
              className="flex h-9 w-9 items-center justify-center rounded-full text-deep transition hover:bg-mist disabled:pointer-events-none disabled:opacity-25"
            >
              <ChevronRight aria-hidden="true" className="h-4.5 w-4.5" />
            </button>
          </div>

          {view === 'days' && (
            <div ref={gridRef} onKeyDown={onGridKeyDown}>
              <div className="mb-1 grid grid-cols-7" aria-hidden="true">
                {weekdays.map((weekday, index) => (
                  <span
                    key={index}
                    className={`py-1 text-center text-xs font-semibold ${
                      index === 5 || index === 6 ? 'text-ochre' : 'text-deep/45'
                    }`}
                  >
                    {weekday}
                  </span>
                ))}
              </div>
              <div className="grid grid-cols-7 gap-y-1">
                {days.map((day) => {
                  const [, dayMonth, dayNumber] = parts(day);
                  const inMonth = dayMonth === monthIndex;
                  const selected = day === value;
                  const isToday = day === today;
                  const disabled = outOfRange(day);
                  const inRange = rangeLow && rangeHigh && day > rangeLow && day < rangeHigh;
                  const rangeEnd = rangeWith && day === rangeWith && value;
                  return (
                    <div
                      key={day}
                      className={`flex justify-center ${inRange ? 'bg-hill/10' : ''} ${
                        rangeLow === day && rangeHigh !== day ? 'rounded-l-full bg-hill/10' : ''
                      } ${rangeHigh === day && rangeLow !== day ? 'rounded-r-full bg-hill/10' : ''}`}
                    >
                      <button
                        type="button"
                        data-day={day}
                        tabIndex={day === focused ? 0 : -1}
                        disabled={disabled}
                        aria-label={formats.full.format(asDate(day))}
                        aria-pressed={selected}
                        aria-current={isToday ? 'date' : undefined}
                        onClick={() => choose(day)}
                        className={`relative flex aspect-square w-full max-w-9 items-center justify-center rounded-full text-sm transition ${
                          selected
                            ? 'bg-hill font-semibold text-white shadow-[0_6px_14px_rgba(36,92,67,0.35)]'
                            : rangeEnd
                              ? 'bg-hill/80 font-semibold text-white'
                              : disabled
                                ? 'cursor-not-allowed text-deep/20'
                                : inMonth
                                  ? 'text-deep hover:bg-mist'
                                  : 'text-deep/30 hover:bg-mist'
                        } ${isToday && !selected ? 'font-semibold text-hill ring-1 ring-turmeric ring-inset' : ''}`}
                      >
                        {formats.number.format(dayNumber)}
                      </button>
                    </div>
                  );
                })}
              </div>
            </div>
          )}

          {view === 'months' && (
            <div className="grid grid-cols-3 gap-2">
              {Array.from({ length: 12 }, (_, index) => {
                const start = toIso(year, index, 1);
                const end = addDays(toIso(year, index + 1, 1), -1);
                const disabled =
                  (min !== undefined && end < min) || (max !== undefined && start > max);
                const current = index === monthIndex;
                return (
                  <button
                    key={index}
                    type="button"
                    disabled={disabled}
                    onClick={() => {
                      setFocused(
                        clamp(toIso(year, index, Math.min(parts(focused)[2], 28)), min, max),
                      );
                      setView('days');
                    }}
                    className={`rounded-xl py-3 text-sm font-medium transition disabled:pointer-events-none disabled:opacity-25 ${
                      current ? 'bg-hill text-white' : 'text-deep hover:bg-mist'
                    }`}
                  >
                    {formats.month.format(new Date(Date.UTC(year, index, 1)))}
                  </button>
                );
              })}
            </div>
          )}

          {view === 'years' && (
            <div className="grid grid-cols-3 gap-2">
              {Array.from({ length: 12 }, (_, index) => {
                const candidate = yearPage + index;
                const disabled = candidate < minYear || candidate > maxYear;
                return (
                  <button
                    key={candidate}
                    type="button"
                    disabled={disabled}
                    onClick={() => {
                      setFocused(clamp(toIso(candidate, monthIndex, 1), min, max));
                      setView('months');
                    }}
                    className={`rounded-xl py-3 text-sm font-medium transition disabled:pointer-events-none disabled:opacity-25 ${
                      candidate === year ? 'bg-hill text-white' : 'text-deep hover:bg-mist'
                    }`}
                  >
                    {formats.number.format(candidate)}
                  </button>
                );
              })}
            </div>
          )}

          <div className="mt-3 flex items-center justify-between border-t border-hill/10 pt-3">
            <button
              type="button"
              disabled={outOfRange(today)}
              onClick={() => choose(today)}
              className="rounded-full px-3 py-1.5 text-sm font-semibold text-hill transition hover:bg-mist disabled:opacity-30"
            >
              {t('datePicker.today')}
            </button>
            {value && (
              <button
                type="button"
                onClick={() => choose('')}
                className="rounded-full px-3 py-1.5 text-sm font-medium text-deep/60 transition hover:bg-mist hover:text-deep"
              >
                {t('datePicker.clear')}
              </button>
            )}
          </div>
        </div>
      )}
    </div>
  );
}
