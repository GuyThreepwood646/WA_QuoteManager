import { AppShell, Group, NavLink, Text, Title } from '@mantine/core'
import { NavLink as RouterNavLink, Navigate, Route, Routes } from 'react-router'

/**
 * Application shell and route table.
 *
 * The dashboard is the root route deliberately. The brief's real requirement is that a user can see
 * what is happening and focus on the right work, so the landing surface is a triage view rather than
 * a list of entities.
 */
export function App() {
  return (
    <AppShell header={{ height: 56 }} navbar={{ width: 220, breakpoint: 'sm' }} padding="md">
      <AppShell.Header>
        <Group h="100%" px="md" justify="space-between">
          <Title order={4}>Quote Manager</Title>
          <Text size="sm" c="dimmed">
            Service requests and quotes
          </Text>
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
