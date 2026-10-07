import { useState } from 'react';
import { useInfiniteQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { Link, useSearchParams } from 'react-router';
import { MapPin, Trash2, X } from 'lucide-react';

import { asNumber } from '@/api/client';
import { Avatar } from '@/components/Avatar';
import { cardClass, dangerButtonClass, secondaryButtonClass } from '@/components/Field';
import { EmptyState, ErrorState } from '@/components/States';
import type { PostView } from '@/features/feed/feedApi';
import { errorText } from '@/lib/errors';
import { toLanguage } from '@/lib/format';
import { moderationApi } from './moderationApi';
import { ReasonDialog } from './ReasonDialog';

/**
 * Every story on Ghurify, newest first, or one person's (?author=), for moderators and admins.
 * Removing one needs a reason: it is audited and sent to the author.
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
  const when = (iso: string) =>
    new Intl.DateTimeFormat(i18n.language, {
      dateStyle: 'medium',
      timeStyle: 'short',
      timeZone: 'Asia/Dhaka',
    }).format(new Date(iso));

  return (
    <div className="flex flex-col gap-4">
      <header className="flex flex-wrap items-center justify-between gap-3">
        <h2 className="font-display text-2xl font-semibold text-deep sm:text-[1.75rem]">
          {t('admin.stories.title')}
        </h2>
        {authorId && (
          <button
            type="button"
            onClick={() => setParams({})}
            className="inline-flex items-center gap-1.5 rounded-full bg-mist px-3 py-1.5 text-sm font-semibold text-deep transition hover:bg-hill/10"
          >
            {t('admin.stories.onePerson', {
              name: items[0]?.authorName ?? `#${authorId}`,
            })}
            <X aria-hidden="true" className="h-4 w-4" />
            <span className="sr-only">{t('admin.stories.everyone')}</span>
          </button>
        )}
      </header>

      {stories.isPending && (
        <div role="status" className="h-40 animate-pulse rounded-2xl bg-hill/10">
          <span className="sr-only">{t('common.loading')}</span>
        </div>
      )}
      {stories.isError && (
        <ErrorState message={errorText(stories.error, t)} onRetry={() => void stories.refetch()} />
      )}
      {stories.isSuccess && items.length === 0 && <EmptyState title={t('admin.stories.empty')} />}

      <ul className="flex flex-col gap-3">
        {items.map((post) => (
          <li key={String(post.id)} className={`${cardClass} flex flex-col gap-3 p-4!`}>
            <div className="flex items-center gap-3">
              <Avatar userId={asNumber(post.authorId)} name={post.authorName} size="sm" />
              <div className="min-w-0 flex-1 text-sm">
                <Link
                  to={`/admin/users/${asNumber(post.authorId)}`}
                  className="font-semibold text-deep hover:text-hill hover:underline"
                >
                  {post.authorName ?? t('admin.noName')}
                </Link>
                <p className="text-xs text-deep/60">
                  #{asNumber(post.id)} · {when(post.created)}
                  {post.editedOn && ` · ${t('feed.edited')}`}
                </p>
              </div>
              {post.destinationSlug && (
                <span className="inline-flex shrink-0 items-center gap-1 rounded-full bg-mist px-2.5 py-1 text-xs font-semibold text-deep">
                  <MapPin aria-hidden="true" className="h-3.5 w-3.5" />
                  {language === 'bn' ? post.destinationNameBn : post.destinationName}
                </span>
              )}
            </div>

            {post.body && (
              <p className="whitespace-pre-wrap text-sm leading-relaxed text-deep">{post.body}</p>
            )}

            {post.media.length > 0 && (
              <div className="flex flex-wrap gap-2">
                {post.media.map((item) =>
                  item.kind === 'Video' ? (
                    <video
                      key={String(item.id)}
                      src={item.url}
                      controls
                      preload="metadata"
                      className="h-28 rounded-lg bg-night"
                    />
                  ) : (
                    <img
                      key={String(item.id)}
                      src={item.url}
                      alt={t('feed.photoAlt', { name: post.authorName ?? '' })}
                      loading="lazy"
                      className="h-28 w-28 rounded-lg object-cover"
                    />
                  ),
                )}
              </div>
            )}

            <div className="flex flex-wrap items-center justify-between gap-2 border-t border-hill/10 pt-3 text-xs text-deep/60">
              <span>
                ♥ {asNumber(post.likes)} ·{' '}
                {t('admin.stories.comments', { count: asNumber(post.comments) })}
              </span>
              <button type="button" className={dangerButtonClass} onClick={() => setRemoving(post)}>
                <Trash2 aria-hidden="true" className="h-4 w-4" />
                {t('admin.stories.remove')}
              </button>
            </div>
          </li>
        ))}
      </ul>

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
