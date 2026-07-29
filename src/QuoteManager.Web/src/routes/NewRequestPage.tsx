import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ArrowLeft } from 'lucide-react'
import { useState } from 'react'
import type { FormEvent } from 'react'
import { Link, useNavigate } from 'react-router'

import { ApiError } from '@/api/apiClient'
import { listOrganizations } from '@/api/organizations'
import { createRequest } from '@/api/requests'
import { useAuth } from '@/auth/AuthProvider'
import { FieldError, FormField, fieldControlProps, textareaClassName } from '@/components/form-field'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import {
  type FieldErrors,
  clearFieldError,
  hasFieldErrors,
  validateRequired,
} from '@/lib/form-validation'
import { cn } from '@/lib/utils'

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
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({})

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

  function validateForm(): FieldErrors {
    const next: FieldErrors = {}

    const titleError = validateRequired(title, 'Title')
    if (titleError) {
      next.title = titleError
    }

    const clientError = validateRequired(clientOrganizationId, 'Client organization')
    if (clientError) {
      next.clientOrganizationId = clientError
    }

    return next
  }

  function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setError(null)

    const next = validateForm()
    setFieldErrors(next)
    if (hasFieldErrors(next)) {
      return
    }

    mutation.mutate()
  }

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

          <form onSubmit={handleSubmit} noValidate className="flex flex-col gap-4">
            <FormField id="title" label="Title" error={fieldErrors.title}>
              <Input
                value={title}
                onChange={(event) => {
                  setTitle(event.currentTarget.value)
                  setFieldErrors((current) => clearFieldError(current, 'title'))
                }}
                autoFocus
                maxLength={200}
              />
            </FormField>

            <div className="flex flex-col gap-2">
              <Label htmlFor="clientOrganization">Client organization</Label>
              {fieldErrors.clientOrganizationId && (
                <FieldError id="clientOrganization-error">{fieldErrors.clientOrganizationId}</FieldError>
              )}
              <Select
                value={clientOrganizationId}
                onValueChange={(value) => {
                  setClientOrganizationId(value)
                  setFieldErrors((current) => clearFieldError(current, 'clientOrganizationId'))
                }}
                disabled={isPending}
              >
                <SelectTrigger
                  className="w-full"
                  {...fieldControlProps('clientOrganization', fieldErrors.clientOrganizationId)}
                >
                  <SelectValue placeholder={isPending ? 'Loading…' : 'Select a client organization'} />
                </SelectTrigger>
                <SelectContent>
                  {clientOrganizations.map((org) => (
                    <SelectItem key={org.id} value={org.id}>
                      {org.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div className="flex flex-col gap-2">
              <Label htmlFor="description">Description</Label>
              <textarea
                id="description"
                className={cn(textareaClassName)}
                value={description}
                onChange={(event) => setDescription(event.currentTarget.value)}
                maxLength={2000}
                rows={3}
              />
            </div>

            <FormField id="neededBy" label="Needed by">
              <Input
                type="date"
                value={neededBy}
                onChange={(event) => setNeededBy(event.currentTarget.value)}
              />
            </FormField>

            <Button type="submit" disabled={mutation.isPending} className="mt-2 self-start">
              {mutation.isPending ? 'Creating…' : 'Create request'}
            </Button>
          </form>
        </CardContent>
      </Card>
    </div>
  )
}
