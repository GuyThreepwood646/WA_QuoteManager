import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Plus } from 'lucide-react'
import { Fragment, useState } from 'react'
import { Link } from 'react-router'

import { ApiError } from '@/api/apiClient'
import { listOrganizations, retireOrganization, updateOrganization } from '@/api/organizations'
import type { OrganizationListItem } from '@/api/types'
import { useAuth } from '@/auth/AuthProvider'
import {
  OrganizationContactSummary,
  OrganizationDetailPanel,
  PreferredVendorMark,
} from '@/components/organization-detail-panel'
import type { OrganizationDraft } from '@/lib/organization-validation'
import {
  draftToLocationInputs,
  organizationToDraft,
  validateOrganizationDraft,
} from '@/lib/organization-validation'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { type FieldErrors, clearFieldError } from '@/lib/form-validation'

export function OrganizationsPage() {
  const queryClient = useQueryClient()
  const { session } = useAuth()
  const isAdmin = session?.user.roles.includes('Admin') ?? false
  const [expandedId, setExpandedId] = useState<string | null>(null)
  const [editingId, setEditingId] = useState<string | null>(null)
  const [drafts, setDrafts] = useState<Record<string, OrganizationDraft>>({})
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({})
  const [error, setError] = useState<string | null>(null)

  const { data, isPending, isError } = useQuery({
    queryKey: ['organizations', { includeRetired: isAdmin }],
    queryFn: () => listOrganizations(100, isAdmin),
  })

  const updateMutation = useMutation({
    mutationFn: ({ organizationId, draft }: { organizationId: string; draft: OrganizationDraft }) =>
      updateOrganization(organizationId, {
        name: draft.name.trim(),
        primaryAddress: draft.primaryAddress.trim() === '' ? undefined : draft.primaryAddress.trim(),
        primaryContactName: draft.primaryContactName.trim() === '' ? undefined : draft.primaryContactName.trim(),
        primaryContactEmail: draft.primaryContactEmail.trim() === '' ? undefined : draft.primaryContactEmail.trim(),
        primaryContactPhone: draft.primaryContactPhone.trim() === '' ? undefined : draft.primaryContactPhone.trim(),
        isPreferredVendor: draft.isPreferredVendor,
        locations: draftToLocationInputs(draft),
      }),
    onSuccess: () => {
      setError(null)
      setEditingId(null)
      setFieldErrors({})
      void queryClient.invalidateQueries({ queryKey: ['organizations'] })
    },
    onError: (err) => setError(err instanceof ApiError ? err.message : 'Something went wrong.'),
  })

  const retireMutation = useMutation({
    mutationFn: (organizationId: string) => retireOrganization(organizationId),
    onSuccess: () => {
      setError(null)
      setExpandedId(null)
      setEditingId(null)
      void queryClient.invalidateQueries({ queryKey: ['organizations'] })
    },
    onError: (err) => setError(err instanceof ApiError ? err.message : 'Something went wrong.'),
  })

  if (isPending) {
    return <Skeleton className="h-64 rounded-lg" />
  }

  if (isError || !data) {
    return <p className="text-sm text-destructive">Could not load organizations.</p>
  }

  const columnCount = 5

  function toggleDetails(org: OrganizationListItem) {
    setError(null)
    setFieldErrors({})

    if (expandedId === org.id) {
      setExpandedId(null)
      setEditingId(null)
      return
    }

    setExpandedId(org.id)
    setEditingId(null)
    setDrafts((current) => ({ ...current, [org.id]: organizationToDraft(org) }))
  }

  function startEditing(org: OrganizationListItem) {
    setEditingId(org.id)
    setFieldErrors({})
    setDrafts((current) => ({ ...current, [org.id]: organizationToDraft(org) }))
  }

  function discardEditing(org: OrganizationListItem) {
    setEditingId(null)
    setFieldErrors({})
    setDrafts((current) => ({ ...current, [org.id]: organizationToDraft(org) }))
  }

  function saveDraft(org: OrganizationListItem) {
    const draft = drafts[org.id] ?? organizationToDraft(org)
    const nextErrors = validateOrganizationDraft(draft, org.kind)
    setFieldErrors(nextErrors)
    if (Object.keys(nextErrors).length > 0) {
      return
    }

    updateMutation.mutate({ organizationId: org.id, draft })
  }

  return (
    <div className="flex flex-col gap-4">
      {isAdmin && (
        <div className="flex justify-end">
          <Button asChild size="sm">
            <Link to="/organizations/new">
              <Plus className="size-4" />
              New organization
            </Link>
          </Button>
        </div>
      )}

      {error && (
        <div role="alert" className="rounded-md border border-destructive/30 bg-destructive/10 px-3 py-2 text-sm text-destructive">
          {error}
        </div>
      )}

      <div className="rounded-lg border border-border bg-card">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Name</TableHead>
              <TableHead>Primary contact</TableHead>
              <TableHead>Preferred</TableHead>
              <TableHead>Kind</TableHead>
              <TableHead className="text-right">Actions</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {data.items.map((org) => {
              const isExpanded = expandedId === org.id
              const isEditing = editingId === org.id
              const draft = drafts[org.id] ?? organizationToDraft(org)

              return (
                <Fragment key={org.id}>
                  <TableRow>
                    <TableCell className="font-medium">{org.name}</TableCell>
                    <TableCell>
                      <OrganizationContactSummary org={org} />
                    </TableCell>
                    <TableCell>
                      {org.kind === 'Vendor' ? (
                        <PreferredVendorMark isPreferred={org.isPreferredVendor} />
                      ) : (
                        <span className="text-sm text-muted-foreground">—</span>
                      )}
                    </TableCell>
                    <TableCell>
                      <div className="flex items-center gap-2">
                        <Badge variant="secondary">{org.kind}</Badge>
                        {org.retiredAt && <Badge variant="outline">Retired</Badge>}
                      </div>
                    </TableCell>
                    <TableCell className="text-right">
                      <div className="flex justify-end gap-2">
                        <Button size="sm" variant="outline" onClick={() => toggleDetails(org)}>
                          {isExpanded ? 'Hide' : 'Details'}
                        </Button>
                        {isAdmin && !org.retiredAt && (
                          <Button
                            size="sm"
                            variant="outline"
                            disabled={retireMutation.isPending}
                            onClick={() => {
                              if (window.confirm(`Retire "${org.name}"? It will no longer be offered for new requests or invitations.`)) {
                                retireMutation.mutate(org.id)
                              }
                            }}
                          >
                            Retire
                          </Button>
                        )}
                      </div>
                    </TableCell>
                  </TableRow>
                  {isExpanded && (
                    <TableRow>
                      <TableCell colSpan={columnCount} className="bg-muted/10 p-4">
                        <OrganizationDetailPanel
                          org={org}
                          isEditing={isEditing}
                          draft={draft}
                          fieldErrors={fieldErrors}
                          isSaving={updateMutation.isPending}
                          isAdmin={isAdmin}
                          onDraftChange={(next) => setDrafts((current) => ({ ...current, [org.id]: next }))}
                          onClearFieldError={(field) => setFieldErrors((current) => clearFieldError(current, field))}
                          onEdit={() => startEditing(org)}
                          onDiscard={() => discardEditing(org)}
                          onSave={() => saveDraft(org)}
                        />
                      </TableCell>
                    </TableRow>
                  )}
                </Fragment>
              )
            })}
          </TableBody>
        </Table>
      </div>
    </div>
  )
}
