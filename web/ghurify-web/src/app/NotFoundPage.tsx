import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';
import { ArrowLeft, Compass } from 'lucide-react';
import { useHeroUnderHeader } from '@/components/ui/headerStore';
import { Photo } from '@/components/ui/Photo';

/** Any address that matches no route: a full-screen photo and two ways back on track. */
export function NotFoundPage() {
  const { t } = useTranslation();
  useHeroUnderHeader();

  return (
    <section className="relative isolate flex min-h-svh items-center justify-center overflow-hidden bg-night px-4 pb-16 pt-32 text-center text-white">
      <Photo
        slug="kuakata"
        kind="Beach"
        cut="wide"
        decorative
        className="absolute! inset-0 -z-10"
      />
      <div aria-hidden="true" className="absolute inset-0 -z-10 bg-night/65" />

      <div className="max-w-xl animate-rise">
        <p
          aria-hidden="true"
          className="font-display text-[7rem] font-bold leading-none text-white/90 sm:text-[10rem]"
        >
          404
        </p>
        <h1 className="mt-2 text-3xl font-bold uppercase text-white! sm:text-4xl">
          {t('notFound.title')}
        </h1>
        <p className="mt-4 text-lg text-white/85">{t('notFound.body')}</p>
        <div className="mt-8 flex flex-wrap items-center justify-center gap-3">
          <Link
            to="/trips"
            className="inline-flex items-center gap-2 rounded-full bg-turmeric px-6 py-3 font-semibold text-night transition hover:bg-dusk"
          >
            <Compass aria-hidden="true" className="h-4 w-4" />
            {t('nav.explore')}
          </Link>
          <Link
            to="/"
            className="inline-flex items-center gap-2 rounded-full border border-white/40 px-6 py-3 font-semibold text-white transition hover:bg-white/15"
          >
            <ArrowLeft aria-hidden="true" className="h-4 w-4" />
            {t('notFound.home')}
          </Link>
        </div>
      </div>
    </section>
  );
}
