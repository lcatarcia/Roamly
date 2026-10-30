import { NavLink } from 'react-router-dom'

import { t } from '@/i18n/catalog'

function navLinkClass(isActive: boolean): string {
  return isActive
    ? 'inline-flex min-h-11 items-center gap-2 rounded-full bg-primary px-5 text-sm font-semibold text-primary-foreground shadow-xs transition-colors'
    : 'inline-flex min-h-11 items-center gap-2 rounded-full bg-secondary px-5 text-sm font-semibold text-secondary-foreground transition-colors hover:bg-muted'
}

export function TripNavigation() {
  return (
    <nav
      aria-label={t('narrative.navigation.label')}
      className="fixed inset-x-0 bottom-0 z-40 flex justify-around border-t border-border bg-card/95 px-4 pb-[env(safe-area-inset-bottom)] pt-2 backdrop-blur-xs md:static md:mx-auto md:w-full md:max-w-7xl md:justify-start md:gap-2.5 md:border-0 md:bg-transparent md:px-6 md:pt-1 md:backdrop-blur-none lg:px-8"
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
      className={({ isActive }) => navLinkClass(isActive)}
    >
      {label}
    </NavLink>
  )
}
