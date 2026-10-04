/**
 * Thin fetch wrapper shared by the feature hooks.
 *
 * The typed client in `src/api/schema.d.ts` is generated from the backend OpenAPI document
 * (`npm run gen:api`). Request and response types are never hand-written; this file only
 * handles transport concerns: base URL, JSON, and turning a failure into a useful error.
 */

/** Relative by default so the Vite dev proxy and the deployed origin both just work. */
const baseUrl = import.meta.env.VITE_API_BASE_URL ?? '';

/** An error carrying what the API reported, so screens can show something specific. */
export class ApiError extends Error {
  readonly status: number;
  readonly detail: string | undefined;

  constructor(message: string, status: number, detail?: string) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.detail = detail;
  }
}

interface ProblemDetails {
  title?: string;
  detail?: string;
  status?: number;
}

export async function apiGet<T>(path: string, signal?: AbortSignal): Promise<T> {
  const response = await fetch(`${baseUrl}${path}`, {
    method: 'GET',
    headers: { Accept: 'application/json' },
    ...(signal ? { signal } : {}),
  });

  if (!response.ok) {
    throw await toApiError(response);
  }

  return (await response.json()) as T;
}

async function toApiError(response: Response): Promise<ApiError> {
  // The API reports expected failures as ProblemDetails; anything else may not be JSON.
  try {
    const problem = (await response.json()) as ProblemDetails;
    return new ApiError(
      problem.title ?? response.statusText,
      response.status,
      problem.detail ?? '',
    );
  } catch {
    return new ApiError(response.statusText || 'Request failed', response.status);
  }
}
