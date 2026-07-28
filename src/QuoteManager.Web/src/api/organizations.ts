import { apiClient } from './apiClient'
import type { OrganizationListItem, PagedResult } from './types'

export function listOrganizations(pageSize = 100): Promise<PagedResult<OrganizationListItem>> {
  return apiClient.get<PagedResult<OrganizationListItem>>(`/api/organizations?pageSize=${pageSize}`)
}
