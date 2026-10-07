import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';

import { asNumber } from '@/api/client';
import { Avatar } from '@/components/Avatar';
import { EmptyState, ErrorState } from '@/components/States';
import { cardClass, dangerButtonClass, inputClass, primaryButtonClass } from '@/components/Field';
import { errorText } from '@/lib/errors';
import { adminApi, type VerificationQueueItem } from './adminApi';

const statuses = ['Pending', 'Approved', 'Rejected'] as const;

/** Identity checks waiting for a person, oldest first. Approve, or reject with a reason. */
export function VerificationQueuePage() {
  const { t, i18n } = useTranslation();
  const [status, setStatus] = useState<(typeof statuses)[number]>('Pending');
  const [page, setPage] = useState(1);

  const queue = useQuery({
    queryKey: ['admin', 'verifications', status, page],
    queryFn: ({ signal }) => adminApi.verifications(status, page, signal),
  });

  const total = queue.data ? asNumber(queue.data.totalCount) : 0;
  const pageSize = queue.data ? asNumber(queue.data.pageSize) : 25;

  return (
    <div className="flex flex-col gap-4">
      <header className="flex flex-wrap items-center justify-between gap-3">
        <h2 className="font-display text-2xl font-semibold text-deep sm:text-[1.75rem]">
          {t('admin.verifications.title')}
        </h2>
        <div
          role="group"
          aria-label={t('admin.verifications.filter')}
          className="flex gap-1 rounded-full bg-white p-1 shadow-sm ring-1 ring-hill/10"
        >
          {statuses.map((option) => (
            <button
              key={option}
              type="button"
              aria-pressed={status === option}
              onClick={() => {
                setStatus(option);
                setPage(1);
              }}
              className={`rounded-full px-4 py-1.5 text-sm font-semibold transition ${
                status === option
                  ? 'bg-hill text-white shadow-sm'
                  : 'text-deep/70 hover:bg-mist hover:text-deep'
              }`}
            >
              {t(`verification.status.${option}`)}
            </button>
          ))}
        </div>
      </header>

      {queue.isPending && (
        <div role="status" className="h-40 animate-pulse rounded-2xl bg-hill/10">
          <span className="sr-only">{t('common.loading')}</span>
        </div>
      )}

      {queue.isError && (
        <ErrorState message={errorText(queue.error, t)} onRetry={() => void queue.refetch()} />
      )}

      {queue.data && queue.data.items.length === 0 && (
        <EmptyState title={t('admin.verifications.empty')} />
      )}

      {queue.data && queue.data.items.length > 0 && (
        <ul className="flex flex-col gap-3">
          {queue.data.items.map((item) => (
            <li key={String(item.id)}>
              <QueueRow item={item} locale={i18n.language} />
            </li>
          ))}
        </ul>
      )}

      {total > pageSize && (
        <nav className="flex justify-between" aria-label={t('common.pagination')}>
          <button
            type="button"
            disabled={page <= 1}
            onClick={() => setPage((current) => current - 1)}
            className="inline-flex items-center gap-1 rounded-full border border-hill/15 bg-white px-4 py-2 text-sm font-semibold text-deep transition hover:bg-mist disabled:pointer-events-none disabled:opacity-40"
          >
            ← {t('common.previous')}
          </button>
          <button
            type="button"
            disabled={page * pageSize >= total}
            onClick={() => setPage((current) => current + 1)}
            className="inline-flex items-center gap-1 rounded-full border border-hill/15 bg-white px-4 py-2 text-sm font-semibold text-deep transition hover:bg-mist disabled:pointer-events-none disabled:opacity-40"
          >
            {t('common.next')} →
          </button>
        </nav>
      )}
    </div>
  );
}

function QueueRow({ item, locale }: { item: VerificationQueueItem; locale: string }) {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const [reason, setReason] = useState('');
  const [showPhotos, setShowPhotos] = useState(false);
  const id = asNumber(item.id);

  // Fetched only when asked for: each fetch is an audited look at someone's ID.
  const photos = useQuery({
    queryKey: ['admin', 'verification-documents', id],
    queryFn: () => adminApi.verificationDocuments(id),
    enabled: showPhotos,
    // The links expire after five minutes; never reuse a stale set.
    staleTime: 0,
    gcTime: 0,
  });

  const review = useMutation({
    mutationFn: (approve: boolean) =>
      adminApi.reviewVerification(id, { approve, reason: reason.trim() || null }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['admin', 'verifications'] }),
  });

  return (
    <article className={cardClass}>
      <div className="flex flex-wrap items-baseline justify-between gap-2">
        <p className="flex items-center gap-3 font-bold text-deep">
          <Avatar userId={asNumber(item.userId)} name={item.displayName} size="sm" />
          <span>
            {item.displayName ?? t('admin.noName')}{' '}
            <span className="text-sm font-normal text-deep/60">{item.maskedEmail}</span>
          </span>
        </p>
        <p className="text-xs text-deep/60">
          {new Intl.DateTimeFormat(locale, {
            dateStyle: 'medium',
            timeStyle: 'short',
            timeZone: 'Asia/Dhaka',
          }).format(new Date(item.created))}
        </p>
      </div>
      <p className="mt-1 text-sm text-deep/70">
        {t(`verification.levels.${item.level}`)} ·{' '}
        {t(`verification.idTypes.${item.idType ?? 'Nid'}`)} ·{' '}
        {t('admin.verifications.photoCount', { count: asNumber(item.documentCount ?? 0) })} ·{' '}
        {item.provider}
        {item.reason ? ` · ${item.reason}` : ''}
      </p>

      {asNumber(item.documentCount ?? 0) > 0 && (
        <div className="mt-3">
          <button
            type="button"
            aria-expanded={showPhotos}
            onClick={() => setShowPhotos((shown) => !shown)}
            className="text-sm font-semibold text-hill underline underline-offset-4"
          >
            {showPhotos ? t('admin.verifications.hidePhotos') : t('admin.verifications.showPhotos')}
          </button>
          {showPhotos && (
            <div className="mt-3">
              {photos.isPending && (
                <div role="status" className="h-32 animate-pulse rounded-2xl bg-hill/10">
                  <span className="sr-only">{t('common.loading')}</span>
                </div>
              )}
              {photos.isError && (
                <p role="alert" className="text-sm text-jamdani">
                  {errorText(photos.error, t)}
                </p>
              )}
              {photos.data && (
                <>
                  <ul className="grid grid-cols-2 gap-3 sm:grid-cols-3">
                    {photos.data.map((photo) => (
                      <li key={String(photo.id)} className="flex flex-col gap-1">
                        {photo.url ? (
                          <a href={photo.url} target="_blank" rel="noreferrer noopener">
                            <img
                              src={photo.url}
                              alt={t(`verification.kinds.${photo.kind}`)}
                              className="h-32 w-full rounded-xl bg-mist object-contain ring-1 ring-hill/15"
                            />
                          </a>
                        ) : (
                          <span className="flex h-32 items-center justify-center rounded-xl bg-mist text-xs text-deep/60">
                            {t('admin.verifications.photoDeleted')}
                          </span>
                        )}
                        <span className="text-xs text-deep/70">
                          {t(`verification.kinds.${photo.kind}`)}
                        </span>
                      </li>
                    ))}
                  </ul>
                  <p className="mt-2 text-xs text-deep/60">
                    {t('admin.verifications.photoNotice')}
                  </p>
                </>
              )}
            </div>
          )}
        </div>
      )}

      {item.status === 'Pending' && (
        <div className="mt-3 flex flex-col gap-2 sm:flex-row sm:items-end">
          <div className="flex-1">
            <label htmlFor={`reason-${id}`} className="text-sm font-medium text-deep">
              {t('admin.verifications.reason')}
            </label>
            <input
              id={`reason-${id}`}
              value={reason}
              maxLength={300}
              onChange={(event) => setReason(event.target.value)}
              className={inputClass}
            />
          </div>
          <div className="flex gap-2">
            <button
              type="button"
              className={primaryButtonClass}
              disabled={review.isPending}
              onClick={() => review.mutate(true)}
            >
              {t('admin.verifications.approve')}
            </button>
            <button
              type="button"
              className={dangerButtonClass}
              disabled={review.isPending || reason.trim() === ''}
              onClick={() => review.mutate(false)}
            >
              {t('admin.verifications.reject')}
            </button>
          </div>
        </div>
      )}
      {review.isError && (
        <p role="alert" className="mt-2 text-sm text-jamdani">
          {errorText(review.error, t)}
        </p>
      )}
    </article>
  );
}
