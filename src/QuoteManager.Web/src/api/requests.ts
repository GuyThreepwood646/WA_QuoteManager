import { apiClient } from './apiClient'
import type {
  ActivityEntryItem,
  CreateQuoteInput,
  CreateRequestInput,
  PagedResult,
  RequestDetailResponse,
  RequestListItem,
  RequestQuoteItem,
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
