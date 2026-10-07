import { vi } from 'vitest';

interface StubResponse {
  ok: boolean;
  status: number;
  statusText: string;
  json: () => Promise<unknown>;
  text: () => Promise<string>;
  headers: Headers;
}

/** A fetch response the API client can read: text() on success, json() on a problem. */
export function stubResponse(body: unknown, status = 200): StubResponse {
  const serialized = JSON.stringify(body);
  return {
    ok: status >= 200 && status < 300,
    status,
    statusText: status < 300 ? 'OK' : 'Error',
    json: () => Promise.resolve(body),
    text: () => Promise.resolve(serialized),
    headers: new Headers({ 'Content-Type': 'application/json' }),
  };
}

/**
 * Replaces fetch with a router: the first route whose pattern matches the URL answers.
 * Anything unmatched gets a 404, so a test never silently depends on a real network.
 * Returns the mock so tests can inspect which URLs were requested.
 */
export function stubApi(routes: [RegExp, unknown, number?][]) {
  // `_init` is not used to route, but is in the signature so tests can inspect method and body.
  const fetchMock = vi.fn((input: string | URL | Request, _init?: RequestInit) => {
    const url = urlOf(input);
    const route = routes.find(([pattern]) => pattern.test(url));

    return Promise.resolve(
      route ? stubResponse(route[1], route[2] ?? 200) : stubResponse({ title: 'Not found' }, 404),
    );
  });

  vi.stubGlobal('fetch', fetchMock);
  return fetchMock;
}

function urlOf(input: string | URL | Request): string {
  return typeof input === 'string' ? input : input instanceof URL ? input.href : input.url;
}

/** Every URL the stubbed fetch was asked for, in order. */
export function requestedUrls(fetchMock: ReturnType<typeof stubApi>): string[] {
  return fetchMock.mock.calls.map(([input]) => urlOf(input));
}

/** Every request the stubbed fetch received: URL, method and body text, in order. */
export function requests(
  fetchMock: ReturnType<typeof stubApi>,
): { url: string; method: string; body: string | undefined }[] {
  return fetchMock.mock.calls.map(([input, init]) => ({
    url: urlOf(input),
    method: init?.method ?? 'GET',
    body: typeof init?.body === 'string' ? init.body : undefined,
  }));
}

export const sampleDestinations = [
  {
    slug: 'sajek',
    name: 'Sajek Valley',
    nameBn: 'সাজেক ভ্যালি',
    division: 'Chattogram',
    divisionBn: 'চট্টগ্রাম',
    summary: 'A ridge-top village above the clouds.',
    summaryBn: 'মেঘের ওপরে পাহাড়চূড়ার গ্রাম।',
    kind: 'Hills',
    status: 'Open',
    statusNote: null,
    statusNoteBn: null,
    latitude: 23.382,
    longitude: 92.2938,
    upcomingTrips: 2,
    fromPrice: 6800,
  },
  {
    slug: 'saint-martins',
    name: "Saint Martin's Island",
    nameBn: 'সেন্ট মার্টিন দ্বীপ',
    division: 'Chattogram',
    divisionBn: 'চট্টগ্রাম',
    summary: 'The only coral island.',
    summaryBn: 'একমাত্র প্রবাল দ্বীপ।',
    kind: 'Island',
    status: 'Caution',
    statusNote: 'Visitor limits apply this season.',
    statusNoteBn: 'এই মৌসুমে পর্যটকের সংখ্যা সীমিত।',
    latitude: 20.6237,
    longitude: 92.3234,
    upcomingTrips: 1,
    fromPrice: 11500,
  },
];

export const sampleTrip = {
  id: 7,
  title: 'Sajek sunrise weekend',
  destination: {
    slug: 'sajek',
    name: 'Sajek Valley',
    nameBn: 'সাজেক ভ্যালি',
    kind: 'Hills',
    status: 'Open',
    statusNote: null,
    statusNoteBn: null,
  },
  startDate: '2026-10-14',
  endDate: '2026-10-16',
  seats: 14,
  seatsLeft: 5,
  pricePerPerson: 6800,
  groupType: 'Open',
  status: 'Published',
  hostName: 'Tanvir Hasan',
};

export const sampleTripDetail = {
  ...sampleTrip,
  summary: 'Three days above the clouds.',
  meetingPoint: 'Arambagh bus counter, Dhaka',
  host: { id: 2, displayName: 'Tanvir Hasan', memberSince: '2026-01-10' },
  costItems: [
    { category: 'Transport', description: 'AC bus and jeep', amount: 2800 },
    { category: 'Stay', description: 'Two nights in a cottage', amount: 1800 },
    { category: 'Food', description: 'Seven meals', amount: 1500 },
    { category: 'Fees', description: 'Entry fees', amount: 300 },
    { category: 'Buffer', description: 'Road delays', amount: 400 },
  ],
  itinerary: [
    { dayNo: 1, title: 'Into the hills', details: 'Jeep convoy up to Sajek.', difficulty: 'Easy' },
    {
      dayNo: 2,
      title: 'Sunrise over the clouds',
      details: 'Konglak Para at dawn.',
      difficulty: 'Moderate',
    },
    { dayNo: 3, title: 'Back to Dhaka', details: 'Overnight bus home.', difficulty: 'Easy' },
  ],
};

export function samplePage(items: unknown[], totalCount = items.length) {
  return { items, totalCount, page: 1, pageSize: 12 };
}
