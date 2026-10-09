/**
 * The same readability check the API applies, so the theme screen can show a problem while the
 * colour is being chosen rather than after the save is refused.
 *
 * Deliberately a mirror of Ghurify.Domain.Site.SiteSettingRules.CheckContrast: the API is the
 * one that decides, and this exists only to explain that decision in advance. The pairs below
 * and the minimums are the ones that file documents; a unit test keeps the two lists in step.
 */

/** WCAG 2.1 AA for ordinary text. */
export const minimumTextContrast = 4.5;

/** WCAG 2.1 AA for large text and the edges of controls. */
export const minimumLargeTextContrast = 3;

/** Where in the site each combination appears. The keys are i18n keys. */
const pairs: readonly {
  token: string;
  /** Another token, or the literal "white", which is the page background. */
  against: string;
  minimum: number;
  where: string;
}[] = [
  { token: 'hill', against: 'white', minimum: minimumTextContrast, where: 'hillOnWhite' },
  { token: 'hill', against: 'mist', minimum: minimumTextContrast, where: 'hillOnMist' },
  { token: 'deep', against: 'white', minimum: minimumTextContrast, where: 'deepOnWhite' },
  { token: 'deep', against: 'mist', minimum: minimumTextContrast, where: 'deepOnMist' },
  { token: 'night', against: 'white', minimum: minimumTextContrast, where: 'whiteOnNight' },
  { token: 'ochre', against: 'white', minimum: minimumTextContrast, where: 'ochreOnWhite' },
  { token: 'jamdani', against: 'white', minimum: minimumTextContrast, where: 'jamdaniOnWhite' },
  {
    token: 'turmeric',
    against: 'night',
    minimum: minimumLargeTextContrast,
    where: 'nightOnTurmeric',
  },
  { token: 'mist', against: 'white', minimum: 1.05, where: 'mistOnWhite' },
];

export interface ContrastProblem {
  /** The colour to mark in the form. */
  token: string;
  where: string;
  ratio: number;
  minimum: number;
}

/** Every pair that cannot be read, for the colours as they currently stand in the form. */
export function contrastProblems(colours: Record<string, string>): ContrastProblem[] {
  const problems: ContrastProblem[] = [];

  for (const pair of pairs) {
    const white: [number, number, number] = [255, 255, 255];
    const one = parse(colours[pair.token]);
    const other = pair.against === 'white' ? white : parse(colours[pair.against]);

    // A half-typed hex is not a failure, just not yet an answer.
    if (!one || !other) {
      continue;
    }

    const ratio = ratioOf(one, other);
    if (ratio < pair.minimum) {
      problems.push({ token: pair.token, where: pair.where, ratio, minimum: pair.minimum });
    }
  }

  return problems;
}

export const contrast = {
  /** One decimal place, which is how WCAG ratios are quoted. */
  format: (ratio: number) => ratio.toFixed(1),
  ratio: (one: string, other: string) => {
    const first = parse(one);
    const second = parse(other);
    return first && second ? ratioOf(first, second) : null;
  },
};

function parse(value: string | undefined): [number, number, number] | null {
  if (!value || !/^#[0-9a-fA-F]{6}$/.test(value)) {
    return null;
  }

  return [
    Number.parseInt(value.slice(1, 3), 16),
    Number.parseInt(value.slice(3, 5), 16),
    Number.parseInt(value.slice(5, 7), 16),
  ] as [number, number, number];
}

/** WCAG relative luminance: each channel un-gamma'd, then weighted for perceived brightness. */
function luminance([red, green, blue]: [number, number, number]): number {
  const channel = (value: number) => {
    const scaled = value / 255;
    return scaled <= 0.03928 ? scaled / 12.92 : Math.pow((scaled + 0.055) / 1.055, 2.4);
  };

  return 0.2126 * channel(red) + 0.7152 * channel(green) + 0.0722 * channel(blue);
}

function ratioOf(one: [number, number, number], other: [number, number, number]): number {
  const first = luminance(one);
  const second = luminance(other);

  return (Math.max(first, second) + 0.05) / (Math.min(first, second) + 0.05);
}
