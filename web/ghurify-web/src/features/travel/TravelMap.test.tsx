import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import i18n from '@/i18n';
import { useAuthStore } from '@/features/auth/authStore';
import { renderScreen } from '@/test/render';
import { chooseOption } from '@/test/chooseOption';
import { requests, sampleDestinations, stubApi } from '@/test/fetchStub';
import { districtAt, districtCount, districtsByDivision, placesByDistrict } from './districts';
import { pdfFromJpeg } from './posterExport';
import { SharedTravelMap } from './SharedTravelMap';
import type { AddVisitCommand, VisitedPlace } from './travelApi';
import { TravelMapPage } from './TravelMapPage';
import type { TravelMapCanvasProps } from './TravelMapCanvas';

// Leaflet needs a real browser to draw; here the map is a button per pin, which is what matters.
vi.mock('./TravelMapCanvas', () => ({
  default: ({ places, upcoming = [], onSelect }: TravelMapCanvasProps) => (
    <div data-testid="map">
      {places.map((place) => (
        <button key={place.key} type="button" onClick={() => onSelect(place.key)}>
          pin {place.name}
        </button>
      ))}
      {upcoming.map((trip) => (
        <button
          key={String(trip.tripId)}
          type="button"
          onClick={() => onSelect(`u:${String(trip.tripId)}`)}
        >
          upcoming {trip.destinationName}
        </button>
      ))}
    </div>
  ),
}));

vi.mock('@/features/feed/feedApi', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/features/feed/feedApi')>()),
  uploadToStorage: vi.fn((_url: string, _file: File, onProgress: (fraction: number) => void) => {
    onProgress(1);
    return Promise.resolve();
  }),
}));

vi.mock('./PinPicker', () => ({
  default: ({ onPick }: { onPick: (point: [number, number]) => void }) => (
    <button type="button" onClick={() => onPick([24.894929, 91.868706])}>
      pick a spot near Sylhet
    </button>
  ),
}));

const map = {
  userId: 7,
  displayName: 'Mitu Akter',
  shared: false,
  summary: {
    places: 2,
    divisions: 2,
    trips: 1,
    tripDays: 3,
    firstVisit: '2025-12-02',
    lastVisit: '2026-03-01',
  },
  places: [
    {
      key: 'd:sajek',
      destinationSlug: 'sajek',
      name: 'Sajek Valley',
      nameBn: 'সাজেক ভ্যালি',
      division: 'Chattogram',
      kind: 'Hills',
      latitude: 23.382,
      longitude: 92.2938,
      lastVisitedOn: '2026-03-01',
      visits: [
        {
          id: 31,
          source: 'Trip',
          visitedOn: '2026-03-01',
          note: null,
          photosProcessing: 0,
          trip: {
            id: 9,
            title: 'Sajek sunrise weekend',
            startDate: '2026-03-01',
            endDate: '2026-03-03',
            hostName: 'Nadia Rahman',
            asHost: false,
          },
        },
      ],
      photos: [
        {
          mediaId: 70,
          postId: 12,
          visitId: null,
          kind: 'Image',
          url: 'https://storage.test/media/7/sajek.jpg',
        },
      ],
    },
    {
      key: 'p:32',
      destinationSlug: null,
      name: 'Ratargul swamp forest',
      nameBn: null,
      division: 'Sylhet',
      kind: null,
      latitude: 25.0099,
      longitude: 91.9325,
      lastVisitedOn: '2025-12-02',
      visits: [
        {
          id: 32,
          source: 'Added',
          visitedOn: '2025-12-02',
          note: 'Boat through the trees',
          photosProcessing: 0,
          trip: null,
        },
      ],
      photos: [
        {
          mediaId: 71,
          postId: null,
          visitId: 32,
          kind: 'Image',
          url: 'https://storage.test/media/7/ratargul.jpg',
        },
      ],
    },
  ],
  upcoming: [
    {
      tripId: 15,
      title: 'Tea garden trail',
      startDate: '2026-11-10',
      endDate: '2026-11-12',
      asHost: false,
      destinationSlug: 'sreemangal',
      destinationName: 'Sreemangal',
      destinationNameBn: 'শ্রীমঙ্গল',
      latitude: 24.3065,
      longitude: 91.7296,
    },
  ],
};

describe('travel map', () => {
  beforeEach(async () => {
    localStorage.clear();
    await i18n.changeLanguage('en');
    useAuthStore.setState({
      status: 'authenticated',
      user: { id: 7, maskedEmail: 'm****u@example.com', displayName: 'Mitu Akter' },
    });
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('shows the summary and the history, and opens a place from its pin', async () => {
    const user = userEvent.setup();
    stubApi([[/\/api\/v1\/me\/travel-map$/, map]]);

    renderScreen(<TravelMapPage />, { at: '/me/map' });

    expect(await screen.findByText('2/8')).toBeInTheDocument();
    expect(screen.getByText('2/64')).toBeInTheDocument();
    const history = screen.getByRole('region', { name: 'Your travel history' });
    expect(within(history).getByText('Tea garden trail', { exact: false })).toBeInTheDocument();
    expect(within(history).getByText('Ratargul swamp forest')).toBeInTheDocument();

    const drawing = screen.getByRole('group', {
      name: 'Districts of Bangladesh: 2 of 64 visited',
    });
    await user.click(within(drawing).getByRole('button', { name: 'Sajek Valley' }));

    const place = screen.getByRole('article', { name: 'Sajek Valley' });
    expect(within(place).getByRole('link', { name: 'Sajek sunrise weekend' })).toHaveAttribute(
      'href',
      '/trips/9',
    );
    expect(within(place).getByText('Hosted by Nadia Rahman')).toBeInTheDocument();
    expect(within(place).getByText('2 nights')).toBeInTheDocument();
    expect(within(place).getByRole('img', { name: 'A photo from Sajek Valley' })).toHaveAttribute(
      'src',
      'https://storage.test/media/7/sajek.jpg',
    );
    expect(within(place).getByRole('link', { name: 'Share a story about it' })).toHaveAttribute(
      'href',
      '/feed?destination=sajek',
    );
  });

  it('adds a place somewhere else, by name, division and a pin on the map', async () => {
    const user = userEvent.setup();
    const fetchMock = stubApi([
      [/\/api\/v1\/me\/travel-map\/visits$/, { id: 40 }, 201],
      [/\/api\/v1\/me\/travel-map$/, map],
      [/\/api\/v1\/destinations$/, sampleDestinations],
    ]);

    renderScreen(<TravelMapPage />, { at: '/me/map' });
    await user.click(await screen.findByRole('button', { name: 'Add a place' }));
    const dialog = screen.getByRole('dialog', { name: 'Add a place you have been' });

    await user.click(within(dialog).getByText('Somewhere else'));
    await user.type(within(dialog).getByLabelText('Name of the place'), 'Jaflong');
    await chooseOption(user, within(dialog).getByLabelText('Division'), 'Sylhet');
    await user.click(within(dialog).getByRole('button', { name: 'pick a spot near Sylhet' }));
    await user.type(within(dialog).getByLabelText('When you went'), '2026-09-20');
    await user.type(within(dialog).getByLabelText('A note (optional)'), 'Stones and clear water');
    await user.click(within(dialog).getByRole('button', { name: 'Add to my map' }));

    await waitFor(() => {
      const added = requests(fetchMock).find(
        (request) => request.method === 'POST' && request.url.endsWith('/travel-map/visits'),
      );
      expect(JSON.parse(added?.body ?? '{}')).toEqual({
        destinationSlug: null,
        placeName: 'Jaflong',
        division: 'Sylhet',
        latitude: 24.894929,
        longitude: 91.868706,
        visitedOn: '2026-09-20',
        note: 'Stones and clear water',
      });
    });
  });

  it('asks for a pin before adding a place that is not a destination', async () => {
    const user = userEvent.setup();
    const fetchMock = stubApi([
      [/\/api\/v1\/me\/travel-map$/, map],
      [/\/api\/v1\/destinations$/, sampleDestinations],
    ]);

    renderScreen(<TravelMapPage />, { at: '/me/map' });
    await user.click(await screen.findByRole('button', { name: 'Add a place' }));
    const dialog = screen.getByRole('dialog', { name: 'Add a place you have been' });
    await user.click(within(dialog).getByText('Somewhere else'));
    await user.type(within(dialog).getByLabelText('Name of the place'), 'Jaflong');
    await chooseOption(user, within(dialog).getByLabelText('Division'), 'Sylhet');
    await user.type(within(dialog).getByLabelText('When you went'), '2026-09-20');
    await user.click(within(dialog).getByRole('button', { name: 'Add to my map' }));

    expect(
      await within(dialog).findByText('Tap the map to put the pin on the place.'),
    ).toBeInTheDocument();
    expect(requests(fetchMock).some((request) => request.method === 'POST')).toBe(false);
  });

  it('takes a visit off the map only after confirming', async () => {
    const user = userEvent.setup();
    const fetchMock = stubApi([
      [/\/api\/v1\/me\/travel-map\/visits\/32$/, null, 204],
      [/\/api\/v1\/me\/travel-map$/, map],
    ]);
    const removals = () =>
      requests(fetchMock).filter(
        (request) => request.method === 'DELETE' && request.url.endsWith('/visits/32'),
      );

    renderScreen(<TravelMapPage />, { at: '/me/map' });
    const drawing = await screen.findByRole('group', { name: /^Districts of Bangladesh/ });
    await user.click(within(drawing).getByRole('button', { name: 'Ratargul swamp forest' }));
    const place = screen.getByRole('article', { name: 'Ratargul swamp forest' });
    expect(within(place).getByText('“Boat through the trees”')).toBeInTheDocument();

    await user.click(within(place).getByRole('button', { name: 'Remove' }));
    expect(removals()).toHaveLength(0);
    const confirm = within(place).getByRole('group', { name: 'Take this off your map?' });
    await user.click(within(confirm).getByRole('button', { name: 'Remove' }));

    await waitFor(() => expect(removals()).toHaveLength(1));
  });

  it('shares the map on the public profile with one switch', async () => {
    const user = userEvent.setup();
    const fetchMock = stubApi([
      [/\/api\/v1\/me\/travel-map\/sharing$/, null, 204],
      [/\/api\/v1\/me\/travel-map$/, map],
    ]);

    renderScreen(<TravelMapPage />, { at: '/me/map' });
    const toggle = await screen.findByRole('switch', { name: 'Show on my public profile' });
    expect(toggle).toHaveAttribute('aria-checked', 'false');
    await user.click(toggle);

    await waitFor(() => {
      const sharing = requests(fetchMock).find((request) => request.url.endsWith('/sharing'));
      expect(JSON.parse(sharing?.body ?? '{}')).toEqual({ share: true });
    });
  });

  it('shows a shared map on a public profile, and nothing when it is not shared', async () => {
    stubApi([[/\/api\/v1\/users\/7\/travel-map$/, { ...map, shared: true, upcoming: [] }]]);
    const { unmount } = renderScreen(<SharedTravelMap userId={7} name="Mitu Akter" />);

    expect(await screen.findByText('Where Mitu Akter has been')).toBeInTheDocument();
    const chips = screen.getByRole('list', { name: 'Places visited' });
    expect(within(chips).getByRole('button', { name: 'Sajek Valley' })).toHaveAttribute(
      'aria-pressed',
      'true',
    );
    unmount();

    stubApi([[/\/api\/v1\/users\/7\/travel-map$/, { ...map, shared: false, places: [] }]]);
    renderScreen(<SharedTravelMap userId={7} name="Mitu Akter" />);
    await waitFor(() => expect(screen.queryByText('Where Mitu Akter has been')).toBeNull());
  });

  it('opens a visited district from the list, with the places there', async () => {
    const user = userEvent.setup();
    stubApi([[/\/api\/v1\/me\/travel-map$/, map]]);

    renderScreen(<TravelMapPage />, { at: '/me/map' });
    const districts = await screen.findByRole('region', { name: 'Districts you have been to' });
    expect(within(districts).getByText('Chattogram division')).toBeInTheDocument();
    await user.click(
      within(districts).getByRole('button', { name: 'Rangamati: visited, 1 place' }),
    );

    const district = screen.getByRole('article', { name: 'Rangamati' });
    await user.click(within(district).getByRole('button', { name: /Sajek Valley/ }));
    expect(screen.getByRole('article', { name: 'Sajek Valley' })).toBeInTheDocument();
  });

  it('starts a place in a district not visited yet, already pinned there', async () => {
    const user = userEvent.setup();
    const fetchMock = stubApi([
      [/\/api\/v1\/me\/travel-map\/visits$/, { id: 41 }, 201],
      [/\/api\/v1\/me\/travel-map$/, map],
      [/\/api\/v1\/destinations$/, sampleDestinations],
    ]);

    renderScreen(<TravelMapPage />, { at: '/me/map' });
    const districts = await screen.findByRole('region', { name: 'Districts you have been to' });
    await user.type(within(districts).getByLabelText('Find a district'), 'bog');
    await user.click(
      within(districts).getByRole('button', { name: 'Bogura: not yet. Add a place there' }),
    );

    const dialog = screen.getByRole('dialog', { name: 'Add a place you have been' });
    expect(within(dialog).getByLabelText('Name of the place')).toHaveValue('Bogura');
    expect(within(dialog).getByLabelText('Division')).toHaveTextContent('Rajshahi');
    expect(within(dialog).getByText(/The pin is in the middle of Bogura/)).toBeInTheDocument();
    await user.type(within(dialog).getByLabelText('When you went'), '2026-01-15');
    await user.click(within(dialog).getByRole('button', { name: 'Add to my map' }));

    await waitFor(() => {
      const added = requests(fetchMock).find((request) => request.method === 'POST');
      const body = JSON.parse(added?.body ?? '{}') as AddVisitCommand;
      expect(body).toMatchObject({ placeName: 'Bogura', division: 'Rajshahi' });
      expect(districtAt(Number(body.latitude), Number(body.longitude))?.slug).toBe('bogura');
    });
  });

  it('opens the street map on request, and remembers it', async () => {
    const user = userEvent.setup();
    stubApi([[/\/api\/v1\/me\/travel-map$/, map]]);

    renderScreen(<TravelMapPage />, { at: '/me/map' });
    await user.click(await screen.findByRole('button', { name: 'Street map' }));

    expect(await screen.findByRole('button', { name: 'pin Sajek Valley' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Street map' })).toHaveAttribute(
      'aria-pressed',
      'true',
    );
    expect(localStorage.getItem('ghurify.travelMap.view')).toBe('street');
  });

  it('styles the poster with a theme and the name on it', async () => {
    const user = userEvent.setup();
    stubApi([[/\/api\/v1\/me\/travel-map$/, map]]);

    renderScreen(<TravelMapPage />, { at: '/me/map' });
    expect(await screen.findByRole('heading', { name: "Mitu Akter's Bangladesh" })).toBeVisible();

    await user.click(screen.getByRole('radio', { name: 'Night' }));
    expect(screen.getByRole('radio', { name: 'Night' })).toHaveAttribute('aria-checked', 'true');

    const name = screen.getByLabelText('Name on the map');
    await user.clear(name);
    expect(screen.getByRole('heading', { name: 'My Bangladesh' })).toBeVisible();
    expect(screen.getByText('3% of Bangladesh explored')).toBeInTheDocument();
  });
});

describe('travel photos', () => {
  beforeEach(async () => {
    localStorage.clear();
    await i18n.changeLanguage('en');
    useAuthStore.setState({
      status: 'authenticated',
      user: { id: 7, maskedEmail: 'm****u@example.com', displayName: 'Mitu Akter' },
    });
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  const photo = () => new File(['jpeg'], 'boat.jpg', { type: 'image/jpeg' });

  it('adds a place with a photo, uploaded first and sent with it by id', async () => {
    const user = userEvent.setup();
    const fetchMock = stubApi([
      [
        /\/api\/v1\/media\/upload-url$/,
        {
          mediaId: 90,
          uploadUrl: 'https://storage.test/upload/90',
          expiresOn: '2026-10-08T10:00:00Z',
        },
      ],
      [/\/api\/v1\/media\/90\/complete$/, null, 202],
      [/\/api\/v1\/me\/travel-map\/visits$/, { id: 41 }, 201],
      [/\/api\/v1\/me\/travel-map$/, map],
      [/\/api\/v1\/destinations$/, sampleDestinations],
    ]);

    renderScreen(<TravelMapPage />, { at: '/me/map' });
    await user.click(await screen.findByRole('button', { name: 'Add a place' }));
    const dialog = screen.getByRole('dialog', { name: 'Add a place you have been' });
    await chooseOption(
      user,
      within(dialog).getByLabelText('Destination', { exact: true }),
      'sajek',
    );
    await user.type(within(dialog).getByLabelText('When you went'), '2026-09-20');
    await user.upload(within(dialog).getByLabelText('Add photos'), photo());

    expect(
      await within(dialog).findByRole('button', { name: 'Leave out boat.jpg' }),
    ).toBeInTheDocument();
    await waitFor(() =>
      expect(within(dialog).getByRole('button', { name: 'Add to my map' })).toBeEnabled(),
    );
    await user.click(within(dialog).getByRole('button', { name: 'Add to my map' }));

    await waitFor(() => {
      const added = requests(fetchMock).find(
        (request) => request.method === 'POST' && request.url.endsWith('/travel-map/visits'),
      );
      expect(JSON.parse(added?.body ?? '{}')).toMatchObject({
        destinationSlug: 'sajek',
        mediaIds: [90],
      });
    });
    expect(requests(fetchMock).some((request) => request.url.endsWith('/media/90/complete'))).toBe(
      true,
    );
  });

  it('only takes photos', async () => {
    const user = userEvent.setup({ applyAccept: false });
    stubApi([
      [/\/api\/v1\/me\/travel-map$/, map],
      [/\/api\/v1\/destinations$/, sampleDestinations],
    ]);

    renderScreen(<TravelMapPage />, { at: '/me/map' });
    await user.click(await screen.findByRole('button', { name: 'Add a place' }));
    const dialog = screen.getByRole('dialog', { name: 'Add a place you have been' });
    await user.upload(
      within(dialog).getByLabelText('Add photos'),
      new File(['%PDF'], 'ticket.pdf', { type: 'application/pdf' }),
    );

    expect(
      within(dialog).getByText('Only JPEG, PNG or WebP photos can be added.'),
    ).toBeInTheDocument();
    expect(within(dialog).queryByRole('button', { name: 'Leave out ticket.pdf' })).toBeNull();
  });

  it('shows a visit’s photos with the place, opens them large, and removes one after confirming', async () => {
    const user = userEvent.setup();
    const fetchMock = stubApi([
      [/\/api\/v1\/me\/travel-map\/visits\/32\/photos\/71$/, null, 204],
      [/\/api\/v1\/me\/travel-map$/, map],
    ]);

    renderScreen(<TravelMapPage />, { at: '/me/map' });
    const drawing = await screen.findByRole('group', { name: /^Districts of Bangladesh/ });
    await user.click(within(drawing).getByRole('button', { name: 'Ratargul swamp forest' }));
    const place = screen.getByRole('article', { name: 'Ratargul swamp forest' });

    const own = within(place).getByRole('list', { name: 'Your photos from Ratargul swamp forest' });
    await user.click(within(own).getByRole('button', { name: 'Open photo 1 of 1' }));
    const viewer = screen.getByRole('dialog', { name: 'Ratargul swamp forest' });
    expect(
      within(viewer).getByRole('img', { name: 'A photo from Ratargul swamp forest' }),
    ).toHaveAttribute('src', 'https://storage.test/media/7/ratargul.jpg');
    expect(within(viewer).getByText('2 Dec 2025')).toBeInTheDocument();
    await user.click(within(viewer).getByRole('button', { name: 'Close' }));

    await user.click(within(own).getByRole('button', { name: 'Remove photo 1' }));
    const removals = () => requests(fetchMock).filter((request) => request.method === 'DELETE');
    expect(removals()).toHaveLength(0);
    const confirm = within(place).getByRole('group', { name: 'Remove this photo from your map?' });
    await user.click(within(confirm).getByRole('button', { name: 'Remove' }));

    await waitFor(() => expect(removals()).toHaveLength(1));
    expect(removals()[0]!.url).toMatch(/\/visits\/32\/photos\/71$/);
  });

  it('adds photos to a visit already on the map', async () => {
    const user = userEvent.setup();
    const fetchMock = stubApi([
      [
        /\/api\/v1\/media\/upload-url$/,
        {
          mediaId: 90,
          uploadUrl: 'https://storage.test/upload/90',
          expiresOn: '2026-10-08T10:00:00Z',
        },
      ],
      [/\/api\/v1\/media\/90\/complete$/, null, 202],
      [/\/api\/v1\/me\/travel-map\/visits\/31\/photos$/, null, 204],
      [/\/api\/v1\/me\/travel-map$/, map],
    ]);

    renderScreen(<TravelMapPage />, { at: '/me/map' });
    const drawing = await screen.findByRole('group', { name: /^Districts of Bangladesh/ });
    await user.click(within(drawing).getByRole('button', { name: 'Sajek Valley' }));
    const place = screen.getByRole('article', { name: 'Sajek Valley' });

    await user.click(within(place).getByRole('button', { name: 'Add photos' }));
    await user.upload(within(place).getByLabelText('Add photos'), photo());
    const save = within(place).getByRole('button', { name: 'Save photos' });
    await waitFor(() => expect(save).toBeEnabled());
    await user.click(save);

    await waitFor(() => {
      const added = requests(fetchMock).find((request) =>
        request.url.endsWith('/visits/31/photos'),
      );
      expect(JSON.parse(added?.body ?? '{}')).toEqual({ mediaIds: [90] });
    });
  });
});

describe('districts', () => {
  it.each([
    ['Sajek Valley', 23.382, 92.2938, 'rangamati'],
    ['Bandarban', 22.1953, 92.2184, 'bandarban'],
    ["Cox's Bazar", 21.4272, 92.0058, 'coxs-bazar'],
    ["Saint Martin's Island", 20.6237, 92.3234, 'coxs-bazar'],
    ['Sylhet', 24.8949, 91.8687, 'sylhet'],
    ['Sreemangal', 24.3065, 91.7296, 'moulvibazar'],
    ['Tanguar Haor', 25.15, 91.0833, 'sunamganj'],
    ['Kuakata', 21.8167, 90.1167, 'patuakhali'],
    ['Rangamati', 22.6533, 92.175, 'rangamati'],
    ['Dhaka', 23.8103, 90.4125, 'dhaka'],
  ])('puts %s in its district', (_, latitude, longitude, district) => {
    expect(districtAt(latitude, longitude)?.slug).toBe(district);
  });

  it('puts the Sundarbans in Khulna division', () => {
    expect(districtAt(21.9497, 89.1833)?.division).toBe('Khulna');
  });

  it('finds no district across the border', () => {
    expect(districtAt(22.5726, 88.3639)).toBeNull(); // Kolkata
    expect(districtAt(23.8315, 91.2868)).toBeNull(); // Agartala
    expect(districtAt(25.5788, 91.8933)).toBeNull(); // Shillong
  });

  it('has all 64 districts, each in its division', () => {
    expect(districtCount).toBe(64);
    expect(
      Object.fromEntries(
        districtsByDivision.map(({ division, districts }) => [division, districts.length]),
      ),
    ).toEqual({
      Rangpur: 8,
      Rajshahi: 8,
      Mymensingh: 4,
      Sylhet: 4,
      Dhaka: 13,
      Khulna: 10,
      Barishal: 6,
      Chattogram: 11,
    });
  });

  it('groups places by the district they are in', () => {
    const grouped = placesByDistrict(map.places as unknown as VisitedPlace[]);
    expect([...grouped.keys()].sort()).toEqual(['rangamati', 'sylhet']);
  });
});

describe('poster PDF', () => {
  it('writes a one-page PDF whose table points at every object', async () => {
    const jpeg = new Uint8Array([0xff, 0xd8, 0xff, 0xe0, 1, 2, 3, 0xff, 0xd9]);
    const blob = pdfFromJpeg(jpeg, 1200, 1600);
    const bytes = new Uint8Array(
      await new Promise<ArrayBuffer>((resolve) => {
        const reader = new FileReader();
        reader.onload = () => resolve(reader.result as ArrayBuffer);
        reader.readAsArrayBuffer(blob);
      }),
    );
    // One character per byte, so string positions are byte offsets.
    const text = Array.from(bytes, (byte) => String.fromCharCode(byte)).join('');

    expect(blob.type).toBe('application/pdf');
    expect(text.startsWith('%PDF-1.4')).toBe(true);
    expect(text).toContain('/MediaBox [0 0 600 800]');
    expect(text).toContain('/Width 1200 /Height 1600');
    const table = Number(/startxref\n(\d+)/.exec(text)?.[1]);
    expect(text.slice(table, table + 4)).toBe('xref');
    const offsets = [...text.slice(table).matchAll(/(\d{10}) 00000 n /g)].map((match) =>
      Number(match[1]),
    );
    expect(offsets).toHaveLength(5);
    offsets.forEach((offset, index) =>
      expect(text.slice(offset).startsWith(`${index + 1} 0 obj`)).toBe(true),
    );
  });
});
