import { useEffect, useRef } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { useAuthStore } from './authStore';

/**
 * When a session ends (signed out here, or ended elsewhere), drops everything cached while it
 * lasted, so none of it stays in memory for whoever uses this browser next. It runs after the
 * screens that used the data have gone; what is still on screen keeps its data and stays live.
 */
export function useForgetOnSignOut(): void {
  const status = useAuthStore((state) => state.status);
  const queryClient = useQueryClient();
  const previous = useRef(status);

  useEffect(() => {
    if (previous.current === 'authenticated' && status === 'anonymous') {
      queryClient.removeQueries({ type: 'inactive' });
    }
    previous.current = status;
  }, [status, queryClient]);
}
