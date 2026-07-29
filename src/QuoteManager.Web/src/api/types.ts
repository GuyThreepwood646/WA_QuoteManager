export interface PagedResult<T> {
  items: T[]
  page: number
  pageSize: number
  total: number
}

export interface QuoteTriageItem {
  quoteId: string
  requestId: string
  requestTitle: string
  vendorOrganizationName: string
  amount: number
  currency: string
  status: string
  expiresAt: string | null
  statusChangedAt: string
  version: number
  permittedActions: string[]
}

export interface RequestAwaitingResponseItem {
  requestId: string
  title: string
  clientOrganizationName: string
  createdAt: string
  awaitingVendorNames: string[]
}

export interface DashboardResponse {
  quotesNeedingReview: QuoteTriageItem[]
  quotesUnderReview: QuoteTriageItem[]
  quotesExpiringSoon: QuoteTriageItem[]
  requestsAwaitingResponse: RequestAwaitingResponseItem[]
}

export interface RequestListItem {
  id: string
  title: string
  clientOrganizationName: string
  status: string
  quoteCount: number
  neededBy: string | null
  createdAt: string
}

export interface RequestQuoteItem {
  id: string
  vendorOrganizationId: string
  vendorOrganizationName: string
  status: string
  amount: number
  currency: string
  expiresAt: string | null
  notes: string | null
  statusChangedAt: string
  statusReason: string | null
  version: number
  permittedActions: string[]
}

export interface RequestInvitationItem {
  vendorOrganizationId: string
  vendorOrganizationName: string
  invitedAt: string
  hasQuoted: boolean
}

export interface RequestDetailResponse {
  id: string
  title: string
  description: string | null
  clientOrganizationId: string
  clientOrganizationName: string
  status: string
  neededBy: string | null
  createdAt: string
  isEditable: boolean
  canAddQuote: boolean
  quotes: RequestQuoteItem[]
  invitations: RequestInvitationItem[]
}

export interface OrganizationListItem {
  id: string
  name: string
  kind: string
}

export interface CreateRequestInput {
  title: string
  description?: string
  clientOrganizationId: string
  neededBy?: string
}

export interface ActivityEntryItem {
  id: string
  subjectType: string
  subjectId: string
  action: string
  summary: string
  actorDisplayName: string
  occurredAt: string
}

export interface CreateQuoteInput {
  vendorOrganizationId: string
  amount: number
  currency: string
  expiresAt?: string
  notes?: string
}
