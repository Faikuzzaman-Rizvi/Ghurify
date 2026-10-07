import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { currentPosition, safetyApi, type FileReportCommand } from './safetyApi';

const checkInsKey = (tripId: number) => ['safety', 'check-ins', tripId] as const;

/** The trip's check-ins, for the people on it. Polls so a "we're safe" from someone else shows up. */
export function useCheckIns(tripId: number) {
  return useQuery({
    queryKey: checkInsKey(tripId),
    queryFn: ({ signal }) => safetyApi.checkIns(tripId, signal),
    enabled: Number.isFinite(tripId) && tripId > 0,
    refetchInterval: 60_000,
  });
}

export function useScheduleCheckIn(tripId: number) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ label, dueAt }: { label: string; dueAt: string }) =>
      safetyApi.scheduleCheckIn(tripId, label, dueAt),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: checkInsKey(tripId) }),
  });
}

export function useCompleteCheckIn(tripId: number) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ checkInId, note }: { checkInId: number; note: string | null }) =>
      safetyApi.completeCheckIn(checkInId, note),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: checkInsKey(tripId) }),
  });
}

/** Finds the phone's position, then raises the SOS with it. */
export function useRaiseSos(tripId: number) {
  return useMutation({
    mutationFn: async (message: string | null) =>
      safetyApi.raiseSos(tripId, await currentPosition(), message),
  });
}

export function useImSafe() {
  return useMutation({ mutationFn: (sosId: number) => safetyApi.imSafe(sosId) });
}

export function useFileReport() {
  return useMutation({ mutationFn: (command: FileReportCommand) => safetyApi.fileReport(command) });
}
