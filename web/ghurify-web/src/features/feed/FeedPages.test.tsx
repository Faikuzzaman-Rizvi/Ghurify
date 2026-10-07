import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { FeedPage } from './FeedPage';
import { PublicProfilePage } from './PublicProfilePage';
import { ReviewPage } from './ReviewPage';
import i18n from '@/i18n';
import { useAuthStore } from '@/features/auth/authStore';
import { renderScreen } from '@/test/render';
import { requests, sampleDestinations, stubApi } from '@/test/fetchStub';

const post = {
  id: 40,
  authorId: 3,
  authorName: 'Nadia Rahman',
  authorVerifiedLevel: 'NidSelfie',
  body: 'Clouds rolling over Sajek at dawn.',
  destinationSlug: 'sajek',
  destinationName: 'Sajek Valley',
  destinationNameBn: 'সাজেক ভ্যালি',
  tripId: null,
  likes: 4,
  comments: 1,
  likedByMe: false,
  created: '2026-10-05T06:00:00Z',
  media: [
    { id: 9, kind: 'Image', contentType: 'image/jpeg', url: 'https://storage.test/media/a.jpg' },
  ],
};

describe('stories and reviews', () => {
  beforeEach(async () => {
    await i18n.changeLanguage('en');
    useAuthStore.setState({
      status: 'authenticated',
      user: { id: 1, maskedEmail: 'r****i@example.com', displayName: 'Rizvi' },
    });
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('shows stories with their photos, and likes one', async () => {
    const user = userEvent.setup();
    const fetchMock = stubApi([
      [/\/api\/v1\/posts\/40\/likes$/, null, 204],
      [/\/api\/v1\/feed/, { items: [post], nextBefore: null }],
      [/\/api\/v1\/destinations$/, sampleDestinations],
    ]);

    renderScreen(<FeedPage />);

    expect(await screen.findByText('Clouds rolling over Sajek at dawn.')).toBeInTheDocument();
    expect(screen.getByRole('img', { name: 'Photo shared by Nadia Rahman' })).toHaveAttribute(
      'src',
      'https://storage.test/media/a.jpg',
    );
    expect(screen.getByText('ID + selfie verified')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: '♡ 4' }));
    await waitFor(() =>
      expect(
        requests(fetchMock).some(
          (request) => request.method === 'POST' && request.url.endsWith('/posts/40/likes'),
        ),
      ).toBe(true),
    );
  });

  it('shows a person with their rating and reviews, and lets you follow them', async () => {
    stubApi([
      [
        /\/api\/v1\/users\/3$/,
        {
          userId: 3,
          displayName: 'Nadia Rahman',
          bio: 'I host hill trips for women.',
          homeDistrict: 'Chattogram',
          memberSince: '2026-01-10',
          verifiedLevel: 'NidSelfie',
          isHost: true,
          followers: 12,
          following: 3,
          followedByMe: false,
          asHostCount: 2,
          asHostAverage: 4.5,
          asTravelerCount: 0,
          asTravelerAverage: null,
          hostedTrips: [],
          reviews: [
            {
              id: 1,
              tripId: 7,
              tripTitle: 'Sylhet for women',
              reviewerId: 4,
              reviewerName: 'Farhana',
              direction: 'TravelerToHost',
              rating: 5,
              body: 'Felt safe the whole way.',
              created: '2026-09-01T00:00:00Z',
            },
          ],
        },
      ],
      [/\/api\/v1\/users\/3\/posts$/, { items: [], nextBefore: null }],
    ]);

    renderScreen(<PublicProfilePage />, { at: '/users/3', path: '/users/:id' });

    expect(await screen.findByRole('heading', { name: 'Nadia Rahman' })).toBeInTheDocument();
    expect(screen.getByText('Felt safe the whole way.')).toBeInTheDocument();
    expect(screen.getByText(/4\.5 ★ as host \(2 reviews\)/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Follow' })).toBeInTheDocument();
  });

  it('will not submit a review without a rating, then submits one', async () => {
    const user = userEvent.setup();
    const fetchMock = stubApi([
      [/\/api\/v1\/trips\/7\/reviews$/, { id: 1 }, 201],
      [
        /\/api\/v1\/trips\/7\/reviewable$/,
        [
          {
            userId: 3,
            displayName: 'Nadia Rahman',
            direction: 'TravelerToHost',
            alreadyReviewed: false,
          },
        ],
      ],
    ]);

    renderScreen(<ReviewPage />, { at: '/trips/7/review', path: '/trips/:id/review' });

    const submit = await screen.findByRole('button', { name: 'Submit review' });
    expect(submit).toBeDisabled();

    await user.click(screen.getByRole('button', { name: '4 out of 5' }));
    await user.click(submit);

    await waitFor(() => {
      const sent = requests(fetchMock).find((request) => request.method === 'POST');
      expect(JSON.parse(sent!.body!)).toMatchObject({
        revieweeId: 3,
        direction: 'TravelerToHost',
        rating: 4,
      });
    });
  });
});
