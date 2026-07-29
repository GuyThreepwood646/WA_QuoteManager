import { useQuery } from '@tanstack/react-query'
import { AlertTriangle, Clock, Inbox, Search } from 'lucide-react'
import type { ReactNode } from 'react'
import { useNavigate } from 'react-router'

import { getDashboard } from '@/api/dashboard'
import type { QuoteTriageItem, RequestAwaitingResponseItem } from '@/api/types'
import { StatusBadge } from '@/components/status-badge'
import { Skeleton } from '@/components/ui/skeleton'
import { formatMoney, formatRelative } from '@/lib/format'

/**
 * A triage surface, not a CRUD grid. Each section answers one question a user actually has
 * ("what needs my review", "what's about to lapse", "who's gone quiet") rather than being a
 * filtered slice of one big list - so an empty section is a real, useful answer ("nothing here"),
 * not a hidden feature.
 */
export function DashboardPage() {
  const { data, isPending, isError } = useQuery({ queryKey: ['dashboard'], queryFn: getDashboard })

  if (isPending) {
    return (
      <div className="grid gap-4 md:grid-cols-2">
        {Array.from({ length: 4 }, (_, i) => (
          <Skeleton key={i} className="h-48 rounded-lg" />
        ))}
      </div>
    )
  }

  if (isError || !data) {
    return <p className="text-sm text-destructive">Could not load the dashboard.</p>
  }

  return (
    <div className="grid gap-4 md:grid-cols-2">
      <Section title="Needs your review" icon={<Inbox className="size-4" />} count={data.quotesNeedingReview.length}>
        {data.quotesNeedingReview.map((item) => (
          <QuoteRow key={item.quoteId} item={item} />
        ))}
      </Section>

      <Section title="Under review" icon={<Search className="size-4" />} count={data.quotesUnderReview.length}>
        {data.quotesUnderReview.map((item) => (
          <QuoteRow key={item.quoteId} item={item} />
        ))}
      </Section>

      <Section title="Expiring soon" icon={<Clock className="size-4" />} count={data.quotesExpiringSoon.length} urgent>
        {data.quotesExpiringSoon.map((item) => (
          <QuoteRow key={item.quoteId} item={item} />
        ))}
      </Section>

      <Section
        title="Awaiting partner response"
        icon={<AlertTriangle className="size-4" />}
        count={data.requestsAwaitingResponse.length}
      >
        {data.requestsAwaitingResponse.map((item) => (
          <RequestRow key={item.requestId} item={item} />
        ))}
      </Section>
    </div>
  )
}

function Section({
  title,
  icon,
  count,
  urgent,
  children,
}: {
  title: string
  icon: ReactNode
  count: number
  urgent?: boolean
  children: ReactNode
}) {
  return (
    <div className="flex flex-col gap-3 rounded-lg border border-border bg-card p-4">
      <div className="flex items-center justify-between">
        <div className={`flex items-center gap-2 text-sm font-semibold ${urgent && count > 0 ? 'text-warning' : ''}`}>
          {icon}
          {title}
        </div>
        <span className="text-xs text-muted-foreground">{count}</span>
      </div>
      {count === 0 ? (
        <p className="text-sm text-muted-foreground">Nothing here right now.</p>
      ) : (
        <div className="flex flex-col gap-2">{children}</div>
      )}
    </div>
  )
}

function QuoteRow({ item }: { item: QuoteTriageItem }) {
  const navigate = useNavigate()

  return (
    <button
      type="button"
      onClick={() => navigate(`/requests/${item.requestId}`)}
      className="flex items-center justify-between gap-3 rounded-md border border-transparent px-2 py-2 text-left text-sm transition-colors hover:border-border hover:bg-accent/50"
    >
      <div className="min-w-0">
        <p className="truncate font-medium">{item.requestTitle}</p>
        <p className="truncate text-muted-foreground">
          {item.vendorOrganizationName} · {formatMoney(item.amount, item.currency)}
        </p>
      </div>
      <StatusBadge status={item.status} />
    </button>
  )
}

function RequestRow({ item }: { item: RequestAwaitingResponseItem }) {
  const navigate = useNavigate()

  return (
    <button
      type="button"
      onClick={() => navigate(`/requests/${item.requestId}`)}
      className="flex flex-col gap-1 rounded-md border border-transparent px-2 py-2 text-left text-sm transition-colors hover:border-border hover:bg-accent/50"
    >
      <p className="font-medium">{item.title}</p>
      <p className="text-muted-foreground">
        Waiting on {item.awaitingVendorNames.join(', ')} · invited {formatRelative(item.createdAt)}
      </p>
    </button>
  )
}
