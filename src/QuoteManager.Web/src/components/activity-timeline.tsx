import { useQuery } from '@tanstack/react-query'

import { getRequestActivity } from '@/api/requests'
import { Skeleton } from '@/components/ui/skeleton'
import { formatDate, formatRelative } from '@/lib/format'

/**
 * The audit trail rendered as a request-scoped timeline rather than a raw log. The endpoint
 * already applies the vendor read filter, so this component never has to reason about roles - it
 * renders exactly what it's given.
 */
export function ActivityTimeline({ requestId }: { requestId: string }) {
  const { data, isPending, isError } = useQuery({
    queryKey: ['requests', requestId, 'activity'],
    queryFn: () => getRequestActivity(requestId),
  })

  if (isPending) {
    return <Skeleton className="h-32 rounded-lg" />
  }

  if (isError || !data) {
    return <p className="text-sm text-destructive">Could not load the activity timeline.</p>
  }

  if (data.items.length === 0) {
    return <p className="text-sm text-muted-foreground">No activity recorded yet.</p>
  }

  return (
    <ol className="flex flex-col gap-4">
      {data.items.map((entry, index) => (
        <li key={entry.id} className="flex gap-3">
          <div className="flex flex-col items-center pt-1.5">
            <span className="size-2 rounded-full bg-primary" />
            {index < data.items.length - 1 && <span className="mt-1 w-px flex-1 bg-border" />}
          </div>
          <div className="flex flex-col pb-1">
            <p className="text-sm">
              <span className="font-medium">{entry.actorDisplayName}</span>{' '}
              <span className="text-muted-foreground">{entry.summary}</span>
            </p>
            {entry.note && <p className="text-sm italic text-muted-foreground">"{entry.note}"</p>}
            <p className="text-xs text-muted-foreground" title={formatDate(entry.occurredAt)}>
              {formatRelative(entry.occurredAt)}
            </p>
          </div>
        </li>
      ))}
    </ol>
  )
}
