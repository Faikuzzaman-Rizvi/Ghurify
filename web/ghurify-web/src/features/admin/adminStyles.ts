/*
 * The admin portal's shared surfaces, so every section has the same cards, panels and compact
 * buttons. The site-wide buttons in components/Field.tsx are sized for forms; on a card the
 * compact ones here keep a row of actions from crowding the content.
 */

/** A card in a grid: white, softly lifted, rounded, and a little higher on hover. */
export const adminCardClass =
  'group/card flex w-full flex-col overflow-hidden rounded-3xl bg-white shadow-[0_6px_28px_rgba(15,42,31,0.07)] ring-1 ring-hill/10 transition duration-300 hover:-translate-y-0.5 hover:shadow-[0_14px_40px_rgba(15,42,31,0.12)] motion-reduce:transition-none motion-reduce:hover:translate-y-0';

/** One panel holding a list, the rows divided by hairlines. */
export const adminPanelClass =
  'overflow-hidden rounded-3xl bg-white shadow-[0_6px_28px_rgba(15,42,31,0.06)] ring-1 ring-hill/10';

/** A row inside an admin panel. */
export const adminRowClass = 'border-t border-hill/8 first:border-t-0';

export const smallPrimaryButtonClass =
  'inline-flex items-center justify-center gap-1.5 rounded-full bg-hill px-4 py-2 text-sm font-semibold text-white shadow-sm transition hover:bg-deep focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-turmeric focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50';

export const smallSecondaryButtonClass =
  'inline-flex items-center justify-center gap-1.5 rounded-full border border-hill/15 bg-white px-4 py-2 text-sm font-semibold text-deep transition hover:border-hill/35 hover:bg-mist focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-turmeric focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50';

export const smallDangerButtonClass =
  'inline-flex items-center justify-center gap-1.5 rounded-full border border-jamdani/25 bg-white px-4 py-2 text-sm font-semibold text-jamdani transition hover:border-jamdani/50 hover:bg-jamdani/5 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-jamdani/40 focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50';

/** The colours a status can take, shared by pills, dots and card accents. */
export type Tone = 'good' | 'warn' | 'bad' | 'info' | 'neutral';

export const tonePill: Record<Tone, string> = {
  good: 'bg-emerald-50 text-emerald-800 ring-emerald-600/15',
  warn: 'bg-turmeric/15 text-ochre ring-turmeric/30',
  bad: 'bg-jamdani/10 text-jamdani ring-jamdani/20',
  info: 'bg-river/10 text-river ring-river/20',
  neutral: 'bg-mist text-deep/75 ring-hill/10',
};

export const toneDot: Record<Tone, string> = {
  good: 'bg-emerald-500',
  warn: 'bg-turmeric',
  bad: 'bg-jamdani',
  info: 'bg-river',
  neutral: 'bg-deep/40',
};

/** The icon tile at the head of a card. */
export const toneTile: Record<Tone, string> = {
  good: 'bg-emerald-50 text-emerald-700',
  warn: 'bg-turmeric/15 text-ochre',
  bad: 'bg-jamdani/10 text-jamdani',
  info: 'bg-river/10 text-river',
  neutral: 'bg-mist text-hill',
};
