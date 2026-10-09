/**
 * The step-up receipt: proof that the admin at the keyboard typed their own password a few
 * minutes ago.
 *
 * Kept in a module variable, like the access token, and deliberately lost on reload — it is
 * proof of something that just happened, not a session, so it must not survive one. Nothing
 * writes it to storage.
 */
let receipt: { token: string; expiresAt: number } | null = null;

/** A minute of headroom, so a receipt is never sent just as it expires. */
const margin = 60_000;

export function setStepUpReceipt(token: string, expiresOn: string): void {
  receipt = { token, expiresAt: new Date(expiresOn).getTime() };
}

export function clearStepUpReceipt(): void {
  receipt = null;
}

/** Whether the portal currently holds a usable receipt. */
export function hasStepUpReceipt(): boolean {
  return receipt !== null && receipt.expiresAt - margin > Date.now();
}

/**
 * The header for a call that demands the password again, or nothing. Sending nothing is fine:
 * the API answers 403 with <c>step_up_required</c> and the portal then asks.
 */
export function stepUpHeaders(): Record<string, string> {
  return hasStepUpReceipt() ? { 'X-Ghurify-Step-Up': receipt!.token } : {};
}
