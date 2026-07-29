/**
 * Holds the current session outside React so `apiClient` can read and clear it without importing
 * a hook (apiClient is plain code, not a component). `AuthProvider` is the only thing that exposes
 * this to the component tree, via `useSyncExternalStore`.
 */
export interface CurrentUser {
  id: string
  displayName: string
  roles: string[]
  organizationId: string | null
}

export interface AuthSession {
  accessToken: string
  expiresAt: string
  user: CurrentUser
}

const storageKey = 'qm.session'

let session: AuthSession | null = null
const listeners = new Set<() => void>()

export function getSession(): AuthSession | null {
  return session
}

export function setSession(next: AuthSession | null): void {
  session = next

  if (next) {
    sessionStorage.setItem(storageKey, JSON.stringify(next))
  } else {
    sessionStorage.removeItem(storageKey)
  }

  for (const listener of listeners) {
    listener()
  }
}

export function subscribeToSession(listener: () => void): () => void {
  listeners.add(listener)
  return () => listeners.delete(listener)
}

/** Rehydrates from sessionStorage once, at module load, so a page refresh does not log a user out. */
export function restoreSession(): AuthSession | null {
  const raw = sessionStorage.getItem(storageKey)
  if (!raw) {
    return null
  }

  try {
    session = JSON.parse(raw) as AuthSession
    return session
  } catch {
    sessionStorage.removeItem(storageKey)
    return null
  }
}
