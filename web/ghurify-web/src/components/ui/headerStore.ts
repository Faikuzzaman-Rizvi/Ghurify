import { useEffect } from 'react';
import { create } from 'zustand';

/**
 * Whether the page starts with a full-bleed photo, so the header can sit transparent on top
 * of it. A counter rather than a flag: during a route change the new page's hero can mount
 * before the old one unmounts.
 */
interface HeaderState {
  heroes: number;
  add: () => void;
  remove: () => void;
}

export const useHeaderStore = create<HeaderState>()((set) => ({
  heroes: 0,
  add: () => set((state) => ({ heroes: state.heroes + 1 })),
  remove: () => set((state) => ({ heroes: Math.max(0, state.heroes - 1) })),
}));

/** Call from a full-bleed hero: while it is on screen, the header floats over it. */
export function useHeroUnderHeader() {
  const add = useHeaderStore((state) => state.add);
  const remove = useHeaderStore((state) => state.remove);

  useEffect(() => {
    add();
    return remove;
  }, [add, remove]);
}

export function useIsOverHero() {
  return useHeaderStore((state) => state.heroes > 0);
}
