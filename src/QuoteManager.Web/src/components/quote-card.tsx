import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'

import { applyQuoteAction } from '@/api/requests'
import type { RequestQuoteItem } from '@/api/types'
import { ApiError } from '@/api/apiClient'
import { Button } from '@/components/ui/button'
import { formatDate, formatMoney } from '@/lib/format'

import { StatusBadge } from './status-badge'

interface ActionStyle {
  label: string
  variant: 'default' | 'destructive' | 'outline' | 'secondary'
}

// "Edit" travels in permittedActions but is a field-mutability signal, not a status transition -
// it has no endpoint of its own yet, so it isn't rendered as a button here.
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

  const mutation = useMutation({
    mutationFn: (action: string) => applyQuoteAction(requestId, quote, action),
    onSuccess: () => {
      setError(null)
      void queryClient.invalidateQueries({ queryKey: ['requests', requestId] })
      void queryClient.invalidateQueries({ queryKey: ['dashboard'] })
    },
    onError: (err) => setError(err instanceof ApiError ? err.message : 'Something went wrong.'),
  })

  const actions = quote.permittedActions.filter((action) => action in actionStyles)

  return (
    <div className="flex flex-col gap-3 rounded-lg border border-border bg-card p-4">
      <div className="flex items-start justify-between gap-3">
        <div>
          <p className="font-medium">{quote.vendorOrganizationName}</p>
          <p className="text-sm text-muted-foreground">
            {formatMoney(quote.amount, quote.currency)}
            {quote.expiresAt && <> · expires {formatDate(quote.expiresAt)}</>}
          </p>
        </div>
        <StatusBadge status={quote.status} />
      </div>

      {quote.notes && <p className="text-sm text-muted-foreground">{quote.notes}</p>}

      {error && (
        <div role="alert" className="rounded-md border border-destructive/30 bg-destructive/10 px-3 py-2 text-sm text-destructive">
          {error}
        </div>
      )}

      {actions.length > 0 && (
        <div className="flex flex-wrap gap-2 pt-1">
          {actions.map((action) => (
            <Button
              key={action}
              size="sm"
              variant={actionStyles[action].variant}
              disabled={mutation.isPending}
              onClick={() => mutation.mutate(action)}
            >
              {actionStyles[action].label}
            </Button>
          ))}
        </div>
      )}
    </div>
  )
}
