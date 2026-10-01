import type { ReactNode } from 'react'

interface PageContainerProps {
  children: ReactNode
  className?: string
}

/** Contenitore di pagina: larghezza massima leggibile, respiro verticale
 * generoso (correttivo (4) di R11: la densita' non e' uniforme). */
export function PageContainer({ children, className }: PageContainerProps) {
  return (
    <div
      className={
        'mx-auto flex w-full max-w-md flex-1 flex-col justify-center gap-8 px-4 py-8 sm:px-6' +
        (className ? ` ${className}` : '')
      }
    >
      {children}
    </div>
  )
}
