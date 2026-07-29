import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ArrowLeft } from 'lucide-react'
import { useState } from 'react'
import type { FormEvent } from 'react'
import { Link, useNavigate } from 'react-router'

import { ApiError } from '@/api/apiClient'
import { listOrganizations } from '@/api/organizations'
import { createRequest } from '@/api/requests'
import { useAuth } from '@/auth/AuthProvider'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'

const selectClassName =
  'h-9 w-full min-w-0 rounded-md border border-input bg-transparent px-3 py-1 text-base shadow-xs outline-none focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/50 disabled:pointer-events-none disabled:cursor-not-allowed disabled:opacity-50 md:text-sm dark:bg-input/30'

/**
 * Raising a request is a Requester/Admin action, mirroring the same gate on <c>Request.Create</c>
 * - a Vendor who reaches this page by URL still gets a form, but submitting it surfaces the API's
 * 403 rather than the page pretending the action doesn't exist, which would only hide the same
 * rule one layer up.
 */
export function NewRequestPage() {
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const { session } = useAuth()
  const [title, setTitle] = useState('')
  const [description, setDescription] = useState('')
  const [clientOrganizationId, setClientOrganizationId] = useState('')
  const [neededBy, setNeededBy] = useState('')
  const [error, setError] = useState<string | null>(null)

  const { data: organizations, isPending } = useQuery({
    queryKey: ['organizations'],
    queryFn: () => listOrganizations(),
  })

  const clientOrganizations = organizations?.items.filter((org) => org.kind === 'Client') ?? []

  const mutation = useMutation({
    mutationFn: () =>
      createRequest({
        title,
        description: description.trim() === '' ? undefined : description,
        clientOrganizationId,
        neededBy: neededBy === '' ? undefined : new Date(neededBy).toISOString(),
      }),
    onSuccess: (created) => {
      void queryClient.invalidateQueries({ queryKey: ['requests'] })
      navigate(`/requests/${created.id}`)
    },
    onError: (err) => setError(err instanceof ApiError ? err.message : 'Something went wrong.'),
  })

  function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setError(null)
    mutation.mutate()
  }

  const canSubmit = title.trim() !== '' && clientOrganizationId !== ''
  const isRequesterOrAdmin = session?.user.roles.some((role) => role === 'Requester' || role === 'Admin') ?? false

  return (
    <div className="flex flex-col gap-6">
      <Link to="/requests" className="flex items-center gap-1.5 text-sm text-muted-foreground hover:text-foreground">
        <ArrowLeft className="size-4" />
        Back to requests
      </Link>

      <Card className="max-w-xl">
        <CardHeader>
          <CardTitle>New request</CardTitle>
          <CardDescription>Raise a request for storage, packing, or transportation quotes.</CardDescription>
        </CardHeader>
        <CardContent>
          {!isRequesterOrAdmin && (
            <div
              role="alert"
              className="mb-4 rounded-md border border-warning/30 bg-warning/10 px-3 py-2 text-sm text-warning"
            >
              Your account is not able to raise requests - only a Requester or Admin can. Submitting will be refused.
            </div>
          )}

          {error && (
            <div
              role="alert"
              className="mb-4 rounded-md border border-destructive/30 bg-destructive/10 px-3 py-2 text-sm text-destructive"
            >
              {error}
            </div>
          )}

          <form onSubmit={handleSubmit} className="flex flex-col gap-4">
            <div className="flex flex-col gap-2">
              <Label htmlFor="title">Title</Label>
              <Input
                id="title"
                value={title}
                onChange={(event) => setTitle(event.currentTarget.value)}
                required
                autoFocus
                maxLength={200}
              />
            </div>

            <div className="flex flex-col gap-2">
              <Label htmlFor="clientOrganization">Client organisation</Label>
              <select
                id="clientOrganization"
                className={selectClassName}
                value={clientOrganizationId}
                onChange={(event) => setClientOrganizationId(event.currentTarget.value)}
                disabled={isPending}
                required
              >
                <option value="" disabled>
                  {isPending ? 'Loading…' : 'Select a client organisation'}
                </option>
                {clientOrganizations.map((org) => (
                  <option key={org.id} value={org.id}>
                    {org.name}
                  </option>
                ))}
              </select>
            </div>

            <div className="flex flex-col gap-2">
              <Label htmlFor="description">Description</Label>
              <textarea
                id="description"
                className={selectClassName.replace('h-9', 'min-h-20 py-2')}
                value={description}
                onChange={(event) => setDescription(event.currentTarget.value)}
                maxLength={2000}
                rows={3}
              />
            </div>

            <div className="flex flex-col gap-2">
              <Label htmlFor="neededBy">Needed by</Label>
              <Input
                id="neededBy"
                type="date"
                value={neededBy}
                onChange={(event) => setNeededBy(event.currentTarget.value)}
              />
            </div>

            <Button type="submit" disabled={!canSubmit || mutation.isPending} className="mt-2 self-start">
              {mutation.isPending ? 'Creating…' : 'Create request'}
            </Button>
          </form>
        </CardContent>
      </Card>
    </div>
  )
}
