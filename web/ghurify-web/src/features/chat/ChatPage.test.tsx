import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { ChatPage } from './ChatPage';
import i18n from '@/i18n';
import { useAuthStore } from '@/features/auth/authStore';
import { renderScreen } from '@/test/render';
import { sampleTripDetail, stubApi } from '@/test/fetchStub';

function message(id: number, body: string, senderId = 3, extra: Record<string, unknown> = {}) {
  return {
    id,
    tripId: 7,
    senderId,
    senderName: 'Nusrat',
    kind: 'Message',
    body,
    isPinned: false,
    wasMasked: false,
    created: '2026-10-05T06:00:00Z',
    ...extra,
  };
}

describe('ChatPage', () => {
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

  it('shows the conversation and the pinned announcement', async () => {
    stubApi([
      [/\/api\/v1\/trips\/7\/chat\/read$/, null, 204],
      [
        /\/api\/v1\/trips\/7\/chat$/,
        {
          messages: [message(2, 'See you at Arambagh'), message(1, 'Hello all')],
          pinned: [message(5, 'Bus leaves at 9pm', 2, { kind: 'Announcement', isPinned: true })],
        },
      ],
      [/\/api\/v1\/trips\/7$/, sampleTripDetail],
    ]);

    renderScreen(<ChatPage />, { at: '/trips/7/chat', path: '/trips/:id/chat' });

    expect(await screen.findByText('See you at Arambagh')).toBeInTheDocument();
    expect(screen.getByText('Hello all')).toBeInTheDocument();
    expect(screen.getByText('Bus leaves at 9pm')).toBeInTheDocument();
  });

  it('warns the sender when numbers were hidden', async () => {
    const user = userEvent.setup();
    stubApi([
      [/\/api\/v1\/trips\/7\/chat\/read$/, null, 204],
      [/\/api\/v1\/trips\/7\/chat$/, { messages: [], pinned: [] }],
      [/\/api\/v1\/trips\/7$/, sampleTripDetail],
    ]);

    renderScreen(<ChatPage />, { at: '/trips/7/chat', path: '/trips/:id/chat' });
    await screen.findByText('No messages yet. Say hello to the group.');

    stubApi([
      [
        /\/api\/v1\/trips\/7\/chat$/,
        {
          message: message(9, 'Call me on ••••••••', 1, { wasMasked: true }),
          contactsMasked: true,
        },
      ],
      [/\/api\/v1\/trips\/7\/chat\/read$/, null, 204],
    ]);
    await user.type(screen.getByLabelText('Write a message'), 'Call me on 01712345678');
    await user.click(screen.getByRole('button', { name: 'Send' }));

    expect(
      await screen.findByText(
        /Phone and wallet numbers are hidden until everyone in the group has paid/,
      ),
    ).toBeInTheDocument();
    expect(screen.getByText('Call me on ••••••••')).toBeInTheDocument();
  });

  it('tells someone who is not on the trip that the chat is not for them', async () => {
    stubApi([
      [/\/api\/v1\/trips\/7\/chat$/, { title: 'Not found', code: 'chat_not_found' }, 404],
      [/\/api\/v1\/trips\/7$/, sampleTripDetail],
    ]);

    renderScreen(<ChatPage />, { at: '/trips/7/chat', path: '/trips/:id/chat' });

    expect(await screen.findByText('This chat is for the people on the trip.')).toBeInTheDocument();
  });
});
