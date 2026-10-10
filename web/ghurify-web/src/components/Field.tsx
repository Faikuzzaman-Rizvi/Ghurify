import {
  Children,
  Fragment,
  isValidElement,
  type InputHTMLAttributes,
  type OptionHTMLAttributes,
  type ReactNode,
  type TextareaHTMLAttributes,
} from 'react';
import { DatePicker, type DatePickerProps } from './ui/DatePicker';
import { Select, type SelectOption } from './ui/Select';

/** The one input style, so every form in the app looks and focuses the same way. */
export const inputClass =
  'w-full rounded-xl border border-hill/15 bg-white px-4 py-3 text-base text-deep shadow-[inset_0_1px_2px_rgba(15,42,31,0.04)] transition placeholder:text-deep/40 hover:border-hill/30 focus:border-hill focus:outline-none focus:ring-4 focus:ring-hill/10 disabled:cursor-not-allowed disabled:bg-mist disabled:text-deep/60 aria-invalid:border-jamdani aria-invalid:ring-jamdani/10';

interface FieldShellProps {
  id: string;
  label: string;
  hint?: string | undefined;
  error?: string | undefined;
  children: ReactNode;
}

/** Label, control, hint and error, wired together for screen readers. */
function FieldShell({ id, label, hint, error, children }: FieldShellProps) {
  return (
    <div className="flex flex-col gap-1.5">
      <label htmlFor={id} className="text-sm font-semibold text-deep">
        {label}
      </label>
      {children}
      {hint && !error && (
        <p id={`${id}-hint`} className="text-xs text-deep/60">
          {hint}
        </p>
      )}
      {error && (
        <p id={`${id}-error`} role="alert" className="text-sm text-jamdani">
          {error}
        </p>
      )}
    </div>
  );
}

function describedBy(id: string, hint?: string, error?: string): string | undefined {
  if (error) return `${id}-error`;
  if (hint) return `${id}-hint`;
  return undefined;
}

type TextFieldProps = InputHTMLAttributes<HTMLInputElement> & {
  id: string;
  label: string;
  hint?: string | undefined;
  error?: string | undefined;
};

export function TextField({ id, label, hint, error, ...input }: TextFieldProps) {
  return (
    <FieldShell id={id} label={label} hint={hint} error={error}>
      <input
        id={id}
        aria-invalid={error ? 'true' : 'false'}
        aria-describedby={describedBy(id, hint, error)}
        className={inputClass}
        {...input}
      />
    </FieldShell>
  );
}

type TextAreaFieldProps = TextareaHTMLAttributes<HTMLTextAreaElement> & {
  id: string;
  label: string;
  hint?: string | undefined;
  error?: string | undefined;
};

export function TextAreaField({ id, label, hint, error, ...textarea }: TextAreaFieldProps) {
  return (
    <FieldShell id={id} label={label} hint={hint} error={error}>
      <textarea
        id={id}
        rows={3}
        aria-invalid={error ? 'true' : 'false'}
        aria-describedby={describedBy(id, hint, error)}
        className={`${inputClass} resize-y`}
        {...textarea}
      />
    </FieldShell>
  );
}

interface SelectFieldProps {
  id: string;
  label: string;
  hint?: string | undefined;
  error?: string | undefined;
  value: string | number | null | undefined;
  /** Shaped like a change event, so a handler written for a native select keeps working. */
  onChange: (event: { target: { value: string } }) => void;
  onBlur?: (() => void) | undefined;
  disabled?: boolean | undefined;
  /** The empty option is a prompt ("Choose…"), shown muted until something is chosen. */
  placeholder?: boolean | undefined;
  /** The choices, written as <option value="…">Label</option> just as for a native select. */
  children: ReactNode;
}

/** The text inside an <option>, however it was written (a string, numbers, fragments). */
function textOf(node: ReactNode): string {
  if (node === null || node === undefined || typeof node === 'boolean') return '';
  if (typeof node === 'string' || typeof node === 'number') return String(node);
  if (Array.isArray(node)) return node.map(textOf).join('');
  if (isValidElement<{ children?: ReactNode }>(node)) return textOf(node.props.children);
  return '';
}

/** The <option> children of a SelectField as the custom dropdown's choices. */
function optionsOf(children: ReactNode): SelectOption[] {
  const options: SelectOption[] = [];
  Children.forEach(children, (child) => {
    if (!isValidElement<OptionHTMLAttributes<HTMLOptionElement>>(child)) return;
    if (child.type === Fragment) {
      options.push(...optionsOf((child.props as { children?: ReactNode }).children));
      return;
    }
    if (child.type !== 'option') return;
    const { value, children: text, disabled } = child.props;
    const option: SelectOption = { value: String(value ?? textOf(text)), label: textOf(text) };
    if (disabled) option.disabled = true;
    options.push(option);
  });
  return options;
}

/**
 * A labelled dropdown. The browser's own <select> cannot be styled when open (a bare list with
 * the system's blue highlight), so this is the app's custom one, with the same keyboard
 * behaviour: see components/ui/Select. Controlled: give it `value` and `onChange`, or wire it
 * to a form with react-hook-form's Controller.
 */
export function SelectField({
  id,
  label,
  hint,
  error,
  value,
  onChange,
  onBlur,
  disabled = false,
  placeholder = false,
  children,
}: SelectFieldProps) {
  return (
    <FieldShell id={id} label={label} hint={hint} error={error}>
      <Select
        id={id}
        value={value === null || value === undefined ? '' : String(value)}
        onChange={(next) => onChange({ target: { value: next } })}
        onBlur={onBlur}
        options={optionsOf(children)}
        disabled={disabled}
        invalid={Boolean(error)}
        describedBy={describedBy(id, hint, error)}
        emptyIsPlaceholder={placeholder}
        buttonClassName={`${inputClass} cursor-pointer`}
      />
    </FieldShell>
  );
}

/** The main action of a form or card. */
export const primaryButtonClass =
  'inline-flex items-center justify-center gap-2 rounded-full bg-hill px-6 py-3 font-semibold text-white shadow-sm transition hover:bg-deep hover:shadow-md focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-turmeric focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50 disabled:shadow-none';

/** The one action that matters most on a page (publish, book): turmeric. */
export const accentButtonClass =
  'inline-flex items-center justify-center gap-2 rounded-full bg-turmeric px-6 py-3 font-semibold text-night shadow-sm transition hover:bg-dusk hover:shadow-md focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-hill focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50 disabled:shadow-none';

/** A secondary action next to a primary one. */
export const secondaryButtonClass =
  'inline-flex items-center justify-center gap-2 rounded-full border border-hill/20 bg-white px-5 py-2.5 font-semibold text-deep transition hover:border-hill/40 hover:bg-mist focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-turmeric focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50';

/** A destructive action: decline, cancel, reject. */
export const dangerButtonClass =
  'inline-flex items-center justify-center gap-2 rounded-full border border-jamdani/30 bg-white px-5 py-2.5 font-semibold text-jamdani transition hover:border-jamdani/50 hover:bg-jamdani/5 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-jamdani/40 focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50';

/** A white card on the page background. */
export const cardClass =
  'rounded-2xl bg-white p-5 shadow-[0_4px_24px_rgba(15,42,31,0.06)] ring-1 ring-hill/10 sm:p-7';

type DateFieldProps = Omit<DatePickerProps, 'inputClassName' | 'invalid' | 'describedBy'> & {
  label: string;
  hint?: string | undefined;
  error?: string | undefined;
};

/** A labelled calendar date picker, styled and wired like the other fields. */
export function DateField({ id, label, hint, error, ...picker }: DateFieldProps) {
  return (
    <FieldShell id={id} label={label} hint={hint} error={error}>
      <DatePicker
        id={id}
        invalid={Boolean(error)}
        describedBy={describedBy(id, hint, error)}
        inputClassName={inputClass}
        {...picker}
      />
    </FieldShell>
  );
}
