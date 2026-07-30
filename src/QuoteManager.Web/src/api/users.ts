import { apiClient } from './apiClient'
import type {
  CreateUserInput,
  PagedResult,
  ResetPasswordInput,
  UpdateUserInput,
  UpdateUserResult,
  UserListItem,
} from './types'

/** Admin gets every user; anyone else's result is scoped server-side to just their own row. */
export function listUsers(pageSize = 100): Promise<PagedResult<UserListItem>> {
  return apiClient.get<PagedResult<UserListItem>>(`/api/users?pageSize=${pageSize}`)
}

export function createUser(input: CreateUserInput): Promise<UserListItem> {
  return apiClient.post<UserListItem>('/api/users', input)
}

export function updateUser(userId: string, input: UpdateUserInput): Promise<UpdateUserResult> {
  return apiClient.put<UpdateUserResult>(`/api/users/${userId}`, input)
}

/** Omit `currentPassword` when an admin is resetting someone else's password. */
export function resetPassword(userId: string, input: ResetPasswordInput): Promise<void> {
  return apiClient.post<void>(`/api/users/${userId}/reset-password`, input)
}
