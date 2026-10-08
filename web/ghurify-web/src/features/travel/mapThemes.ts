/**
 * The looks a travel map can take, on the page and in the downloaded poster alike. Raw colours on
 * purpose: the poster is drawn on a canvas, which cannot read Tailwind classes. Each stays within
 * Ghurify's palette (hill, deep, night, turmeric, jamdani, river, sand, dusk).
 */
export interface MapTheme {
  id: 'hill' | 'jamdani' | 'river' | 'turmeric' | 'night';
  /** The poster's paper. */
  paper: string;
  /** A district not visited yet, and the lines between districts. */
  land: string;
  border: string;
  /** Where one division meets another. */
  divisionLine: string;
  /** A visited district: a gradient, light edge to deep. */
  visited: readonly [string, string];
  /** The dot on a visited district, and the place pins. */
  dot: string;
  /** Hovered or chosen district. */
  highlight: string;
  ink: string;
  muted: string;
  /** The big count, and the filled part of the progress bar. */
  accent: string;
  track: string;
  /** A district name on the map: text, and the halo that keeps it readable over any fill. */
  label: string;
  labelHalo: string;
}

export const mapThemes: readonly MapTheme[] = [
  {
    id: 'hill',
    paper: '#f7f2e8',
    land: '#e6dece',
    border: '#fbf8f2',
    divisionLine: '#c4b796',
    visited: ['#2f7a57', '#1d4d37'],
    dot: '#d99a12',
    highlight: '#f2c46d',
    ink: '#173f2e',
    muted: '#6b7a70',
    accent: '#245c43',
    track: '#e3dccd',
    label: '#ffffff',
    labelHalo: '#173f2e',
  },
  {
    id: 'jamdani',
    paper: '#fbf1f3',
    land: '#eedee3',
    border: '#fdf8f9',
    divisionLine: '#d7b6c1',
    visited: ['#c2477a', '#83244a'],
    dot: '#f2c46d',
    highlight: '#f2c46d',
    ink: '#4a1830',
    muted: '#86606e',
    accent: '#a3305c',
    track: '#ecdbe1',
    label: '#ffffff',
    labelHalo: '#5b1736',
  },
  {
    id: 'river',
    paper: '#eef6f6',
    land: '#d8e7e8',
    border: '#f6fbfb',
    divisionLine: '#a9c6ca',
    visited: ['#2a93a8', '#175b73'],
    dot: '#d99a12',
    highlight: '#f2c46d',
    ink: '#123b4a',
    muted: '#5a7a84',
    accent: '#1f6f8b',
    track: '#d3e4e6',
    label: '#ffffff',
    labelHalo: '#123b4a',
  },
  {
    id: 'turmeric',
    paper: '#fdf6e6',
    land: '#f1e4c4',
    border: '#fffaf0',
    divisionLine: '#dcc58f',
    visited: ['#e8a623', '#c0701a'],
    dot: '#173f2e',
    highlight: '#245c43',
    ink: '#3d2a06',
    muted: '#8a7347',
    accent: '#b9770c',
    track: '#efe1bf',
    label: '#ffffff',
    labelHalo: '#6b3f06',
  },
  {
    id: 'night',
    paper: '#0f2a1f',
    land: '#1d3d2f',
    border: '#0f2a1f',
    divisionLine: '#3a6450',
    visited: ['#f2c46d', '#d99a12'],
    dot: '#ffffff',
    highlight: '#ffffff',
    ink: '#ffffff',
    muted: '#9db8aa',
    accent: '#f2c46d',
    track: '#24473a',
    label: '#0f2a1f',
    labelHalo: '#fdf3d6',
  },
];

export const defaultTheme = mapThemes[0]!;

export const themeById = (id: string | null | undefined): MapTheme =>
  mapThemes.find((theme) => theme.id === id) ?? defaultTheme;
