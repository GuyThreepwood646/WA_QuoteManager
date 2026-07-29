import { useQuery } from '@tanstack/react-query'
import { ArrowLeft } from 'lucide-react'
import { Link, useParams } from 'react-router'

import { getRequest } from '@/api/requests'
import { ActivityTimeline } from '@/components/activity-timeline'
import { AddQuoteForm } from '@/components/add-quote-form'
import { QuoteCard } from '@/components/quote-card'
import { StatusBadge } from '@/components/status-badge'
import { Skeleton } from '@/components/ui/skeleton'
import { formatDate } from '@/lib/format'

export function RequestDetailPage() {
  const { requestId } = useParams<{ requestId: string }>()
  const { data, isPending, isError } = useQuery({
    queryKey: ['requests', requestId],
    queryFn: () => getRequest(requestId!),
    enabled: Boolean(requestId),
  })

  if (isPending) {
    return <Skeleton className="h-64 rounded-lg" />
  }

  if (isError || !data) {
    return <p className="text-sm text-destructive">Could not load this request.</p>
  }

  const silentInvitations = data.invitations.filter((invitation) => !invitation.hasQuoted)

  return (
    <div className="flex flex-col gap-6">
      <Link to="/requests" className="flex items-center gap-1.5 text-sm text-muted-foreground hover:text-foreground">
        <ArrowLeft className="size-4" />
        Back to requests
      </Link>

      <div className="flex items-start justify-between gap-4">
        <div>
          <h1 className="text-lg font-semibold">{data.title}</h1>
          <p className="text-sm text-muted-foreground">
            {data.clientOrganizationName}
            {data.neededBy && <> · needed by {formatDate(data.neededBy)}</>}
          </p>
        </div>
        <StatusBadge status={data.status} />
      </div>

      {data.description && <p className="max-w-2xl text-sm text-muted-foreground">{data.description}</p>}

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

      {data.canAddQuote && <AddQuoteForm requestId={data.id} />}

      <div className="flex flex-col gap-3">
        <h2 className="text-sm font-semibold">Activity</h2>
        <ActivityTimeline requestId={data.id} />
      </div>
    </div>
  )
}
