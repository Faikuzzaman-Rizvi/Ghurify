import { create } from 'zustand';

export type ToastTone = 'success' | 'error';

export interface Toast {
  id: number;
  message: string;
  tone: ToastTone;
}

/** Long enough to read a sentence, short enough not to sit over the page. */
const lifetimeMs = 5000;

interface ToastState {
  toasts: Toast[];
  show: (message: string, tone?: ToastTone) => void;
  dismiss: (id: number) => void;
}

let nextId = 1;

/**
 * Short confirmations of something that has already happened — a place added to the map — for
 * actions whose result is otherwise only visible somewhere the reader is not looking.
 *
 * Only for what has finished: anything the reader must act on belongs on the page, beside the
 * control it concerns, where it stays until it is dealt with.
 */
export const useToastStore = create<ToastState>()((set) => ({
  toasts: [],
  show: (message, tone = 'success') => {
    const id = nextId++;
    set((state) => ({ toasts: [...state.toasts, { id, message, tone }] }));
    window.setTimeout(
      () => set((state) => ({ toasts: state.toasts.filter((toast) => toast.id !== id) })),
      lifetimeMs,
    );
  },
  dismiss: (id) => set((state) => ({ toasts: state.toasts.filter((toast) => toast.id !== id) })),
}));

/** Shows a toast from anywhere, including outside React. */
export const showToast = (message: string, tone?: ToastTone) =>
  useToastStore.getState().show(message, tone);
