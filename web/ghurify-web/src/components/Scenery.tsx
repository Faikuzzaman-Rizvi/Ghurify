import { useId, type ReactElement } from 'react';
import type { DestinationKind } from '@/features/trips/tripsApi';

/**
 * Illustrated landscapes, one per kind of destination. Drawn inline rather than loaded as
 * photos: nothing to download on slow mobile data, nothing to break offline, and every card
 * still looks like where it goes.
 *
 * Decorative only: callers put the place name in text next to it, so the SVG is aria-hidden.
 */
interface SceneryProps {
  kind: DestinationKind;
  className?: string;
  /** Drifting clouds and a floating sun, for the hero. Off for cards. */
  animated?: boolean;
}

interface Palette {
  skyTop: string;
  skyBottom: string;
  sun: string;
  far: string;
  mid: string;
  near: string;
  water: string;
  waterDeep: string;
}

const palettes: Record<DestinationKind, Palette> = {
  Hills: {
    skyTop: '#f4b98a',
    skyBottom: '#fde6c8',
    sun: '#fff1bf',
    far: '#9cc1ae',
    mid: '#4f8f6f',
    near: '#245c43',
    water: '#ffffff',
    waterDeep: '#ffffff',
  },
  Beach: {
    skyTop: '#7fcbe3',
    skyBottom: '#e3f5f8',
    sun: '#ffe28a',
    far: '#7fb7a0',
    mid: '#5fb9d1',
    near: '#f1dcaa',
    water: '#5fb9d1',
    waterDeep: '#22819f',
  },
  Island: {
    skyTop: '#6cc0dc',
    skyBottom: '#def3f7',
    sun: '#fff0a8',
    far: '#e8d197',
    mid: '#2d7a4f',
    near: '#1d5a3a',
    water: '#4fb3c8',
    waterDeep: '#1a7a96',
  },
  Forest: {
    skyTop: '#c6dfc0',
    skyBottom: '#eef5e6',
    sun: '#fff7d6',
    far: '#4f8a63',
    mid: '#2f6b4a',
    near: '#1b4630',
    water: '#7aa391',
    waterDeep: '#557f6d',
  },
  Wetland: {
    skyTop: '#92c4e6',
    skyBottom: '#eef6fb',
    sun: '#fff4c9',
    far: '#7f9fcc',
    mid: '#5c8fbf',
    near: '#2f6b4a',
    water: '#8fc2dc',
    waterDeep: '#4d93ba',
  },
  TeaGarden: {
    skyTop: '#cfe8bb',
    skyBottom: '#f6fbea',
    sun: '#fff6c7',
    far: '#8fc07a',
    mid: '#4f9b5c',
    near: '#2e7442',
    water: '#ffffff',
    waterDeep: '#ffffff',
  },
  Lake: {
    skyTop: '#f3c693',
    skyBottom: '#fdeedd',
    sun: '#ffe9b0',
    far: '#86ad95',
    mid: '#4f8a68',
    near: '#2f7350',
    water: '#5aa6c4',
    waterDeep: '#2c7f9f',
  },
  River: {
    skyTop: '#b6dce8',
    skyBottom: '#eef8fa',
    sun: '#fff6cf',
    far: '#7fae92',
    mid: '#4e8c69',
    near: '#2d6a49',
    water: '#5bb2c4',
    waterDeep: '#2f8ea3',
  },
};

export function Scenery({ kind, className, animated = false }: SceneryProps) {
  // useId may contain characters that are not valid in url(#...), so keep only safe ones.
  const id = useId().replace(/[^a-zA-Z0-9_-]/g, '');
  const p = palettes[kind];
  const sky = `sky-${id}`;
  const water = `water-${id}`;

  return (
    <svg
      viewBox="0 0 400 240"
      preserveAspectRatio="xMidYMax slice"
      className={className}
      aria-hidden="true"
      focusable="false"
    >
      <defs>
        <linearGradient id={sky} x1="0" y1="0" x2="0" y2="1">
          <stop offset="0%" stopColor={p.skyTop} />
          <stop offset="100%" stopColor={p.skyBottom} />
        </linearGradient>
        <linearGradient id={water} x1="0" y1="0" x2="0" y2="1">
          <stop offset="0%" stopColor={p.water} />
          <stop offset="100%" stopColor={p.waterDeep} />
        </linearGradient>
      </defs>

      <rect width="400" height="240" fill={`url(#${sky})`} />

      <g className={animated ? 'animate-float' : undefined}>
        <circle cx="300" cy="70" r="30" fill={p.sun} opacity="0.95" />
        <circle cx="300" cy="70" r="44" fill={p.sun} opacity="0.25" />
      </g>

      <Birds />

      {sceneFor(kind, p, `url(#${water})`)}

      <Clouds animated={animated} />
    </svg>
  );
}

function sceneFor(kind: DestinationKind, p: Palette, waterFill: string): ReactElement {
  switch (kind) {
    case 'Hills':
      return (
        <g>
          <path d="M0 140 Q 50 80 110 120 T 220 100 T 330 95 T 400 105 V240 H0Z" fill={p.far} />
          {/* The Sajek "sea of clouds" sits between the ridges. */}
          <ellipse cx="90" cy="150" rx="120" ry="16" fill="#ffffff" opacity="0.85" />
          <ellipse cx="300" cy="145" rx="130" ry="18" fill="#ffffff" opacity="0.8" />
          <path d="M0 175 Q 70 125 150 165 T 300 150 T 400 160 V240 H0Z" fill={p.mid} />
          <path d="M0 210 Q 110 175 210 205 T 400 195 V240 H0Z" fill={p.near} />
          <Cottage x={262} y={178} />
        </g>
      );

    case 'Beach':
      return (
        <g>
          <rect y="130" width="400" height="110" fill={waterFill} />
          <Waves y={150} />
          <Waves y={172} />
          <path d="M0 195 Q 130 172 270 198 T 400 190 V240 H0Z" fill={p.near} />
          <path
            d="M0 195 Q 130 172 270 198 T 400 190"
            fill="none"
            stroke="#ffffff"
            strokeWidth="3"
            opacity="0.8"
          />
          <Palm x={340} y={210} />
          <Palm x={372} y={214} scale={0.8} />
        </g>
      );

    case 'Island':
      return (
        <g>
          <rect y="135" width="400" height="105" fill={waterFill} />
          <Waves y={190} />
          <Waves y={215} />
          <ellipse cx="205" cy="160" rx="110" ry="20" fill={p.far} />
          <ellipse cx="205" cy="152" rx="80" ry="14" fill={p.mid} />
          <Palm x={170} y={152} />
          <Palm x={225} y={150} scale={0.9} />
          <Palm x={250} y={154} scale={0.7} />
          <Boat x={70} y={198} />
        </g>
      );

    case 'Forest':
      return (
        <g>
          <path d="M0 150 Q 100 125 200 145 T 400 140 V240 H0Z" fill={p.far} />
          <rect y="185" width="400" height="55" fill={waterFill} />
          <Waves y={205} />
          <Mangroves y={185} color={p.near} />
          <Mangroves y={182} color={p.mid} offset={22} />
          <Boat x={250} y={214} />
        </g>
      );

    case 'Wetland':
      return (
        <g>
          {/* The Meghalaya hills that turn blue at dusk over Tanguar Haor. */}
          <path d="M0 145 Q 70 105 150 135 T 290 120 T 400 130 V240 H0Z" fill={p.far} />
          <path d="M0 155 Q 90 135 190 152 T 400 148 V240 H0Z" fill={p.mid} opacity="0.7" />
          <rect y="158" width="400" height="82" fill={waterFill} />
          <Waves y={185} />
          <Waves y={210} />
          <rect x="40" y="148" width="4" height="14" fill={p.near} />
          <circle cx="42" cy="146" r="8" fill={p.near} />
          <Houseboat x={210} y={190} />
        </g>
      );

    case 'TeaGarden':
      return (
        <g>
          <path d="M0 140 Q 90 105 190 135 T 400 125 V240 H0Z" fill={p.far} />
          <path d="M0 165 Q 120 130 240 160 T 400 150 V240 H0Z" fill={p.mid} />
          {[0, 1, 2, 3, 4, 5].map((row) => (
            <path
              key={row}
              d={`M-10 ${178 + row * 12} Q 120 ${150 + row * 12} 240 ${175 + row * 12} T 410 ${168 + row * 12}`}
              fill="none"
              stroke={p.near}
              strokeWidth="7"
              strokeLinecap="round"
              opacity={0.85}
            />
          ))}
          <ShadeTree x={90} y={168} color={p.near} />
          <ShadeTree x={300} y={160} color={p.near} />
        </g>
      );

    case 'Lake':
      return (
        <g>
          <path d="M0 140 Q 60 95 130 130 T 260 115 T 400 125 V240 H0Z" fill={p.far} />
          <path d="M0 160 Q 100 130 200 155 T 400 150 V240 H0Z" fill={p.mid} />
          <rect y="168" width="400" height="72" fill={waterFill} />
          <Waves y={192} />
          <ellipse cx="110" cy="180" rx="45" ry="9" fill={p.near} />
          <ellipse cx="300" cy="186" rx="35" ry="7" fill={p.near} />
          <Boat x={200} y={210} />
        </g>
      );

    case 'River':
      return (
        <g>
          <path d="M0 135 Q 80 90 160 125 T 320 110 T 400 120 V240 H0Z" fill={p.far} />
          <path d="M0 170 Q 100 140 200 165 T 400 160 V240 H0Z" fill={p.mid} />
          <path
            d="M150 240 Q 210 205 185 180 T 215 160 L 245 160 Q 225 182 262 205 T 300 240Z"
            fill={p.water}
          />
          {[
            [140, 228, 9],
            [168, 214, 6],
            [270, 222, 8],
            [300, 232, 10],
            [250, 196, 5],
          ].map(([cx, cy, r]) => (
            <ellipse
              key={`${cx}-${cy}`}
              cx={cx}
              cy={cy}
              rx={r}
              ry={(r ?? 6) * 0.6}
              fill="#c9c2b4"
            />
          ))}
          <path d="M0 215 Q 60 200 130 220 V240 H0Z" fill={p.near} />
          <path d="M310 225 Q 360 205 400 212 V240 H310Z" fill={p.near} />
        </g>
      );
  }
}

function Clouds({ animated }: { animated: boolean }) {
  return (
    <g fill="#ffffff" opacity="0.9" className={animated ? 'animate-drift' : undefined}>
      <ellipse cx="70" cy="45" rx="28" ry="9" />
      <ellipse cx="88" cy="39" rx="18" ry="10" />
      <ellipse cx="190" cy="30" rx="22" ry="7" opacity="0.7" />
    </g>
  );
}

function Birds() {
  return (
    <g fill="none" stroke="#3b4a40" strokeWidth="1.4" strokeLinecap="round" opacity="0.55">
      <path d="M150 60 q 5 -5 10 0 q 5 -5 10 0" />
      <path d="M172 72 q 4 -4 8 0 q 4 -4 8 0" />
    </g>
  );
}

function Waves({ y }: { y: number }) {
  return (
    <path
      d={`M0 ${y} q 12 -5 25 0 t 25 0 t 25 0 t 25 0 t 25 0 t 25 0 t 25 0 t 25 0 t 25 0 t 25 0 t 25 0 t 25 0 t 25 0 t 25 0 t 25 0 t 25 0`}
      fill="none"
      stroke="#ffffff"
      strokeWidth="1.5"
      opacity="0.45"
    />
  );
}

function Palm({ x, y, scale = 1 }: { x: number; y: number; scale?: number }) {
  return (
    <g transform={`translate(${x} ${y}) scale(${scale})`}>
      <path
        d="M0 0 Q -4 -30 4 -58"
        fill="none"
        stroke="#6b4a2b"
        strokeWidth="4"
        strokeLinecap="round"
      />
      <g fill="#1f5a3f">
        <path d="M4 -58 Q -18 -66 -30 -52 Q -14 -60 4 -56Z" />
        <path d="M4 -58 Q 26 -68 36 -52 Q 20 -60 4 -56Z" />
        <path d="M4 -58 Q -6 -80 -22 -78 Q -6 -72 3 -57Z" />
        <path d="M4 -58 Q 18 -80 30 -74 Q 14 -70 5 -57Z" />
      </g>
    </g>
  );
}

function Boat({ x, y }: { x: number; y: number }) {
  return (
    <g transform={`translate(${x} ${y})`}>
      <path d="M-22 0 Q 0 10 22 0 L 16 -4 L -16 -4Z" fill="#6b4423" />
      <path d="M0 -4 V -26 L 14 -8Z" fill="#f5e6c4" />
      <path d="M0 -4 V -22 L -10 -8Z" fill="#e8b04a" />
    </g>
  );
}

function Houseboat({ x, y }: { x: number; y: number }) {
  return (
    <g transform={`translate(${x} ${y})`}>
      <path d="M-46 0 Q 0 12 46 0 L 40 -6 L -40 -6Z" fill="#6b4423" />
      <rect x="-28" y="-20" width="50" height="14" rx="2" fill="#f1e2bf" />
      <path d="M-32 -20 Q -3 -32 26 -20Z" fill="#a3305c" />
      <rect x="-20" y="-16" width="8" height="6" fill="#d99a12" />
      <rect x="0" y="-16" width="8" height="6" fill="#d99a12" />
    </g>
  );
}

function Cottage({ x, y }: { x: number; y: number }) {
  return (
    <g transform={`translate(${x} ${y})`}>
      <rect x="-12" y="-14" width="24" height="14" fill="#f1e2bf" />
      <path d="M-16 -14 L 0 -26 L 16 -14Z" fill="#a3305c" />
      <rect x="-3" y="-8" width="6" height="8" fill="#6b4423" />
    </g>
  );
}

function ShadeTree({ x, y, color }: { x: number; y: number; color: string }) {
  return (
    <g transform={`translate(${x} ${y})`}>
      <rect x="-1.5" y="-26" width="3" height="26" fill="#5a4630" />
      <ellipse cx="0" cy="-30" rx="22" ry="8" fill={color} />
    </g>
  );
}

function Mangroves({ y, color, offset = 0 }: { y: number; color: string; offset?: number }) {
  const trees = Array.from({ length: 10 }, (_, index) => index * 44 + offset);

  return (
    <g fill={color}>
      {trees.map((x) => (
        <g key={x} transform={`translate(${x} ${y})`}>
          <path d="M-6 0 L -2 -10 M 6 0 L 2 -10 M 0 0 V -12" stroke="#4b3a26" strokeWidth="1.5" />
          <ellipse cx="0" cy="-24" rx="18" ry="16" />
        </g>
      ))}
    </g>
  );
}
