import { useQuery } from '@tanstack/react-query'
import { useNavigate } from 'react-router'

import { listRequests } from '@/api/requests'
import { StatusBadge } from '@/components/status-badge'
import { Skeleton } from '@/components/ui/skeleton'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { formatDate } from '@/lib/format'

export function RequestsListPage() {
  const navigate = useNavigate()
  const { data, isPending, isError } = useQuery({
    queryKey: ['requests'],
    queryFn: () => listRequests(),
  })

  if (isPending) {
    return <Skeleton className="h-64 rounded-lg" />
  }

  if (isError) {
    return <p className="text-sm text-destructive">Could not load requests.</p>
  }

  return (
    <div className="rounded-lg border border-border bg-card">
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>Title</TableHead>
            <TableHead>Client</TableHead>
            <TableHead>Status</TableHead>
            <TableHead className="text-right">Quotes</TableHead>
            <TableHead>Needed by</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {data.items.map((request) => (
            <TableRow
              key={request.id}
              className="cursor-pointer"
              onClick={() => navigate(`/requests/${request.id}`)}
            >
              <TableCell className="font-medium">{request.title}</TableCell>
              <TableCell className="text-muted-foreground">{request.clientOrganizationName}</TableCell>
              <TableCell>
                <StatusBadge status={request.status} />
              </TableCell>
              <TableCell className="text-right">{request.quoteCount}</TableCell>
              <TableCell className="text-muted-foreground">
                {request.neededBy ? formatDate(request.neededBy) : '—'}
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  )
}
