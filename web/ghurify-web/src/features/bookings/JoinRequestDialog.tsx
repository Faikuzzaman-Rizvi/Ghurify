import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';

import { ApiError } from '@/api/client';
import { Dialog } from '@/components/Dialog';
import { primaryButtonClass, secondaryButtonClass, TextAreaField } from '@/components/Field';
import { errorText } from '@/lib/errors';
import { useRequestToJoin } from './useBookings';

/** Asks the host to join: a short note, then a clear "what happens next". */
export function JoinRequestDialog({
  tripId,
  open,
  onClose,
}: {
  tripId: number;
  open: boolean;
  onClose: () => void;
}) {
  const { t } = useTranslation();
  const [message, setMessage] = useState('');
  const request = useRequestToJoin(tripId);
  const needsVerification = request.error instanceof ApiError && request.error.status === 403;

  return (
    <Dialog open={open} onClose={onClose} title={t('join.title')}>
      {request.isSuccess ? (
        <div role="status" className="flex flex-col gap-3">
          <p className="font-semibold text-hill">{t('join.sent')}</p>
          <p className="text-sm text-deep/70">{t('join.next')}</p>
          <Link to="/me/trips" className={primaryButtonClass}>
            {t('nav.myTrips')}
          </Link>
        </div>
      ) : (
        <form
          className="flex flex-col gap-4"
          onSubmit={(event) => {
            event.preventDefault();
            request.mutate(message);
          }}
        >
          <p className="text-sm text-deep/70">{t('join.intro')}</p>
          <TextAreaField
            id="join-message"
            label={t('join.message')}
            hint={t('join.messageHint')}
            maxLength={500}
            value={message}
            onChange={(event) => setMessage(event.target.value)}
          />
          {request.isError &&
            (needsVerification ? (
              <p role="alert" className="rounded-xl bg-amber-50 p-3 text-sm text-amber-900">
                {t('join.verifyFirst')}{' '}
                <Link to="/account/verify" className="font-semibold underline">
                  {t('verification.start')}
                </Link>
              </p>
            ) : (
              <p role="alert" className="text-sm text-jamdani">
                {errorText(request.error, t)}
              </p>
            ))}
          <div className="flex justify-end gap-2">
            <button type="button" className={secondaryButtonClass} onClick={onClose}>
              {t('common.cancel')}
            </button>
            <button type="submit" className={primaryButtonClass} disabled={request.isPending}>
              {t('join.send')}
            </button>
          </div>
        </form>
      )}
    </Dialog>
  );
}
