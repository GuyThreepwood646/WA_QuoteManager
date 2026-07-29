import { createContext, useCallback, useContext, useMemo, useSyncExternalStore } from 'react'
import type { ReactNode } from 'react'

import { login as loginRequest } from '../api/auth'
import { getSession, restoreSession, setSession, subscribeToSession } from './authSession'
import type { AuthSession } from './authSession'

// Rehydrates once at module load, before the first render, so a page refresh doesn't log a
// reviewer out.
restoreSession()

interface AuthContextValue {
  session: AuthSession | null
  login: (email: string, password: string) => Promise<void>
  logout: () => void
}

const AuthContext = createContext<AuthContextValue | null>(null)

export function AuthProvider({ children }: { children: ReactNode }) {
  const session = useSyncExternalStore(subscribeToSession, getSession)

  const login = useCallback(async (email: string, password: string) => {
    const next = await loginRequest(email, password)
    setSession(next)
  }, [])

  const logout = useCallback(() => setSession(null), [])

  const value = useMemo<AuthContextValue>(
    () => ({ session, login, logout }),
    [session, login, logout],
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
