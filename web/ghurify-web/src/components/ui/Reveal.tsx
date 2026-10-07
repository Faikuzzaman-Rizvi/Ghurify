import { useEffect, useRef, useState, type ElementType, type ReactNode } from 'react';

function canAnimate() {
  return (
    typeof window !== 'undefined' &&
    'IntersectionObserver' in window &&
    !window.matchMedia?.('(prefers-reduced-motion: reduce)').matches
  );
}

/**
 * Rises into place the first time it scrolls into view. Without IntersectionObserver, or
 * when the reader prefers reduced motion, it is simply there: content is never hidden by an
 * animation that might not run.
 */
export function Reveal({
  children,
  as: Tag = 'div',
  className = '',
  delay = 0,
}: {
  children: ReactNode;
  as?: ElementType;
  className?: string;
  /** Milliseconds, to stagger a row of cards. */
  delay?: number;
}) {
  const ref = useRef<HTMLElement>(null);
  const [hidden, setHidden] = useState(canAnimate);

  useEffect(() => {
    const element = ref.current;
    if (!hidden || !element) return;

    const observer = new IntersectionObserver(
      ([entry]) => {
        if (entry?.isIntersecting) {
          setHidden(false);
          observer.disconnect();
        }
      },
      { rootMargin: '0px 0px -8% 0px' },
    );
    observer.observe(element);
    return () => observer.disconnect();
  }, [hidden]);

  return (
    <Tag
      ref={ref}
      className={`${hidden ? 'opacity-0' : 'animate-rise'} ${className}`}
      style={delay && !hidden ? { animationDelay: `${delay}ms` } : undefined}
    >
      {children}
    </Tag>
  );
}
