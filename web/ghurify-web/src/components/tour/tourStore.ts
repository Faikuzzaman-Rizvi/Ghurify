import { create } from 'zustand';

/**
 * The guided tour's state. Small and client-only, so it lives in Zustand like the language.
 * Whether a visitor has already seen the tour is a per-browser convenience in localStorage,
 * wrapped in try/catch because private windows and blocked storage throw.
 */
const seenKey = 'ghurify.tourSeen';

interface TourState {
  active: boolean;
  step: number;
  start: () => void;
  stop: () => void;
  goTo: (step: number) => void;
}

export const useTourStore = create<TourState>()((set) => ({
  active: false,
  step: 0,
  start: () => set({ active: true, step: 0 }),
  stop: () => {
    markTourSeen();
    set({ active: false, step: 0 });
  },
  goTo: (step) => set({ step }),
}));

export function hasSeenTour(): boolean {
  try {
    return window.localStorage.getItem(seenKey) === '1';
  } catch {
    // Storage unavailable: treat as seen, so the tour never nags on every visit.
    return true;
  }
}

function markTourSeen(): void {
  try {
    window.localStorage.setItem(seenKey, '1');
  } catch {
    // Nothing to do: the tour simply may show again next time.
  }
}
