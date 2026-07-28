import { getSession, setSession } from '../auth/authSession'
import { queryClient } from './queryClient'

/**
 * A refused request, carrying the RFC 9457 `code` extension the UI is allowed to branch on
 * (AD-8) - never the human-readable `detail`, which can be reworded without notice.
 */
export class ApiError extends Error {
  readonly status: number
  readonly code: string | undefined

  constructor(status: number, code: string | undefined, detail: string | undefined) {
    super(detail ?? `Request failed with status ${status}`)
    this.name = 'ApiError'
    this.status = status
    this.code = code
  }
}

interface ProblemDetailsBody {
  title?: string
  detail?: string
  code?: string
}

interface RequestOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'DELETE'
  body?: unknown
  headers?: Record<string, string>
}

async function request<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const session = getSession()
  const headers: Record<string, string> = { ...options.headers }

  if (options.body !== undefined) {
    headers['Content-Type'] = 'application/json'
  }
  if (session) {
    headers.Authorization = `Bearer ${session.accessToken}`
  }

  const response = await fetch(path, {
    method: options.method ?? 'GET',
    headers,
    body: options.body === undefined ? undefined : JSON.stringify(options.body),
  })

  // A 401 with no session attached is the endpoint's own answer (e.g. a bad login attempt) and
  // carries its own problem-details code - it is not this client losing an authenticated session,
  // so it falls through to the generic error handling below rather than being reinterpreted here.
  if (response.status === 401 && session) {
    setSession(null)
    queryClient.clear()
    if (window.location.pathname !== '/login') {
      window.location.assign('/login')
    }
    throw new ApiError(401, 'auth.unauthenticated', 'Your session has expired.')
  }

  if (!response.ok) {
    const problem = await tryParseProblemDetails(response)
    throw new ApiError(response.status, problem?.code, problem?.detail ?? problem?.title)
  }

  if (response.status === 204) {
    return undefined as T
  }

  return (await response.json()) as T
}

async function tryParseProblemDetails(response: Response): Promise<ProblemDetailsBody | null> {
  try {
    return (await response.json()) as ProblemDetailsBody
  } catch {
    return null
  }
}

/**
 * The one place in the SPA a network request originates (AD-14). Every resource module - `auth`,
 * `quotes`, and whatever follows - calls through here rather than touching `fetch` directly; the
 * oxlint `no-restricted-globals` rule makes that a build failure, not just a convention.
 */
export const apiClient = {
  get: <T>(path: string): Promise<T> => request<T>(path),
  post: <T>(path: string, body?: unknown, headers?: Record<string, string>): Promise<T> =>
    request<T>(path, { method: 'POST', body, headers }),
}
