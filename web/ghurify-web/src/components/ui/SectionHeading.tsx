import type { ReactNode } from 'react';
import { Trans } from 'react-i18next';

/**
 * A two-tone title from one translation key: the part wrapped in <accent>...</accent> in the
 * string takes the accent colour. The tag lives in the translation, so Bangla and English
 * each put the accent on whichever word reads naturally.
 */
export function AccentTitle({ i18nKey, tone = 'light' }: { i18nKey: string; tone?: Tone }) {
  return (
    <Trans
      i18nKey={i18nKey}
      components={{
        accent: <span className={tone === 'dark' ? 'text-dusk' : 'text-ochre'} />,
      }}
    />
  );
}

type Tone = 'light' | 'dark';

/** Eyebrow line, a large two-tone title, and an optional lead paragraph. */
export function SectionHeading({
  eyebrow,
  titleKey,
  subtitle,
  as: Heading = 'h2',
  align = 'left',
  tone = 'light',
  id,
  action,
}: {
  eyebrow: string;
  titleKey: string;
  subtitle?: string;
  as?: 'h1' | 'h2';
  align?: 'left' | 'center';
  tone?: Tone;
  id?: string;
  /** Sits opposite the heading on wide screens, e.g. a "see all" link. */
  action?: ReactNode;
}) {
  const centred = align === 'center';

  return (
    <div
      className={`flex flex-wrap items-end gap-x-8 gap-y-4 ${
        centred ? 'justify-center text-center' : 'justify-between'
      }`}
    >
      <div className={centred ? 'mx-auto max-w-3xl' : 'max-w-3xl'}>
        <p className={`eyebrow ${tone === 'dark' ? 'text-dusk!' : ''}`}>{eyebrow}</p>
        <Heading
          id={id}
          className={`mt-3 text-3xl font-bold uppercase leading-tight sm:text-4xl lg:text-[2.75rem] ${
            tone === 'dark' ? 'text-white!' : ''
          }`}
        >
          <AccentTitle i18nKey={titleKey} tone={tone} />
        </Heading>
        {subtitle && (
          <p className={`mt-4 text-base sm:text-lg ${tone === 'dark' ? 'text-white/75' : ''}`}>
            {subtitle}
          </p>
        )}
      </div>
      {action}
    </div>
  );
}
