import { useMutation } from '@tanstack/react-query';
import {
  authApi,
  type ChangePasswordCommand,
  type RegisterCommand,
  type ResetPasswordCommand,
} from './authApi';
import { useAuthStore } from './authStore';

/** Creates an account; a code is emailed to confirm the address. */
export function useRegister() {
  return useMutation({ mutationFn: (command: RegisterCommand) => authApi.register(command) });
}

/** Confirms the address with the emailed code, which also signs in. */
export function useConfirmEmail() {
  const signIn = useAuthStore((state) => state.signIn);

  return useMutation({
    mutationFn: ({ email, code }: { email: string; code: string }) =>
      authApi.confirmEmail(email, code),
    onSuccess: (session) => signIn(session),
  });
}

export function useResendCode() {
  return useMutation({ mutationFn: (email: string) => authApi.resendCode(email) });
}

/** Email and password. No code. */
export function useSignIn() {
  const signIn = useAuthStore((state) => state.signIn);

  return useMutation({
    mutationFn: ({ email, password }: { email: string; password: string }) =>
      authApi.signIn(email, password),
    onSuccess: (session) => signIn(session),
  });
}

export function useForgotPassword() {
  return useMutation({ mutationFn: (email: string) => authApi.forgotPassword(email) });
}

/** A new password with the emailed code; every other device is signed out, this one signed in. */
export function useResetPassword() {
  const signIn = useAuthStore((state) => state.signIn);

  return useMutation({
    mutationFn: (command: ResetPasswordCommand) => authApi.resetPassword(command),
    onSuccess: (session) => signIn(session),
  });
}

/** Changes the password; the API ends other sessions and hands this one a fresh token. */
export function useChangePassword() {
  const signIn = useAuthStore((state) => state.signIn);

  return useMutation({
    mutationFn: (command: ChangePasswordCommand) => authApi.changePassword(command),
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
