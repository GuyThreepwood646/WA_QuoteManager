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

/** Raises a request. The API is the sole authority on whether the caller's role permits it. */
export function createRequest(input: CreateRequestInput): Promise<RequestDetailResponse> {
  return apiClient.post<RequestDetailResponse>('/api/requests', input)
}

/** Edits a request's own fields. The API is the sole authority on role and editability. */
export function updateRequest(requestId: string, input: UpdateRequestInput): Promise<RequestDetailResponse> {
  return apiClient.put<RequestDetailResponse>(`/api/requests/${requestId}`, input)
}

/** Cancels a request. The API is the sole authority on role and whether the status permits it. */
export function cancelRequest(requestId: string): Promise<RequestDetailResponse> {
  return apiClient.post<RequestDetailResponse>(`/api/requests/${requestId}/cancel`)
}

/** Invites a vendor organization to quote on a request. */
export function inviteVendor(requestId: string, vendorOrganizationId: string): Promise<RequestDetailResponse> {
  return apiClient.post<RequestDetailResponse>(`/api/requests/${requestId}/invitations`, { vendorOrganizationId })
}

/** Drafts a quote against a request. */
export function createQuote(requestId: string, input: CreateQuoteInput): Promise<RequestQuoteItem> {
  return apiClient.post<RequestQuoteItem>(`/api/requests/${requestId}/quotes`, input)
}

/**
 * The one action-driven transition call: the quote's own `version` is round-tripped as a weak
 * If-Match, so a change made since the page last loaded is refused as a conflict rather than
 * silently overwritten.
 */
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

/** Edits an already-drafted quote's business fields, round-tripping its `version` as If-Match. */
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
