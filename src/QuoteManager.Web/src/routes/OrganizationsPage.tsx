import { useQuery } from '@tanstack/react-query'

import { listOrganizations } from '@/api/organizations'
import { Badge } from '@/components/ui/badge'
import { Skeleton } from '@/components/ui/skeleton'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'

export function OrganizationsPage() {
  const { data, isPending, isError } = useQuery({
    queryKey: ['organizations'],
    queryFn: () => listOrganizations(),
  })

  if (isPending) {
    return <Skeleton className="h-64 rounded-lg" />
  }

  if (isError) {
    return <p className="text-sm text-destructive">Could not load organizations.</p>
  }

  return (
    <div className="rounded-lg border border-border bg-card">
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>Name</TableHead>
            <TableHead>Kind</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {data.items.map((org) => (
            <TableRow key={org.id}>
              <TableCell className="font-medium">{org.name}</TableCell>
              <TableCell>
                <Badge variant="secondary">{org.kind}</Badge>
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  )
}
