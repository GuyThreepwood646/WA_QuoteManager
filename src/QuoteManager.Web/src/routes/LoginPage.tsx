import { Alert, Button, Paper, PasswordInput, Stack, TextInput, Title } from '@mantine/core'
import { useState } from 'react'
import type { FormEvent } from 'react'
import { useLocation, useNavigate } from 'react-router'

import { ApiError } from '../api/apiClient'
import { useAuth } from '../auth/AuthProvider'

export function LoginPage() {
  const { login } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)

  const redirectTo = (location.state as { from?: string } | null)?.from ?? '/'

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setError(null)
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
    <Stack align="center" justify="center" mih="100vh" bg="gray.0">
      <Paper withBorder shadow="sm" p="xl" w={360}>
        <Stack gap="md">
          <Title order={3}>Quote Manager</Title>
          {error && (
            <Alert color="red" title="Sign in failed">
              {error}
            </Alert>
          )}
          <form onSubmit={handleSubmit}>
            <Stack gap="sm">
              <TextInput
                label="Email"
                type="email"
                value={email}
                onChange={(event) => setEmail(event.currentTarget.value)}
                required
                autoFocus
              />
              <PasswordInput
                label="Password"
                value={password}
                onChange={(event) => setPassword(event.currentTarget.value)}
                required
              />
              <Button type="submit" loading={submitting} fullWidth mt="xs">
                Sign in
              </Button>
            </Stack>
          </form>
        </Stack>
      </Paper>
    </Stack>
  )
}
