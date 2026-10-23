import { NavLink } from 'react-router-dom'

import { t } from '@/i18n/catalog'

export function TripNavigation() {
  return (
    <nav
      aria-label={t('narrative.navigation.label')}
      className="fixed inset-x-0 bottom-0 z-40 flex justify-around border-t border-border bg-background px-4 pb-[env(safe-area-inset-bottom)] pt-2 md:static md:mx-auto md:w-full md:max-w-5xl md:justify-start md:gap-2 md:border-0 md:bg-transparent md:px-6 md:pt-0"
    >
      <NavigationLink to="/" label={t('narrative.navigation.camper')} end />
      <NavigationLink to="/trips" label={t('narrative.navigation.trips')} />
    </nav>
  )
}

function NavigationLink({ to, label, end = false }: { to: string; label: string; end?: boolean }) {
  return (
    <NavLink
      to={to}
      end={end}
      className={({ isActive }) =>
        `inline-flex min-h-11 items-center rounded-lg px-4 text-sm font-medium transition-colors ${
          isActive ? 'bg-muted text-foreground' : 'text-muted-foreground hover:text-foreground'
        }`
      }
    >
      {label}
    </NavLink>
  )
}
