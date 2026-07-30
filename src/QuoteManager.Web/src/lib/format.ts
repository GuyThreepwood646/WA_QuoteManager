import dayjs from 'dayjs'
import relativeTime from 'dayjs/plugin/relativeTime'
import utc from 'dayjs/plugin/utc'

dayjs.extend(relativeTime)
dayjs.extend(utc)

export function formatMoney(amount: number, currency: string): string {
  return new Intl.NumberFormat('en-US', { style: 'currency', currency }).format(amount)
}

export function formatDate(iso: string): string {
  return dayjs.utc(iso).format('MMM D, YYYY')
}

export function formatRelative(iso: string): string {
  return dayjs(iso).fromNow()
}

/** Whole calendar days between now and a deadline - negative once it's passed. */
export function daysUntil(iso: string): number {
  return dayjs.utc(iso).startOf('day').diff(dayjs().startOf('day'), 'day')
}

/** A scannable countdown for any deadline (a request's `neededBy`, a quote's `expiresAt`). */
export function formatDaysUntil(iso: string): string {
  const days = daysUntil(iso)

  if (days < 0) {
    return `Overdue by ${Math.abs(days)} day${Math.abs(days) === 1 ? '' : 's'}`
  }

  if (days === 0) {
    return 'Due today'
  }

  return `${days} day${days === 1 ? '' : 's'} left`
}
