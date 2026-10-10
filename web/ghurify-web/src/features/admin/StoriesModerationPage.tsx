import { useState } from 'react';
import { useInfiniteQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { Link, useSearchParams } from 'react-router';
import { Heart, MapPin, MessageCircle, Quote, Trash2, X } from 'lucide-react';

import { asNumber } from '@/api/client';
import { Avatar } from '@/components/Avatar';
import { secondaryButtonClass } from '@/components/Field';
import { EmptyState, ErrorState } from '@/components/States';
import type { PostView } from '@/features/feed/feedApi';
import { errorText } from '@/lib/errors';
import { formatCount, toLanguage, type Language } from '@/lib/format';
import { AdminPageHeader, CardGridSkeleton } from './AdminUi';
import { adminCardClass, smallDangerButtonClass } from './adminStyles';
import { moderationApi } from './moderationApi';
import { ReasonDialog } from './ReasonDialog';

type Media = PostView['media'][number];

/**
 * Every story on Ghurify, newest first, or one person's (?author=), for moderators and admins.
 * Removing one needs a reason: it is audited and sent to the author.
 *
 * Stories are cards in a grid with their first photo as the cover, the way travellers see them,
 * so what is wrong with one is visible at a glance; every other photo or video sits below it.
 */
export function StoriesModerationPage() {
  const { t, i18n } = useTranslation();
  const language = toLanguage(i18n.language);
  const queryClient = useQueryClient();
  const [params, setParams] = useSearchParams();
  const authorId = Number(params.get('author')) || null;
  const [removing, setRemoving] = useState<PostView | null>(null);

  const stories = useInfiniteQuery({
    queryKey: ['admin', 'posts', authorId],
    queryFn: ({ pageParam, signal }) => moderationApi.posts(authorId, pageParam, signal),
    initialPageParam: null as number | null,
    getNextPageParam: (page) => (page.nextBefore ? asNumber(page.nextBefore) : null),
  });

  const remove = useMutation({
    mutationFn: ({ id, reason }: { id: number; reason: string }) =>
      moderationApi.removePost(id, reason),
    onSuccess: () => {
      setRemoving(null);
      void queryClient.invalidateQueries({ queryKey: ['admin', 'posts'] });
      void queryClient.invalidateQueries({ queryKey: ['feed'] });
    },
  });

  const items = stories.data?.pages.flatMap((page) => page.items) ?? [];

  return (
    <div className="flex flex-col gap-6">
      <AdminPageHeader
        eyebrow={t('admin.groups.trust')}
        title={t('admin.stories.title')}
        description={t('admin.stories.lead')}
        actions={
          authorId && (
            <button
              type="button"
              onClick={() => setParams({})}
              className="inline-flex items-center gap-1.5 rounded-full bg-deep px-4 py-2 text-sm font-semibold text-white shadow-sm transition hover:bg-hill"
            >
              {t('admin.stories.onePerson', {
                name: items[0]?.authorName ?? `#${authorId}`,
              })}
              <X aria-hidden="true" className="h-4 w-4" />
              <span className="sr-only">{t('admin.stories.everyone')}</span>
            </button>
          )
        }
      />

      {stories.isPending && <CardGridSkeleton className="h-96" />}
      {stories.isError && (
        <ErrorState message={errorText(stories.error, t)} onRetry={() => void stories.refetch()} />
      )}
      {stories.isSuccess && items.length === 0 && <EmptyState title={t('admin.stories.empty')} />}

      {items.length > 0 && (
        <ul className="grid gap-5 sm:grid-cols-2 xl:grid-cols-3">
          {items.map((post) => (
            <li key={String(post.id)} className="flex">
              <StoryCard post={post} language={language} onRemove={() => setRemoving(post)} />
            </li>
          ))}
        </ul>
      )}

      {stories.hasNextPage && (
        <button
          type="button"
          className={`${secondaryButtonClass} self-center`}
          disabled={stories.isFetchingNextPage}
          onClick={() => void stories.fetchNextPage()}
        >
          {stories.isFetchingNextPage ? t('common.loading') : t('admin.stories.more')}
        </button>
      )}

      <ReasonDialog
        key={removing ? String(removing.id) : 'none'}
        open={removing !== null}
        title={t('admin.stories.confirmTitle', { name: removing?.authorName ?? '' })}
        description={t('admin.stories.confirmText')}
        confirmLabel={t('admin.stories.remove')}
        danger
        pending={remove.isPending}
        error={remove.error}
        onClose={() => {
          remove.reset();
          setRemoving(null);
        }}
        onConfirm={(reason) => removing && remove.mutate({ id: asNumber(removing.id), reason })}
      />
    </div>
  );
}

function StoryCard({
  post,
  language,
  onRemove,
}: {
  post: PostView;
  language: Language;
  onRemove: () => void;
}) {
  const { t, i18n } = useTranslation();
  const [cover, ...rest] = post.media;
  const alt = t('feed.photoAlt', { name: post.authorName ?? '' });
  const when = new Intl.DateTimeFormat(i18n.language, {
    dateStyle: 'medium',
    timeStyle: 'short',
    timeZone: 'Asia/Dhaka',
  }).format(new Date(post.created));

  return (
    <article className={adminCardClass}>
      {/* Every card has a cover the same shape, so a row lines up: the first photo, or for a
          story told only in words, the words themselves. */}
      <div
        className={`relative aspect-4/3 shrink-0 overflow-hidden ${
          cover ? 'bg-night' : 'bg-linear-to-br from-hill via-deep to-night'
        }`}
      >
        {cover ? (
          <MediaItem item={cover} alt={alt} className="h-full w-full object-cover" />
        ) : (
          <div className="flex h-full flex-col justify-center gap-3 px-7 py-10">
            <Quote aria-hidden="true" className="h-7 w-7 text-turmeric" />
            <p className="line-clamp-6 whitespace-pre-wrap font-display text-lg font-medium leading-snug text-white">
              {post.body}
            </p>
          </div>
        )}
        {post.destinationSlug && (
          <span className="absolute left-3 top-3 inline-flex items-center gap-1 rounded-full bg-night/50 px-2.5 py-1 text-xs font-semibold text-white ring-1 ring-white/20 backdrop-blur-sm">
            <MapPin aria-hidden="true" className="h-3.5 w-3.5" />
            {language === 'bn' ? post.destinationNameBn : post.destinationName}
          </span>
        )}
      </div>

      <div className="flex flex-1 flex-col gap-3.5 p-5">
        <div className="flex items-center gap-3">
          <Avatar userId={asNumber(post.authorId)} name={post.authorName} size="sm" />
          <div className="min-w-0 flex-1 text-sm">
            <Link
              to={`/admin/users/${asNumber(post.authorId)}`}
              className="font-semibold text-deep underline-offset-4 hover:text-hill hover:underline"
            >
              {post.authorName ?? t('admin.noName')}
            </Link>
            <p className="text-xs text-deep/55">
              #{asNumber(post.id)} · {when}
              {post.editedOn && ` · ${t('feed.edited')}`}
            </p>
          </div>
        </div>

        {/* Without a photo the words are already on the cover. */}
        {cover && post.body && (
          <p className="line-clamp-5 whitespace-pre-wrap text-sm leading-relaxed text-deep">
            {post.body}
          </p>
        )}

        {rest.length > 0 && (
          <div className="flex flex-wrap gap-2">
            {rest.map((item) => (
              <MediaItem
                key={String(item.id)}
                item={item}
                alt={alt}
                className="h-16 w-16 rounded-xl object-cover ring-1 ring-hill/10"
              />
            ))}
          </div>
        )}

        <div className="mt-auto flex flex-wrap items-center justify-between gap-2 border-t border-hill/8 pt-4">
          <span className="flex items-center gap-3 text-sm text-deep/60">
            <span className="inline-flex items-center gap-1">
              <Heart aria-hidden="true" className="h-4 w-4 text-jamdani/70" />
              <span className="sr-only">{t('admin.stories.likes')}</span>
              {formatCount(asNumber(post.likes), language)}
            </span>
            <span className="inline-flex items-center gap-1">
              <MessageCircle aria-hidden="true" className="h-4 w-4 text-hill/70" />
              {t('admin.stories.comments', { count: asNumber(post.comments) })}
            </span>
          </span>
          <button type="button" className={smallDangerButtonClass} onClick={onRemove}>
            <Trash2 aria-hidden="true" className="h-4 w-4" />
            {t('admin.stories.remove')}
          </button>
        </div>
      </div>
    </article>
  );
}

/** A photo, or a video with its controls, from a story. */
function MediaItem({ item, alt, className }: { item: Media; alt: string; className: string }) {
  return item.kind === 'Video' ? (
    <video src={item.url} controls preload="metadata" className={`${className} bg-night`} />
  ) : (
    <img src={item.url} alt={alt} loading="lazy" className={className} />
  );
}
