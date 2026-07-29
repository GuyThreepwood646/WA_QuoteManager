import { apiClient } from './apiClient'
import type {
  ActivityEntryItem,
  CreateQuoteInput,
  CreateRequestInput,
  EditQuoteInput,
  PagedResult,
  RequestDetailResponse,
  RequestListItem,
  RequestQuoteItem,
  UpdateRequestInput,
} from './types'

export function listRequests(pageSize = 100): Promise<PagedResult<RequestListItem>> {
  return apiClient.get<PagedResult<RequestListItem>>(`/api/requests?pageSize=${pageSize}`)
}

export function getRequest(requestId: string): Promise<RequestDetailResponse> {
  return apiClient.get<RequestDetailResponse>(`/api/requests/${requestId}`)
}

/** The audit trail, projected as a per-request timeline rather than a raw log dump. */
export function getRequestActivity(requestId: string): Promise<PagedResult<ActivityEntryItem>> {
  return apiClient.get<PagedResult<ActivityEntryItem>>(`/api/requests/${requestId}/activity?pageSize=100`)
}

export function createRequest(input: CreateRequestInput): Promise<RequestDetailResponse> {
  return apiClient.post<RequestDetailResponse>('/api/requests', input)
}

export function updateRequest(requestId: string, input: UpdateRequestInput): Promise<RequestDetailResponse> {
  return apiClient.put<RequestDetailResponse>(`/api/requests/${requestId}`, input)
}

export function cancelRequest(requestId: string): Promise<RequestDetailResponse> {
  return apiClient.post<RequestDetailResponse>(`/api/requests/${requestId}/cancel`)
}

export function inviteVendor(requestId: string, vendorOrganizationId: string): Promise<RequestDetailResponse> {
  return apiClient.post<RequestDetailResponse>(`/api/requests/${requestId}/invitations`, { vendorOrganizationId })
}

export function createQuote(requestId: string, input: CreateQuoteInput): Promise<RequestQuoteItem> {
  return apiClient.post<RequestQuoteItem>(`/api/requests/${requestId}/quotes`, input)
}

/** The quote's own `version` round-trips as a weak If-Match, so a stale edit is refused as a conflict rather than silently overwritten. */
export function applyQuoteAction(
  requestId: string,
  quote: Pick<RequestQuoteItem, 'id' | 'version'>,
  action: string,
): Promise<RequestQuoteItem> {
  return apiClient.post<RequestQuoteItem>(
    `/api/requests/${requestId}/quotes/${quote.id}/transitions`,
    { action },
    { 'If-Match': `"${quote.version}"` },
  )
}

export function editQuote(
  requestId: string,
  quote: Pick<RequestQuoteItem, 'id' | 'version'>,
  input: EditQuoteInput,
): Promise<RequestQuoteItem> {
  return apiClient.put<RequestQuoteItem>(
    `/api/requests/${requestId}/quotes/${quote.id}`,
    input,
    { 'If-Match': `"${quote.version}"` },
  )
}
