import { apiClient } from './apiClient'
import type { AuthSession, CurrentUser } from '../auth/authSession'

export function login(email: string, password: string): Promise<AuthSession> {
  return apiClient.post<AuthSession>('/api/auth/login', { email, password })
}

export function getMe(): Promise<CurrentUser> {
  return apiClient.get<CurrentUser>('/api/auth/me')
}
