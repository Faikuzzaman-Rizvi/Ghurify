import { beforeEach, describe, expect, it } from 'vitest';
import { useState } from 'react';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import i18n from '@/i18n';
import { DatePicker } from './DatePicker';

function Harness({ initial = '', min, max }: { initial?: string; min?: string; max?: string }) {
  const [value, setValue] = useState(initial);
  return (
    <>
      <label htmlFor="when">First day</label>
      <DatePicker id="when" value={value} onChange={setValue} min={min} max={max} />
      <output>{value || 'none'}</output>
    </>
  );
}

describe('DatePicker', () => {
  beforeEach(async () => {
    await i18n.changeLanguage('en');
  });

  it('accepts a typed date, in either common form', async () => {
    const user = userEvent.setup();
    render(<Harness />);

    await user.type(screen.getByLabelText('First day'), '2026-10-14');
    expect(screen.getByText('2026-10-14', { selector: 'output' })).toBeInTheDocument();

    await user.clear(screen.getByLabelText('First day'));
    await user.type(screen.getByLabelText('First day'), '3/11/2026');
    expect(screen.getByText('2026-11-03', { selector: 'output' })).toBeInTheDocument();
  });

  it('opens a calendar on click and picks a day from it', async () => {
    const user = userEvent.setup();
    render(<Harness initial="2026-10-14" />);

    await user.click(screen.getByLabelText('First day'));
    const calendar = screen.getByRole('dialog', { name: 'Choose a date' });
    expect(within(calendar).getByText('October 2026')).toBeInTheDocument();
    expect(
      within(calendar).getByRole('button', { name: 'Wednesday, 14 October 2026' }),
    ).toHaveAttribute('aria-pressed', 'true');

    await user.click(within(calendar).getByRole('button', { name: 'Tuesday, 20 October 2026' }));

    expect(screen.getByText('2026-10-20', { selector: 'output' })).toBeInTheDocument();
    expect(screen.getByLabelText('First day')).toHaveValue('20 Oct 2026');
    expect(screen.queryByRole('dialog', { name: 'Choose a date' })).not.toBeInTheDocument();
  });

  it('moves by day and week with the arrow keys, and Enter picks', async () => {
    const user = userEvent.setup();
    render(<Harness initial="2026-10-14" />);

    await user.click(screen.getByLabelText('First day'));
    await user.keyboard('{ArrowDown}');
    expect(screen.getByRole('button', { name: 'Wednesday, 14 October 2026' })).toHaveFocus();

    await user.keyboard('{ArrowRight}{ArrowDown}{Enter}');
    expect(screen.getByText('2026-10-22', { selector: 'output' })).toBeInTheDocument();
  });

  it('will not pick or accept a day outside its limits', async () => {
    const user = userEvent.setup();
    render(<Harness initial="2026-10-14" min="2026-10-10" />);

    await user.click(screen.getByLabelText('First day'));
    expect(screen.getByRole('button', { name: 'Friday, 9 October 2026' })).toBeDisabled();

    await user.clear(screen.getByLabelText('First day'));
    await user.type(screen.getByLabelText('First day'), '2026-10-01');
    expect(screen.getByText('none', { selector: 'output' })).toBeInTheDocument();
  });

  it('steps out to months and years, for dates far away like a birthday', async () => {
    const user = userEvent.setup();
    render(<Harness initial="2026-10-14" />);

    await user.click(screen.getByLabelText('First day'));
    await user.click(screen.getByRole('button', { name: 'October 2026' }));
    await user.click(screen.getByRole('button', { name: '2026' }));
    await user.click(screen.getByRole('button', { name: 'Previous' }));
    await user.click(screen.getByRole('button', { name: 'Previous' }));
    await user.click(screen.getByRole('button', { name: '1995' }));
    await user.click(screen.getByRole('button', { name: 'Apr' }));
    await user.click(screen.getByRole('button', { name: 'Wednesday, 12 April 1995' }));

    expect(screen.getByText('1995-04-12', { selector: 'output' })).toBeInTheDocument();
  });

  it('writes Bangla months and digits for Bangla readers', async () => {
    const user = userEvent.setup();
    await i18n.changeLanguage('bn');
    render(<Harness initial="2026-10-14" />);

    await user.click(screen.getByLabelText('First day'));

    expect(screen.getByRole('button', { name: /অক্টোবর ২০২৬/ })).toBeInTheDocument();
  });
});
