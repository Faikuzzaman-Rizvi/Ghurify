import { useState } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { SelectField } from './Field';

function Division({ onPick }: { onPick: (value: string) => void }) {
  const [value, setValue] = useState('');
  return (
    <SelectField
      id="division"
      label="Division"
      hint="Where the place is."
      placeholder
      value={value}
      onChange={(event) => {
        setValue(event.target.value);
        onPick(event.target.value);
      }}
    >
      <option value="">Choose…</option>
      <option value="Dhaka">Dhaka</option>
      <option value="Sylhet">Sylhet</option>
      <option value="Khulna" disabled>
        Khulna
      </option>
    </SelectField>
  );
}

describe('SelectField', () => {
  it('is a labelled dropdown built from its <option> children, not a native select', async () => {
    const user = userEvent.setup();
    render(<Division onPick={() => {}} />);

    const control = screen.getByRole('combobox', { name: 'Division' });
    expect(document.querySelector('select')).toBeNull();
    expect(control).toHaveTextContent('Choose…');
    expect(control).toHaveAccessibleDescription('Where the place is.');

    await user.click(control);
    const options = screen.getAllByRole('option');
    expect(options.map((option) => option.textContent)).toEqual([
      'Choose…',
      'Dhaka',
      'Sylhet',
      'Khulna',
    ]);
    expect(screen.getByRole('option', { name: 'Choose…' })).toHaveAttribute(
      'aria-selected',
      'true',
    );
    expect(screen.getByRole('option', { name: 'Khulna' })).toHaveAttribute('aria-disabled', 'true');
  });

  it('hands the chosen value over in the shape of a change event', async () => {
    const user = userEvent.setup();
    const onPick = vi.fn();
    render(<Division onPick={onPick} />);

    await user.click(screen.getByRole('combobox', { name: 'Division' }));
    await user.click(screen.getByRole('option', { name: 'Sylhet' }));

    expect(onPick).toHaveBeenCalledWith('Sylhet');
    expect(screen.getByRole('combobox', { name: 'Division' })).toHaveTextContent('Sylhet');
    expect(screen.queryByRole('listbox')).not.toBeInTheDocument();
  });

  it('works from the keyboard, passing over a disabled choice', async () => {
    const user = userEvent.setup();
    const onPick = vi.fn();
    render(<Division onPick={onPick} />);

    screen.getByRole('combobox', { name: 'Division' }).focus();
    await user.keyboard('{ArrowDown}{End}{Enter}');

    // End lands on the last choice that can be chosen: Sylhet, not the disabled Khulna.
    expect(onPick).toHaveBeenCalledWith('Sylhet');
  });
});
