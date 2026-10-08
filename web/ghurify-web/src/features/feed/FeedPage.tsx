import { useInfiniteQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { Link, useSearchParams } from 'react-router';
import { ChevronRight, HeartHandshake, LogIn, MapPin } from 'lucide-react';

import { asNumber } from '@/api/client';
import { cardClass, primaryButtonClass, secondaryButtonClass } from '@/components/Field';
import { CardSkeletons, EmptyState, ErrorState } from '@/components/States';
import { PageBanner } from '@/components/ui/PageBanner';
import { Photo } from '@/components/ui/Photo';
import { useAuthStore } from '@/features/auth/authStore';
import { useDestinations } from '@/features/trips/useTrips';
import { formatCount, toLanguage } from '@/lib/format';
import { feedApi } from './feedApi';
import { PostCard } from './PostCard';
import { StoryComposer } from './StoryComposer';

/** Stories from people you follow, your own, and every destination story. */
export function FeedPage() {
  const { t } = useTranslation();
  const status = useAuthStore((state) => state.status);
  const user = useAuthStore((state) => state.user?.id ?? 'anonymous');
  // ?destination=sajek: a story about a place, started from the travel map.
  const [params] = useSearchParams();

  const feed = useInfiniteQuery({
    queryKey: ['feed', user],
    queryFn: ({ pageParam, signal }) => feedApi.feed(pageParam, signal),
    initialPageParam: undefined as number | undefined,
    getNextPageParam: (page) => (page.nextBefore ? asNumber(page.nextBefore) : undefined),
  });

  const posts = feed.data?.pages.flatMap((page) => page.items) ?? [];

  return (
    <>
      <PageBanner
        compact
        slug="sylhet"
        kind="River"
        eyebrow={t('feed.eyebrow')}
        titleKey="feed.titleAccent"
      >
        <p className="mt-3 max-w-xl text-white/80">{t('feed.subtitle')}</p>
      </PageBanner>

      <div className="container-page relative z-10 -mt-14 grid items-start gap-8 pb-8 lg:grid-cols-[1fr_20rem]">
        <section aria-labelledby="stories-heading" className="flex min-w-0 flex-col gap-6">
          <h2 id="stories-heading" className="sr-only">
            {t('feed.title')}
          </h2>

          {status === 'authenticated' ? (
            <StoryComposer initialDestination={params.get('destination') ?? ''} />
          ) : (
            status === 'anonymous' && (
              <div className={`${cardClass} flex flex-wrap items-center justify-between gap-4`}>
                <p className="text-deep/80">
                  <Link to="/login" className="font-semibold text-hill underline">
                    {t('auth.signIn')}
                  </Link>{' '}
                  {t('feed.signInToShare')}
                </p>
                <Link to="/login" className={primaryButtonClass}>
                  <LogIn aria-hidden="true" className="h-4 w-4" />
                  {t('auth.signIn')}
                </Link>
              </div>
            )
          )}

          {feed.isPending && <CardSkeletons count={3} />}
          {feed.isError && (
            <ErrorState message={t('feed.loadError')} onRetry={() => void feed.refetch()} />
          )}
          {feed.isSuccess && posts.length === 0 && (
            <EmptyState
              title={t('feed.empty')}
              hint={t('feed.emptyHint')}
              action={
                <Link to="/trips" className={`${primaryButtonClass} mt-2`}>
                  {t('nav.explore')}
                </Link>
              }
            />
          )}

          <ul className="flex flex-col gap-6">
            {posts.map((post) => (
              <li key={String(post.id)}>
                <PostCard post={post} />
              </li>
            ))}
          </ul>

          {feed.hasNextPage && (
            <button
              type="button"
              className={`${secondaryButtonClass} self-center`}
              disabled={feed.isFetchingNextPage}
              onClick={() => void feed.fetchNextPage()}
            >
              {feed.isFetchingNextPage ? t('common.loading') : t('feed.more')}
            </button>
          )}
        </section>

        <aside className="flex flex-col gap-6 lg:sticky lg:top-24">
          <DestinationShortcuts />
          <div className={cardClass}>
            <h2 className="flex items-center gap-2 text-lg font-semibold">
              <HeartHandshake aria-hidden="true" className="h-5 w-5 text-hill" />
              {t('feed.guidelinesTitle')}
            </h2>
            <ul className="mt-4 list-disc space-y-2 pl-5 text-sm text-deep/75 marker:text-turmeric">
              {(['kind', 'private', 'real'] as const).map((key) => (
                <li key={key}>{t(`feed.guidelines.${key}`)}</li>
              ))}
            </ul>
          </div>
        </aside>
      </div>
    </>
  );
}

/** Where people are travelling: each destination, with how many trips are coming up. */
function DestinationShortcuts() {
  const { t, i18n } = useTranslation();
  const language = toLanguage(i18n.language);
  const { data } = useDestinations();

  if (!data || data.length === 0) return null;

  return (
    <nav aria-labelledby="places-heading" className={`${cardClass} p-2! sm:p-2!`}>
      <h2 id="places-heading" className="px-4 pb-2 pt-4 text-lg font-semibold">
        {t('feed.placesTitle')}
      </h2>
      <ul>
        {data.slice(0, 6).map((destination) => (
          <li key={destination.slug}>
            <Link
              to={`/destinations/${destination.slug}`}
              className="group flex items-center gap-3 rounded-xl px-3 py-2.5 transition hover:bg-mist"
            >
              <Photo
                slug={destination.slug}
                kind={destination.kind}
                cut="card"
                decorative
                className="h-11 w-11 shrink-0 rounded-lg"
              />
              <span className="min-w-0 flex-1">
                <span className="block truncate font-semibold text-deep">
                  {language === 'bn' ? destination.nameBn : destination.name}
                </span>
                <span className="flex items-center gap-1 text-xs text-deep/60">
                  <MapPin aria-hidden="true" className="h-3 w-3" />
                  {t('destination.upcoming', {
                    count: asNumber(destination.upcomingTrips),
                    n: formatCount(destination.upcomingTrips, language),
                  })}
                </span>
              </span>
              <ChevronRight
                aria-hidden="true"
                className="h-4 w-4 text-deep/40 transition group-hover:translate-x-0.5 group-hover:text-hill"
              />
            </Link>
          </li>
        ))}
      </ul>
    </nav>
  );
}
