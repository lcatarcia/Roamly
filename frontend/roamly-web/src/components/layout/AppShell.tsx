import type { ReactNode } from 'react'

import { ThemeToggle } from '@/components/layout/ThemeToggle'
import { t } from '@/i18n/catalog'

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
          <div className="flex items-center gap-3">
            <span
              aria-hidden="true"
              className="inline-flex size-9 items-center justify-center rounded-xl bg-primary text-primary-foreground shadow-xs"
            >
              <svg
                viewBox="0 0 24 24"
                fill="none"
                stroke="currentColor"
                strokeWidth="2"
                strokeLinecap="round"
                strokeLinejoin="round"
                className="size-5"
              >
                <path d="M3 16V9a2 2 0 0 1 2-2h9l4 4h2a1 1 0 0 1 1 1v4" />
                <circle cx="7.5" cy="16.5" r="2.5" />
                <circle cx="16.5" cy="16.5" r="2.5" />
                <path d="M10 16h4" />
                <path d="M6 11h6" />
              </svg>
            </span>
            <span className="text-base font-bold tracking-[0.04em] text-foreground">
              {t('neutral.app.brand')}
            </span>
          </div>
          <ThemeToggle />
        </div>
      </header>
      <main className="flex flex-1 flex-col">{children}</main>
    </div>
  )
}
