import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import type { FormEvent } from 'react'

import { ApiError } from '@/api/apiClient'
import { listOrganizations } from '@/api/organizations'
import { createQuote } from '@/api/requests'
import { useAuth } from '@/auth/AuthProvider'
import { FieldError, FormField, FormFieldRow, fieldControlProps } from '@/components/form-field'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import {
  type FieldErrors,
  clearFieldError,
  hasFieldErrors,
  validateCurrencyCode,
  validatePositiveAmount,
  validateRequired,
} from '@/lib/form-validation'

/**
 * Shown when the server says <c>canAddQuote</c>. Vendors draft for their own organization;
 * Admins pick the vendor they are drafting on behalf of.
 */
export function AddQuoteForm({
  requestId,
  quotedVendorIds,
}: {
  requestId: string
  quotedVendorIds: string[]
}) {
  const { session } = useAuth()
  const queryClient = useQueryClient()
  const isAdmin = session?.user.roles.includes('Admin') ?? false

  const [vendorOrganizationId, setVendorOrganizationId] = useState(
    isAdmin ? '' : (session?.user.organizationId ?? ''),
  )
  const [amount, setAmount] = useState('')
  const [currency, setCurrency] = useState('USD')
  const [expiresAt, setExpiresAt] = useState('')
  const [notes, setNotes] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({})

  const { data: organizations } = useQuery({
    queryKey: ['organizations'],
    queryFn: () => listOrganizations(),
    enabled: isAdmin,
  })

  const quoted = new Set(quotedVendorIds)
  const availableVendors =
    organizations?.items.filter((org) => org.kind === 'Vendor' && !quoted.has(org.id)) ?? []

  const mutation = useMutation({
    mutationFn: () =>
      createQuote(requestId, {
        vendorOrganizationId,
        amount: Number(amount),
        currency,
        expiresAt: expiresAt === '' ? undefined : new Date(expiresAt).toISOString(),
        notes: notes.trim() === '' ? undefined : notes,
      }),
    onSuccess: () => {
      setError(null)
      setFieldErrors({})
      setAmount('')
      setNotes('')
      setExpiresAt('')
      if (isAdmin) {
        setVendorOrganizationId('')
      }
      void queryClient.invalidateQueries({ queryKey: ['requests', requestId] })
      void queryClient.invalidateQueries({ queryKey: ['dashboard'] })
    },
    onError: (err) => setError(err instanceof ApiError ? err.message : 'Something went wrong.'),
  })

  function validateForm(): FieldErrors {
    const next: FieldErrors = {}

    if (isAdmin) {
      const vendorError = validateRequired(vendorOrganizationId, 'Vendor')
      if (vendorError) {
        next.vendorOrganizationId = vendorError
      }
    }

    const amountError = validatePositiveAmount(amount)
    if (amountError) {
      next.amount = amountError
    }

    const currencyError = validateCurrencyCode(currency)
    if (currencyError) {
      next.currency = currencyError
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

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-sm">{isAdmin ? 'Draft a quote on behalf of a vendor' : 'Draft a quote'}</CardTitle>
      </CardHeader>
      <CardContent>
        {error && (
          <div
            role="alert"
            className="mb-4 rounded-md border border-destructive/30 bg-destructive/10 px-3 py-2 text-sm text-destructive"
          >
            {error}
          </div>
        )}
        <form onSubmit={handleSubmit} noValidate className="flex flex-col gap-3">
          {isAdmin && (
            <div className="flex flex-col gap-2">
              <Label htmlFor="vendorOrganizationId">Vendor</Label>
              {fieldErrors.vendorOrganizationId && (
                <FieldError id="vendorOrganizationId-error">{fieldErrors.vendorOrganizationId}</FieldError>
              )}
              <Select
                value={vendorOrganizationId}
                onValueChange={(value) => {
                  setVendorOrganizationId(value)
                  setFieldErrors((current) => clearFieldError(current, 'vendorOrganizationId'))
                }}
              >
                <SelectTrigger
                  className="w-full"
                  {...fieldControlProps('vendorOrganizationId', fieldErrors.vendorOrganizationId)}
                >
                  <SelectValue
                    placeholder={
                      availableVendors.length === 0
                        ? 'No vendors left to quote for'
                        : 'Select a vendor'
                    }
                  />
                </SelectTrigger>
                <SelectContent>
                  {availableVendors.map((org) => (
                    <SelectItem key={org.id} value={org.id}>
                      {org.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          )}

          <FormFieldRow
            fields={[
              {
                id: 'amount',
                label: 'Amount',
                error: fieldErrors.amount,
                children: (
                  <Input
                    type="number"
                    min={0.01}
                    step="0.01"
                    value={amount}
                    onChange={(event) => {
                      setAmount(event.currentTarget.value)
                      setFieldErrors((current) => clearFieldError(current, 'amount'))
                    }}
                    autoFocus={!isAdmin}
                  />
                ),
              },
              {
                id: 'currency',
                label: 'Currency',
                error: fieldErrors.currency,
                children: (
                  <Input
                    value={currency}
                    onChange={(event) => {
                      setCurrency(event.currentTarget.value.toUpperCase())
                      setFieldErrors((current) => clearFieldError(current, 'currency'))
                    }}
                    maxLength={3}
                  />
                ),
              },
            ]}
          />

          <FormField id="expiresAt" label="Expires">
            <Input
              type="date"
              value={expiresAt}
              onChange={(event) => setExpiresAt(event.currentTarget.value)}
            />
          </FormField>

          <FormField id="notes" label="Notes">
            <Input
              value={notes}
              onChange={(event) => setNotes(event.currentTarget.value)}
              maxLength={2000}
            />
          </FormField>

          <Button type="submit" size="sm" disabled={mutation.isPending} className="mt-1 self-start">
            {mutation.isPending ? 'Submitting…' : 'Save draft'}
          </Button>
        </form>
      </CardContent>
    </Card>
  )
}
