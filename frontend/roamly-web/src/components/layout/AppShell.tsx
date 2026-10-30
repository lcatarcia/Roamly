import type { ReactNode } from 'react'

import { BrandLogo } from '@/components/brand/BrandLogo'
import { ThemeToggle } from '@/components/layout/ThemeToggle'

interface AppShellProps {
  children: ReactNode
}

/**
 * Involucro dell'intera app: fondo/testo dai token semantici (mai letterali,
 * R12), header minimale con il toggle di tema. Nessuna navigazione qui:
 * Phase 0 comprende accesso, registrazione e conferma email; la navigazione entra
 * in Phase 1 (1.8, bottom nav mobile).
 */
export function AppShell({ children }: AppShellProps) {
  return (
    <div className="flex min-h-screen flex-col bg-background text-foreground">
      <header className="sticky top-0 z-30 border-b border-border bg-card/95 backdrop-blur-xs">
        <div className="mx-auto flex w-full max-w-7xl items-center justify-between px-4 py-3 sm:px-6 lg:px-8">
          <BrandLogo className="h-11 w-44 sm:h-14 sm:w-56" />
          <ThemeToggle />
        </div>
      </header>
      <main className="flex flex-1 flex-col">{children}</main>
    </div>
  )
}
