import { useMutation, useQueryClient } from '@tanstack/react-query'
import { ArrowLeft } from 'lucide-react'
import { useState } from 'react'
import type { FormEvent } from 'react'
import { Link, useNavigate } from 'react-router'

import { ApiError } from '@/api/apiClient'
import { createOrganization } from '@/api/organizations'
import type { CreateOrganizationInput } from '@/api/types'
import { useAuth } from '@/auth/AuthProvider'
import { FieldError, fieldControlProps } from '@/components/form-field'
import { OrganizationProfileFields } from '@/components/organization-detail-panel'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Label } from '@/components/ui/label'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import {
  type FieldErrors,
  clearFieldError,
  hasFieldErrors,
} from '@/lib/form-validation'
import {
  draftToLocationInputs,
  emptyOrganizationDraft,
  validateOrganizationDraft,
} from '@/lib/organization-validation'

/**
 * Creating an organization is Admin-only, mirroring the same gate on <c>Organization.Create</c>
 * - a non-Admin who reaches this page by URL still gets a form, but submitting it surfaces the
 * API's 403 rather than the page pretending the action doesn't exist.
 */
export function NewOrganizationPage() {
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const { session } = useAuth()
  const [kind, setKind] = useState<CreateOrganizationInput['kind'] | ''>('')
  const [draft, setDraft] = useState(emptyOrganizationDraft())
  const [error, setError] = useState<string | null>(null)
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({})

  const mutation = useMutation({
    mutationFn: () =>
      createOrganization({
        name: draft.name.trim(),
        kind: kind as CreateOrganizationInput['kind'],
        primaryAddress: draft.primaryAddress.trim() === '' ? undefined : draft.primaryAddress.trim(),
        primaryContactName: draft.primaryContactName.trim() === '' ? undefined : draft.primaryContactName.trim(),
        primaryContactEmail: draft.primaryContactEmail.trim() === '' ? undefined : draft.primaryContactEmail.trim(),
        primaryContactPhone: draft.primaryContactPhone.trim() === '' ? undefined : draft.primaryContactPhone.trim(),
        isPreferredVendor: draft.isPreferredVendor,
        locations: draftToLocationInputs(draft),
      }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['organizations'] })
      navigate('/organizations')
    },
    onError: (err) => setError(err instanceof ApiError ? err.message : 'Something went wrong.'),
  })

  function validateForm(): FieldErrors {
    const next = validateOrganizationDraft(draft, kind)

    if (kind === '') {
      next.kind = 'Kind is required.'
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

  function handleKindChange(value: string) {
    const nextKind = value as CreateOrganizationInput['kind']
    setKind(nextKind)
    setFieldErrors((current) => clearFieldError(current, 'kind'))

    // Mirrors the domain rule that only vendors can be preferred - switching away from Vendor
    // drops a checked flag rather than leaving it set for a kind that would reject it on submit.
    if (nextKind !== 'Vendor' && draft.isPreferredVendor) {
      setDraft((current) => ({ ...current, isPreferredVendor: false }))
    }
  }

  const isAdmin = session?.user.roles.includes('Admin') ?? false

  return (
    <div className="flex flex-col gap-6">
      <Link to="/organizations" className="flex items-center gap-1.5 text-sm text-muted-foreground hover:text-foreground">
        <ArrowLeft className="size-4" />
        Back to organizations
      </Link>

      <Card className="max-w-2xl">
        <CardHeader>
          <CardTitle>New organization</CardTitle>
          <CardDescription>Add a client or vendor organization to the directory.</CardDescription>
        </CardHeader>
        <CardContent>
          {!isAdmin && (
            <div
              role="alert"
              className="mb-4 rounded-md border border-warning/30 bg-warning/10 px-3 py-2 text-sm text-warning"
            >
              Your account is not able to create organizations - only an Admin can. Submitting will be refused.
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
            <div className="flex flex-col gap-2">
              <Label htmlFor="kind">Kind</Label>
              {fieldErrors.kind && <FieldError id="kind-error">{fieldErrors.kind}</FieldError>}
              <Select value={kind} onValueChange={handleKindChange}>
                <SelectTrigger className="w-full" {...fieldControlProps('kind', fieldErrors.kind)}>
                  <SelectValue placeholder="Select a kind" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="Client">Client</SelectItem>
                  <SelectItem value="Vendor">Vendor</SelectItem>
                </SelectContent>
              </Select>
            </div>

            <OrganizationProfileFields
              idPrefix="new"
              draft={draft}
              fieldErrors={fieldErrors}
              isVendor={kind === 'Vendor'}
              onDraftChange={setDraft}
              onClearFieldError={(field) => setFieldErrors((current) => clearFieldError(current, field))}
            />

            <Button type="submit" disabled={mutation.isPending} className="mt-2 self-start">
              {mutation.isPending ? 'Creating…' : 'Create organization'}
            </Button>
          </form>
        </CardContent>
      </Card>
    </div>
  )
}
