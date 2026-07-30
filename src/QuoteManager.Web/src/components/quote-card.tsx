import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useEffect, useState } from 'react'
import type { FormEvent } from 'react'

import { applyQuoteAction, editQuote } from '@/api/requests'
import type { RequestQuoteItem } from '@/api/types'
import { ApiError } from '@/api/apiClient'
import { FormField, FormFieldRow, textareaClassName } from '@/components/form-field'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { formatDate, formatDaysUntil, formatMoney } from '@/lib/format'
import {
  type FieldErrors,
  clearFieldError,
  hasFieldErrors,
  parseAmountInput,
  validateCurrencyCode,
  validatePositiveAmount,
} from '@/lib/form-validation'

import { StatusBadge } from './status-badge'

interface ActionStyle {
  label: string
  variant: 'default' | 'destructive' | 'outline' | 'secondary'
}

const statusReasonText: Record<string, string> = {
  SupersededByAcceptedQuote: 'Automatically rejected — a competing quote on this request was accepted.',
}

const actionStyles: Record<string, ActionStyle> = {
  Submit: { label: 'Submit', variant: 'default' },
  StartReview: { label: 'Start review', variant: 'default' },
  Accept: { label: 'Accept', variant: 'default' },
  Reject: { label: 'Reject', variant: 'destructive' },
  ReturnToSubmitted: { label: 'Return to submitted', variant: 'outline' },
  Withdraw: { label: 'Withdraw', variant: 'outline' },
  Expire: { label: 'Mark expired', variant: 'outline' },
}

async function refreshRequest(queryClient: ReturnType<typeof useQueryClient>, requestId: string) {
  await queryClient.invalidateQueries({ queryKey: ['requests', requestId] })
  await queryClient.refetchQueries({ queryKey: ['requests', requestId] })
  void queryClient.invalidateQueries({ queryKey: ['requests', requestId, 'activity'] })
  void queryClient.invalidateQueries({ queryKey: ['dashboard'] })
}

export function QuoteCard({
  requestId,
  quote,
  readOnly = false,
}: {
  requestId: string
  quote: RequestQuoteItem
  readOnly?: boolean
}) {
  const queryClient = useQueryClient()
  const [error, setError] = useState<string | null>(null)
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({})
  const [isEditing, setIsEditing] = useState(false)
  const [amount, setAmount] = useState(String(quote.amount))
  const [currency, setCurrency] = useState(quote.currency)
  const [expiresAt, setExpiresAt] = useState(quote.expiresAt?.slice(0, 10) ?? '')
  const [notes, setNotes] = useState(quote.notes ?? '')
  const [pendingAction, setPendingAction] = useState<string | null>(null)
  const [actionNote, setActionNote] = useState('')

  useEffect(() => {
    setAmount(String(quote.amount))
    setCurrency(quote.currency)
    setExpiresAt(quote.expiresAt?.slice(0, 10) ?? '')
    setNotes(quote.notes ?? '')
    setPendingAction(null)
    setActionNote('')
    if (readOnly) {
      setIsEditing(false)
      setError(null)
      setFieldErrors({})
    }
  }, [
    readOnly,
    quote.id,
    quote.status,
    quote.version,
    quote.amount,
    quote.currency,
    quote.expiresAt,
    quote.notes,
    quote.lastActivityAt,
    quote.lastActivityNote,
  ])

  const actionMutation = useMutation({
    mutationFn: ({ action, note }: { action: string; note?: string }) => applyQuoteAction(requestId, quote, action, note),
    onSuccess: async () => {
      setError(null)
      setFieldErrors({})
      setIsEditing(false)
      setPendingAction(null)
      setActionNote('')
      await refreshRequest(queryClient, requestId)
    },
    onError: (err) => setError(err instanceof ApiError ? err.message : 'Something went wrong.'),
  })

  const editMutation = useMutation({
    mutationFn: () =>
      editQuote(requestId, quote, {
        amount: Number(parseAmountInput(amount)),
        currency,
        expiresAt: expiresAt === '' ? undefined : new Date(expiresAt).toISOString(),
        notes: notes.trim() === '' ? undefined : notes,
      }),
    onSuccess: async () => {
      setError(null)
      setFieldErrors({})
      setIsEditing(false)
      await refreshRequest(queryClient, requestId)
    },
    onError: (err) => setError(err instanceof ApiError ? err.message : 'Something went wrong.'),
  })

  function validateEditForm(): FieldErrors {
    const next: FieldErrors = {}

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

  function handleEditSubmit(event: FormEvent) {
    event.preventDefault()
    setError(null)

    const next = validateEditForm()
    setFieldErrors(next)
    if (hasFieldErrors(next)) {
      return
    }

    editMutation.mutate()
  }

  const actions = readOnly ? [] : quote.permittedActions.filter((action) => action in actionStyles)
  const canEdit = !readOnly && quote.permittedActions.includes('Edit')
  const isInteractive = !readOnly && (actions.length > 0 || canEdit)

  return (
    <div className="flex flex-col gap-3 rounded-lg border border-border bg-card p-4">
      <div className="flex items-start justify-between gap-3">
        <div>
          <p className="font-medium">{quote.vendorOrganizationName}</p>
          {!isEditing && (
            <p className="text-sm text-muted-foreground">
              {formatMoney(quote.amount, quote.currency)}
              {quote.expiresAt && (
                <> · expires {formatDate(quote.expiresAt)} ({formatDaysUntil(quote.expiresAt)})</>
              )}
            </p>
          )}
        </div>
        <StatusBadge status={quote.status} />
      </div>

      {!isEditing && quote.notes && <p className="text-sm text-muted-foreground">{quote.notes}</p>}

      {!isEditing && quote.lastActivityAt && (
        <p className="text-xs text-muted-foreground">
          Last activity: {formatDate(quote.lastActivityAt)}
          {quote.lastActivityNote && (
            <>
              {' '}
              — <span className="italic">{quote.lastActivityNote}</span>
            </>
          )}
        </p>
      )}

      {!isEditing && quote.statusReason && (
        <p className="text-sm italic text-muted-foreground">
          {statusReasonText[quote.statusReason] ?? quote.statusReason}
        </p>
      )}

      {error && (
        <div role="alert" className="rounded-md border border-destructive/30 bg-destructive/10 px-3 py-2 text-sm text-destructive">
          {error}
        </div>
      )}

      {isEditing ? (
        <form onSubmit={handleEditSubmit} noValidate className="flex flex-col gap-3">
          <FormFieldRow
            fields={[
              {
                id: `amount-${quote.id}`,
                label: 'Amount',
                error: fieldErrors.amount,
                children: (
                  <Input
                    type="text"
                    inputMode="decimal"
                    placeholder="1,234.56"
                    value={amount}
                    onChange={(event) => {
                      setAmount(event.currentTarget.value)
                      setFieldErrors((current) => clearFieldError(current, 'amount'))
                    }}
                    autoFocus
                  />
                ),
              },
              {
                id: `currency-${quote.id}`,
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

          <FormField id={`expiresAt-${quote.id}`} label="Expires">
            <Input
              type="date"
              value={expiresAt}
              onChange={(event) => setExpiresAt(event.currentTarget.value)}
            />
          </FormField>

          <FormField id={`notes-${quote.id}`} label="Notes">
            <Input
              value={notes}
              onChange={(event) => setNotes(event.currentTarget.value)}
              maxLength={2000}
            />
          </FormField>

          <div className="flex gap-2 pt-1">
            <Button type="submit" size="sm" disabled={editMutation.isPending}>
              {editMutation.isPending ? 'Saving…' : quote.status === 'Draft' ? 'Save' : 'Revise to draft'}
            </Button>
            <Button
              type="button"
              size="sm"
              variant="outline"
              disabled={editMutation.isPending}
              onClick={() => {
                setIsEditing(false)
                setError(null)
                setFieldErrors({})
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
      ) : pendingAction ? (
        <form
          onSubmit={(event) => {
            event.preventDefault()
            actionMutation.mutate({
              action: pendingAction,
              note: actionNote.trim() === '' ? undefined : actionNote.trim(),
            })
          }}
          className="flex flex-col gap-2 pt-1"
        >
          <FormField id={`action-note-${quote.id}`} label={`Note for "${actionStyles[pendingAction].label}" (optional)`}>
            <textarea
              className={textareaClassName}
              value={actionNote}
              onChange={(event) => setActionNote(event.currentTarget.value)}
              maxLength={2000}
              rows={2}
              autoFocus
            />
          </FormField>
          <div className="flex gap-2">
            <Button type="submit" size="sm" variant={actionStyles[pendingAction].variant} disabled={actionMutation.isPending}>
              {actionMutation.isPending ? 'Saving…' : `Confirm ${actionStyles[pendingAction].label.toLowerCase()}`}
            </Button>
            <Button
              type="button"
              size="sm"
              variant="outline"
              disabled={actionMutation.isPending}
              onClick={() => {
                setPendingAction(null)
                setActionNote('')
              }}
            >
              Cancel
            </Button>
          </div>
        </form>
      ) : (
        isInteractive && (
          <div className="flex flex-wrap gap-2 pt-1">
            {actions.map((action) => (
              <Button
                key={action}
                size="sm"
                variant={actionStyles[action].variant}
                disabled={actionMutation.isPending}
                onClick={() => setPendingAction(action)}
              >
                {actionStyles[action].label}
              </Button>
            ))}
            {canEdit && (
              <Button size="sm" variant="outline" onClick={() => setIsEditing(true)}>
                {quote.status === 'Draft' ? 'Edit' : 'Revise'}
              </Button>
            )}
          </div>
        )
      )}
    </div>
  )
}
