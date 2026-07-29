import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Plus } from 'lucide-react'
import { useState } from 'react'
import { Link } from 'react-router'

import { ApiError } from '@/api/apiClient'
import { listOrganizations, retireOrganization, updateOrganization } from '@/api/organizations'
import { useAuth } from '@/auth/AuthProvider'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Skeleton } from '@/components/ui/skeleton'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'

export function OrganizationsPage() {
  const queryClient = useQueryClient()
  const { session } = useAuth()
  const isAdmin = session?.user.roles.includes('Admin') ?? false
  const [editingId, setEditingId] = useState<string | null>(null)
  const [name, setName] = useState('')
  const [error, setError] = useState<string | null>(null)

  const { data, isPending, isError } = useQuery({
    queryKey: ['organizations', { includeRetired: isAdmin }],
    queryFn: () => listOrganizations(100, isAdmin),
  })

  const renameMutation = useMutation({
    mutationFn: (organizationId: string) => updateOrganization(organizationId, { name }),
    onSuccess: () => {
      setError(null)
      setEditingId(null)
      void queryClient.invalidateQueries({ queryKey: ['organizations'] })
    },
    onError: (err) => setError(err instanceof ApiError ? err.message : 'Something went wrong.'),
  })

  const retireMutation = useMutation({
    mutationFn: (organizationId: string) => retireOrganization(organizationId),
    onSuccess: () => {
      setError(null)
      void queryClient.invalidateQueries({ queryKey: ['organizations'] })
    },
    onError: (err) => setError(err instanceof ApiError ? err.message : 'Something went wrong.'),
  })

  if (isPending) {
    return <Skeleton className="h-64 rounded-lg" />
  }

  if (isError) {
    return <p className="text-sm text-destructive">Could not load organizations.</p>
  }

  return (
    <div className="flex flex-col gap-4">
      {isAdmin && (
        <div className="flex justify-end">
          <Button asChild size="sm">
            <Link to="/organizations/new">
              <Plus className="size-4" />
              New organization
            </Link>
          </Button>
        </div>
      )}

      {error && (
        <div role="alert" className="rounded-md border border-destructive/30 bg-destructive/10 px-3 py-2 text-sm text-destructive">
          {error}
        </div>
      )}

      <div className="rounded-lg border border-border bg-card">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Name</TableHead>
              <TableHead>Kind</TableHead>
              {isAdmin && <TableHead className="text-right">Actions</TableHead>}
            </TableRow>
          </TableHeader>
          <TableBody>
            {data.items.map((org) => (
              <TableRow key={org.id}>
                <TableCell className="font-medium">
                  {editingId === org.id ? (
                    <Input
                      value={name}
                      onChange={(event) => setName(event.currentTarget.value)}
                      maxLength={200}
                      autoFocus
                      className="h-8"
                    />
                  ) : (
                    org.name
                  )}
                </TableCell>
                <TableCell>
                  <div className="flex items-center gap-2">
                    <Badge variant="secondary">{org.kind}</Badge>
                    {org.retiredAt && <Badge variant="outline">Retired</Badge>}
                  </div>
                </TableCell>
                {isAdmin && (
                  <TableCell className="text-right">
                    {editingId === org.id ? (
                      <div className="flex justify-end gap-2">
                        <Button
                          size="sm"
                          disabled={name.trim() === '' || renameMutation.isPending}
                          onClick={() => renameMutation.mutate(org.id)}
                        >
                          Save
                        </Button>
                        <Button size="sm" variant="outline" onClick={() => setEditingId(null)}>
                          Cancel
                        </Button>
                      </div>
                    ) : (
                      <div className="flex justify-end gap-2">
                        <Button
                          size="sm"
                          variant="outline"
                          onClick={() => {
                            setError(null)
                            setEditingId(org.id)
                            setName(org.name)
                          }}
                        >
                          Rename
                        </Button>
                        {!org.retiredAt && (
                          <Button
                            size="sm"
                            variant="outline"
                            disabled={retireMutation.isPending}
                            onClick={() => {
                              if (window.confirm(`Retire "${org.name}"? It will no longer be offered for new requests or invitations.`)) {
                                retireMutation.mutate(org.id)
                              }
                            }}
                          >
                            Retire
                          </Button>
                        )}
                      </div>
                    )}
                  </TableCell>
                )}
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </div>
    </div>
  )
}
