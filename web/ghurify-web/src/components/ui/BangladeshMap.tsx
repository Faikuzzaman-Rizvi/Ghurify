import { useId } from 'react';

/**
 * Bangladesh, drawn from Natural Earth's 1:10m country outline (public domain), projected
 * equirectangularly at 23.7°N and simplified to a small inline path. The ten destinations sit at
 * their seeded coordinates, with Dhaka as the hub the dashed routes leave from.
 *
 * Purely decorative: it is aria-hidden, and no page relies on it to say anything.
 */
const outline =
  'M431 478.6L432.6 487.7L431 503L432.3 526.8L436.8 544.5L436.3 548.1L430.5 551.6L426.3 540.2L414.9 539.3L410.7 531.2L407.8 530.2L404.3 534.4L399.9 534.6L394.6 546.4L394.3 558.8L398.8 567L402.5 570.2L402.2 582.3L408.8 599.6L408.1 602.5L397.5 583.3L395.3 575.8L383.1 560.9L382.4 546.4L374 532.2L376.7 527.5L379.9 527L377.4 520.2L381.5 512.3L381 505.9L378.3 511.4L376.8 511.3L376.8 505.8L374.9 507.4L373.7 504.8L374 502.5L377.4 500.4L374.6 500L377.4 496.9L369 497.4L373.7 487.9L370.6 490.1L365 465.4L364.9 462L366.1 462.6L364.8 461.5L363.6 464.7L361.1 458.3L363.6 451.7L360.8 445.7L366.7 442.8L369.9 433.9L368 433.9L367 440L359.2 446.2L362.3 451.5L358.8 452.6L354.8 435.2L348.6 422.9L336.9 406.3L329 400.2L331.6 385.9L329 393L324.9 396.4L324.7 401L318.4 398.9L320.3 400.2L319.7 402L313.3 402.5L317.2 404.3L315.8 406.6L309.1 406.5L309 404.3L303.4 403.7L304.9 406.5L302.2 407.9L309.9 410.3L310.9 412.3L308.8 418.8L305.3 422L298.4 423.7L294.9 421.9L288.4 408L287 408.5L290.2 417.5L285.3 418.9L281.4 417.8L275.4 408.1L271.7 407.9L267.9 402.2L260.8 382.3L252.8 369.9L253.8 357.5L256.2 349.6L254.7 346.3L255.7 344.1L252.7 344.2L251.1 342.1L250.1 331.4L253.1 327.7L260.6 326.5L260.7 322.8L259.7 320.9L257.2 320.8L259.4 323.5L258.8 325.6L256.2 324.5L250.8 327.2L249.3 320.1L252.6 316L246.3 317.3L245.2 320L238.8 318.1L240.8 320.6L248 322.4L248.1 331.4L245.7 337.9L224.7 333L218.1 329.7L218.8 331.9L230.7 339.1L234.4 343.7L247.2 346.9L250.7 351.6L250 364.6L247.5 368.2L245.8 368.5L246.3 365.3L243 368.3L236.7 369.1L236.2 370.8L241.9 384.5L244.6 386.7L242.8 388.3L238.8 387.9L235.1 396.3L232.5 396.9L233.6 399.5L229.3 400.2L235 403L237.4 401.5L240.3 405.4L242.1 415.2L237.2 418.4L235.6 427L238.4 418.8L241.4 417.7L246.4 420.1L250.7 429.8L248.8 429L248.8 431.3L251.8 440.8L248.4 452.5L246.2 456L241 458.7L235.3 468.8L230.6 466.1L233.7 456.5L232.8 447.9L231.2 447.5L230.9 455.5L232.9 458.5L229.9 462L229 467.4L223.4 478L221 489.3L213.8 495.9L203.6 493.3L206.7 483.2L201.3 491L197.4 490.1L199.3 479.7L210.2 465L214.9 464L216.8 456.5L214 463.1L208.7 465.3L199.9 477.5L194.8 476.3L198.4 470.4L199.1 463.2L202.3 459.9L200 460.8L193.6 471.9L193.6 477.7L192.2 478.9L189.1 476.6L187.6 471.4L183.9 448.9L196.1 427.3L190.3 432.4L187.9 437.2L183.5 434.5L184.2 442.1L180.4 449.6L183.4 467.2L181.1 469.3L176.6 468.8L181.2 473L181.1 479.1L184.8 486.3L184.2 487.9L175.6 493.5L173.7 491.7L168.5 493.3L164.1 491.4L162.8 483.8L160.2 489.4L167.7 497.9L164.8 501.3L158.1 500.4L159.1 504.4L157.3 506.3L155.4 505L152.8 501L151.9 495.1L155.3 487.9L155.9 473.2L159.1 465L155.5 456.1L159.1 450.7L160.9 442.8L153.9 455.8L156.6 465L154.1 476.9L152.4 477.9L152.8 467.5L148.6 457.5L148.7 449L147.1 446.8L147.1 466.1L148.8 462.7L151 468.3L150.2 472.2L148.4 473.2L151.4 480.1L150.5 485.4L146.1 490.4L146.2 498.2L140.8 504.6L137.4 502L137.1 493.5L139 486.2L135.8 479.1L136.2 487.1L131.2 495.4L129.6 494.1L130.6 500.6L128.6 506.5L130.8 508.5L126.3 511.8L122.8 510.9L123.8 505.4L120.8 495.2L117.7 497.7L114.5 496.3L110.9 486.6L111.9 476.3L108.8 469.8L110.5 458.9L104.4 447.8L105.7 446.9L103.7 443L102.6 436.7L103.8 433.6L100.9 423.6L97 421.5L99.6 417.9L98.3 412.6L95.2 402.3L97.8 398.1L98.5 391.6L93.8 388.4L88.8 378.8L90 368.8L99.9 356.1L87.3 352.6L83 354.2L75.8 351.1L74.8 347L82.5 327.5L81.3 326.5L77.9 329.5L67.8 317.5L63.4 316.2L63.9 313.5L61.5 311.4L64.9 290.8L71.8 289.4L77.4 283.8L76.2 276.8L78.4 272.5L76.2 272L73.7 268.1L74.1 260.9L80.3 254.9L79.5 247.6L72.5 242.5L70.1 246.9L55.5 244.8L48.8 239.4L22.1 226.3L19.9 222.7L19.5 216.4L17.2 212.9L14 211.8L16.9 200.9L19.1 200.2L20.1 196.7L24.9 191.9L22.5 184.7L30.5 181.8L34.2 188.3L41.6 188.9L51 171.2L52.4 157.4L63.2 159.3L70.1 157.1L83.8 160.3L86.8 156.7L96.6 159.6L95.3 157.4L97 157.1L97.1 153.4L102.2 147.9L101.3 146.1L94.7 146L89.1 143.1L85.2 136.8L86.6 130.5L82.7 126.2L79.9 125.1L80 126.8L73 130.1L66.4 126.8L60.6 126.3L50.8 117.1L50.4 111L35.8 97.5L32.8 95.9L23.7 98.9L20 93.3L18.8 85.5L23.5 75L25.8 73.2L24.8 67.8L27 62.3L32 58.3L41.9 54.8L42.8 48.2L51.5 40.1L57.6 41.1L54.2 31L44.4 27.9L42.7 31.5L40.9 29.8L47.3 14L49.6 21.8L72.1 34.8L71.7 37.2L77.2 43.5L71.1 48.8L72.8 49.9L80.5 47.1L83.8 48.4L87.5 53.7L92.2 48.6L101.3 53.9L105.4 52.9L107.2 51.6L104 48.8L104.3 46.6L101.9 47.2L100.5 43.5L95.6 41.5L93.6 37.8L98.2 32.4L107.6 38.2L108.6 41.7L113 43.6L112.3 45.4L109.9 44.9L109.9 47.6L114.5 61.7L123 66.5L125.3 70.5L134.3 76.5L139.4 74.8L141.2 77L148 76.5L151 80.1L153.2 80.4L154.8 74.1L159.6 69.1L155.5 67.6L158.9 64.3L155.1 62.1L157.3 59.4L159.8 59.9L159.7 54.4L163.4 54.1L163.6 60.2L169.4 61.8L172.8 73.1L175.9 77.6L178.1 76.7L179.4 78.4L176.1 81.4L180 83.2L175.3 94.9L180 112.9L176.4 138.9L178.6 147.9L186.7 146.7L207 155.2L228.5 161.4L252.2 159.2L259.9 161.1L264.2 159.6L270.4 162.1L308.3 156.2L326.2 162.6L338.5 160.4L341.3 163.5L345.2 163.9L348.7 161.1L351.8 161.9L353.6 159.6L381.4 158.2L386.8 159.9L388.9 162.4L394.1 163.2L399.7 167.9L406.1 168.9L422.6 183.5L423.6 186.9L422 190L413.1 192.7L404.9 187.2L401.4 186.1L398.7 187.2L399.7 198.6L393.3 220.4L388.4 224.9L388.1 235.8L384 239.3L373.7 238.8L375.7 244.7L371.1 241.9L369.1 242.5L369.7 250.3L367 260.6L364 260.8L361.2 258L360.6 254.2L353.8 252.9L353.8 259.9L351.1 262.1L347.7 260.8L344.3 254.8L342.2 264.5L340 266.7L334.1 267.8L319.9 266.4L317.7 276.8L311.8 277.5L306.5 285.5L307.6 291.9L305.5 299.2L298.8 304.3L302.7 309.3L299.5 311L299.6 315.1L304.6 326.8L307.5 329.5L310.3 339L313 340.8L311 343.4L315.4 366.2L317.6 369.2L320.7 370.1L319.3 353.9L320.8 350.2L322.8 350.1L329.2 357.9L334.7 376.8L339.5 379.5L340.4 381.9L343.2 378.5L351.4 376.8L359.6 367.4L354.8 349.1L361.6 336.3L369.6 331.7L372.8 326.8L370.6 311.3L371.2 304.1L380 312.3L383.7 310.4L388.2 304.5L392.1 303.2L396.1 311.5L398.6 305.6L402 305.7L401.4 315.4L405.9 333.2L410.2 340.9L411 348.5L408.3 359.2L411.2 383.7L417.3 389.2L418.7 400.1L423.7 406.8L424.1 420.3L431.1 459.5L431 462L427.5 463.5L431 478.6ZM372.4 500.4L376.8 515.4L375.5 524L374.7 525.7L368.6 525.7L369.1 527.8L367 528.3L365 524.4L366.5 521.6L364.9 503.8L366.6 500.9L368.9 502L368 507.9L370 503.7L369.2 498.9L372.4 498.2L372.4 500.4ZM114.2 498.2L116.3 499.7L115.7 504.2L108.3 497L109.5 494.1L108.3 487.3L107.8 484.6L109.5 485.3L112.1 497L114.2 498.2ZM368 489.7L362.9 503.8L362.9 488.7L366.1 483.2L368 489.7ZM249.5 477.7L250.7 482.5L248.3 485.1L246.9 484.6L242.9 490.2L241.2 489.4L243.8 488.7L241.9 486.6L242.9 482.5L249.5 477.7ZM225.6 482.5L229.1 476.9L231.2 476.3L226.4 490.3L224.3 491.7L225.6 482.5ZM235 472.2L239.7 478.7L238.8 481.9L233.2 486.3L231.2 482.5L233.2 472.5L235 472.2ZM245 470.2L239.4 472.9L240.2 468.3L251.3 459.9L249.5 466.1L248.8 463.3L246.4 474L244.4 476.3L241.7 476.4L241.2 473.9L245 470.2ZM281.4 455.9L282.7 456.5L282 464.2L279.3 463.1L281.4 455.9ZM282.4 453.7L281 453.1L284.5 440L286.2 446.1L282.4 453.7ZM300.3 440.6L303.8 453.6L299.7 461.7L292.5 467.4L288.8 466.9L287.2 464.7L292.1 457.2L293.6 437.5L300.3 440.6ZM251.9 433.9L250.2 433.4L250.2 431.5L253.1 433.2L254.7 438.7L253.8 445.5L251.9 433.9ZM301.2 426L305 424.9L306 426.8L301.3 431.3L297.1 430.2L298 427.4L301.2 426ZM329.5 417.7L337.6 428.5L338.3 432.8L337.5 436.2L333.2 436.7L330.3 435L325.5 423.8L329.5 417.7ZM267.5 426.8L273.4 432.9L270.9 461.1L266.1 468.8L264.5 470.2L263.8 468.8L258.5 475.7L254.4 476.9L253.7 474.7L257.6 466.1L251 473.9L250.8 469.1L253.1 464L252.1 461.8L257.6 446.2L257.6 431.8L255.5 423.9L252.9 420.2L246.3 416.1L248.1 400.7L254.7 397.6L267.5 426.8ZM254.2 391.4L257.2 395.5L249.2 395L243.8 399.4L242.8 405.2L239.4 400.4L236.9 399.6L238.5 389.8L249.4 388.2L254.2 391.4ZM256.2 381.1L253.1 381.1L256.2 384.5L252.6 383.9L248.7 380.3L243.1 381.1L241.7 378.8L243.5 375.6L248.8 372.9L256.2 381.1ZM241.9 369.5L242.4 371.2L238.1 375.6L236.9 370.1L241.9 369.5ZM243.1 344.1L238.1 343.2L234.2 338.8L236.9 337.9L248 344L248.8 346.2L243.6 343L243.1 344.1Z';

const mapViewBox = { width: 450.8, height: 616.5 };

/** Destination slug -> position in the viewBox. */
const destinations: Record<string, readonly [number, number]> = {
  sajek: [405.2, 338.2],
  bandarban: [398.3, 456.8],
  'coxs-bazar': [378.8, 533.6],
  'saint-martins': [407.9, 610],
  sylhet: [366.2, 186.9],
  sreemangal: [353.5, 245.7],
  'tanguar-haor': [294.3, 161.4],
  sundarbans: [120.4, 481.4],
  kuakata: [205.8, 494.7],
  rangamati: [394.3, 411],
};
const dhaka = [232.9, 295.3] as const;

/** Where a longitude and latitude fall in the viewBox (same projection as the outline). */
const lonToX = (lon: number) => (lon - 88) * 91.57 + 12;
const latToY = (lat: number) => (26.65 - lat) * 100 + 11.33;

/** Coastline contour rings, as stroke widths: each becomes one thin line at half its width out. */
const contours = [26, 54, 88, 128, 176] as const;

/**
 * The map as background artwork: a gradient-filled silhouette with a halftone texture and a
 * gradient edge, topographic contour lines rippling out from the coast into the Bay of Bengal,
 * a faint latitude/longitude graticule, and the destinations as gently pulsing pins on routes out
 * of Dhaka. Every layer is low-contrast on its own; the caller sets the overall strength.
 */
export function BangladeshArtwork({ className = '' }: { className?: string }) {
  const id = useId();
  const pad = 190;
  const box = {
    x: -pad,
    y: -pad,
    width: mapViewBox.width + pad * 2,
    height: mapViewBox.height + pad * 2,
  };

  return (
    <svg
      viewBox={`${box.x} ${box.y} ${box.width} ${box.height}`}
      aria-hidden="true"
      focusable="false"
      preserveAspectRatio="xMidYMid meet"
      className={className}
    >
      <defs>
        <linearGradient id={`${id}-land`} x1="0" y1="0" x2="1" y2="1">
          <stop offset="0%" stopColor="#245c43" />
          <stop offset="55%" stopColor="#2f7a5a" />
          <stop offset="100%" stopColor="#1f6f8b" />
        </linearGradient>
        <linearGradient id={`${id}-edge`} x1="0" y1="0" x2="0" y2="1">
          <stop offset="0%" stopColor="#d99a12" />
          <stop offset="45%" stopColor="#245c43" />
          <stop offset="100%" stopColor="#1f6f8b" />
        </linearGradient>
        <radialGradient id={`${id}-glow`} cx="0.5" cy="0.5" r="0.5">
          <stop offset="0%" stopColor="#f2c46d" stopOpacity="0.55" />
          <stop offset="100%" stopColor="#f2c46d" stopOpacity="0" />
        </radialGradient>
        {/* Fades the graticule out towards the corners, so it reads as atmosphere, not a chart. */}
        <radialGradient id={`${id}-vignette`} cx="0.5" cy="0.5" r="0.55">
          <stop offset="0%" stopColor="#fff" stopOpacity="1" />
          <stop offset="100%" stopColor="#fff" stopOpacity="0" />
        </radialGradient>
        <mask id={`${id}-graticule-mask`}>
          <rect {...box} fill={`url(#${id}-vignette)`} />
        </mask>
        <pattern id={`${id}-dots`} width="7" height="7" patternUnits="userSpaceOnUse">
          <circle cx="1.5" cy="1.5" r="1" fill="#173f2e" fillOpacity="0.55" />
        </pattern>
        <clipPath id={`${id}-clip`}>
          <path d={outline} />
        </clipPath>
        {/* One mask per contour: a thin ring at the stroke's outer edge, outside the land only. */}
        {contours.map((width) => (
          <mask key={width} id={`${id}-ring-${width}`}>
            <path
              d={outline}
              fill="none"
              stroke="#fff"
              strokeWidth={width}
              strokeLinejoin="round"
            />
            <path
              d={outline}
              fill="#000"
              stroke="#000"
              strokeWidth={width - 2.4}
              strokeLinejoin="round"
            />
          </mask>
        ))}
      </defs>

      {/* Graticule: a degree of latitude and longitude apart. */}
      <g mask={`url(#${id}-graticule-mask)`} stroke="#245c43" strokeOpacity="0.16" strokeWidth="1">
        {[86, 87, 88, 89, 90, 91, 92, 93, 94, 95].map((lon) => (
          <line
            key={`lon-${lon}`}
            x1={lonToX(lon)}
            x2={lonToX(lon)}
            y1={box.y}
            y2={box.y + box.height}
            strokeDasharray="2 6"
          />
        ))}
        {[19, 20, 21, 22, 23, 24, 25, 26, 27, 28].map((lat) => (
          <line
            key={`lat-${lat}`}
            y1={latToY(lat)}
            y2={latToY(lat)}
            x1={box.x}
            x2={box.x + box.width}
            strokeDasharray="2 6"
          />
        ))}
        {/* The Tropic of Cancer crosses the country: drawn a little stronger. */}
        <line
          y1={latToY(23.44)}
          y2={latToY(23.44)}
          x1={box.x}
          x2={box.x + box.width}
          stroke="#d99a12"
          strokeOpacity="0.5"
          strokeDasharray="10 8"
        />
      </g>

      {/* Contour lines rippling out into the bay, fainter as they go. */}
      {contours.map((width, index) => (
        <rect
          key={width}
          {...box}
          fill="#1f6f8b"
          fillOpacity={0.42 - index * 0.07}
          mask={`url(#${id}-ring-${width})`}
        />
      ))}

      {/* A warm glow around the capital, under the land. */}
      <circle cx={dhaka[0]} cy={dhaka[1]} r="230" fill={`url(#${id}-glow)`} />

      {/* The land: gradient, halftone, and a crisp gradient edge. */}
      <path d={outline} fill={`url(#${id}-land)`} fillOpacity="0.2" />
      <rect {...box} fill={`url(#${id}-dots)`} clipPath={`url(#${id}-clip)`} opacity="0.45" />
      <path
        d={outline}
        fill="none"
        stroke={`url(#${id}-edge)`}
        strokeWidth="2.2"
        strokeLinejoin="round"
        strokeOpacity="0.9"
      />

      {/* Routes out of Dhaka, drifting slowly towards each destination. */}
      <g fill="none" stroke="#d99a12" strokeWidth="1.6" strokeLinecap="round">
        {Object.entries(destinations).map(([slug, [x, y]]) => (
          <path
            key={`route-${slug}`}
            d={`M${dhaka[0]} ${dhaka[1]} Q${(dhaka[0] + x) / 2} ${Math.min(dhaka[1], y) - 46} ${x} ${y}`}
            strokeDasharray="4 4"
            strokeOpacity="0.55"
            className="animate-map-route"
          />
        ))}
      </g>

      {/* Destination pins: a breathing halo and a solid dot with a white rim. */}
      {Object.entries(destinations).map(([slug, [x, y]], index) => (
        <g key={slug}>
          <circle
            cx={x}
            cy={y}
            r="7"
            fill="#d99a12"
            className="animate-map-pulse [transform-box:fill-box] [transform-origin:center]"
            style={{ animationDelay: `${(index * 320) % 3200}ms` }}
          />
          <circle cx={x} cy={y} r="5" fill="#d99a12" stroke="#fff" strokeWidth="2" />
        </g>
      ))}

      {/* Dhaka, the hub. */}
      <circle cx={dhaka[0]} cy={dhaka[1]} r="16" fill="#173f2e" fillOpacity="0.12" />
      <circle cx={dhaka[0]} cy={dhaka[1]} r="7" fill="#173f2e" stroke="#fff" strokeWidth="2.5" />
    </svg>
  );
}

export function BangladeshMap({
  tone = 'light',
  showDestinations = false,
  className = '',
}: {
  /** light: green lines for pale backgrounds. dark: warm lines for the deep-green panels. */
  tone?: 'light' | 'dark';
  /** Dots for the destinations and faint routes out of Dhaka. */
  showDestinations?: boolean;
  className?: string;
}) {
  const id = useId();
  const line = tone === 'dark' ? '#f2c46d' : '#245c43';
  const fill = tone === 'dark' ? '#ffffff' : '#245c43';

  return (
    <svg
      viewBox={`0 0 ${mapViewBox.width} ${mapViewBox.height}`}
      aria-hidden="true"
      focusable="false"
      preserveAspectRatio="xMidYMid meet"
      className={className}
    >
      <defs>
        {/* A fine dot texture inside the borders, like an old survey sheet. */}
        <pattern id={`${id}-dots`} width="9" height="9" patternUnits="userSpaceOnUse">
          <circle
            cx="1.5"
            cy="1.5"
            r="1.1"
            fill={fill}
            fillOpacity={tone === 'dark' ? 0.35 : 0.5}
          />
        </pattern>
        <clipPath id={`${id}-clip`}>
          <path d={outline} />
        </clipPath>
      </defs>

      <path d={outline} fill={fill} fillOpacity={tone === 'dark' ? 0.06 : 0.08} />
      <rect
        width={mapViewBox.width}
        height={mapViewBox.height}
        fill={`url(#${id}-dots)`}
        clipPath={`url(#${id}-clip)`}
      />
      <path
        d={outline}
        fill="none"
        stroke={line}
        strokeWidth="1.6"
        strokeLinejoin="round"
        strokeOpacity={tone === 'dark' ? 0.55 : 0.6}
      />

      {showDestinations && (
        <g>
          {Object.entries(destinations).map(([slug, [x, y]]) => (
            <path
              key={`route-${slug}`}
              d={`M${dhaka[0]} ${dhaka[1]} Q${(dhaka[0] + x) / 2} ${Math.min(dhaka[1], y) - 40} ${x} ${y}`}
              fill="none"
              stroke={line}
              strokeWidth="1.1"
              strokeDasharray="3 5"
              strokeOpacity="0.45"
            />
          ))}
          {Object.entries(destinations).map(([slug, [x, y]]) => (
            <g key={slug}>
              <circle cx={x} cy={y} r="9" fill={line} fillOpacity="0.18" />
              <circle cx={x} cy={y} r="3.6" fill={line} />
            </g>
          ))}
          <circle cx={dhaka[0]} cy={dhaka[1]} r="12" fill={line} fillOpacity="0.2" />
          <circle
            cx={dhaka[0]}
            cy={dhaka[1]}
            r="5"
            fill={tone === 'dark' ? '#ffffff' : '#173f2e'}
          />
        </g>
      )}
    </svg>
  );
}
