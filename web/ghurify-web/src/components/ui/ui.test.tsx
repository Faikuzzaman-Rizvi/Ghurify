import { beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import i18n from '@/i18n';
import { Accordion } from './Accordion';
import { pageWindow } from './pageWindow';
import { Pagination } from './Pagination';
import { Photo } from './Photo';

describe('Photo', () => {
  beforeEach(async () => {
    await i18n.changeLanguage('en');
  });

  it('describes a destination with its own photo', () => {
    render(<Photo slug="sajek" kind="Hills" cut="wide" />);

    const img = screen.getByRole('img', { name: 'Cottages above a sea of clouds in Sajek Valley' });
    expect(img).toHaveAttribute('src', '/photos/sajek-1080.webp');
    expect(img).toHaveAttribute('srcset', expect.stringContaining('sajek-1920.webp 1920w'));
  });

  it('borrows a photo of the same kind, without describing a place it is not', () => {
    const { container } = render(<Photo slug="new-hill-town" kind="Hills" cut="card" />);

    const img = container.querySelector('img');
    expect(img).toHaveAttribute('src', '/photos/bandarban-card.webp');
    expect(img).toHaveAttribute('alt', '');
  });

  it('falls back to the illustrated scenery when the photo fails to load', () => {
    const { container } = render(<Photo slug="sajek" kind="Hills" cut="card" />);

    fireEvent.error(container.querySelector('img')!);

    expect(container.querySelector('img')).toBeNull();
    expect(container.querySelector('svg')).not.toBeNull();
  });
});

describe('Accordion', () => {
  const items = [
    { id: '1', title: 'Into the hills', content: 'Jeep convoy up to Sajek.' },
    { id: '2', title: 'Sunrise over the clouds', content: 'Konglak Para at dawn.' },
  ];

  it('opens and closes a row with its button', async () => {
    const user = userEvent.setup();
    render(<Accordion items={items} defaultOpen={['1']} />);

    const second = screen.getByRole('button', { name: 'Sunrise over the clouds' });
    expect(second).toHaveAttribute('aria-expanded', 'false');
    expect(screen.getByText('Konglak Para at dawn.')).not.toBeVisible();

    await user.click(second);

    expect(second).toHaveAttribute('aria-expanded', 'true');
    expect(screen.getByRole('region', { name: 'Sunrise over the clouds' })).toBeVisible();
  });

  it('opens every row at once, then closes them again', async () => {
    const user = userEvent.setup();
    render(
      <Accordion items={items} toggleAllLabels={{ expand: 'Open all', collapse: 'Close all' }} />,
    );

    await user.click(screen.getByRole('button', { name: 'Open all' }));
    expect(screen.getByText('Jeep convoy up to Sajek.')).toBeVisible();
    expect(screen.getByText('Konglak Para at dawn.')).toBeVisible();

    await user.click(screen.getByRole('button', { name: 'Close all' }));
    expect(screen.getByText('Jeep convoy up to Sajek.')).not.toBeVisible();
  });
});

describe('Pagination', () => {
  beforeEach(async () => {
    await i18n.changeLanguage('en');
  });

  it('keeps the first, last and neighbouring pages, with gaps between', () => {
    expect(pageWindow(1, 3)).toEqual([1, 2, 3]);
    expect(pageWindow(5, 10)).toEqual([1, 'gap', 4, 5, 6, 'gap', 10]);
    expect(pageWindow(10, 10)).toEqual([1, 'gap', 9, 10]);
  });

  it('marks the current page and moves between pages', async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(<Pagination page={1} pages={3} onChange={onChange} label="Pages" />);

    expect(screen.getByRole('button', { name: 'Page 1' })).toHaveAttribute('aria-current', 'page');
    expect(screen.getByRole('button', { name: 'Previous' })).toBeDisabled();

    await user.click(screen.getByRole('button', { name: 'Page 3' }));
    await user.click(screen.getByRole('button', { name: 'Next' }));

    expect(onChange).toHaveBeenNthCalledWith(1, 3);
    expect(onChange).toHaveBeenNthCalledWith(2, 2);
  });

  it('shows nothing when everything fits on one page', () => {
    const { container } = render(
      <Pagination page={1} pages={1} onChange={() => {}} label="Pages" />,
    );

    expect(container).toBeEmptyDOMElement();
  });
});
