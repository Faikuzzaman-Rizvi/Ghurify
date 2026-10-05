import {
  useCallback,
  useEffect,
  useLayoutEffect,
  useRef,
  useState,
  type CSSProperties,
} from 'react';
import { useTranslation } from 'react-i18next';
import { useLocation, useNavigate } from 'react-router';
import { useTourStore } from './tourStore';

/**
 * The tour's stops, in order. `target` is a `data-tour` attribute on the home page; a step
 * without one is shown centred. Text comes from `tour.steps.{key}` in both languages.
 */
const steps: readonly { key: string; target?: string }[] = [
  { key: 'welcome' },
  { key: 'search', target: 'search' },
  { key: 'destinations', target: 'destinations' },
  { key: 'trips', target: 'trips' },
  { key: 'safety', target: 'safety' },
  { key: 'language', target: 'language' },
  { key: 'account', target: 'account' },
];

interface Box {
  top: number;
  left: number;
  width: number;
  height: number;
}

const padding = 8;
const cardWidth = 340;

/**
 * A spotlight tour of the home page: dims everything except the feature being explained and
 * puts a short card beside it. Esc closes it; the arrow keys move between steps.
 */
export function GuidedTour() {
  const { t } = useTranslation();
  const active = useTourStore((state) => state.active);
  const step = useTourStore((state) => state.step);
  const goTo = useTourStore((state) => state.goTo);
  const stop = useTourStore((state) => state.stop);
  const location = useLocation();
  const navigate = useNavigate();
  const [box, setBox] = useState<Box | null>(null);
  const primaryButton = useRef<HTMLButtonElement>(null);

  const current = steps[step];
  const isLast = step === steps.length - 1;

  // The tour explains the home page, so it always runs there.
  useEffect(() => {
    if (active && location.pathname !== '/') {
      void navigate('/');
    }
  }, [active, location.pathname, navigate]);

  const measure = useCallback(() => {
    if (!current?.target) {
      setBox(null);
      return;
    }

    const element = document.querySelector(`[data-tour="${current.target}"]`);
    if (!element) {
      setBox(null);
      return;
    }

    const rect = element.getBoundingClientRect();
    setBox({
      top: rect.top - padding,
      left: rect.left - padding,
      width: rect.width + padding * 2,
      height: rect.height + padding * 2,
    });
  }, [current]);

  // Bring the target into view, then keep the spotlight on it while the page moves.
  useLayoutEffect(() => {
    if (!active) {
      return;
    }

    const element = current?.target
      ? document.querySelector(`[data-tour="${current.target}"]`)
      : null;

    element?.scrollIntoView({ block: 'center', behavior: 'smooth' });

    // Measured on the next frame (and again once smooth scrolling settles), never synchronously.
    const frame = window.requestAnimationFrame(measure);
    const settle = window.setTimeout(measure, 450);

    window.addEventListener('resize', measure);
    window.addEventListener('scroll', measure, true);

    return () => {
      window.cancelAnimationFrame(frame);
      window.clearTimeout(settle);
      window.removeEventListener('resize', measure);
      window.removeEventListener('scroll', measure, true);
    };
  }, [active, current, measure, location.pathname]);

  useEffect(() => {
    if (active) {
      primaryButton.current?.focus();
    }
  }, [active, step]);

  useEffect(() => {
    if (!active) {
      return;
    }

    function onKey(event: KeyboardEvent) {
      if (event.key === 'Escape') {
        stop();
      } else if (event.key === 'ArrowRight' && step < steps.length - 1) {
        goTo(step + 1);
      } else if (event.key === 'ArrowLeft' && step > 0) {
        goTo(step - 1);
      }
    }

    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [active, step, goTo, stop]);

  if (!active || !current) {
    return null;
  }

  const cardStyle = placeCard(box);

  return (
    <div className="fixed inset-0 z-50" role="presentation">
      {box ? (
        // The spotlight: a transparent hole whose enormous shadow dims everything else.
        <div
          aria-hidden="true"
          className="pointer-events-none absolute rounded-2xl ring-4 ring-turmeric transition-all duration-300"
          style={{
            top: box.top,
            left: box.left,
            width: box.width,
            height: box.height,
            boxShadow: '0 0 0 9999px rgba(23, 63, 46, 0.65)',
          }}
        />
      ) : (
        <div aria-hidden="true" className="absolute inset-0 bg-deep/65" />
      )}

      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby="tour-title"
        aria-describedby="tour-body"
        className="absolute animate-rise rounded-2xl bg-white p-5 shadow-2xl"
        style={cardStyle}
      >
        <p className="text-xs font-semibold uppercase tracking-wide text-turmeric">
          {t('tour.label')} · {t('tour.step', { current: step + 1, total: steps.length })}
        </p>
        <h2 id="tour-title" className="mt-1 text-xl font-bold text-deep">
          {t(`tour.steps.${current.key}.title`)}
        </h2>
        <p id="tour-body" className="mt-2 text-sm text-deep/80">
          {t(`tour.steps.${current.key}.body`)}
        </p>

        <div className="mt-4 flex items-center gap-1.5" aria-hidden="true">
          {steps.map((s, index) => (
            <span
              key={s.key}
              className={`h-1.5 rounded-full transition-all ${
                index === step ? 'w-6 bg-hill' : 'w-1.5 bg-hill/25'
              }`}
            />
          ))}
        </div>

        <div className="mt-4 flex items-center justify-between gap-2">
          <button
            type="button"
            onClick={stop}
            className="text-sm text-deep/60 underline-offset-4 hover:underline"
          >
            {t('tour.skip')}
          </button>
          <div className="flex gap-2">
            {step > 0 && (
              <button
                type="button"
                onClick={() => goTo(step - 1)}
                className="rounded-full border border-hill/25 px-4 py-1.5 text-sm text-deep transition hover:bg-hill/10"
              >
                {t('tour.back')}
              </button>
            )}
            <button
              ref={primaryButton}
              type="button"
              onClick={() => (isLast ? stop() : goTo(step + 1))}
              className="rounded-full bg-hill px-4 py-1.5 text-sm font-medium text-white transition hover:bg-deep"
            >
              {isLast ? t('tour.finish') : t('tour.next')}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}

/** Below the target when there is room, above it otherwise, centred when there is none. */
function placeCard(box: Box | null): CSSProperties {
  const viewportWidth = window.innerWidth;
  const viewportHeight = window.innerHeight;
  const width = Math.min(cardWidth, viewportWidth - 32);

  if (!box) {
    return { width, top: '50%', left: '50%', transform: 'translate(-50%, -50%)' };
  }

  const left = Math.min(Math.max(16, box.left), viewportWidth - width - 16);
  const spaceBelow = viewportHeight - (box.top + box.height);

  if (spaceBelow > 230) {
    return { width, left, top: box.top + box.height + 12 };
  }

  if (box.top > 230) {
    return { width, left, bottom: viewportHeight - box.top + 12 };
  }

  return { width, left: (viewportWidth - width) / 2, bottom: 16 };
}
