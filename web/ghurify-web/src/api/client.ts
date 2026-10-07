/**
 * Thin fetch wrapper shared by the feature hooks.
 *
 * The typed client in `src/api/schema.d.ts` is generated from the backend OpenAPI document
 * (`npm run gen:api`). Request and response types are never hand-written; this file only
 * handles transport concerns: base URL, JSON, the bearer token, and turning a failure into a
 * useful error.
 */

/** Relative by default so the Vite dev proxy and the deployed origin both just work. */
const baseUrl = import.meta.env.VITE_API_BASE_URL ?? '';

/**
 * The access token lives here, in a module variable, and never in localStorage or a cookie
 * readable by scripts. It is deliberately lost on reload: the httpOnly refresh cookie is what
 * restores the session, and that one cannot be read by any page script.
 */
let accessToken: string | null = null;

export function setAccessToken(token: string | null): void {
  accessToken = token;
}

export function getAccessToken(): string | null {
  return accessToken;
}

/**
 * Renews the session when a request finds its access token expired, and says whether it worked.
 * Set by the auth feature (useSilentRefresh); this file knows nothing about sessions itself.
 */
let renewSession: (() => Promise<boolean>) | null = null;

export function setSessionRenewer(renew: (() => Promise<boolean>) | null): void {
  renewSession = renew;
}

/**
 * The generated types widen integers to `number | string`, because a 64-bit id can exceed
 * what JSON numbers hold exactly and may arrive as a string. Everything we read fits in a
 * JavaScript number, so narrow it once, here, rather than at every call site.
 */
export function asNumber(value: number | string): number {
  return typeof value === 'number' ? value : Number(value);
}

/** An error carrying what the API reported, so screens can show something specific. */
export class ApiError extends Error {
  readonly status: number;
  readonly detail: string | undefined;
  /** Stable machine-readable reason (`nid_in_use`, `seat_taken`) for translated messages. */
  readonly code: string | undefined;
  /** For a pause (too many attempts): how long until trying again makes sense. */
  readonly retryAfterSeconds: number | undefined;

  constructor(
    message: string,
    status: number,
    detail?: string,
    code?: string,
    retryAfterSeconds?: number,
  ) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.detail = detail;
    this.code = code;
    this.retryAfterSeconds = retryAfterSeconds;
  }
}

interface ProblemDetails {
  title?: string;
  detail?: string;
  status?: number;
  code?: string;
  retryAfterSeconds?: number;
  errors?: Record<string, string[]>;
}

interface RequestOptions {
  signal?: AbortSignal;
  /** Extra headers, e.g. an idempotency key on payment calls. */
  headers?: Record<string, string>;
  /** Send the bearer token. Off for the sign-in endpoints, which have no token yet. */
  authenticated?: boolean;
}

export async function apiGet<T>(path: string, options: RequestOptions = {}): Promise<T> {
  return request<T>('GET', path, undefined, options);
}

export async function apiPut<T>(
  path: string,
  body?: unknown,
  options: RequestOptions = {},
): Promise<T> {
  return request<T>('PUT', path, body, options);
}

export async function apiDelete<T>(path: string, options: RequestOptions = {}): Promise<T> {
  return request<T>('DELETE', path, undefined, options);
}

export async function apiPost<T>(
  path: string,
  body?: unknown,
  options: RequestOptions = {},
): Promise<T> {
  return request<T>('POST', path, body, options);
}

async function request<T>(
  method: string,
  path: string,
  body: unknown,
  { signal, authenticated = true, headers: extraHeaders }: RequestOptions,
): Promise<T> {
  const headers: Record<string, string> = { Accept: 'application/json', ...extraHeaders };

  if (body !== undefined) {
    headers['Content-Type'] = 'application/json';
  }

  const send = (token: string | null) =>
    fetch(`${baseUrl}${path}`, {
      method,
      headers: token ? { ...headers, Authorization: `Bearer ${token}` } : headers,
      // Carries the httpOnly refresh cookie, including when the web app and the API are served
      // from different origins.
      credentials: 'include',
      ...(body !== undefined ? { body: JSON.stringify(body) } : {}),
      ...(signal ? { signal } : {}),
    });

  const token = authenticated ? accessToken : null;
  let response = await send(token);

  // The token ran out (a laptop that slept through its renewal, say): renew once and try again,
  // rather than show a signed-in person "you are signed out" on whatever they opened next.
  if (response.status === 401 && token && renewSession && (await renewSession())) {
    response = await send(accessToken);
  }

  if (!response.ok) {
    throw await toApiError(response);
  }

  // 204 and 202-with-no-body have nothing to parse.
  if (response.status === 204 || response.headers.get('Content-Length') === '0') {
    return undefined as T;
  }

  const text = await response.text();
  return (text ? JSON.parse(text) : undefined) as T;
}

async function toApiError(response: Response): Promise<ApiError> {
  // The API reports expected failures as ProblemDetails; anything else may not be JSON.
  try {
    const problem = (await response.json()) as ProblemDetails;

    // Validation problems carry the useful text one level down.
    const firstFieldError = problem.errors ? Object.values(problem.errors).flat()[0] : undefined;

    return new ApiError(
      problem.title ?? response.statusText,
      response.status,
      firstFieldError ?? problem.detail ?? '',
      problem.code,
      problem.retryAfterSeconds,
    );
  } catch {
    return new ApiError(response.statusText || 'Request failed', response.status);
  }
}
