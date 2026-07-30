import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ArrowLeft } from 'lucide-react'
import { useState } from 'react'
import type { FormEvent } from 'react'
import { Link, useNavigate } from 'react-router'

import { ApiError } from '@/api/apiClient'
import { listOrganizations } from '@/api/organizations'
import { createUser } from '@/api/users'
import { useAuth } from '@/auth/AuthProvider'
import { FormField } from '@/components/form-field'
import { PasswordRequirements } from '@/components/password-requirements'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { UserProfileFields } from '@/components/user-profile-fields'
import {
  type FieldErrors,
  clearFieldError,
  hasFieldErrors,
} from '@/lib/form-validation'
import { passwordMeetsRequirements, passwordsMatch } from '@/lib/password-validation'
import { draftToUpdateInput, emptyUserDraft, validateUserDraft } from '@/lib/user-validation'

/**
 * Creating a user is Admin-only, mirroring the same gate on `POST /api/users` - a non-Admin who
 * reaches this page by URL still gets a form, but submitting it surfaces the API's 403 rather than
 * the page pretending the action doesn't exist (same precedent as `NewOrganizationPage`).
 */
export function NewUserPage() {
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const { session } = useAuth()
  const [draft, setDraft] = useState(emptyUserDraft())
  const [password, setPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({})

  const { data: organizations } = useQuery({
    queryKey: ['organizations'],
    queryFn: () => listOrganizations(),
  })

  const mutation = useMutation({
    mutationFn: () => {
      const input = draftToUpdateInput(draft)
      return createUser({ ...input, password, confirmPassword })
    },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['users'] })
      navigate('/users')
    },
    onError: (err) => setError(err instanceof ApiError ? err.message : 'Something went wrong.'),
  })

  function validateForm(): FieldErrors {
    const next = validateUserDraft(draft, true)

    if (!passwordMeetsRequirements(password)) {
      next.password = 'Password does not meet the requirements below.'
    }

    if (!passwordsMatch(password, confirmPassword)) {
      next.confirmPassword = 'Passwords do not match.'
    }

    return next
  }

  function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setError(null)

    const next = validateForm()
    setFieldErrors(next)
    if (hasFieldErrors(next)) {
      return
    }

    mutation.mutate()
  }

  const isAdmin = session?.user.roles.includes('Admin') ?? false

  return (
    <div className="flex flex-col gap-6">
      <Link to="/users" className="flex items-center gap-1.5 text-sm text-muted-foreground hover:text-foreground">
        <ArrowLeft className="size-4" />
        Back to users
      </Link>

      <Card className="max-w-2xl">
        <CardHeader>
          <CardTitle>New user</CardTitle>
          <CardDescription>Create a new user account.</CardDescription>
        </CardHeader>
        <CardContent>
          {!isAdmin && (
            <div
              role="alert"
              className="mb-4 rounded-md border border-warning/30 bg-warning/10 px-3 py-2 text-sm text-warning"
            >
              Your account is not able to create users - only an Admin can. Submitting will be refused.
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

          <form onSubmit={handleSubmit} noValidate className="flex flex-col gap-4">
            <UserProfileFields
              idPrefix="new"
              draft={draft}
              fieldErrors={fieldErrors}
              isAdminEditing
              organizations={organizations?.items ?? []}
              onDraftChange={setDraft}
              onClearFieldError={(field) => setFieldErrors((current) => clearFieldError(current, field))}
            />

            <FormField id="new-password" label="Password" error={fieldErrors.password}>
              <Input
                type="password"
                value={password}
                onChange={(event) => {
                  setPassword(event.currentTarget.value)
                  setFieldErrors((current) => clearFieldError(current, 'password'))
                }}
                autoComplete="new-password"
              />
            </FormField>

            <PasswordRequirements password={password} />

            <FormField id="new-confirmPassword" label="Confirm password" error={fieldErrors.confirmPassword}>
              <Input
                type="password"
                value={confirmPassword}
                onChange={(event) => {
                  setConfirmPassword(event.currentTarget.value)
                  setFieldErrors((current) => clearFieldError(current, 'confirmPassword'))
                }}
                autoComplete="new-password"
              />
            </FormField>

            <Button type="submit" disabled={mutation.isPending} className="mt-2 self-start">
              {mutation.isPending ? 'Creating…' : 'Create user'}
            </Button>
          </form>
        </CardContent>
      </Card>
    </div>
  )
}
