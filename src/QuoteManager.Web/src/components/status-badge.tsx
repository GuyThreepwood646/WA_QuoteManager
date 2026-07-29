import { Badge } from '@/components/ui/badge'
import { cn } from '@/lib/utils'

const statusStyles: Record<string, string> = {
  Draft: 'bg-muted text-muted-foreground border-transparent',
  Submitted: 'bg-primary/15 text-primary border-primary/25',
  UnderReview: 'bg-warning/15 text-warning border-warning/25',
  Accepted: 'bg-success/15 text-success border-success/25',
  Awarded: 'bg-success/15 text-success border-success/25',
  Rejected: 'bg-destructive/15 text-destructive border-destructive/25',
  Withdrawn: 'bg-muted text-muted-foreground border-transparent',
  Expired: 'bg-muted text-muted-foreground border-transparent',
  Open: 'bg-primary/15 text-primary border-primary/25',
  Cancelled: 'bg-muted text-muted-foreground border-transparent',
}

/**
 * The one place a lifecycle status becomes a colour: every
 * screen that shows a quote or request status renders it through here, so a status never gets a
 * one-off colour decided in the component that happens to need it that day.
 */
export function StatusBadge({ status }: { status: string }) {
  return (
    <Badge variant="outline" className={cn('border font-medium', statusStyles[status])}>
      {status}
    </Badge>
  )
}
