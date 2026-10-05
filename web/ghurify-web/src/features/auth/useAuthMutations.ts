import { useMutation } from '@tanstack/react-query';
import { authApi } from './authApi';
import { useAuthStore } from './authStore';

/** Asks the API to send a code. */
export function useRequestOtp() {
  return useMutation({
    mutationFn: (email: string) => authApi.requestOtp(email),
  });
}

/** Exchanges the code for a session and stores it. */
export function useVerifyOtp() {
  const signIn = useAuthStore((state) => state.signIn);

  return useMutation({
    mutationFn: ({ email, code }: { email: string; code: string }) =>
      authApi.verifyOtp(email, code),
    onSuccess: (session) => signIn(session),
  });
}

/** Ends the session. */
export function useLogout() {
  const signOut = useAuthStore((state) => state.signOut);

  return useMutation({
    mutationFn: () => authApi.logout(),
    // Clear locally either way: if the call failed, the user still asked to be signed out,
    // and the server-side token expires on its own.
    onSettled: () => signOut(),
  });
}
