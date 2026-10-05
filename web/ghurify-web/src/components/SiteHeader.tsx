import { useTranslation } from 'react-i18next';
import { Link, NavLink } from 'react-router';
import { useAuthStore } from '@/features/auth/authStore';
import { useTourStore } from '@/components/tour/tourStore';
import { LanguageToggle } from './LanguageToggle';
import { Logo } from './Logo';

const navLinkClass = ({ isActive }: { isActive: boolean }) =>
  `rounded-full px-3 py-1.5 text-sm font-medium transition ${
    isActive ? 'bg-hill text-white' : 'text-deep hover:bg-hill/10'
  }`;

/** Sticky top bar: brand, the main sections, language, the tour, and sign-in. */
export function SiteHeader() {
  const { t } = useTranslation();
  const status = useAuthStore((state) => state.status);
  const startTour = useTourStore((state) => state.start);

  return (
    <header className="sticky top-0 z-40 border-b border-hill/10 bg-sand/85 backdrop-blur">
      <div className="mx-auto flex max-w-6xl flex-wrap items-center justify-between gap-x-4 gap-y-2 px-4 py-3">
        <Link to="/" aria-label={t('nav.home')} className="rounded-xl">
          <Logo />
        </Link>

        <nav aria-label={t('nav.main')} className="order-3 w-full sm:order-none sm:w-auto">
          <ul className="flex items-center gap-1 overflow-x-auto">
            <li>
              <NavLink to="/trips" className={navLinkClass}>
                {t('nav.explore')}
              </NavLink>
            </li>
            <li>
              <Link to="/#destinations" className={navLinkClass({ isActive: false })}>
                {t('nav.destinations')}
              </Link>
            </li>
            <li>
              <Link to="/#safety" className={navLinkClass({ isActive: false })}>
                {t('nav.safety')}
              </Link>
            </li>
          </ul>
        </nav>

        <div className="flex items-center gap-2">
          <button
            type="button"
            onClick={startTour}
            className="hidden rounded-full border border-turmeric/60 px-3 py-1.5 text-sm font-medium text-deep transition hover:bg-turmeric/15 md:inline-flex md:items-center md:gap-1.5"
          >
            <span aria-hidden="true">🧭</span>
            {t('nav.tour')}
          </button>

          <div data-tour="language">
            <LanguageToggle compact />
          </div>

          <div data-tour="account">
            {/* While the silent refresh is still deciding, show neither: offering "sign in"
                to someone who turns out to be signed in is worse than a brief gap. */}
            {status === 'authenticated' ? (
              <Link
                to="/account"
                className="inline-flex items-center gap-2 rounded-full bg-deep px-4 py-1.5 text-sm font-medium text-white transition hover:bg-hill"
              >
                {t('account.title')}
              </Link>
            ) : status === 'anonymous' ? (
              <Link
                to="/login"
                className="inline-flex items-center rounded-full bg-hill px-4 py-1.5 text-sm font-medium text-white transition hover:bg-deep"
              >
                {t('auth.signIn')}
              </Link>
            ) : (
              <span className="inline-block h-8 w-20" aria-hidden="true" />
            )}
          </div>
        </div>
      </div>
    </header>
  );
}
