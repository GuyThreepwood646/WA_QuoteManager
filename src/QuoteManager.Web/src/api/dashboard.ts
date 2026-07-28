import { apiClient } from './apiClient'
import type { DashboardResponse } from './types'

export function getDashboard(): Promise<DashboardResponse> {
  return apiClient.get<DashboardResponse>('/api/dashboard')
}
