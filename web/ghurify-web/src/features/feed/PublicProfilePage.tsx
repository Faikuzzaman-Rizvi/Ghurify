import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { Link, useParams } from 'react-router';
import { CalendarDays, Flag, MapPin, Quote, Star, UserCheck, UserPlus } from 'lucide-react';

import { ApiError, asNumber } from '@/api/client';
import { cardClass, primaryButtonClass, secondaryButtonClass } from '@/components/Field';
import { EmptyState, ErrorState } from '@/components/States';
import { PageBanner } from '@/components/ui/PageBanner';
import { Photo } from '@/components/ui/Photo';
import { Avatar as UserAvatar } from '@/components/Avatar';
import { VerificationBadge } from '@/components/VerificationBadge';
import { useAuthStore } from '@/features/auth/authStore';
import { ReportDialog } from '@/features/safety/ReportDialog';
import { formatCount, formatDateRange, formatMonthYear, toLanguage } from '@/lib/format';
import { feedApi } from './feedApi';
import { PostCard } from './PostCard';

/** Five stars, filled up to the rating. Decorative: the label carries the number. */
function Stars({ rating, label }: { rating: number; label: string }) {
  return (
    <span role="img" aria-label={label} className="flex gap-0.5">
      {[1, 2, 3, 4, 5].map((value) => (
        <Star
          key={value}
          aria-hidden="true"
          className={`h-4 w-4 ${value <= rating ? 'fill-turmeric text-turmeric' : 'text-deep/20'}`}
        />
      ))}
    </span>
  );
}

/** Someone's public page: who they are, how they are rated, their trips, reviews and stories. */
export function PublicProfilePage() {
  const { t, i18n } = useTranslation();
  const language = toLanguage(i18n.language);
  const { id } = useParams();
  const userId = Number(id);
  const viewer = useAuthStore((state) => state.user?.id);
  const [reporting, setReporting] = useState(false);
  const queryClient = useQueryClient();

  const profile = useQuery({
    queryKey: ['profile', userId, viewer ?? 'anonymous'],
    queryFn: ({ signal }) => feedApi.profile(userId, signal),
    enabled: userId > 0,
  });

  const posts = useQuery({
    queryKey: ['feed', 'user', userId],
    queryFn: ({ signal }) => feedApi.userPosts(userId, signal),
    enabled: userId > 0,
  });

  const follow = useMutation({
    mutationFn: () =>
      profile.data?.followedByMe ? feedApi.unfollow(userId) : feedApi.follow(userId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['profile', userId] }),
  });

  if (profile.isError) {
    return (
      <div className="container-page max-w-3xl py-12">
        {profile.error instanceof ApiError && profile.error.status === 404 ? (
          <EmptyState title={t('people.notFound')} />
        ) : (
          <ErrorState message={t('common.error')} onRetry={() => void profile.refetch()} />
        )}
      </div>
    );
  }

  if (profile.isPending) {
    return (
      <div role="status">
        <span className="sr-only">{t('common.loading')}</span>
        <div aria-hidden="true" className="h-80 animate-pulse bg-hill/15" />
      </div>
    );
  }

  const person = profile.data;
  const hostAverage = person.asHostAverage === null ? null : Number(person.asHostAverage);
  const isOther = viewer !== undefined && viewer !== userId;

  const stats = [
    { label: t('people.statFollowers'), value: formatCount(person.followers, language) },
    { label: t('people.statFollowing'), value: formatCount(person.following, language) },
    { label: t('people.statTrips'), value: formatCount(person.hostedTrips.length, language) },
  ];

  return (
    <>
      <PageBanner
        compact
        slug="tanguar-haor"
        kind="Wetland"
        eyebrow={person.isHost ? t('roles.Host') : t('roles.Traveler')}
        title={person.displayName ?? t('chat.someone')}
      >
        <p className="mt-3 flex flex-wrap items-center gap-x-4 gap-y-1 text-white/80">
          <span className="inline-flex items-center gap-1.5">
            <CalendarDays aria-hidden="true" className="h-4 w-4" />
            {t('account.memberSince', { date: formatMonthYear(person.memberSince, language) })}
          </span>
          {person.homeDistrict && (
            <span className="inline-flex items-center gap-1.5">
              <MapPin aria-hidden="true" className="h-4 w-4" />
              {person.homeDistrict}
            </span>
          )}
        </p>
      </PageBanner>

      <div className="container-page relative z-10 -mt-14 grid items-start gap-8 pb-8 lg:grid-cols-[20rem_1fr]">
        <aside className="flex flex-col gap-6 lg:sticky lg:top-24">
          <div className={`${cardClass} text-center`}>
            <UserAvatar
              userId={userId}
              name={person.displayName}
              size="xl"
              className="mx-auto ring-8 ring-mist"
            />
            <div className="mt-4 flex flex-wrap justify-center gap-2">
              <VerificationBadge level={person.verifiedLevel} size="md" />
              {person.isHost && (
                <span className="rounded-full bg-turmeric/20 px-3 py-1 text-sm font-semibold text-deep">
                  {t('roles.Host')}
                </span>
              )}
            </div>

            <dl className="mt-6 grid grid-cols-3 divide-x divide-hill/10 rounded-xl bg-mist py-3">
              {stats.map((stat) => (
                <div key={stat.label}>
                  <dt className="text-xs text-deep/60">{stat.label}</dt>
                  <dd className="font-display text-xl font-bold text-deep">{stat.value}</dd>
                </div>
              ))}
            </dl>

            {hostAverage !== null && (
              <p className="mt-4 flex items-center justify-center gap-2 text-sm text-deep/80">
                <Star aria-hidden="true" className="h-4 w-4 fill-turmeric text-turmeric" />
                {t('people.hostRating', {
                  rating: hostAverage.toFixed(1),
                  count: asNumber(person.asHostCount),
                })}
              </p>
            )}

            {isOther && (
              <button
                type="button"
                className={`${person.followedByMe ? secondaryButtonClass : primaryButtonClass} mt-6 w-full`}
                disabled={follow.isPending}
                onClick={() => follow.mutate()}
              >
                {person.followedByMe ? (
                  <UserCheck aria-hidden="true" className="h-4 w-4" />
                ) : (
                  <UserPlus aria-hidden="true" className="h-4 w-4" />
                )}
                {person.followedByMe ? t('people.unfollow') : t('people.follow')}
              </button>
            )}
            {isOther && (
              <>
                <button
                  type="button"
                  onClick={() => setReporting(true)}
                  className="mt-4 inline-flex items-center gap-1.5 text-sm text-deep/50 transition hover:text-jamdani"
                >
                  <Flag aria-hidden="true" className="h-4 w-4" />
                  {t('report.open.User')}
                </button>
                <ReportDialog
                  kind="User"
                  targetId={userId}
                  open={reporting}
                  onClose={() => setReporting(false)}
                />
              </>
            )}
          </div>
        </aside>

        <div className="flex min-w-0 flex-col gap-8">
          {person.bio && (
            <section className={`${cardClass} relative`}>
              <Quote
                aria-hidden="true"
                className="absolute right-6 top-6 h-10 w-10 text-turmeric/30"
              />
              <h2 className="text-lg font-semibold">{t('people.about')}</h2>
              <p className="mt-3 whitespace-pre-wrap leading-relaxed text-deep/80">{person.bio}</p>
            </section>
          )}

          {person.hostedTrips.length > 0 && (
            <section aria-labelledby="hosted-title">
              <h2 id="hosted-title" className="mb-4 text-xl font-semibold">
                {t('people.hostedTrips')}
              </h2>
              <ul className="grid gap-4 sm:grid-cols-2">
                {person.hostedTrips.map((trip) => (
                  <li key={String(trip.id)}>
                    <Link
                      to={`/trips/${asNumber(trip.id)}`}
                      className={`${cardClass} group flex items-center gap-4 p-3! transition hover:ring-hill/30`}
                    >
                      <Photo
                        slug={trip.destinationSlug}
                        kind="Hills"
                        cut="card"
                        decorative
                        className="h-20 w-20 shrink-0 rounded-xl"
                      />
                      <span className="min-w-0">
                        <span className="block font-display font-semibold text-deep group-hover:text-hill">
                          {trip.title}
                        </span>
                        <span className="mt-1 block text-sm text-deep/60">
                          {language === 'bn' ? trip.destinationNameBn : trip.destinationName} ·{' '}
                          {formatDateRange(trip.startDate, trip.endDate, language)}
                        </span>
                      </span>
                    </Link>
                  </li>
                ))}
              </ul>
            </section>
          )}

          <section aria-labelledby="reviews-title">
            <h2 id="reviews-title" className="mb-4 text-xl font-semibold">
              {t('people.reviews')}
            </h2>
            {person.reviews.length === 0 ? (
              <p className="rounded-2xl border-2 border-dashed border-hill/15 px-6 py-8 text-center text-sm text-deep/60">
                {t('people.noReviews')}
              </p>
            ) : (
              <ul className="grid gap-4 sm:grid-cols-2">
                {person.reviews.map((review) => (
                  <li key={String(review.id)} className={`${cardClass} flex flex-col gap-3`}>
                    <Stars
                      rating={asNumber(review.rating)}
                      label={t('people.rating', { rating: asNumber(review.rating) })}
                    />
                    {review.body && <p className="leading-relaxed text-deep">{review.body}</p>}
                    <p className="mt-auto text-xs text-deep/60">
                      <span className="font-semibold text-deep/80">
                        {review.reviewerName ?? t('chat.someone')}
                      </span>{' '}
                      · {review.tripTitle}
                    </p>
                  </li>
                ))}
              </ul>
            )}
          </section>

          <section aria-labelledby="stories-title" className="flex flex-col gap-4">
            <h2 id="stories-title" className="text-xl font-semibold">
              {t('feed.title')}
            </h2>
            {posts.data?.items.length === 0 && (
              <p className="rounded-2xl border-2 border-dashed border-hill/15 px-6 py-8 text-center text-sm text-deep/60">
                {t('people.noStories')}
              </p>
            )}
            {posts.data?.items.map((post) => (
              <PostCard key={String(post.id)} post={post} />
            ))}
          </section>
        </div>
      </div>
    </>
  );
}
