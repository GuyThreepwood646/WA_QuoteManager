import { useQuery } from '@tanstack/react-query'
import { Plus } from 'lucide-react'
import { Link, useNavigate } from 'react-router'

import { listRequests } from '@/api/requests'
import { useAuth } from '@/auth/AuthProvider'
import { StatusBadge } from '@/components/status-badge'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { formatDate } from '@/lib/format'

export function RequestsListPage() {
  const navigate = useNavigate()
  const { session } = useAuth()
  const { data, isPending, isError } = useQuery({
    queryKey: ['requests'],
    queryFn: () => listRequests(),
  })

  // Mirrors the API's own gate on Request.Create: only who could succeed sees the button.
  const canCreateRequest = session?.user.roles.some((role) => role === 'Requester' || role === 'Admin') ?? false

  if (isPending) {
    return <Skeleton className="h-64 rounded-lg" />
  }

  if (isError) {
    return <p className="text-sm text-destructive">Could not load requests.</p>
  }

  return (
    <div className="flex flex-col gap-4">
      {canCreateRequest && (
        <div className="flex justify-end">
          <Button asChild size="sm">
            <Link to="/requests/new">
              <Plus className="size-4" />
              New request
            </Link>
          </Button>
        </div>
      )}

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
    </div>
  )
}
