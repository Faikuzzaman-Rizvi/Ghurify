import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';
import { Scenery } from '@/components/Scenery';

/** Any address that matches no route. */
export function NotFoundPage() {
  const { t } = useTranslation();

  return (
    <div className="mx-auto flex max-w-xl flex-col items-center px-4 pt-16 text-center">
      <Scenery kind="River" className="h-48 w-full rounded-3xl" />
      <h1 className="mt-6 text-3xl font-extrabold text-deep">{t('notFound.title')}</h1>
      <p className="mt-2 text-deep/70">{t('notFound.body')}</p>
      <Link
        to="/"
        className="mt-6 rounded-full bg-hill px-5 py-2.5 font-medium text-white transition hover:bg-deep"
      >
        {t('notFound.home')}
      </Link>
    </div>
  );
}
