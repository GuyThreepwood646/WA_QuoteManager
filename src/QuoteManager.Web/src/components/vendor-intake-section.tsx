import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'

import { ApiError } from '@/api/apiClient'
import { inviteVendor } from '@/api/requests'
import { AddQuoteForm } from '@/components/add-quote-form'
import { Button } from '@/components/ui/button'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { cn } from '@/lib/utils'

type IntakeMode = 'invite' | 'quote'

type VendorOption = {
  id: string
  name: string
}

/**
 * Invite and draft-on-behalf are alternative paths for the same goal (getting a vendor onto
 * the request). When both are available, this section forces an exclusive choice so they do
 * not read as two sequential required steps.
 */
export function VendorIntakeSection({
  requestId,
  canInviteVendor,
  canAddQuote,
  availableInviteVendors,
  quotedVendorIds,
}: {
  requestId: string
  canInviteVendor: boolean
  canAddQuote: boolean
  availableInviteVendors: VendorOption[]
  quotedVendorIds: string[]
}) {
  const bothAvailable = canInviteVendor && canAddQuote
  const [mode, setMode] = useState<IntakeMode>(canInviteVendor ? 'invite' : 'quote')

  if (!canInviteVendor && !canAddQuote) {
    return null
  }

  if (!bothAvailable) {
    return canInviteVendor ? (
      <InviteVendorPanel requestId={requestId} availableVendors={availableInviteVendors} />
    ) : (
      <AddQuoteForm requestId={requestId} quotedVendorIds={quotedVendorIds} />
    )
  }

  return (
    <section className="overflow-hidden rounded-xl border border-border bg-card">
      <header className="border-b border-border px-4 py-3.5">
        <h2 className="text-sm font-semibold tracking-tight">Add a vendor</h2>
        <p className="mt-0.5 text-xs text-muted-foreground">
          Choose one path per vendor — invite them to respond, or enter their quote yourself.
        </p>
      </header>

      <div className="grid gap-2 p-3 sm:grid-cols-2" role="radiogroup" aria-label="How to add a vendor">
        <ModeOption
          selected={mode === 'invite'}
          title="Invite to quote"
          description="Ask the vendor to submit their own numbers."
          onSelect={() => setMode('invite')}
        />
        <ModeOption
          selected={mode === 'quote'}
          title="Enter a quote"
          description="Draft on their behalf when you already have pricing."
          onSelect={() => setMode('quote')}
        />
      </div>

      <div className="border-t border-border bg-background/40 px-4 py-4">
        {mode === 'invite' ? (
          <InviteVendorFields requestId={requestId} availableVendors={availableInviteVendors} />
        ) : (
          <AddQuoteForm requestId={requestId} quotedVendorIds={quotedVendorIds} embedded />
        )}
      </div>
    </section>
  )
}

function ModeOption({
  selected,
  title,
  description,
  onSelect,
}: {
  selected: boolean
  title: string
  description: string
  onSelect: () => void
}) {
  return (
    <button
      type="button"
      role="radio"
      aria-checked={selected}
      onClick={onSelect}
      className={cn(
        'rounded-lg border px-3.5 py-3 text-left transition-colors',
        selected
          ? 'border-primary/40 bg-primary/10'
          : 'border-transparent bg-background/50 hover:bg-accent/40',
      )}
    >
      <div className="flex items-start gap-2.5">
        <span
          aria-hidden
          className={cn(
            'mt-0.5 flex size-3.5 shrink-0 items-center justify-center rounded-full border',
            selected ? 'border-primary' : 'border-muted-foreground/40',
          )}
        >
          {selected && <span className="size-1.5 rounded-full bg-primary" />}
        </span>
        <span className="min-w-0">
          <span className="block text-sm font-medium">{title}</span>
          <span className="mt-0.5 block text-xs text-muted-foreground">{description}</span>
        </span>
      </div>
    </button>
  )
}

function InviteVendorPanel({
  requestId,
  availableVendors,
}: {
  requestId: string
  availableVendors: VendorOption[]
}) {
  return (
    <div className="flex flex-col gap-2">
      <h2 className="text-sm font-semibold">Invite a vendor</h2>
      <InviteVendorFields requestId={requestId} availableVendors={availableVendors} />
    </div>
  )
}

function InviteVendorFields({
  requestId,
  availableVendors,
}: {
  requestId: string
  availableVendors: VendorOption[]
}) {
  const queryClient = useQueryClient()
  const [vendorToInvite, setVendorToInvite] = useState('')
  const [error, setError] = useState<string | null>(null)

  const inviteMutation = useMutation({
    mutationFn: () => inviteVendor(requestId, vendorToInvite),
    onSuccess: () => {
      setError(null)
      setVendorToInvite('')
      void queryClient.invalidateQueries({ queryKey: ['requests', requestId] })
      void queryClient.invalidateQueries({ queryKey: ['dashboard'] })
    },
    onError: (err) => setError(err instanceof ApiError ? err.message : 'Something went wrong.'),
  })

  return (
    <div className="flex flex-col gap-3">
      {error && (
        <div
          role="alert"
          className="rounded-md border border-destructive/30 bg-destructive/10 px-3 py-2 text-sm text-destructive"
        >
          {error}
        </div>
      )}
      <div className="flex max-w-md gap-2">
        <Select value={vendorToInvite} onValueChange={setVendorToInvite}>
          <SelectTrigger className="w-full">
            <SelectValue
              placeholder={
                availableVendors.length === 0 ? 'No more vendors to invite' : 'Select a vendor'
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
        <Button
          size="sm"
          disabled={vendorToInvite === '' || inviteMutation.isPending}
          onClick={() => inviteMutation.mutate()}
        >
          {inviteMutation.isPending ? 'Inviting…' : 'Invite'}
        </Button>
      </div>
    </div>
  )
}
