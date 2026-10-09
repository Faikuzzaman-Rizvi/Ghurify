import { useCallback, useState } from 'react';

import { ApiError } from '@/api/client';
import { StepUpDialog } from './StepUpDialog';

/**
 * Runs an admin action that may demand the password again.
 *
 * Wrap the call in `run(...)`. If the API answers that it needs the password, the action is held,
 * the dialog asks for it, and the exact same call is retried once the receipt is in hand — so the
 * person types their password instead of losing the form they had just filled in. Later calls
 * inside the receipt's few minutes go straight through.
 *
 * Render `dialog` somewhere in the screen; it is nothing until it is needed.
 */
export function useStepUp() {
  const [held, setHeld] = useState<{ action: () => Promise<unknown> } | null>(null);

  const run = useCallback(async <T,>(action: () => Promise<T>): Promise<T | undefined> => {
    try {
      return await action();
    } catch (error) {
      if (error instanceof ApiError && error.code === 'step_up_required') {
        setHeld({ action });
        return undefined;
      }

      throw error;
    }
  }, []);

  return {
    run,
    /** True while the password is being asked for, so a screen can keep its form disabled. */
    asking: held !== null,
    dialog: held ? (
      <StepUpDialog
        onCancel={() => setHeld(null)}
        onConfirmed={async () => {
          const { action } = held;
          setHeld(null);
          await action();
        }}
      />
    ) : null,
  };
}
