import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';
import { Bell } from 'lucide-react';
import { asNumber } from '@/api/client';
import { formatCount, toLanguage } from '@/lib/format';
import {
  useMarkNotificationsRead,
  useNotifications,
  type NotificationItem,
} from '@/hooks/useNotifications';

/** Where a notification takes you, from the ids in its data. */
function linkFor(item: NotificationItem): string {
  const data = parse(item.data);
  const tripId = typeof data.tripId === 'number' ? data.tripId : undefined;

  switch (item.kind) {
    case 'join_request.new':
    case 'join_request.cancelled':
      return tripId ? `/host/trips/${tripId}/requests` : '/host/trips';
    case 'booking.traveler_confirmed':
    case 'payout.released':
      return '/host/payouts';
    default:
      return '/me/trips';
  }
}

function parse(data: string | null | undefined): Record<string, unknown> {
  if (!data) return {};
  try {
    return JSON.parse(data) as Record<string, unknown>;
  } catch {
    return {};
  }
}

/** The bell in the header: unread count, and the latest notifications in a panel. */
export function NotificationBell() {
  const { t, i18n } = useTranslation();
  const language = toLanguage(i18n.language);
  const [open, setOpen] = useState(false);
  const { data } = useNotifications();
  const markRead = useMarkNotificationsRead();
  const unread = data ? asNumber(data.unreadCount) : 0;
  const items = data?.items ?? [];

  function toggle() {
    const next = !open;
    setOpen(next);
    const newest = items[0];
    if (next && unread > 0 && newest) {
      markRead.mutate(asNumber(newest.id));
    }
  }

  return (
    <div className="relative">
      <button
        type="button"
        onClick={toggle}
        aria-expanded={open}
        aria-label={t('notifications.label', { count: unread, n: formatCount(unread, language) })}
        className="relative rounded-full p-2 text-current transition hover:bg-current/10"
      >
        <Bell aria-hidden="true" className="h-5 w-5" />
        {unread > 0 && (
          <span className="absolute -right-0.5 -top-0.5 min-w-5 rounded-full bg-jamdani px-1 text-center text-xs font-bold text-white">
            {formatCount(Math.min(unread, 99), language)}
          </span>
        )}
      </button>

      {open && (
        <div className="absolute right-0 z-50 mt-3 w-[min(22rem,calc(100vw-2rem))] rounded-2xl bg-white p-2 text-deep shadow-xl ring-1 ring-hill/10">
          <p className="px-3 py-2 text-sm font-bold text-deep">{t('notifications.title')}</p>
          {items.length === 0 ? (
            <p className="px-3 pb-3 text-sm text-deep/60">{t('notifications.empty')}</p>
          ) : (
            <ul className="max-h-96 overflow-y-auto">
              {items.map((item) => (
                <li key={String(item.id)}>
                  <Link
                    to={linkFor(item)}
                    onClick={() => setOpen(false)}
                    className={`block rounded-xl px-3 py-2 text-sm hover:bg-mist ${
                      item.isRead ? 'text-deep/70' : 'font-medium text-deep'
                    }`}
                  >
                    {t(`notifications.kinds.${item.kind}`, {
                      defaultValue: t('notifications.kinds.default'),
                      ...parse(item.data),
                    })}
                  </Link>
                </li>
              ))}
            </ul>
          )}
        </div>
      )}
    </div>
  );
}
