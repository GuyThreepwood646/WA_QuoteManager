import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import type { FormEvent } from 'react'

import { ApiError } from '@/api/apiClient'
import { resetPassword } from '@/api/users'
import { Button } from '@/components/ui/button'
import { FormField } from '@/components/form-field'
import { Input } from '@/components/ui/input'
import { PasswordRequirements } from '@/components/password-requirements'
import { type FieldErrors, clearFieldError, hasFieldErrors } from '@/lib/form-validation'
import { passwordMeetsRequirements, passwordsMatch } from '@/lib/password-validation'

/**
 * Shared by the admin table's per-row "Reset password" action and a user's own profile card -
 * `requireCurrentPassword` is the only thing that differs: resetting your own password proves you
 * know the current one, but an admin resetting someone else's has no way to supply it and doesn't
 * need to, since admin authority substitutes for that proof.
 */
export function PasswordResetForm({
  userId,
  requireCurrentPassword,
  onSuccess,
  onCancel,
}: {
  userId: string
  requireCurrentPassword: boolean
  onSuccess: () => void
  onCancel?: () => void
}) {
  const queryClient = useQueryClient()
  const [currentPassword, setCurrentPassword] = useState('')
  const [newPassword, setNewPassword] = useState('')
  const [confirmNewPassword, setConfirmNewPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({})

  const mutation = useMutation({
    mutationFn: () =>
      resetPassword(userId, {
        currentPassword: requireCurrentPassword ? currentPassword : undefined,
        newPassword,
        confirmNewPassword,
      }),
    onSuccess: () => {
      setError(null)
      setFieldErrors({})
      setCurrentPassword('')
      setNewPassword('')
      setConfirmNewPassword('')
      void queryClient.invalidateQueries({ queryKey: ['users'] })
      onSuccess()
    },
    onError: (err) => setError(err instanceof ApiError ? err.message : 'Something went wrong.'),
  })

  function validate(): FieldErrors {
    const next: FieldErrors = {}

    if (requireCurrentPassword && currentPassword.trim() === '') {
      next.currentPassword = 'Enter your current password.'
    }

    if (!passwordMeetsRequirements(newPassword)) {
      next.newPassword = 'Password does not meet the requirements below.'
    }

    if (!passwordsMatch(newPassword, confirmNewPassword)) {
      next.confirmNewPassword = 'Passwords do not match.'
    }

    return next
  }

  function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setError(null)

    const next = validate()
    setFieldErrors(next)
    if (hasFieldErrors(next)) {
      return
    }

    mutation.mutate()
  }

  return (
    <form onSubmit={handleSubmit} noValidate className="flex flex-col gap-3">
      {error && (
        <div role="alert" className="rounded-md border border-destructive/30 bg-destructive/10 px-3 py-2 text-sm text-destructive">
          {error}
        </div>
      )}

      {requireCurrentPassword && (
        <FormField id={`${userId}-currentPassword`} label="Current password" error={fieldErrors.currentPassword}>
          <Input
            type="password"
            value={currentPassword}
            onChange={(event) => {
              setCurrentPassword(event.currentTarget.value)
              setFieldErrors((current) => clearFieldError(current, 'currentPassword'))
            }}
            autoComplete="current-password"
          />
        </FormField>
      )}

      <FormField id={`${userId}-newPassword`} label="New password" error={fieldErrors.newPassword}>
        <Input
          type="password"
          value={newPassword}
          onChange={(event) => {
            setNewPassword(event.currentTarget.value)
            setFieldErrors((current) => clearFieldError(current, 'newPassword'))
          }}
          autoComplete="new-password"
        />
      </FormField>

      <PasswordRequirements password={newPassword} />

      <FormField id={`${userId}-confirmNewPassword`} label="Confirm new password" error={fieldErrors.confirmNewPassword}>
        <Input
          type="password"
          value={confirmNewPassword}
          onChange={(event) => {
            setConfirmNewPassword(event.currentTarget.value)
            setFieldErrors((current) => clearFieldError(current, 'confirmNewPassword'))
          }}
          autoComplete="new-password"
        />
      </FormField>

      <div className="flex gap-2 pt-1">
        <Button type="submit" size="sm" disabled={mutation.isPending}>
          {mutation.isPending ? 'Saving…' : 'Reset password'}
        </Button>
        {onCancel && (
          <Button type="button" size="sm" variant="outline" disabled={mutation.isPending} onClick={onCancel}>
            Cancel
          </Button>
        )}
      </div>
    </form>
  )
}
