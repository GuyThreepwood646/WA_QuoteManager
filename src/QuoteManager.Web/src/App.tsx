import { AppShell, Button, Group, NavLink, Text, Title } from '@mantine/core'
import type { ReactNode } from 'react'
import { NavLink as RouterNavLink, Navigate, Route, Routes, useLocation } from 'react-router'

import { useAuth } from './auth/AuthProvider'
import { LoginPage } from './routes/LoginPage'

/**
 * Route table. `/login` renders standalone; every other path requires a session and renders
 * inside the application shell.
 */
export function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route path="/*" element={<RequireAuth><Shell /></RequireAuth>} />
    </Routes>
  )
}

function RequireAuth({ children }: { children: ReactNode }) {
  const { session } = useAuth()
  const location = useLocation()

  if (!session) {
    return <Navigate to="/login" replace state={{ from: location.pathname }} />
  }

  return children
}

/**
 * The dashboard is the root route deliberately. The brief's real requirement is that a user can see
 * what is happening and focus on the right work, so the landing surface is a triage view rather than
 * a list of entities.
 */
function Shell() {
  const { session, logout } = useAuth()

  return (
    <AppShell header={{ height: 56 }} navbar={{ width: 220, breakpoint: 'sm' }} padding="md">
      <AppShell.Header>
        <Group h="100%" px="md" justify="space-between">
          <Title order={4}>Quote Manager</Title>
          <Group gap="sm">
            <Text size="sm" c="dimmed">
              {session?.user.displayName}
            </Text>
            <Button variant="subtle" size="xs" onClick={logout}>
              Sign out
            </Button>
          </Group>
        </Group>
      </AppShell.Header>

      <AppShell.Navbar p="xs">
        <NavLink component={RouterNavLink} to="/dashboard" label="Dashboard" />
        <NavLink component={RouterNavLink} to="/requests" label="Requests" />
        <NavLink component={RouterNavLink} to="/organizations" label="Organizations" />
      </AppShell.Navbar>

      <AppShell.Main>
        <Routes>
          <Route path="/" element={<Navigate to="/dashboard" replace />} />
          <Route path="/dashboard" element={<Placeholder name="Dashboard" />} />
          <Route path="/requests" element={<Placeholder name="Requests" />} />
          <Route path="/organizations" element={<Placeholder name="Organizations" />} />
        </Routes>
      </AppShell.Main>
    </AppShell>
  )
}

function Placeholder({ name }: { name: string }) {
  return <Text c="dimmed">{name} — not implemented yet.</Text>
}
