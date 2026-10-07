import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useAuthStore } from './authStore';
import { uploadToStorage } from '@/features/feed/feedApi';
import { prepareImage } from '@/lib/images';
import {
  profileApi,
  type StartVerificationRequest,
  type UpdateProfileCommand,
  type VerificationDocumentKind,
} from './profileApi';

/** The user id is in every key, so one person's profile is never served to the next. */
function useUserKey(): number | 'anonymous' {
  return useAuthStore((state) => state.user?.id ?? 'anonymous');
}

export function useMyProfile() {
  const userKey = useUserKey();
  const status = useAuthStore((state) => state.status);

  return useQuery({
    queryKey: ['me', userKey, 'profile'],
    queryFn: ({ signal }) => profileApi.get(signal),
    enabled: status === 'authenticated',
    staleTime: 60_000,
  });
}

export function useUpdateProfile() {
  const queryClient = useQueryClient();
  const userKey = useUserKey();

  return useMutation({
    mutationFn: (command: UpdateProfileCommand) => profileApi.update(command),
    onSuccess: (profile) => queryClient.setQueryData(['me', userKey, 'profile'], profile),
  });
}

export function useBecomeHost() {
  const queryClient = useQueryClient();
  const userKey = useUserKey();

  return useMutation({
    mutationFn: () => profileApi.becomeHost(),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['me', userKey] }),
  });
}

export function useMyVerifications() {
  const userKey = useUserKey();
  const status = useAuthStore((state) => state.status);

  return useQuery({
    queryKey: ['me', userKey, 'verifications'],
    queryFn: ({ signal }) => profileApi.verifications(signal),
    enabled: status === 'authenticated',
  });
}

export function useStartVerification() {
  const queryClient = useQueryClient();
  const userKey = useUserKey();

  return useMutation({
    mutationFn: (request: StartVerificationRequest) => profileApi.startVerification(request),
    // The badge, the roles and the queue position may all have changed.
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['me', userKey] }),
  });
}

export function useMyDocuments() {
  const userKey = useUserKey();
  const status = useAuthStore((state) => state.status);

  return useQuery({
    queryKey: ['me', userKey, 'documents'],
    queryFn: ({ signal }) => profileApi.documents(signal),
    enabled: status === 'authenticated',
  });
}

/**
 * Uploads one identity photo: shrink it in the browser, ask for an upload link, put the file
 * straight into private storage, then have the API check it and strip its metadata.
 */
export function useUploadDocument() {
  const queryClient = useQueryClient();
  const userKey = useUserKey();

  return useMutation({
    mutationFn: async ({
      kind,
      file,
      onProgress,
    }: {
      kind: VerificationDocumentKind;
      file: File;
      onProgress?: (fraction: number) => void;
    }) => {
      const photo = await prepareImage(file, { maxSide: 2000 });
      const ticket = await profileApi.startDocumentUpload(kind, photo.type, photo.size);
      await uploadToStorage(ticket.uploadUrl, photo, onProgress ?? (() => undefined));
      return profileApi.completeDocumentUpload(Number(ticket.id));
    },
    onSettled: () => queryClient.invalidateQueries({ queryKey: ['me', userKey, 'documents'] }),
  });
}

export function useRemoveDocument() {
  const queryClient = useQueryClient();
  const userKey = useUserKey();

  return useMutation({
    mutationFn: (id: number) => profileApi.removeDocument(id),
    onSettled: () => queryClient.invalidateQueries({ queryKey: ['me', userKey, 'documents'] }),
  });
}

/** Crops the photo square at 512px in the browser, uploads it, and makes it the picture. */
export function useUploadAvatar() {
  const queryClient = useQueryClient();
  const userKey = useUserKey();

  return useMutation({
    mutationFn: async (file: File) => {
      const photo = await prepareImage(file, { maxSide: 512, square: true });
      const ticket = await profileApi.startAvatarUpload(photo.type, photo.size);
      await uploadToStorage(ticket.uploadUrl, photo, () => undefined);
      await profileApi.completeAvatarUpload(ticket.id);
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['me', userKey, 'profile'] }),
  });
}

export function useRemoveAvatar() {
  const queryClient = useQueryClient();
  const userKey = useUserKey();

  return useMutation({
    mutationFn: () => profileApi.removeAvatar(),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['me', userKey, 'profile'] }),
  });
}
