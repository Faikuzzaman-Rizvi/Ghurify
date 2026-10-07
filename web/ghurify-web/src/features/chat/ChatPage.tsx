import { useEffect, useRef, useState } from 'react';
import { useMutation } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { Link, useParams } from 'react-router';
import { ArrowLeft, Lock, Megaphone, Pin, SendHorizontal, ShieldAlert } from 'lucide-react';

import { asNumber } from '@/api/client';
import { secondaryButtonClass } from '@/components/Field';
import { EmptyState, ErrorState } from '@/components/States';
import { Photo } from '@/components/ui/Photo';
import { useAuthStore } from '@/features/auth/authStore';
import { Avatar } from '@/features/feed/PostCard';
import { useTrip } from '@/features/trips/useTrips';
import { errorText } from '@/lib/errors';
import { formatDateRange, toLanguage } from '@/lib/format';
import { chatApi, type ChatMessage } from './chatApi';
import { useTripChat } from './useChat';

/** A trip's group chat: the host and everyone with a seat, live. */
export function ChatPage() {
  const { t, i18n } = useTranslation();
  const language = toLanguage(i18n.language);
  const { id } = useParams();
  const tripId = Number(id);
  const userId = useAuthStore((state) => state.user?.id);
  const trip = useTrip(tripId);
  const chat = useTripChat(tripId);
  const [draft, setDraft] = useState('');
  const [pin, setPin] = useState(false);
  const [maskedWarning, setMaskedWarning] = useState(false);
  const isHost = trip.data !== undefined && userId === asNumber(trip.data.host.id);
  const logRef = useRef<HTMLDivElement>(null);

  const send = useMutation({
    mutationFn: () => chatApi.send(tripId, draft.trim(), pin),
    onSuccess: (sent) => {
      chat.add(sent.message);
      setMaskedWarning(sent.contactsMasked);
      setDraft('');
      setPin(false);
    },
  });

  // Keep the newest message in view as messages arrive.
  const newest = chat.messages.at(-1)?.id;
  useEffect(() => {
    const log = logRef.current;
    if (log) log.scrollTop = log.scrollHeight;
  }, [newest]);

  if (chat.notFound) {
    return (
      <div className="container-page max-w-2xl py-12">
        <EmptyState title={t('chat.notMember')} hint={t('chat.notMemberHint')} />
      </div>
    );
  }

  const place = trip.data
    ? language === 'bn'
      ? trip.data.destination.nameBn
      : trip.data.destination.name
    : null;

  return (
    <div className="container-page flex h-[calc(100dvh-4.5rem)] max-w-4xl flex-col gap-4 py-6">
      <header className="flex items-center gap-4 rounded-2xl bg-white p-3 pr-5 shadow-[0_4px_24px_rgba(15,42,31,0.06)] ring-1 ring-hill/10">
        <Link
          to={`/trips/${tripId}`}
          aria-label={trip.data?.title ?? t('chat.trip')}
          className="rounded-full p-2 text-deep transition hover:bg-mist"
        >
          <ArrowLeft aria-hidden="true" className="h-5 w-5" />
        </Link>
        {trip.data && (
          <Photo
            slug={trip.data.destination.slug}
            kind={trip.data.destination.kind}
            cut="card"
            decorative
            className="hidden h-12 w-12 shrink-0 rounded-xl sm:block"
          />
        )}
        <div className="min-w-0 flex-1">
          <h1 className="text-lg font-semibold">{t('chat.title')}</h1>
          <p className="truncate text-sm text-deep/60">
            {trip.data
              ? `${trip.data.title} · ${place} · ${formatDateRange(trip.data.startDate, trip.data.endDate, language)}`
              : t('chat.trip')}
          </p>
        </div>
        <Link
          to={`/trips/${tripId}/safety`}
          className="hidden items-center gap-1.5 rounded-full px-3 py-2 text-sm font-semibold text-jamdani transition hover:bg-jamdani/5 sm:inline-flex"
        >
          <ShieldAlert aria-hidden="true" className="h-4 w-4" />
          {t('safety.open')}
        </Link>
        <ConnectionBadge state={chat.connection} />
      </header>

      {chat.pinned.length > 0 && (
        <section
          aria-label={t('chat.pinned')}
          className="flex flex-col gap-1.5 rounded-2xl bg-turmeric/15 px-4 py-3 ring-1 ring-turmeric/40"
        >
          {chat.pinned.slice(0, 3).map((message) => (
            <p key={String(message.id)} className="flex gap-2 text-sm text-deep">
              <Pin aria-hidden="true" className="mt-0.5 h-4 w-4 shrink-0 text-ochre" />
              {message.body}
            </p>
          ))}
        </section>
      )}

      <div
        ref={logRef}
        className="flex flex-1 flex-col gap-3 overflow-y-auto rounded-2xl bg-mist p-4 ring-1 ring-hill/10 sm:p-6"
        role="log"
        aria-live="polite"
        aria-label={t('chat.messages')}
      >
        {chat.hasOlder && chat.messages.length > 0 && (
          <button
            type="button"
            className={`${secondaryButtonClass} self-center px-4! py-1.5! text-sm`}
            onClick={() => void chat.loadOlder()}
          >
            {t('chat.older')}
          </button>
        )}
        {chat.isLoading && (
          <p role="status" className="m-auto text-sm text-deep/60">
            {t('common.loading')}
          </p>
        )}
        {chat.isError && <ErrorState message={t('chat.loadError')} onRetry={chat.retry} />}
        {!chat.isLoading && !chat.isError && chat.messages.length === 0 && (
          <p className="m-auto max-w-xs text-center text-sm text-deep/60">{t('chat.empty')}</p>
        )}
        {chat.messages.map((message, index) => {
          const mine = asNumber(message.senderId) === userId;
          const previous = chat.messages[index - 1];
          // Consecutive messages from one person share a single name and avatar.
          const grouped =
            previous !== undefined && asNumber(previous.senderId) === asNumber(message.senderId);
          return (
            <Bubble key={String(message.id)} message={message} mine={mine} grouped={grouped} />
          );
        })}
      </div>

      {maskedWarning && (
        <p
          role="alert"
          className="flex gap-2 rounded-xl bg-amber-50 p-3 text-sm text-amber-900 ring-1 ring-amber-600/20"
        >
          <Lock aria-hidden="true" className="mt-0.5 h-4 w-4 shrink-0" />
          {t('chat.maskedWarning')}
        </p>
      )}
      {send.isError && (
        <p role="alert" className="text-sm text-jamdani">
          {errorText(send.error, t)}
        </p>
      )}

      <form
        className="flex flex-col gap-2"
        onSubmit={(event) => {
          event.preventDefault();
          if (draft.trim()) send.mutate();
        }}
      >
        <label htmlFor="chat-input" className="sr-only">
          {t('chat.write')}
        </label>
        <div className="flex items-end gap-2 rounded-2xl bg-white p-2 shadow-[0_4px_24px_rgba(15,42,31,0.06)] ring-1 ring-hill/10 focus-within:ring-hill/40">
          <textarea
            id="chat-input"
            rows={1}
            maxLength={2000}
            value={draft}
            onChange={(event) => setDraft(event.target.value)}
            onKeyDown={(event) => {
              // Enter sends; Shift+Enter starts a new line.
              if (event.key === 'Enter' && !event.shiftKey && !event.nativeEvent.isComposing) {
                event.preventDefault();
                if (draft.trim() && !send.isPending) send.mutate();
              }
            }}
            placeholder={t('chat.placeholder')}
            className="max-h-40 min-h-11 flex-1 resize-none bg-transparent px-3 py-2.5 text-base text-deep outline-none placeholder:text-deep/40"
          />
          <button
            type="submit"
            className="flex h-11 w-11 shrink-0 items-center justify-center rounded-xl bg-hill text-white transition hover:bg-deep disabled:opacity-40"
            disabled={send.isPending || !draft.trim()}
          >
            <SendHorizontal aria-hidden="true" className="h-5 w-5" />
            <span className="sr-only">{t('chat.send')}</span>
          </button>
        </div>
        {isHost && (
          <label className="flex items-center gap-2 px-1 text-sm text-deep">
            <input
              type="checkbox"
              className="h-4 w-4 accent-hill"
              checked={pin}
              onChange={(event) => setPin(event.target.checked)}
            />
            <Megaphone aria-hidden="true" className="h-4 w-4 text-ochre" />
            {t('chat.pin')}
          </label>
        )}
      </form>
    </div>
  );
}

function Bubble({
  message,
  mine,
  grouped,
}: {
  message: ChatMessage;
  mine: boolean;
  grouped: boolean;
}) {
  const { t, i18n } = useTranslation();
  const time = new Intl.DateTimeFormat(i18n.language, {
    hour: 'numeric',
    minute: '2-digit',
    timeZone: 'Asia/Dhaka',
  }).format(new Date(message.created));
  const announcement = message.kind === 'Announcement';

  return (
    <div
      className={`flex items-end gap-2 ${mine ? 'flex-row-reverse' : ''} ${grouped ? '-mt-2' : ''}`}
    >
      {!mine && (
        <span className={grouped ? 'invisible' : ''}>
          <Avatar name={message.senderName} userId={message.senderId} size="sm" />
        </span>
      )}
      <article
        className={`max-w-[78%] rounded-2xl px-4 py-2.5 text-sm shadow-sm ${
          mine ? 'rounded-br-md bg-hill text-white' : 'rounded-bl-md bg-white text-deep'
        } ${announcement ? 'ring-2 ring-turmeric' : ''}`}
      >
        {!mine && !grouped && (
          <p className="mb-0.5 text-xs font-semibold text-hill">
            {message.senderName ?? t('chat.someone')}
          </p>
        )}
        {announcement && (
          <p className="mb-1 flex items-center gap-1 text-xs font-semibold text-ochre">
            <Megaphone aria-hidden="true" className="h-3.5 w-3.5" />
            {t('chat.announcement')}
          </p>
        )}
        <p className="whitespace-pre-wrap leading-relaxed">{message.body}</p>
        <p
          className={`mt-1 flex items-center justify-end gap-1 text-[0.7rem] ${
            mine ? 'text-white/70' : 'text-deep/50'
          }`}
        >
          {message.wasMasked && <Lock aria-label={t('chat.maskedNote')} className="h-3 w-3" />}
          {time}
        </p>
      </article>
    </div>
  );
}

function ConnectionBadge({ state }: { state: 'connecting' | 'live' | 'reconnecting' | 'offline' }) {
  const { t } = useTranslation();
  const style = {
    connecting: 'bg-mist text-deep',
    live: 'bg-emerald-50 text-emerald-800',
    reconnecting: 'bg-amber-50 text-amber-900',
    offline: 'bg-jamdani/10 text-jamdani',
  }[state];
  const dot = {
    connecting: 'bg-deep/40',
    live: 'bg-emerald-500',
    reconnecting: 'bg-amber-500',
    offline: 'bg-jamdani',
  }[state];

  return (
    <span
      role="status"
      className={`inline-flex shrink-0 items-center gap-1.5 rounded-full px-3 py-1 text-xs font-semibold ${style}`}
    >
      <span
        aria-hidden="true"
        className={`h-2 w-2 rounded-full ${dot} ${state === 'live' ? 'animate-pulse' : ''}`}
      />
      {t(`chat.connection.${state}`)}
    </span>
  );
}
