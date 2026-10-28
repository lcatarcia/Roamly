import type { ReactNode } from 'react'

interface PageContainerProps {
  children: ReactNode
  className?: string
  size?: 'form' | 'wide' | 'dashboard'
}

/** Contenitore di pagina: larghezza massima leggibile, respiro verticale
 * generoso (correttivo (4) di R11: la densita' non e' uniforme). */
export function PageContainer({ children, className, size = 'form' }: PageContainerProps) {
  const maxWidthClass = {
    form: 'max-w-md',
    wide: 'max-w-6xl',
    dashboard: 'max-w-7xl',
  }[size]

  return (
    <div
      className={
        `mx-auto flex w-full min-w-0 flex-1 flex-col justify-center gap-8 px-4 py-8 sm:px-6 ${maxWidthClass}` +
        (className ? ` ${className}` : '')
      }
    >
      {children}
    </div>
  )
}
