import { Badge } from '@/components/ui/badge'
import { cn } from '@/lib/utils'

const statusStyles: Record<string, string> = {
  Draft: 'bg-muted text-muted-foreground border-transparent',
  Submitted: 'bg-primary/20 text-primary border-primary/30',
  UnderReview: 'bg-warning/20 text-warning border-warning/30',
  Accepted: 'bg-success/20 text-success border-success/30',
  Awarded: 'bg-success/20 text-success border-success/30',
  Rejected: 'bg-destructive/20 text-destructive border-destructive/30',
  Withdrawn: 'bg-muted text-muted-foreground border-transparent',
  Expired: 'bg-muted text-muted-foreground border-transparent',
  Open: 'bg-primary/20 text-primary border-primary/30',
  Cancelled: 'bg-muted text-muted-foreground border-transparent',
}

const sizeStyles = {
  default: 'font-semibold text-sm px-3 py-1',
  sm: 'font-medium text-[11px] leading-none px-2 py-1',
} as const

/** The one place a lifecycle status becomes a color, so no screen decides its own one-off color. */
export function StatusBadge({ status, size = 'default' }: { status: string; size?: keyof typeof sizeStyles }) {
  return (
    <Badge variant="outline" className={cn('border', sizeStyles[size], statusStyles[status])}>
      {status}
    </Badge>
  )
}
