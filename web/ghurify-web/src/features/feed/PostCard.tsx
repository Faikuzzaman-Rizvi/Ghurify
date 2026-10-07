import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';
import { Flag, MapPin, MessageCircle, Pencil, SendHorizontal, Trash2 } from 'lucide-react';

import { asNumber } from '@/api/client';
import { Dialog } from '@/components/Dialog';
import { cardClass, dangerButtonClass, inputClass, secondaryButtonClass } from '@/components/Field';
import { Avatar as UserAvatar } from '@/components/Avatar';
import { VerificationBadge } from '@/components/VerificationBadge';
import { useAuthStore } from '@/features/auth/authStore';
import { ReportDialog } from '@/features/safety/ReportDialog';
import { errorText } from '@/lib/errors';
import { formatCount, toLanguage } from '@/lib/format';
import { feedApi, type PostView } from './feedApi';
import { PostEditor } from './PostEditor';

/**
 * Someone's picture next to their name: their profile photo when we know who they are, else a
 * round initial.
 */
export function Avatar({
  name,
  size = 'md',
  userId,
}: {
  name: string | null;
  size?: 'sm' | 'md';
  userId?: number | string | null;
}) {
  if (userId !== undefined && userId !== null) {
    return <UserAvatar userId={userId} name={name} size={size} />;
  }

  return (
    <span
      aria-hidden="true"
      className={`flex shrink-0 items-center justify-center rounded-full bg-hill font-display font-bold text-white ${
        size === 'sm' ? 'h-8 w-8 text-sm' : 'h-11 w-11 text-base'
      }`}
    >
      {(name ?? '?').charAt(0).toUpperCase()}
    </span>
  );
}

const actionClass =
  'inline-flex items-center gap-1.5 rounded-full px-3.5 py-2 text-sm font-semibold transition disabled:cursor-not-allowed disabled:opacity-60';

/** One story: who, what, where, the photos, and the conversation under it. */
export function PostCard({ post }: { post: PostView }) {
  const { t, i18n } = useTranslation();
  const language = toLanguage(i18n.language);
  const queryClient = useQueryClient();
  const status = useAuthStore((state) => state.status);
  const [showComments, setShowComments] = useState(false);
  const [reporting, setReporting] = useState(false);
  const [editing, setEditing] = useState(false);
  const [deleting, setDeleting] = useState(false);
  const viewer = useAuthStore((state) => state.user?.id);
  const id = asNumber(post.id);
  const mine = status === 'authenticated' && viewer === asNumber(post.authorId);

  const remove = useMutation({
    mutationFn: () => feedApi.deletePost(id),
    onSuccess: async () => {
      setDeleting(false);
      await queryClient.invalidateQueries({ queryKey: ['feed'] });
    },
  });

  const like = useMutation({
    mutationFn: () => (post.likedByMe ? feedApi.unlike(id) : feedApi.like(id)),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['feed'] }),
  });

  const formatWhen = (iso: string) =>
    new Intl.DateTimeFormat(i18n.language, {
      dateStyle: 'medium',
      timeZone: 'Asia/Dhaka',
    }).format(new Date(iso));
  const when = formatWhen(post.created);

  return (
    <article className={`${cardClass} flex flex-col gap-4`}>
      <header className="flex items-center gap-3">
        <Avatar name={post.authorName} userId={post.authorId} />
        <div className="min-w-0 flex-1">
          <p className="flex flex-wrap items-center gap-2">
            <Link
              to={`/users/${asNumber(post.authorId)}`}
              className="font-display font-semibold text-deep hover:text-hill"
            >
              {post.authorName ?? t('chat.someone')}
            </Link>
            <VerificationBadge level={post.authorVerifiedLevel} />
          </p>
          <p className="text-xs text-deep/60">
            {when}
            {post.editedOn && <span title={formatWhen(post.editedOn)}> · {t('feed.edited')}</span>}
          </p>
        </div>
        {post.destinationSlug && (
          <Link
            to={`/destinations/${post.destinationSlug}`}
            className="inline-flex shrink-0 items-center gap-1 rounded-full bg-mist px-3 py-1 text-xs font-semibold text-deep transition hover:bg-hill hover:text-white"
          >
            <MapPin aria-hidden="true" className="h-3.5 w-3.5" />
            {language === 'bn' ? post.destinationNameBn : post.destinationName}
          </Link>
        )}
      </header>

      {editing ? (
        <PostEditor post={post} onDone={() => setEditing(false)} />
      ) : (
        post.body && <p className="whitespace-pre-wrap leading-relaxed text-deep">{post.body}</p>
      )}

      {!editing && post.media.length > 0 && (
        <div
          className={`grid gap-2 overflow-hidden rounded-xl ${post.media.length > 1 ? 'grid-cols-2' : ''}`}
        >
          {post.media.map((item) =>
            item.kind === 'Video' ? (
              <video
                key={String(item.id)}
                src={item.url}
                controls
                preload="metadata"
                className="w-full rounded-xl bg-night"
              />
            ) : (
              <img
                key={String(item.id)}
                src={item.url}
                alt={t('feed.photoAlt', { name: post.authorName ?? '' })}
                loading="lazy"
                className={`w-full rounded-xl object-cover ${
                  post.media.length > 1 ? 'aspect-square' : 'max-h-128'
                }`}
              />
            ),
          )}
        </div>
      )}

      <footer className="flex items-center gap-2 border-t border-hill/10 pt-3">
        <button
          type="button"
          aria-pressed={post.likedByMe}
          disabled={status !== 'authenticated' || like.isPending}
          onClick={() => like.mutate()}
          className={`${actionClass} ${
            post.likedByMe ? 'bg-jamdani/10 text-jamdani' : 'text-deep/70 hover:bg-mist'
          }`}
        >
          {post.likedByMe ? '♥' : '♡'} {formatCount(post.likes, language)}
        </button>
        <button
          type="button"
          aria-expanded={showComments}
          onClick={() => setShowComments((open) => !open)}
          className={`${actionClass} ${showComments ? 'bg-mist text-hill' : 'text-deep/70 hover:bg-mist'}`}
        >
          <MessageCircle aria-hidden="true" className="h-4 w-4" />
          {formatCount(post.comments, language)}
          <span className="sr-only">{t('feed.comments')}</span>
        </button>
        {mine && !editing && (
          <div className="ml-auto flex items-center gap-1">
            <button
              type="button"
              onClick={() => setEditing(true)}
              className="inline-flex items-center gap-1 rounded-full px-2.5 py-1.5 text-xs font-semibold text-deep/70 transition hover:bg-mist hover:text-hill"
            >
              <Pencil aria-hidden="true" className="h-3.5 w-3.5" />
              {t('feed.edit.open')}
            </button>
            <button
              type="button"
              onClick={() => setDeleting(true)}
              className="inline-flex items-center gap-1 rounded-full px-2.5 py-1.5 text-xs font-semibold text-deep/70 transition hover:bg-jamdani/10 hover:text-jamdani"
            >
              <Trash2 aria-hidden="true" className="h-3.5 w-3.5" />
              {t('feed.delete.open')}
            </button>
          </div>
        )}
        {status === 'authenticated' && !mine && (
          <button
            type="button"
            onClick={() => setReporting(true)}
            className="ml-auto inline-flex items-center gap-1 text-xs text-deep/50 transition hover:text-jamdani"
          >
            <Flag aria-hidden="true" className="h-3.5 w-3.5" />
            {t('report.open.Post')}
          </button>
        )}
      </footer>
      <Dialog
        open={deleting}
        title={t('feed.delete.title')}
        onClose={() => {
          remove.reset();
          setDeleting(false);
        }}
      >
        <p className="text-sm text-deep/80">{t('feed.delete.body')}</p>
        {remove.isError && (
          <p role="alert" className="mt-2 text-sm text-jamdani">
            {errorText(remove.error, t)}
          </p>
        )}
        <div className="mt-5 flex justify-end gap-2">
          <button type="button" className={secondaryButtonClass} onClick={() => setDeleting(false)}>
            {t('common.cancel')}
          </button>
          <button
            type="button"
            className={dangerButtonClass}
            disabled={remove.isPending}
            onClick={() => remove.mutate()}
          >
            {t('feed.delete.confirm')}
          </button>
        </div>
      </Dialog>
      {reporting && (
        <ReportDialog kind="Post" targetId={id} open onClose={() => setReporting(false)} />
      )}

      {showComments && <Comments postId={id} />}
    </article>
  );
}

function Comments({ postId }: { postId: number }) {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const status = useAuthStore((state) => state.status);
  const [draft, setDraft] = useState('');

  const comments = useQuery({
    queryKey: ['feed', 'comments', postId],
    queryFn: ({ signal }) => feedApi.comments(postId, signal),
  });

  const add = useMutation({
    mutationFn: () => feedApi.addComment(postId, draft.trim()),
    onSuccess: () => {
      setDraft('');
      void queryClient.invalidateQueries({ queryKey: ['feed'] });
    },
  });

  return (
    <section className="flex flex-col gap-3 rounded-xl bg-mist p-4" aria-label={t('feed.comments')}>
      {comments.isPending && (
        <p role="status" className="text-sm text-deep/60">
          {t('common.loading')}
        </p>
      )}
      {comments.data?.length === 0 && (
        <p className="text-sm text-deep/60">{t('feed.noComments')}</p>
      )}
      {comments.data?.map((comment) => (
        <div key={String(comment.id)} className="flex gap-2.5">
          <Avatar name={comment.authorName} userId={comment.authorId} size="sm" />
          <p className="rounded-2xl rounded-tl-sm bg-white px-3.5 py-2 text-sm text-deep shadow-sm">
            <span className="block text-xs font-semibold">
              {comment.authorName ?? t('chat.someone')}
            </span>
            {comment.body}
          </p>
        </div>
      ))}
      {status === 'authenticated' && (
        <form
          className="flex gap-2"
          onSubmit={(event) => {
            event.preventDefault();
            if (draft.trim()) add.mutate();
          }}
        >
          <label htmlFor={`comment-${postId}`} className="sr-only">
            {t('feed.addComment')}
          </label>
          <input
            id={`comment-${postId}`}
            value={draft}
            maxLength={1000}
            onChange={(event) => setDraft(event.target.value)}
            placeholder={t('feed.addComment')}
            className={`${inputClass} rounded-full! py-2.5`}
          />
          <button
            type="submit"
            className="flex h-11 w-11 shrink-0 items-center justify-center rounded-full bg-hill text-white transition hover:bg-deep disabled:opacity-40"
            disabled={add.isPending || !draft.trim()}
          >
            <SendHorizontal aria-hidden="true" className="h-4 w-4" />
            <span className="sr-only">{t('chat.send')}</span>
          </button>
        </form>
      )}
      {add.isError && (
        <p role="alert" className="text-sm text-jamdani">
          {errorText(add.error, t)}
        </p>
      )}
    </section>
  );
}
