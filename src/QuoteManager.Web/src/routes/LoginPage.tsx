import { useState } from 'react'
import type { FormEvent } from 'react'
import { Navigate, useLocation, useNavigate } from 'react-router'

import { ApiError } from '../api/apiClient'
import { useAuth } from '../auth/AuthProvider'
import { FormField } from '@/components/form-field'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import {
  type FieldErrors,
  clearFieldError,
  hasFieldErrors,
  validateEmail,
  validateRequired,
} from '@/lib/form-validation'

export function LoginPage() {
  const { session, isReady, login } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({})
  const [submitting, setSubmitting] = useState(false)

  const redirectTo = (location.state as { from?: string } | null)?.from ?? '/'

  if (isReady && session) {
    return <Navigate to={redirectTo === '/login' ? '/' : redirectTo} replace />
  }

  function validateForm(): FieldErrors {
    const next: FieldErrors = {}

    const emailError = validateEmail(email)
    if (emailError) {
      next.email = emailError
    }

    const passwordError = validateRequired(password, 'Password')
    if (passwordError) {
      next.password = passwordError
    }

    return next
  }

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setError(null)

    const next = validateForm()
    setFieldErrors(next)
    if (hasFieldErrors(next)) {
      return
    }

    setSubmitting(true)

    try {
      await login(email, password)
      navigate(redirectTo, { replace: true })
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Something went wrong. Try again.')
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div className="relative flex min-h-screen items-center justify-center overflow-hidden bg-background px-4">
      {/* A soft, low-opacity glow behind the card is the one decorative flourish on this page -
          enough to keep a plain dark background from feeling inert, without competing with the form. */}
      <div
        aria-hidden
        className="pointer-events-none absolute top-1/2 left-1/2 size-[36rem] -translate-x-1/2 -translate-y-1/2 rounded-full bg-primary/15 blur-[120px]"
      />

      <Card className="relative w-full max-w-sm border-border/60 shadow-2xl shadow-black/40">
        <CardHeader className="space-y-1 text-center">
          <CardTitle className="text-xl">Warehouse Anywhere</CardTitle>
          <CardDescription>Sign in to review storage requests and partner quotes.</CardDescription>
        </CardHeader>
        <CardContent>
          {error && (
            <div
              role="alert"
              className="mb-4 rounded-md border border-destructive/30 bg-destructive/10 px-3 py-2 text-sm text-destructive"
            >
              {error}
            </div>
          )}
          <form onSubmit={handleSubmit} noValidate className="flex flex-col gap-4">
            <FormField id="email" label="Email" error={fieldErrors.email}>
              <Input
                type="email"
                autoComplete="email"
                value={email}
                onChange={(event) => {
                  setEmail(event.currentTarget.value)
                  setFieldErrors((current) => clearFieldError(current, 'email'))
                }}
                autoFocus
              />
            </FormField>
            <FormField id="password" label="Password" error={fieldErrors.password}>
              <Input
                type="password"
                autoComplete="current-password"
                value={password}
                onChange={(event) => {
                  setPassword(event.currentTarget.value)
                  setFieldErrors((current) => clearFieldError(current, 'password'))
                }}
              />
            </FormField>
            <Button type="submit" disabled={submitting} className="mt-2">
              {submitting ? 'Signing in…' : 'Sign in'}
            </Button>
          </form>
        </CardContent>
      </Card>
    </div>
  )
}
