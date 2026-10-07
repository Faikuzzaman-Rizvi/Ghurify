import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import i18n from '@/i18n';
import { useAuthStore } from '@/features/auth/authStore';
import { renderScreen } from '@/test/render';
import { requests, stubApi } from '@/test/fetchStub';
import { StoriesModerationPage } from './StoriesModerationPage';

const story = {
  id: 77,
  authorId: 5,
  authorName: 'Farhana Akter',
  authorVerifiedLevel: null,
  body: 'Tickets for sale, message me.',
  destinationSlug: null,
  destinationName: null,
  destinationNameBn: null,
  tripId: null,
  likes: 0,
  comments: 2,
  likedByMe: false,
  created: '2026-10-06T08:00:00Z',
  media: [],
  editedOn: '2026-10-06T09:00:00Z',
};

describe('admin stories', () => {
  beforeEach(async () => {
    await i18n.changeLanguage('en');
    useAuthStore.setState({
      status: 'authenticated',
      user: { id: 9, maskedEmail: 'a****n@demo.ghurify.app', displayName: 'Ghurify Admin' },
    });
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('lists one person’s stories and removes one only with a reason', async () => {
    const user = userEvent.setup();
    const fetchMock = stubApi([
      [/\/api\/v1\/admin\/posts\/77\/remove$/, null, 204],
      [/\/api\/v1\/admin\/posts\?authorId=5$/, { items: [story], nextBefore: null }],
    ]);

    renderScreen(<StoriesModerationPage />, { at: '/admin/stories?author=5' });

    expect(await screen.findByText('Tickets for sale, message me.')).toBeInTheDocument();
    expect(screen.getByText(/edited/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Only Farhana Akter/ })).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Remove story' }));
    const dialog = screen.getByRole('dialog');
    const confirm = within(dialog).getByRole('button', { name: 'Remove story' });
    expect(confirm).toBeDisabled();

    await user.type(within(dialog).getByRole('textbox'), 'Scam: selling fake tickets.');
    await user.click(confirm);

    await waitFor(() => {
      const removal = requests(fetchMock).find((request) =>
        request.url.endsWith('/posts/77/remove'),
      );
      expect(JSON.parse(removal?.body ?? '{}')).toEqual({ reason: 'Scam: selling fake tickets.' });
    });
  });
});
