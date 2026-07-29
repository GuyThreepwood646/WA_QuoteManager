import { useMutation, useQueryClient } from '@tanstack/react-query'
import { ArrowLeft } from 'lucide-react'
import { useState } from 'react'
import type { FormEvent } from 'react'
import { Link, useNavigate } from 'react-router'

import { ApiError } from '@/api/apiClient'
import { createOrganization } from '@/api/organizations'
import type { CreateOrganizationInput } from '@/api/types'
import { useAuth } from '@/auth/AuthProvider'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'

/**
 * Creating an organization is Admin-only, mirroring the same gate on <c>Organization.Create</c>
 * - a non-Admin who reaches this page by URL still gets a form, but submitting it surfaces the
 * API's 403 rather than the page pretending the action doesn't exist.
 */
export function NewOrganizationPage() {
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const { session } = useAuth()
  const [name, setName] = useState('')
  const [kind, setKind] = useState<CreateOrganizationInput['kind'] | ''>('')
  const [error, setError] = useState<string | null>(null)

  const mutation = useMutation({
    mutationFn: () => createOrganization({ name, kind: kind as CreateOrganizationInput['kind'] }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['organizations'] })
      navigate('/organizations')
    },
    onError: (err) => setError(err instanceof ApiError ? err.message : 'Something went wrong.'),
  })

  function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setError(null)
    mutation.mutate()
  }

  const canSubmit = name.trim() !== '' && kind !== ''
  const isAdmin = session?.user.roles.includes('Admin') ?? false

  return (
    <div className="flex flex-col gap-6">
      <Link to="/organizations" className="flex items-center gap-1.5 text-sm text-muted-foreground hover:text-foreground">
        <ArrowLeft className="size-4" />
        Back to organizations
      </Link>

      <Card className="max-w-xl">
        <CardHeader>
          <CardTitle>New organization</CardTitle>
          <CardDescription>Add a client or vendor organization to the directory.</CardDescription>
        </CardHeader>
        <CardContent>
          {!isAdmin && (
            <div
              role="alert"
              className="mb-4 rounded-md border border-warning/30 bg-warning/10 px-3 py-2 text-sm text-warning"
            >
              Your account is not able to create organizations - only an Admin can. Submitting will be refused.
            </div>
          )}

          {error && (
            <div
              role="alert"
              className="mb-4 rounded-md border border-destructive/30 bg-destructive/10 px-3 py-2 text-sm text-destructive"
            >
              {error}
            </div>
          )}

          <form onSubmit={handleSubmit} className="flex flex-col gap-4">
            <div className="flex flex-col gap-2">
              <Label htmlFor="name">Name</Label>
              <Input
                id="name"
                value={name}
                onChange={(event) => setName(event.currentTarget.value)}
                required
                autoFocus
                maxLength={200}
              />
            </div>

            <div className="flex flex-col gap-2">
              <Label htmlFor="kind">Kind</Label>
              <Select value={kind} onValueChange={(value) => setKind(value as CreateOrganizationInput['kind'])} required>
                <SelectTrigger id="kind" className="w-full">
                  <SelectValue placeholder="Select a kind" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="Client">Client</SelectItem>
                  <SelectItem value="Vendor">Vendor</SelectItem>
                </SelectContent>
              </Select>
            </div>

            <Button type="submit" disabled={!canSubmit || mutation.isPending} className="mt-2 self-start">
              {mutation.isPending ? 'Creating…' : 'Create organization'}
            </Button>
          </form>
        </CardContent>
      </Card>
    </div>
  )
}
