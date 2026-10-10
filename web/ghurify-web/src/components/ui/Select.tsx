import { useEffect, useId, useRef, useState, type KeyboardEvent, type ReactNode } from 'react';
import { Check, ChevronDown } from 'lucide-react';
import { usePopoverPosition } from './usePopoverPosition';

export interface SelectOption {
  value: string;
  label: string;
  /** Shown under the label in the list, e.g. a division or a count. */
  hint?: string;
  disabled?: boolean;
}

/**
 * A styled single-choice dropdown that replaces the browser's native list (which cannot be
 * themed and renders as a bare white box). It follows the ARIA "select-only combobox" pattern:
 * a button labelled by its <label>, a listbox popup, arrow keys / Home / End / type-ahead to
 * move, Enter or Space to choose, Escape or a click outside to close. Focus stays on the button
 * and the highlighted option is announced through aria-activedescendant.
 */
export function Select({
  id,
  value,
  onChange,
  options,
  className = '',
  buttonClassName = '',
  icon,
  align = 'left',
  disabled = false,
  invalid = false,
  describedBy,
  onBlur,
  emptyIsPlaceholder = false,
}: {
  /** Put the same id in the <label htmlFor>: the button is the labelled control. */
  id: string;
  value: string;
  onChange: (value: string) => void;
  options: readonly SelectOption[];
  className?: string;
  buttonClassName?: string;
  /** Drawn before the chosen label inside the button. */
  icon?: ReactNode;
  align?: 'left' | 'right';
  /** Shown but not changeable, for a setting the reader may see and not edit. */
  disabled?: boolean;
  /** Marks the field as failing validation, for screen readers and the red border. */
  invalid?: boolean;
  /** The id of the hint or error that describes the field. */
  describedBy?: string | undefined;
  /** Called when focus leaves the field, so a form can validate on blur. */
  onBlur?: (() => void) | undefined;
  /**
   * The empty option is a prompt ("Choose…") rather than an answer, so it is shown muted while
   * chosen. Off for filters, where the empty option ("All statuses") is a real choice.
   */
  emptyIsPlaceholder?: boolean;
}) {
  const listId = useId();
  const [open, setOpen] = useState(false);
  const [active, setActive] = useState(0);
  const rootRef = useRef<HTMLDivElement>(null);
  const typed = useRef({ text: '', at: 0 });
  // Against the window, so the list is never cut off by a sidebar or a dialog that scrolls.
  const { anchorRef: buttonRef, panelRef: listRef } = usePopoverPosition<
    HTMLButtonElement,
    HTMLUListElement
  >(open, { align });

  const selectedIndex = Math.max(
    0,
    options.findIndex((option) => option.value === value),
  );
  const selected = options[selectedIndex];

  // Close on a click anywhere else.
  useEffect(() => {
    if (!open) return;
    function onPointer(event: PointerEvent) {
      if (!rootRef.current?.contains(event.target as Node)) setOpen(false);
    }
    document.addEventListener('pointerdown', onPointer);
    return () => document.removeEventListener('pointerdown', onPointer);
  }, [open]);

  // Keep the highlighted option in view while moving through a long list.
  useEffect(() => {
    if (!open) return;
    listRef.current
      ?.querySelector<HTMLElement>(`[data-index="${active}"]`)
      ?.scrollIntoView?.({ block: 'nearest' });
  }, [open, active, listRef]);

  function openAt(index: number) {
    setActive(index);
    setOpen(true);
  }

  function move(from: number, step: 1 | -1) {
    for (let index = from + step; index >= 0 && index < options.length; index += step) {
      if (!options[index]?.disabled) return index;
    }
    return from;
  }

  function choose(index: number) {
    const option = options[index];
    if (!option || option.disabled) return;
    onChange(option.value);
    setOpen(false);
  }

  function onKeyDown(event: KeyboardEvent<HTMLButtonElement>) {
    switch (event.key) {
      case 'ArrowDown':
        event.preventDefault();
        if (open) setActive((current) => move(current, 1));
        else openAt(selectedIndex);
        return;
      case 'ArrowUp':
        event.preventDefault();
        if (open) setActive((current) => move(current, -1));
        else openAt(selectedIndex);
        return;
      case 'Home':
        if (open) {
          event.preventDefault();
          setActive(move(-1, 1));
        }
        return;
      case 'End':
        if (open) {
          event.preventDefault();
          setActive(move(options.length, -1));
        }
        return;
      case 'Enter':
      case ' ':
        event.preventDefault();
        if (open) choose(active);
        else openAt(selectedIndex);
        return;
      case 'Escape':
        if (open) {
          event.preventDefault();
          setOpen(false);
        }
        return;
      case 'Tab':
        if (open) choose(active);
        return;
      default:
        // Type-ahead: letters typed in quick succession jump to the first matching option.
        if (event.key.length === 1 && !event.ctrlKey && !event.metaKey && !event.altKey) {
          const now = Date.now();
          typed.current.text =
            now - typed.current.at < 600 ? typed.current.text + event.key : event.key;
          typed.current.at = now;
          const needle = typed.current.text.toLowerCase();
          const match = options.findIndex(
            (option) => !option.disabled && option.label.toLowerCase().startsWith(needle),
          );
          if (match >= 0) {
            if (open) setActive(match);
            else onChange(options[match]!.value);
          }
        }
    }
  }

  return (
    <div ref={rootRef} className={`relative ${className}`}>
      <button
        id={id}
        ref={buttonRef}
        type="button"
        role="combobox"
        aria-haspopup="listbox"
        aria-expanded={open}
        aria-controls={listId}
        aria-activedescendant={open ? `${listId}-${active}` : undefined}
        aria-invalid={invalid || undefined}
        aria-describedby={describedBy}
        disabled={disabled}
        onClick={() => (open ? setOpen(false) : openAt(selectedIndex))}
        onKeyDown={onKeyDown}
        onBlur={() => {
          // Leaving the field closes it; a click on an option keeps focus here, so it is
          // never mistaken for leaving.
          setOpen(false);
          onBlur?.();
        }}
        className={`flex w-full items-center gap-2 text-left outline-none disabled:cursor-default disabled:bg-mist disabled:text-deep/50 aria-expanded:border-hill aria-expanded:ring-4 aria-expanded:ring-hill/10 ${buttonClassName}`}
      >
        {icon}
        <span
          className={`min-w-0 flex-1 truncate ${
            emptyIsPlaceholder && selected?.value === '' ? 'text-deep/45' : ''
          }`}
        >
          {selected?.label}
        </span>
        <ChevronDown
          aria-hidden="true"
          className={`h-4 w-4 shrink-0 text-hill transition-transform duration-200 ${open ? 'rotate-180' : ''}`}
        />
      </button>

      {/* Only in the document while it is open: it is measured against the window as it opens. */}
      {open && (
        <ul
          ref={listRef}
          id={listId}
          role="listbox"
          aria-labelledby={id}
          // A press anywhere in the list, its scrollbar included, keeps focus on the button.
          onPointerDown={(event) => event.preventDefault()}
          className="fixed z-50 animate-menu-in overflow-y-auto overscroll-contain rounded-2xl bg-white p-1.5 text-left shadow-[0_18px_50px_rgba(15,42,31,0.18)] ring-1 ring-hill/10"
        >
          {options.map((option, index) => {
            const isSelected = option.value === value;
            return (
              <li
                key={option.value || '__any'}
                id={`${listId}-${index}`}
                data-index={index}
                data-value={option.value}
                role="option"
                aria-selected={isSelected}
                aria-disabled={option.disabled || undefined}
                onPointerEnter={() => !option.disabled && setActive(index)}
                // Keep focus on the button so keyboard users never lose their place.
                onPointerDown={(event) => event.preventDefault()}
                onClick={() => choose(index)}
                // Three looks that never clash: the chosen option keeps a green ground and a
                // tick, the one under the pointer or the arrow keys a mist ground, and both
                // together a deeper green, so the reader always sees where they are and what
                // is chosen.
                className={`flex cursor-pointer items-center gap-3 whitespace-nowrap rounded-xl px-3 py-2.5 text-[0.95rem] transition-colors ${
                  option.disabled
                    ? 'cursor-not-allowed text-deep/35'
                    : isSelected
                      ? index === active
                        ? 'bg-hill/15 font-semibold text-hill'
                        : 'bg-hill/8 font-semibold text-hill'
                      : index === active
                        ? 'bg-mist text-deep'
                        : 'text-deep/85'
                }`}
              >
                <span className="min-w-0 flex-1">
                  <span className="block">{option.label}</span>
                  {option.hint && (
                    <span className="block text-xs font-normal text-deep/50">{option.hint}</span>
                  )}
                </span>
                <Check
                  aria-hidden="true"
                  className={`h-4 w-4 shrink-0 text-hill ${isSelected ? 'opacity-100' : 'opacity-0'}`}
                />
              </li>
            );
          })}
        </ul>
      )}
    </div>
  );
}
