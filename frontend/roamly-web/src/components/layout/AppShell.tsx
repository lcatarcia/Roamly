import type { ReactNode } from 'react'

import { ThemeToggle } from '@/components/layout/ThemeToggle'
import { t } from '@/i18n/catalog'

interface AppShellProps {
  children: ReactNode
}

/**
 * Involucro dell'intera app: fondo/testo dai token semantici (mai letterali,
 * R12), header minimale con il toggle di tema. Nessuna navigazione qui:
 * l'MVP di Phase 0 e' solo la schermata di login, la navigazione entra
 * in Phase 1 (1.8, bottom nav mobile).
 */
export function AppShell({ children }: AppShellProps) {
  return (
    <div className="flex min-h-screen flex-col bg-background text-foreground">
      <header className="flex items-center justify-between border-b border-border px-4 py-3 sm:px-6">
        <span className="text-sm font-semibold uppercase tracking-[0.08em] text-muted-foreground">
          {t('neutral.app.brand')}
        </span>
        <ThemeToggle />
      </header>
      <main className="flex flex-1 flex-col">{children}</main>
    </div>
  )
}
