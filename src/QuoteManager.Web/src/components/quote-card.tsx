import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import type { FormEvent } from 'react'

import { applyQuoteAction, editQuote } from '@/api/requests'
import type { RequestQuoteItem } from '@/api/types'
import { ApiError } from '@/api/apiClient'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { formatDate, formatMoney } from '@/lib/format'

import { StatusBadge } from './status-badge'

interface ActionStyle {
  label: string
  variant: 'default' | 'destructive' | 'outline' | 'secondary'
}

// "Edit" travels in permittedActions but is a field-mutability signal, not a status transition -
// it's rendered as its own toggle below rather than alongside these status-transition buttons.
const actionStyles: Record<string, ActionStyle> = {
  Submit: { label: 'Submit', variant: 'default' },
  StartReview: { label: 'Start review', variant: 'default' },
  Accept: { label: 'Accept', variant: 'default' },
  Reject: { label: 'Reject', variant: 'destructive' },
  ReturnToSubmitted: { label: 'Return to submitted', variant: 'outline' },
  Withdraw: { label: 'Withdraw', variant: 'outline' },
  Expire: { label: 'Mark expired', variant: 'outline' },
}

export function QuoteCard({ requestId, quote }: { requestId: string; quote: RequestQuoteItem }) {
  const queryClient = useQueryClient()
  const [error, setError] = useState<string | null>(null)
  const [isEditing, setIsEditing] = useState(false)
  const [amount, setAmount] = useState(String(quote.amount))
  const [currency, setCurrency] = useState(quote.currency)
  const [expiresAt, setExpiresAt] = useState(quote.expiresAt?.slice(0, 10) ?? '')
  const [notes, setNotes] = useState(quote.notes ?? '')

  const actionMutation = useMutation({
    mutationFn: (action: string) => applyQuoteAction(requestId, quote, action),
    onSuccess: () => {
      setError(null)
      void queryClient.invalidateQueries({ queryKey: ['requests', requestId] })
      void queryClient.invalidateQueries({ queryKey: ['dashboard'] })
    },
    onError: (err) => setError(err instanceof ApiError ? err.message : 'Something went wrong.'),
  })

  const editMutation = useMutation({
    mutationFn: () =>
      editQuote(requestId, quote, {
        amount: Number(amount),
        currency,
        expiresAt: expiresAt === '' ? undefined : new Date(expiresAt).toISOString(),
        notes: notes.trim() === '' ? undefined : notes,
      }),
    onSuccess: () => {
      setError(null)
      setIsEditing(false)
      void queryClient.invalidateQueries({ queryKey: ['requests', requestId] })
      void queryClient.invalidateQueries({ queryKey: ['dashboard'] })
    },
    onError: (err) => setError(err instanceof ApiError ? err.message : 'Something went wrong.'),
  })

  function handleEditSubmit(event: FormEvent) {
    event.preventDefault()
    setError(null)
    editMutation.mutate()
  }

  const actions = quote.permittedActions.filter((action) => action in actionStyles)
  const canEdit = quote.permittedActions.includes('Edit')
  const canSubmitEdit = amount !== '' && Number(amount) > 0 && currency.trim().length === 3

  return (
    <div className="flex flex-col gap-3 rounded-lg border border-border bg-card p-4">
      <div className="flex items-start justify-between gap-3">
        <div>
          <p className="font-medium">{quote.vendorOrganizationName}</p>
          {!isEditing && (
            <p className="text-sm text-muted-foreground">
              {formatMoney(quote.amount, quote.currency)}
              {quote.expiresAt && <> · expires {formatDate(quote.expiresAt)}</>}
            </p>
          )}
        </div>
        <StatusBadge status={quote.status} />
      </div>

      {!isEditing && quote.notes && <p className="text-sm text-muted-foreground">{quote.notes}</p>}

      {error && (
        <div role="alert" className="rounded-md border border-destructive/30 bg-destructive/10 px-3 py-2 text-sm text-destructive">
          {error}
        </div>
      )}

      {isEditing ? (
        <form onSubmit={handleEditSubmit} className="flex flex-col gap-3">
          <div className="grid grid-cols-2 gap-3">
            <div className="flex flex-col gap-2">
              <Label htmlFor={`amount-${quote.id}`}>Amount</Label>
              <Input
                id={`amount-${quote.id}`}
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
              <Label htmlFor={`currency-${quote.id}`}>Currency</Label>
              <Input
                id={`currency-${quote.id}`}
                value={currency}
                onChange={(event) => setCurrency(event.currentTarget.value.toUpperCase())}
                maxLength={3}
                required
              />
            </div>
          </div>

          <div className="flex flex-col gap-2">
            <Label htmlFor={`expiresAt-${quote.id}`}>Expires</Label>
            <Input
              id={`expiresAt-${quote.id}`}
              type="date"
              value={expiresAt}
              onChange={(event) => setExpiresAt(event.currentTarget.value)}
            />
          </div>

          <div className="flex flex-col gap-2">
            <Label htmlFor={`notes-${quote.id}`}>Notes</Label>
            <Input
              id={`notes-${quote.id}`}
              value={notes}
              onChange={(event) => setNotes(event.currentTarget.value)}
              maxLength={2000}
            />
          </div>

          <div className="flex gap-2 pt-1">
            <Button type="submit" size="sm" disabled={!canSubmitEdit || editMutation.isPending}>
              {editMutation.isPending ? 'Saving…' : 'Save'}
            </Button>
            <Button
              type="button"
              size="sm"
              variant="outline"
              disabled={editMutation.isPending}
              onClick={() => {
                setIsEditing(false)
                setError(null)
                setAmount(String(quote.amount))
                setCurrency(quote.currency)
                setExpiresAt(quote.expiresAt?.slice(0, 10) ?? '')
                setNotes(quote.notes ?? '')
              }}
            >
              Cancel
            </Button>
          </div>
        </form>
      ) : (
        (actions.length > 0 || canEdit) && (
          <div className="flex flex-wrap gap-2 pt-1">
            {actions.map((action) => (
              <Button
                key={action}
                size="sm"
                variant={actionStyles[action].variant}
                disabled={actionMutation.isPending}
                onClick={() => actionMutation.mutate(action)}
              >
                {actionStyles[action].label}
              </Button>
            ))}
            {canEdit && (
              <Button size="sm" variant="outline" onClick={() => setIsEditing(true)}>
                Edit
              </Button>
            )}
          </div>
        )
      )}
    </div>
  )
}
