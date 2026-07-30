import { createContext, useCallback, useContext, useEffect, useMemo, useState, useSyncExternalStore } from 'react'
import type { ReactNode } from 'react'

import { getMe, login as loginRequest } from '../api/auth'
import { queryClient } from '../api/queryClient'
import { getSession, restoreSession, setSession, subscribeToSession } from './authSession'
import type { AuthSession } from './authSession'

// Rehydrates once at module load, before the first render, so a page refresh doesn't log a
// reviewer out. Expired tokens are discarded here so they never look like a live session.
restoreSession()

type AuthStatus = 'loading' | 'authenticated' | 'anonymous'

interface AuthContextValue {
  session: AuthSession | null
  /** False until a restored token has been confirmed with the API (or rejected). */
  isReady: boolean
  login: (email: string, password: string) => Promise<void>
  logout: () => void
}

const AuthContext = createContext<AuthContextValue | null>(null)

export function AuthProvider({ children }: { children: ReactNode }) {
  const session = useSyncExternalStore(subscribeToSession, getSession, () => null)
  const [status, setStatus] = useState<AuthStatus>(() => (getSession() ? 'loading' : 'anonymous'))

  // Confirm a rehydrated token once on boot. Login/logout update status directly; a mid-session
  // 401 is handled by apiClient clearing storage, which makes `session` null below.
  useEffect(() => {
    const current = getSession()
    if (!current) {
      setStatus('anonymous')
      return
    }

    let cancelled = false

    void getMe()
      .then((user) => {
        if (cancelled) {
          return
        }

        const latest = getSession()
        if (latest) {
          setSession({ ...latest, user })
        }
        setStatus('authenticated')
      })
      .catch(() => {
        if (cancelled) {
          return
        }

        if (getSession()) {
          setSession(null)
        }
        setStatus('anonymous')
      })

    return () => {
      cancelled = true
    }
  }, [])

  useEffect(() => {
    if (!session && status === 'authenticated') {
      setStatus('anonymous')
    }
  }, [session, status])

  const login = useCallback(async (email: string, password: string) => {
    const next = await loginRequest(email, password)
    setSession(next)
    setStatus('authenticated')
  }, [])

  const logout = useCallback(() => {
    setSession(null)
    queryClient.clear()
    setStatus('anonymous')
    if (window.location.pathname !== '/login') {
      window.location.assign('/login')
    }
  }, [])

  const value = useMemo<AuthContextValue>(
    () => ({
      session: status === 'authenticated' ? session : null,
      isReady: status !== 'loading',
      login,
      logout,
    }),
    [session, status, login, logout],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext)
  if (!context) {
    throw new Error('useAuth must be used within an AuthProvider')
  }

  return context
}
