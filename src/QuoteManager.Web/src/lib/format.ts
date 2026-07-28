import dayjs from 'dayjs'
import relativeTime from 'dayjs/plugin/relativeTime'

dayjs.extend(relativeTime)

export function formatMoney(amount: number, currency: string): string {
  return new Intl.NumberFormat('en-US', { style: 'currency', currency }).format(amount)
}

export function formatDate(iso: string): string {
  return dayjs(iso).format('MMM D, YYYY')
}

export function formatRelative(iso: string): string {
  return dayjs(iso).fromNow()
}
