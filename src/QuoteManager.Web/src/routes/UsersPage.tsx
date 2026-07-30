import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Plus } from 'lucide-react'
import { Fragment, useState } from 'react'
import { Link } from 'react-router'

import { ApiError } from '@/api/apiClient'
import { listOrganizations } from '@/api/organizations'
import { listUsers, updateUser } from '@/api/users'
import type { UserListItem } from '@/api/types'
import { useAuth } from '@/auth/AuthProvider'
import { PasswordResetForm } from '@/components/password-reset-form'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { UserProfileFields } from '@/components/user-profile-fields'
import { type FieldErrors, clearFieldError } from '@/lib/form-validation'
import { type UserDraft, draftToUpdateInput, userToDraft, validateUserDraft } from '@/lib/user-validation'
import { setSession } from '@/auth/authSession'

export function UsersPage() {
  const { session } = useAuth()
  const isAdmin = session?.user.roles.includes('Admin') ?? false

  return isAdmin ? <AdminUsersTable /> : <MyProfileCard />
}

function AdminUsersTable() {
  const queryClient = useQueryClient()
  const [editingId, setEditingId] = useState<string | null>(null)
  const [resettingId, setResettingId] = useState<string | null>(null)
  const [drafts, setDrafts] = useState<Record<string, UserDraft>>({})
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({})
  const [error, setError] = useState<string | null>(null)

  const { data, isPending, isError } = useQuery({
    queryKey: ['users'],
    queryFn: () => listUsers(100),
  })

  const { data: organizations } = useQuery({
    queryKey: ['organizations', { includeRetired: true }],
    queryFn: () => listOrganizations(100, true),
  })

  const updateMutation = useMutation({
    mutationFn: ({ userId, draft }: { userId: string; draft: UserDraft }) =>
      updateUser(userId, draftToUpdateInput(draft)),
    onSuccess: () => {
      setError(null)
      setEditingId(null)
      setFieldErrors({})
      void queryClient.invalidateQueries({ queryKey: ['users'] })
    },
    onError: (err) => setError(err instanceof ApiError ? err.message : 'Something went wrong.'),
  })

  if (isPending) {
    return <Skeleton className="h-64 rounded-lg" />
  }

  if (isError || !data) {
    return <p className="text-sm text-destructive">Could not load users.</p>
  }

  function startEditing(user: UserListItem) {
    setResettingId(null)
    setEditingId(user.id)
    setFieldErrors({})
    setDrafts((current) => ({ ...current, [user.id]: userToDraft(user) }))
  }

  function discardEditing(user: UserListItem) {
    setEditingId(null)
    setFieldErrors({})
    setDrafts((current) => ({ ...current, [user.id]: userToDraft(user) }))
  }

  function saveDraft(user: UserListItem) {
    const draft = drafts[user.id] ?? userToDraft(user)
    const nextErrors = validateUserDraft(draft, true)
    setFieldErrors(nextErrors)
    if (Object.keys(nextErrors).length > 0) {
      return
    }

    updateMutation.mutate({ userId: user.id, draft })
  }

  function toggleResetting(userId: string) {
    setEditingId(null)
    setResettingId((current) => (current === userId ? null : userId))
  }

  const columnCount = 5

  return (
    <div className="flex flex-col gap-4">
      <div className="flex justify-end">
        <Button asChild size="sm">
          <Link to="/users/new">
            <Plus className="size-4" />
            New user
          </Link>
        </Button>
      </div>

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
              <TableHead>Email</TableHead>
              <TableHead>Roles</TableHead>
              <TableHead>Organization</TableHead>
              <TableHead className="text-right">Actions</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {data.items.map((user) => {
              const isEditing = editingId === user.id
              const isResetting = resettingId === user.id
              const draft = drafts[user.id] ?? userToDraft(user)

              return (
                <Fragment key={user.id}>
                  <TableRow>
                    <TableCell className="font-medium">{user.displayName}</TableCell>
                    <TableCell className="text-muted-foreground">{user.email}</TableCell>
                    <TableCell>
                      <div className="flex flex-wrap gap-1">
                        {user.roles.map((role) => (
                          <Badge key={role} variant="secondary">
                            {role}
                          </Badge>
                        ))}
                      </div>
                    </TableCell>
                    <TableCell className="text-muted-foreground">{user.organizationName ?? '—'}</TableCell>
                    <TableCell className="text-right">
                      <div className="flex justify-end gap-2">
                        <Button size="sm" variant="outline" onClick={() => (isEditing ? discardEditing(user) : startEditing(user))}>
                          {isEditing ? 'Cancel' : 'Edit'}
                        </Button>
                        <Button size="sm" variant="outline" onClick={() => toggleResetting(user.id)}>
                          {isResetting ? 'Cancel' : 'Reset password'}
                        </Button>
                      </div>
                    </TableCell>
                  </TableRow>
                  {isEditing && (
                    <TableRow>
                      <TableCell colSpan={columnCount} className="bg-muted/10 p-4">
                        <div className="flex flex-col gap-3">
                          <UserProfileFields
                            idPrefix={user.id}
                            draft={draft}
                            fieldErrors={fieldErrors}
                            isAdminEditing
                            organizations={organizations?.items ?? []}
                            onDraftChange={(next) => setDrafts((current) => ({ ...current, [user.id]: next }))}
                            onClearFieldError={(field) => setFieldErrors((current) => clearFieldError(current, field))}
                          />
                          <div className="flex gap-2">
                            <Button size="sm" disabled={updateMutation.isPending} onClick={() => saveDraft(user)}>
                              {updateMutation.isPending ? 'Saving…' : 'Save'}
                            </Button>
                            <Button size="sm" variant="outline" disabled={updateMutation.isPending} onClick={() => discardEditing(user)}>
                              Cancel
                            </Button>
                          </div>
                        </div>
                      </TableCell>
                    </TableRow>
                  )}
                  {isResetting && (
                    <TableRow>
                      <TableCell colSpan={columnCount} className="bg-muted/10 p-4">
                        <PasswordResetForm
                          userId={user.id}
                          requireCurrentPassword={false}
                          onSuccess={() => setResettingId(null)}
                          onCancel={() => setResettingId(null)}
                        />
                      </TableCell>
                    </TableRow>
                  )}
                </Fragment>
              )
            })}
          </TableBody>
        </Table>
      </div>
    </div>
  )
}

function MyProfileCard() {
  const { session } = useAuth()
  const queryClient = useQueryClient()
  const [draft, setDraft] = useState<UserDraft | null>(null)
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({})
  const [error, setError] = useState<string | null>(null)
  const [showResetPassword, setShowResetPassword] = useState(false)

  const { data, isPending, isError } = useQuery({
    queryKey: ['users'],
    queryFn: () => listUsers(100),
  })

  const user = data?.items.find((u) => u.id === session?.user.id)
  const currentDraft = draft ?? (user ? userToDraft(user) : null)

  const updateMutation = useMutation({
    mutationFn: () => {
      if (!user || !currentDraft) {
        throw new Error('Profile has not finished loading yet.')
      }

      return updateUser(user.id, draftToUpdateInput(currentDraft))
    },
    onSuccess: (result) => {
      setError(null)
      setFieldErrors({})
      if (result.accessToken && result.expiresAt) {
        setSession({
          accessToken: result.accessToken,
          expiresAt: result.expiresAt,
          user: {
            id: result.user.id,
            displayName: result.user.displayName,
            roles: result.user.roles,
            organizationId: result.user.organizationId,
          },
        })
      }
      void queryClient.invalidateQueries({ queryKey: ['users'] })
    },
    onError: (err) => setError(err instanceof ApiError ? err.message : 'Something went wrong.'),
  })

  if (isPending) {
    return <Skeleton className="h-64 rounded-lg" />
  }

  if (isError || !data || !user || !currentDraft) {
    return <p className="text-sm text-destructive">Could not load your profile.</p>
  }

  function handleSave() {
    const nextErrors = validateUserDraft(currentDraft!, false)
    setFieldErrors(nextErrors)
    if (Object.keys(nextErrors).length > 0) {
      return
    }

    updateMutation.mutate()
  }

  return (
    <div className="flex max-w-xl flex-col gap-6">
      <Card>
        <CardHeader>
          <CardTitle>My profile</CardTitle>
          <CardDescription>Update your own contact details. Roles and organization are managed by an Admin.</CardDescription>
        </CardHeader>
        <CardContent className="flex flex-col gap-4">
          {error && (
            <div role="alert" className="rounded-md border border-destructive/30 bg-destructive/10 px-3 py-2 text-sm text-destructive">
              {error}
            </div>
          )}

          <UserProfileFields
            idPrefix="me"
            draft={currentDraft}
            fieldErrors={fieldErrors}
            isAdminEditing={false}
            organizations={[]}
            onDraftChange={setDraft}
            onClearFieldError={(field) => setFieldErrors((current) => clearFieldError(current, field))}
          />

          <Button size="sm" disabled={updateMutation.isPending} className="self-start" onClick={handleSave}>
            {updateMutation.isPending ? 'Saving…' : 'Save'}
          </Button>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Password</CardTitle>
          <CardDescription>Change your own password.</CardDescription>
        </CardHeader>
        <CardContent>
          {showResetPassword ? (
            <PasswordResetForm
              userId={user.id}
              requireCurrentPassword
              onSuccess={() => setShowResetPassword(false)}
              onCancel={() => setShowResetPassword(false)}
            />
          ) : (
            <Button size="sm" variant="outline" onClick={() => setShowResetPassword(true)}>
              Reset password
            </Button>
          )}
        </CardContent>
      </Card>
    </div>
  )
}
