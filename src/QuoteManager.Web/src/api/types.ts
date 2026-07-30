export interface PagedResult<T> {
  items: T[]
  page: number
  pageSize: number
  total: number
}

export interface DashboardQuoteItem {
  quoteId: string
  vendorOrganizationId: string
  vendorOrganizationName: string
  amount: number
  currency: string
  status: string
  expiresAt: string | null
  statusChangedAt: string
  version: number
  isExpiringSoon: boolean
  permittedActions: string[]
}

export interface DashboardRequestItem {
  requestId: string
  title: string
  clientOrganizationName: string
  neededBy: string | null
  createdAt: string
  quotes: DashboardQuoteItem[]
  awaitingVendorNames: string[]
}

export interface DashboardKpis {
  openRequestCount: number
  quotesAwaitingDecisionCount: number
  requestsOpenedThisMonth: number
  requestsClosedThisMonth: number
  vendorResponseRatePercent: number | null
}

export interface DashboardResponse {
  kpis: DashboardKpis
  requests: DashboardRequestItem[]
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
  createdAt: string
  statusChangedAt: string
  statusReason: string | null
  lastActivityAt: string | null
  lastActivityNote: string | null
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
  canEdit: boolean
  canCancel: boolean
  canInviteVendor: boolean
  quotes: RequestQuoteItem[]
  invitations: RequestInvitationItem[]
}

export interface OrganizationLocationItem {
  id: string
  address: string
  phone: string | null
  sortOrder: number
}

export interface OrganizationListItem {
  id: string
  name: string
  kind: string
  retiredAt: string | null
  primaryAddress: string | null
  primaryContactName: string | null
  primaryContactEmail: string | null
  primaryContactPhone: string | null
  isPreferredVendor: boolean
  locations: OrganizationLocationItem[]
}

export interface CreateOrganizationInput {
  name: string
  kind: 'Client' | 'Vendor'
  primaryAddress?: string
  primaryContactName?: string
  primaryContactEmail?: string
  primaryContactPhone?: string
  isPreferredVendor?: boolean
  locations?: OrganizationLocationInput[]
}

export interface OrganizationLocationInput {
  address: string
  phone?: string
}

export interface UpdateOrganizationInput {
  name: string
  primaryAddress?: string
  primaryContactName?: string
  primaryContactEmail?: string
  primaryContactPhone?: string
  isPreferredVendor?: boolean
  locations?: OrganizationLocationInput[]
}

export interface CreateRequestInput {
  title: string
  description?: string
  clientOrganizationId: string
  neededBy?: string
}

export interface UpdateRequestInput {
  title: string
  description?: string
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
  note: string | null
}

export interface CreateQuoteInput {
  vendorOrganizationId: string
  amount: number
  currency: string
  expiresAt?: string
  notes?: string
}

export interface EditQuoteInput {
  amount: number
  currency: string
  expiresAt?: string
  notes?: string
}

export interface UserListItem {
  id: string
  email: string
  displayName: string
  roles: string[]
  organizationId: string | null
  organizationName: string | null
  address: string | null
  phone: string | null
}

export interface CreateUserInput {
  email: string
  displayName: string
  roles: string[]
  organizationId?: string
  address?: string
  phone?: string
  password: string
  confirmPassword: string
}

export interface UpdateUserInput {
  email: string
  displayName: string
  address?: string
  phone?: string
  roles: string[]
  organizationId?: string
}

/**
 * `accessToken`/`expiresAt` are only present when the edited user was the caller themselves -
 * `DisplayName`/`Email`/`Roles` are baked into the JWT at login and never re-read from the
 * database per request, so a self-edit needs a fresh token or the header would keep showing
 * stale values until the next login.
 */
export interface UpdateUserResult {
  user: UserListItem
  accessToken?: string
  expiresAt?: string
}

export interface ResetPasswordInput {
  currentPassword?: string
  newPassword: string
  confirmNewPassword: string
}
