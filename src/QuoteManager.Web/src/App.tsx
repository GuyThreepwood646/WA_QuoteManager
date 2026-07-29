import { Building2, FileText, LayoutDashboard, LogOut } from 'lucide-react'
import type { ReactNode } from 'react'
import { NavLink as RouterNavLink, Navigate, Route, Routes, useLocation } from 'react-router'

import { Button } from '@/components/ui/button'
import { Separator } from '@/components/ui/separator'
import { cn } from '@/lib/utils'

import { useAuth } from './auth/AuthProvider'
import { DashboardPage } from './routes/DashboardPage'
import { LoginPage } from './routes/LoginPage'
import { NewOrganizationPage } from './routes/NewOrganizationPage'
import { NewRequestPage } from './routes/NewRequestPage'
import { OrganizationsPage } from './routes/OrganizationsPage'
import { RequestDetailPage } from './routes/RequestDetailPage'
import { RequestsListPage } from './routes/RequestsListPage'

export function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route
        path="/*"
        element={
          <RequireAuth>
            <Shell />
          </RequireAuth>
        }
      />
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

const navItems = [
  { to: '/dashboard', label: 'Dashboard', icon: LayoutDashboard },
  { to: '/requests', label: 'Requests', icon: FileText },
  { to: '/organizations', label: 'Organizations', icon: Building2 },
]

/**
 * The dashboard is the root route deliberately. The brief's real requirement is that a user can see
 * what is happening and focus on the right work, so the landing surface is a triage view rather than
 * a list of entities.
 */
function Shell() {
  const { session, logout } = useAuth()

  return (
    <div className="flex h-screen bg-background text-foreground">
      <aside className="flex w-60 shrink-0 flex-col border-r border-border bg-sidebar text-sidebar-foreground">
        <div className="flex h-14 items-center px-5">
          <span className="text-sm font-semibold tracking-tight">Warehouse Anywhere</span>
        </div>
        <Separator className="bg-sidebar-border" />
        <nav className="flex flex-1 flex-col gap-1 p-3">
          {navItems.map(({ to, label, icon: Icon }) => (
            <RouterNavLink
              key={to}
              to={to}
              className={({ isActive }) =>
                cn(
                  'flex items-center gap-2.5 rounded-md px-3 py-2 text-sm font-medium transition-colors',
                  isActive
                    ? 'bg-sidebar-accent text-sidebar-accent-foreground'
                    : 'text-muted-foreground hover:bg-sidebar-accent/60 hover:text-sidebar-accent-foreground',
                )
              }
            >
              <Icon className="size-4" />
              {label}
            </RouterNavLink>
          ))}
        </nav>
      </aside>

      <div className="flex flex-1 flex-col overflow-hidden">
        <header className="flex h-14 shrink-0 items-center justify-between border-b border-border px-6">
          <span className="text-sm text-muted-foreground">Storage, packing & transportation requests</span>
          <div className="flex items-center gap-3">
            <span className="text-sm font-medium">{session?.user.displayName}</span>
            <Button variant="ghost" size="sm" onClick={logout}>
              <LogOut className="size-4" />
              Sign out
            </Button>
          </div>
        </header>

        <main className="flex-1 overflow-y-auto p-6">
          <Routes>
            <Route path="/" element={<Navigate to="/dashboard" replace />} />
            <Route path="/dashboard" element={<DashboardPage />} />
            <Route path="/requests" element={<RequestsListPage />} />
            <Route path="/requests/new" element={<NewRequestPage />} />
            <Route path="/requests/:requestId" element={<RequestDetailPage />} />
            <Route path="/organizations" element={<OrganizationsPage />} />
            <Route path="/organizations/new" element={<NewOrganizationPage />} />
          </Routes>
        </main>
      </div>
    </div>
  )
}
