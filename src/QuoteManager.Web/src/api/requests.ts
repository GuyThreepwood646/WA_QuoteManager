import { apiClient } from './apiClient'
import type { PagedResult, RequestDetailResponse, RequestListItem, RequestQuoteItem } from './types'

export function listRequests(pageSize = 100): Promise<PagedResult<RequestListItem>> {
  return apiClient.get<PagedResult<RequestListItem>>(`/api/requests?pageSize=${pageSize}`)
}

export function getRequest(requestId: string): Promise<RequestDetailResponse> {
  return apiClient.get<RequestDetailResponse>(`/api/requests/${requestId}`)
}

/**
 * The one action-driven transition call (AD-2): the quote's own `version` is round-tripped as a
 * weak If-Match (AD-15), so a change made since the page last loaded is refused as a conflict
 * rather than silently overwritten.
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
