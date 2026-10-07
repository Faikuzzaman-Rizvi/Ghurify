import { describe, expect, it } from 'vitest';
import { useState } from 'react';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { Select } from './Select';

const places = [
  { value: '', label: 'Anywhere' },
  { value: 'sajek', label: 'Sajek Valley' },
  { value: 'sylhet', label: 'Sylhet' },
  { value: 'closed', label: 'Sundarbans', disabled: true },
  { value: 'kuakata', label: 'Kuakata' },
];

function Harness() {
  const [value, setValue] = useState('');
  return (
    <>
      <label htmlFor="place">Destination</label>
      <Select id="place" value={value} onChange={setValue} options={places} />
      <output>{value || 'none'}</output>
    </>
  );
}

describe('Select', () => {
  it('is labelled by its label and opens a list of options on click', async () => {
    const user = userEvent.setup();
    render(<Harness />);

    const button = screen.getByLabelText('Destination');
    expect(button).toHaveAttribute('aria-expanded', 'false');
    expect(button).toHaveTextContent('Anywhere');

    await user.click(button);
    expect(button).toHaveAttribute('aria-expanded', 'true');
    await user.click(screen.getByRole('option', { name: 'Sylhet' }));

    expect(screen.getByText('sylhet', { selector: 'output' })).toBeInTheDocument();
    expect(button).toHaveTextContent('Sylhet');
    expect(button).toHaveAttribute('aria-expanded', 'false');
  });

  it('works by keyboard, skipping disabled options, and Escape closes it', async () => {
    const user = userEvent.setup();
    render(<Harness />);

    const button = screen.getByLabelText('Destination');
    button.focus();
    await user.keyboard('{ArrowDown}');
    expect(button).toHaveAttribute('aria-expanded', 'true');

    // Anywhere -> Sajek -> Sylhet -> (Sundarbans is disabled) -> Kuakata
    await user.keyboard('{ArrowDown}{ArrowDown}{ArrowDown}{Enter}');
    expect(screen.getByText('kuakata', { selector: 'output' })).toBeInTheDocument();
    expect(button).toHaveFocus();

    await user.keyboard('{ArrowDown}{Escape}');
    expect(button).toHaveAttribute('aria-expanded', 'false');
    expect(screen.getByText('kuakata', { selector: 'output' })).toBeInTheDocument();
  });

  it('jumps to an option by typing its first letters', async () => {
    const user = userEvent.setup();
    render(<Harness />);

    screen.getByLabelText('Destination').focus();
    await user.keyboard('sy');

    expect(screen.getByText('sylhet', { selector: 'output' })).toBeInTheDocument();
  });

  it('marks the chosen option as selected', async () => {
    const user = userEvent.setup();
    render(<Harness />);

    await user.click(screen.getByLabelText('Destination'));
    await user.click(screen.getByRole('option', { name: 'Sajek Valley' }));
    await user.click(screen.getByLabelText('Destination'));

    expect(screen.getByRole('option', { name: 'Sajek Valley' })).toHaveAttribute(
      'aria-selected',
      'true',
    );
    expect(screen.getByRole('option', { name: 'Sundarbans' })).toHaveAttribute(
      'aria-disabled',
      'true',
    );
  });
});
