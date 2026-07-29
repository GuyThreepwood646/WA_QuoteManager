import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import type { FormEvent } from 'react'

import { ApiError } from '@/api/apiClient'
import { createQuote } from '@/api/requests'
import { useAuth } from '@/auth/AuthProvider'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'

/**
 * FR-1's vendor side, shown only when the server says <c>canAddQuote</c> (AD-7's request-level
 * counterpart to a quote's <c>permittedActions</c>) - the vendor organisation is the caller's own,
 * never a field on this form, so there is nothing here for a Vendor to get wrong about whose
 * organisation the draft belongs to.
 */
export function AddQuoteForm({ requestId }: { requestId: string }) {
  const { session } = useAuth()
  const queryClient = useQueryClient()
  const [amount, setAmount] = useState('')
  const [currency, setCurrency] = useState('USD')
  const [expiresAt, setExpiresAt] = useState('')
  const [notes, setNotes] = useState('')
  const [error, setError] = useState<string | null>(null)

  const mutation = useMutation({
    mutationFn: () =>
      createQuote(requestId, {
        vendorOrganizationId: session!.user.organizationId!,
        amount: Number(amount),
        currency,
        expiresAt: expiresAt === '' ? undefined : new Date(expiresAt).toISOString(),
        notes: notes.trim() === '' ? undefined : notes,
      }),
    onSuccess: () => {
      setError(null)
      setAmount('')
      setNotes('')
      setExpiresAt('')
      void queryClient.invalidateQueries({ queryKey: ['requests', requestId] })
      void queryClient.invalidateQueries({ queryKey: ['dashboard'] })
    },
    onError: (err) => setError(err instanceof ApiError ? err.message : 'Something went wrong.'),
  })

  function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setError(null)
    mutation.mutate()
  }

  const canSubmit = amount !== '' && Number(amount) > 0 && currency.trim().length === 3

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-sm">Draft a quote</CardTitle>
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
        <form onSubmit={handleSubmit} className="flex flex-col gap-3">
          <div className="grid grid-cols-2 gap-3">
            <div className="flex flex-col gap-2">
              <Label htmlFor="amount">Amount</Label>
              <Input
                id="amount"
                type="number"
                min={0.01}
                step="0.01"
                value={amount}
                onChange={(event) => setAmount(event.currentTarget.value)}
                required
                autoFocus
              />
            </div>
            <div className="flex flex-col gap-2">
              <Label htmlFor="currency">Currency</Label>
              <Input
                id="currency"
                value={currency}
                onChange={(event) => setCurrency(event.currentTarget.value.toUpperCase())}
                maxLength={3}
                required
              />
            </div>
          </div>

          <div className="flex flex-col gap-2">
            <Label htmlFor="expiresAt">Expires</Label>
            <Input
              id="expiresAt"
              type="date"
              value={expiresAt}
              onChange={(event) => setExpiresAt(event.currentTarget.value)}
            />
          </div>

          <div className="flex flex-col gap-2">
            <Label htmlFor="notes">Notes</Label>
            <Input
              id="notes"
              value={notes}
              onChange={(event) => setNotes(event.currentTarget.value)}
              maxLength={2000}
            />
          </div>

          <Button type="submit" size="sm" disabled={!canSubmit || mutation.isPending} className="mt-1 self-start">
            {mutation.isPending ? 'Submitting…' : 'Save draft'}
          </Button>
        </form>
      </CardContent>
    </Card>
  )
}
