import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ArrowLeft } from 'lucide-react'
import { useState } from 'react'
import type { FormEvent } from 'react'
import { Link, useParams } from 'react-router'

import { ApiError } from '@/api/apiClient'
import { listOrganizations } from '@/api/organizations'
import { cancelRequest, getRequest, inviteVendor, updateRequest } from '@/api/requests'
import { ActivityTimeline } from '@/components/activity-timeline'
import { AddQuoteForm } from '@/components/add-quote-form'
import { QuoteCard } from '@/components/quote-card'
import { StatusBadge } from '@/components/status-badge'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { Skeleton } from '@/components/ui/skeleton'
import { formatDate } from '@/lib/format'

export function RequestDetailPage() {
  const { requestId } = useParams<{ requestId: string }>()
  const queryClient = useQueryClient()
  const [isEditing, setIsEditing] = useState(false)
  const [title, setTitle] = useState('')
  const [description, setDescription] = useState('')
  const [neededBy, setNeededBy] = useState('')
  const [vendorToInvite, setVendorToInvite] = useState('')
  const [error, setError] = useState<string | null>(null)

  const { data, isPending, isError } = useQuery({
    queryKey: ['requests', requestId],
    queryFn: () => getRequest(requestId!),
    enabled: Boolean(requestId),
  })

  const { data: organizations } = useQuery({
    queryKey: ['organizations'],
    queryFn: () => listOrganizations(),
    enabled: Boolean(data?.canInviteVendor),
  })

  const updateMutation = useMutation({
    mutationFn: () =>
      updateRequest(requestId!, {
        title,
        description: description.trim() === '' ? undefined : description,
        neededBy: neededBy === '' ? undefined : new Date(neededBy).toISOString(),
      }),
    onSuccess: () => {
      setError(null)
      setIsEditing(false)
      void queryClient.invalidateQueries({ queryKey: ['requests', requestId] })
      void queryClient.invalidateQueries({ queryKey: ['requests'] })
    },
    onError: (err) => setError(err instanceof ApiError ? err.message : 'Something went wrong.'),
  })

  const cancelMutation = useMutation({
    mutationFn: () => cancelRequest(requestId!),
    onSuccess: () => {
      setError(null)
      void queryClient.invalidateQueries({ queryKey: ['requests', requestId] })
      void queryClient.invalidateQueries({ queryKey: ['requests'] })
      void queryClient.invalidateQueries({ queryKey: ['dashboard'] })
    },
    onError: (err) => setError(err instanceof ApiError ? err.message : 'Something went wrong.'),
  })

  const inviteMutation = useMutation({
    mutationFn: () => inviteVendor(requestId!, vendorToInvite),
    onSuccess: () => {
      setError(null)
      setVendorToInvite('')
      void queryClient.invalidateQueries({ queryKey: ['requests', requestId] })
      void queryClient.invalidateQueries({ queryKey: ['dashboard'] })
    },
    onError: (err) => setError(err instanceof ApiError ? err.message : 'Something went wrong.'),
  })

  if (isPending) {
    return <Skeleton className="h-64 rounded-lg" />
  }

  if (isError || !data) {
    return <p className="text-sm text-destructive">Could not load this request.</p>
  }

  const silentInvitations = data.invitations.filter((invitation) => !invitation.hasQuoted)
  const invitedVendorIds = new Set(data.invitations.map((invitation) => invitation.vendorOrganizationId))
  const availableVendors = organizations?.items.filter(
    (org) => org.kind === 'Vendor' && !invitedVendorIds.has(org.id),
  ) ?? []

  function startEditing() {
    setError(null)
    setIsEditing(true)
    setTitle(data!.title)
    setDescription(data!.description ?? '')
    setNeededBy(data!.neededBy?.slice(0, 10) ?? '')
  }

  function handleUpdateSubmit(event: FormEvent) {
    event.preventDefault()
    setError(null)
    updateMutation.mutate()
  }

  return (
    <div className="flex flex-col gap-6">
      <Link to="/requests" className="flex items-center gap-1.5 text-sm text-muted-foreground hover:text-foreground">
        <ArrowLeft className="size-4" />
        Back to requests
      </Link>

      {error && (
        <div role="alert" className="rounded-md border border-destructive/30 bg-destructive/10 px-3 py-2 text-sm text-destructive">
          {error}
        </div>
      )}

      {isEditing ? (
        <form onSubmit={handleUpdateSubmit} className="flex max-w-xl flex-col gap-4">
          <div className="flex flex-col gap-2">
            <Label htmlFor="title">Title</Label>
            <Input id="title" value={title} onChange={(event) => setTitle(event.currentTarget.value)} required maxLength={200} />
          </div>
          <div className="flex flex-col gap-2">
            <Label htmlFor="description">Description</Label>
            <Input id="description" value={description} onChange={(event) => setDescription(event.currentTarget.value)} maxLength={2000} />
          </div>
          <div className="flex flex-col gap-2">
            <Label htmlFor="neededBy">Needed by</Label>
            <Input id="neededBy" type="date" value={neededBy} onChange={(event) => setNeededBy(event.currentTarget.value)} />
          </div>
          <div className="flex gap-2">
            <Button type="submit" size="sm" disabled={title.trim() === '' || updateMutation.isPending}>
              {updateMutation.isPending ? 'Saving…' : 'Save'}
            </Button>
            <Button type="button" size="sm" variant="outline" onClick={() => setIsEditing(false)}>
              Cancel
            </Button>
          </div>
        </form>
      ) : (
        <div className="flex items-start justify-between gap-4">
          <div>
            <h1 className="text-lg font-semibold">{data.title}</h1>
            <p className="text-sm text-muted-foreground">
              {data.clientOrganizationName}
              {data.neededBy && <> · needed by {formatDate(data.neededBy)}</>}
            </p>
          </div>
          <div className="flex items-center gap-2">
            <StatusBadge status={data.status} />
            {data.canEdit && (
              <Button size="sm" variant="outline" onClick={startEditing}>
                Edit
              </Button>
            )}
            {data.canCancel && (
              <Button
                size="sm"
                variant="destructive"
                disabled={cancelMutation.isPending}
                onClick={() => {
                  if (window.confirm('Cancel this request? This cannot be undone.')) {
                    cancelMutation.mutate()
                  }
                }}
              >
                Cancel request
              </Button>
            )}
          </div>
        </div>
      )}

      {!isEditing && data.description && <p className="max-w-2xl text-sm text-muted-foreground">{data.description}</p>}

      <div className="flex flex-col gap-3">
        <h2 className="text-sm font-semibold">Quotes</h2>
        {data.quotes.length === 0 ? (
          <p className="text-sm text-muted-foreground">No quotes yet.</p>
        ) : (
          <div className="grid gap-3 md:grid-cols-2">
            {data.quotes.map((quote) => (
              <QuoteCard key={quote.id} requestId={data.id} quote={quote} />
            ))}
          </div>
        )}
      </div>

      {silentInvitations.length > 0 && (
        <div className="flex flex-col gap-2">
          <h2 className="text-sm font-semibold">Invited, no quote yet</h2>
          <p className="text-sm text-muted-foreground">
            {silentInvitations.map((invitation) => invitation.vendorOrganizationName).join(', ')}
          </p>
        </div>
      )}

      {data.canInviteVendor && (
        <div className="flex flex-col gap-2">
          <h2 className="text-sm font-semibold">Invite a vendor</h2>
          <div className="flex max-w-md gap-2">
            <Select value={vendorToInvite} onValueChange={setVendorToInvite}>
              <SelectTrigger className="w-full">
                <SelectValue placeholder={availableVendors.length === 0 ? 'No more vendors to invite' : 'Select a vendor'} />
              </SelectTrigger>
              <SelectContent>
                {availableVendors.map((org) => (
                  <SelectItem key={org.id} value={org.id}>
                    {org.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            <Button
              size="sm"
              disabled={vendorToInvite === '' || inviteMutation.isPending}
              onClick={() => inviteMutation.mutate()}
            >
              {inviteMutation.isPending ? 'Inviting…' : 'Invite'}
            </Button>
          </div>
        </div>
      )}

      {data.canAddQuote && <AddQuoteForm requestId={data.id} />}

      <div className="flex flex-col gap-3">
        <h2 className="text-sm font-semibold">Activity</h2>
        <ActivityTimeline requestId={data.id} />
      </div>
    </div>
  )
}
