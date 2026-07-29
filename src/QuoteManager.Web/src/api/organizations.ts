import { apiClient } from './apiClient'
import type { CreateOrganizationInput, OrganizationListItem, PagedResult, UpdateOrganizationInput } from './types'

export function listOrganizations(pageSize = 100, includeRetired = false): Promise<PagedResult<OrganizationListItem>> {
  return apiClient.get<PagedResult<OrganizationListItem>>(
    `/api/organizations?pageSize=${pageSize}&includeRetired=${includeRetired}`,
  )
}

export function createOrganization(input: CreateOrganizationInput): Promise<OrganizationListItem> {
  return apiClient.post<OrganizationListItem>('/api/organizations', input)
}

/** `kind` is immutable and isn't part of this call. */
export function updateOrganization(organizationId: string, input: UpdateOrganizationInput): Promise<OrganizationListItem> {
  return apiClient.put<OrganizationListItem>(`/api/organizations/${organizationId}`, input)
}

/** Soft-deletes an organization: it stops appearing in pickers but existing records are unaffected. */
export function retireOrganization(organizationId: string): Promise<OrganizationListItem> {
  return apiClient.post<OrganizationListItem>(`/api/organizations/${organizationId}/retire`)
}
