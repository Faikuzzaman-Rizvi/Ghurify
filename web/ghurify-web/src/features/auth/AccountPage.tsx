import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';
import { useAuthStore } from './authStore';
import { useLogout } from './useAuthMutations';

/**
 * A protected screen. It exists in this sprint to prove the gate works end to end; profiles
 * proper arrive with the next prompt.
 */
export function AccountPage() {
  const { t } = useTranslation();
  const user = useAuthStore((state) => state.user);
  const logout = useLogout();

  if (!user) {
    return null;
  }

  return (
    <div className="mx-auto flex max-w-md flex-col gap-6 px-4 py-12">
      <h1 className="text-3xl font-extrabold text-hill">{t('account.title')}</h1>

      <dl className="rounded-3xl bg-white p-6 shadow-sm ring-1 ring-hill/10">
        <dt className="text-sm text-deep/60">{t('account.emailLabel')}</dt>
        {/* Masked: the server never sends the full address to the browser. */}
        <dd className="mb-4 text-lg font-medium text-deep">{user.maskedEmail}</dd>

        <dt className="text-sm text-deep/60">{t('account.nameLabel')}</dt>
        <dd className="text-lg font-medium text-deep">{user.displayName ?? t('account.noName')}</dd>
      </dl>

      <div className="flex items-center justify-between">
        <Link to="/" className="text-hill underline underline-offset-4">
          {t('app.name')}
        </Link>

        <button
          type="button"
          onClick={() => logout.mutate()}
          disabled={logout.isPending}
          className="rounded-full border border-hill/25 px-4 py-2 text-deep transition hover:bg-hill/10 disabled:opacity-60"
        >
          {t('auth.signOut')}
        </button>
      </div>
    </div>
  );
}
