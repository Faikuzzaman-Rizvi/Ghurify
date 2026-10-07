import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { formatCount, toLanguage } from '@/lib/format';

/** "Pay within 24:13", counting down to the seat hold's deadline; "Hold expired" after it. */
export function HoldCountdown({ expiresAt }: { expiresAt: string }) {
  const { t, i18n } = useTranslation();
  const language = toLanguage(i18n.language);
  const deadline = new Date(expiresAt).getTime();
  const [now, setNow] = useState(() => Date.now());

  useEffect(() => {
    const timer = window.setInterval(() => setNow(Date.now()), 1000);
    return () => window.clearInterval(timer);
  }, []);

  const left = Math.max(0, Math.floor((deadline - now) / 1000));

  if (left === 0) {
    return <span className="text-sm font-semibold text-jamdani">{t('bookings.holdExpired')}</span>;
  }

  const minutes = formatCount(Math.floor(left / 60), language);
  const seconds = String(left % 60).padStart(2, '0');
  const clock = `${minutes}:${language === 'bn' ? formatCount(Number(seconds), language).padStart(2, '০') : seconds}`;

  return (
    <span className="text-sm font-semibold text-jamdani" aria-live="off">
      {t('bookings.payWithin', { time: clock })}
    </span>
  );
}
