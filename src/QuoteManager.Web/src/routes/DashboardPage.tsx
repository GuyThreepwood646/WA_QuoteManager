import { useQuery } from '@tanstack/react-query'
import { Clock } from 'lucide-react'
import { Fragment } from 'react'
import { useNavigate } from 'react-router'

import { getDashboard } from '@/api/dashboard'
import type { DashboardKpis, DashboardQuoteItem, DashboardRequestItem } from '@/api/types'
import { useAuth } from '@/auth/AuthProvider'
import { StatusBadge } from '@/components/status-badge'
import { Skeleton } from '@/components/ui/skeleton'
import { formatDate, formatDaysUntil, formatMoney } from '@/lib/format'

/**
 * A triage surface, not a CRUD grid: one card per request that needs attention, with every quote
 * on it shown as a sub-row - a request with three competing vendor quotes in three different
 * states is one card, not three unrelated entries scattered across separate buckets.
 */
export function DashboardPage() {
  const { session } = useAuth()
  const roles = session?.user.roles ?? []
  const isVendorOnlyViewer =
    roles.includes('Vendor') && !roles.some((role) => role === 'Admin' || role === 'Reviewer' || role === 'Requester')

  const { data, isPending, isError } = useQuery({ queryKey: ['dashboard'], queryFn: getDashboard })

  return (
    <div className="flex flex-col gap-4">
      <LifecycleDiagram />

      {isPending ? (
        <DashboardSkeleton />
      ) : isError || !data ? (
        <p className="text-sm text-destructive">Could not load the dashboard.</p>
      ) : (
        <>
          <KpiStrip kpis={data.kpis} />
          {data.requests.length === 0 ? (
            <div className="rounded-lg border border-border bg-card p-4 text-sm text-muted-foreground">
              Nothing needs your attention right now.
            </div>
          ) : (
            <div className="flex flex-col gap-3">
              {data.requests.map((item) => (
                <RequestTriageCard key={item.requestId} item={item} isVendorOnlyViewer={isVendorOnlyViewer} />
              ))}
            </div>
          )}
        </>
      )}
    </div>
  )
}

function DashboardSkeleton() {
  return (
    <div className="flex flex-col gap-3">
      <Skeleton className="h-16 rounded-lg" />
      {Array.from({ length: 3 }, (_, i) => (
        <Skeleton key={i} className="h-24 rounded-lg" />
      ))}
    </div>
  )
}

type LifecycleStage = {
  title: string
  role: string
  request: string[]
  quote: string[]
  quoteFlow?: boolean
}

/**
 * Static reference content, not data-driven: the states and roles below are fixed domain
 * knowledge (see `QuoteTransitions.cs` and the transition table in docs/api.md), not something
 * the API needs to supply.
 */
function LifecycleDiagram() {
  const stages: LifecycleStage[] = [
    { title: 'Created', role: 'Requester', request: ['Open'], quote: [] },
    { title: 'Quoted', role: 'Vendor', request: ['Open'], quote: ['Draft', 'Submitted'], quoteFlow: true },
    { title: 'Reviewed', role: 'Reviewer', request: ['Open'], quote: ['UnderReview'] },
    {
      title: 'Resolved',
      role: 'Reviewer / Admin',
      request: ['Awarded', 'Cancelled'],
      quote: ['Accepted', 'Rejected'],
    },
  ]

  return (
    <section className="overflow-hidden rounded-xl border border-border bg-card">
      <header className="border-b border-border px-5 py-3.5">
        <h2 className="text-sm font-semibold tracking-tight">How a request moves through the system</h2>
        <p className="mt-0.5 text-xs text-muted-foreground">
          Four stages from open request to award or cancel
        </p>
      </header>

      <div className="px-5 py-5">
        {/* Progress rail */}
        <div className="relative mb-5 hidden md:block">
          <div className="absolute top-3 right-8 left-8 h-px bg-border" />
          <ol className="relative grid grid-cols-4">
            {stages.map((stage, index) => (
              <li key={stage.title} className="flex flex-col items-center gap-2">
                <span className="relative z-10 flex size-6 items-center justify-center rounded-full bg-primary/15 text-[11px] font-semibold text-primary ring-4 ring-card">
                  {index + 1}
                </span>
                <span className="text-xs font-medium">{stage.title}</span>
              </li>
            ))}
          </ol>
        </div>

        {/* Stage panels */}
        <ol className="grid gap-3 sm:grid-cols-2 md:grid-cols-4">
          {stages.map((stage, index) => (
            <li
              key={stage.title}
              className="flex flex-col gap-3 rounded-lg bg-background/60 px-3.5 py-3.5"
            >
              <div className="flex items-center justify-between gap-2 md:hidden">
                <span className="text-xs font-medium">{stage.title}</span>
                <span className="text-[10px] font-medium text-muted-foreground">Step {index + 1}</span>
              </div>

              <StatusGroup label="Request" statuses={stage.request} />
              <StatusGroup
                label="Quote"
                statuses={stage.quote}
                flow={stage.quoteFlow}
                empty="None yet"
              />

              <p className="mt-auto border-t border-border/60 pt-2.5 text-[11px] text-muted-foreground">
                {stage.role}
              </p>
            </li>
          ))}
        </ol>
      </div>

      <footer className="border-t border-border bg-background/40 px-5 py-2.5 text-[11px] leading-relaxed text-muted-foreground">
        Admins can act at every stage. Quotes may also end as{' '}
        <StatusBadge status="Withdrawn" size="sm" /> or <StatusBadge status="Expired" size="sm" />.
      </footer>
    </section>
  )
}

function StatusGroup({
  label,
  statuses,
  flow = false,
  empty,
}: {
  label: string
  statuses: string[]
  flow?: boolean
  empty?: string
}) {
  return (
    <div className="flex flex-col gap-1.5">
      <span className="text-[10px] font-medium tracking-wide text-muted-foreground uppercase">
        {label}
      </span>
      {statuses.length === 0 ? (
        <span className="text-xs text-muted-foreground/70">{empty}</span>
      ) : (
        <div className="flex flex-wrap items-center gap-1.5">
          {statuses.map((status, index) => (
            <Fragment key={status}>
              {flow && index > 0 && (
                <span aria-hidden className="text-muted-foreground/50">
                  →
                </span>
              )}
              <StatusBadge status={status} size="sm" />
            </Fragment>
          ))}
        </div>
      )}
    </div>
  )
}

function KpiStrip({ kpis }: { kpis: DashboardKpis }) {
  const netThisMonth = kpis.requestsOpenedThisMonth - kpis.requestsClosedThisMonth
  const netLabel = netThisMonth > 0 ? `+${netThisMonth}` : `${netThisMonth}`

  return (
    <div className="grid grid-cols-2 gap-3 md:grid-cols-4">
      <KpiTile label="Open requests" value={kpis.openRequestCount.toString()} />
      <KpiTile label="Awaiting a decision" value={kpis.quotesAwaitingDecisionCount.toString()} />
      <KpiTile
        label="This month"
        value={`${kpis.requestsOpenedThisMonth} opened · ${kpis.requestsClosedThisMonth} closed (${netLabel})`}
      />
      {kpis.vendorResponseRatePercent !== null && (
        <KpiTile label="Vendor response rate" value={`${kpis.vendorResponseRatePercent.toFixed(0)}%`} />
      )}
    </div>
  )
}

function KpiTile({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex flex-col gap-1 rounded-lg border border-border bg-card p-3">
      <span className="text-xs text-muted-foreground">{label}</span>
      <span className="text-lg font-semibold">{value}</span>
    </div>
  )
}

function RequestTriageCard({
  item,
  isVendorOnlyViewer,
}: {
  item: DashboardRequestItem
  isVendorOnlyViewer: boolean
}) {
  const navigate = useNavigate()

  return (
    <button
      type="button"
      onClick={() => navigate(`/requests/${item.requestId}`)}
      className="flex flex-col gap-3 rounded-lg border border-border bg-card p-4 text-left transition-colors hover:bg-accent/50"
    >
      <div className="flex items-baseline justify-between gap-3">
        <div className="min-w-0">
          <p className="truncate font-semibold">{item.title}</p>
          <p className="truncate text-sm text-muted-foreground">{item.clientOrganizationName}</p>
        </div>
        {item.neededBy && (
          <div className="shrink-0 text-right text-xs text-muted-foreground">
            <p>Needed by {formatDate(item.neededBy)}</p>
            <p>{formatDaysUntil(item.neededBy)}</p>
          </div>
        )}
      </div>

      {item.quotes.length > 0 && (
        <div className="flex flex-col gap-2 border-t border-border pt-2">
          {item.quotes.map((quote) => (
            <QuoteSubRow key={quote.quoteId} quote={quote} />
          ))}
        </div>
      )}

      {item.awaitingVendorNames.length > 0 && (
        <p className="border-t border-border pt-2 text-sm text-muted-foreground">
          {isVendorOnlyViewer
            ? 'Awaiting your response'
            : `Awaiting response from: ${item.awaitingVendorNames.join(', ')}`}
        </p>
      )}
    </button>
  )
}

function QuoteSubRow({ quote }: { quote: DashboardQuoteItem }) {
  return (
    <div className="flex items-center justify-between gap-3 text-sm">
      <div className="min-w-0">
        <p className="truncate">
          {quote.vendorOrganizationName} · {formatMoney(quote.amount, quote.currency)}
        </p>
        {quote.expiresAt && (
          <p className="flex items-center gap-1 text-xs text-muted-foreground">
            {quote.isExpiringSoon && <Clock className="size-3 text-warning" />}
            {formatDaysUntil(quote.expiresAt)}
          </p>
        )}
      </div>
      <StatusBadge status={quote.status} />
    </div>
  )
}
