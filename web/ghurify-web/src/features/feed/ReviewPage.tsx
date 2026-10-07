import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { Link, useParams } from 'react-router';
import { ArrowLeft, CircleCheck, Star } from 'lucide-react';

import { asNumber } from '@/api/client';
import { cardClass, primaryButtonClass, TextAreaField } from '@/components/Field';
import { EmptyState, ErrorState } from '@/components/States';
import { PageBanner } from '@/components/ui/PageBanner';
import { errorText } from '@/lib/errors';
import { feedApi, type Reviewable } from './feedApi';
import { Avatar } from './PostCard';

/** After a trip: rate and review the people you travelled with. */
export function ReviewPage() {
  const { t } = useTranslation();
  const { id } = useParams();
  const tripId = Number(id);

  const reviewable = useQuery({
    queryKey: ['reviewable', tripId],
    queryFn: ({ signal }) => feedApi.reviewable(tripId, signal),
    enabled: tripId > 0,
  });

  return (
    <>
      <PageBanner
        compact
        slug="kuakata"
        kind="Beach"
        eyebrow={
          <Link to="/me/trips" className="inline-flex items-center gap-1.5 hover:text-white">
            <ArrowLeft aria-hidden="true" className="h-3.5 w-3.5" />
            {t('nav.myTrips')}
          </Link>
        }
        title={t('reviews.title')}
      >
        <p className="mt-3 max-w-xl text-white/80">{t('reviews.lead')}</p>
      </PageBanner>

      <div className="container-page relative z-10 -mt-14 flex flex-col gap-6 pb-8 *:max-w-3xl">
        {reviewable.isPending && (
          <div role="status" className="h-64 animate-pulse rounded-2xl bg-hill/10">
            <span className="sr-only">{t('common.loading')}</span>
          </div>
        )}
        {reviewable.isError && (
          <ErrorState
            message={errorText(reviewable.error, t)}
            onRetry={() => void reviewable.refetch()}
          />
        )}
        {reviewable.data?.length === 0 && (
          <EmptyState title={t('reviews.nobody')} hint={t('reviews.nobodyHint')} />
        )}
        {reviewable.data?.map((person) => (
          <ReviewForm
            key={`${String(person.userId)}-${person.direction}`}
            tripId={tripId}
            person={person}
          />
        ))}
      </div>
    </>
  );
}

function ReviewForm({ tripId, person }: { tripId: number; person: Reviewable }) {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const [rating, setRating] = useState(0);
  const [hover, setHover] = useState(0);
  const [body, setBody] = useState('');

  const submit = useMutation({
    mutationFn: () =>
      feedApi.addReview(tripId, {
        revieweeId: asNumber(person.userId),
        direction: person.direction,
        rating,
        body: body.trim() || null,
      }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['reviewable', tripId] }),
  });

  const name = person.displayName ?? t('chat.someone');

  if (person.alreadyReviewed || submit.isSuccess) {
    return (
      <p className={`${cardClass} flex items-center gap-3 text-deep`} role="status">
        <CircleCheck aria-hidden="true" className="h-6 w-6 shrink-0 text-hill" />
        {t('reviews.done', { name })}
      </p>
    );
  }

  const shown = hover || rating;

  return (
    <form
      className={`${cardClass} flex flex-col gap-5`}
      onSubmit={(event) => {
        event.preventDefault();
        if (rating > 0) submit.mutate();
      }}
    >
      <div className="flex items-center gap-4">
        <Avatar name={person.displayName} userId={person.userId} />
        <h2 className="text-lg font-semibold">
          {person.direction === 'TravelerToHost'
            ? t('reviews.ofHost', { name })
            : t('reviews.ofTraveller', { name })}
        </h2>
      </div>
      <fieldset>
        <legend className="text-sm font-semibold text-deep">{t('reviews.rating')}</legend>
        <div className="mt-2 flex items-center gap-1" onPointerLeave={() => setHover(0)}>
          {[1, 2, 3, 4, 5].map((value) => (
            <button
              key={value}
              type="button"
              aria-pressed={rating === value}
              aria-label={t('people.rating', { rating: value })}
              onClick={() => setRating(value)}
              onPointerEnter={() => setHover(value)}
              className="rounded-lg p-1 transition hover:scale-110"
            >
              <Star
                aria-hidden="true"
                className={`h-9 w-9 transition ${
                  value <= shown ? 'fill-turmeric text-turmeric' : 'text-deep/20'
                }`}
              />
            </button>
          ))}
          {rating > 0 && (
            <span className="ml-2 text-sm font-semibold text-deep/70">
              {t(`reviews.scale.${rating}`)}
            </span>
          )}
        </div>
      </fieldset>
      <TextAreaField
        id={`review-${String(person.userId)}`}
        label={t('reviews.body')}
        rows={4}
        maxLength={1000}
        value={body}
        onChange={(event) => setBody(event.target.value)}
      />
      {submit.isError && (
        <p role="alert" className="text-sm text-jamdani">
          {errorText(submit.error, t)}
        </p>
      )}
      <button
        type="submit"
        className={`${primaryButtonClass} self-start`}
        disabled={rating === 0 || submit.isPending}
      >
        {t('reviews.submit')}
      </button>
    </form>
  );
}
