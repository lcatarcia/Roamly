import { useEffect, useState } from 'react'
import { MoonIcon, SunIcon } from 'lucide-react'

import { Button } from '@/components/ui/button'
import { t } from '@/i18n/catalog'
import { applyTheme, getInitialTheme, setTheme, type Theme } from '@/lib/theme'

/** Toggle di tema con persistenza (localStorage) e rispetto di
 * `prefers-color-scheme` al primo avvio (vedi src/lib/theme.ts). */
export function ThemeToggle() {
  const [theme, setThemeState] = useState<Theme>(() => getInitialTheme())

  useEffect(() => {
    applyTheme(theme)
  }, [theme])

  function toggle() {
    const next: Theme = theme === 'light' ? 'dark' : 'light'
    setTheme(next)
    setThemeState(next)
  }

  return (
    <Button
      type="button"
      variant="ghost"
      size="icon-lg"
      className="size-11"
      onClick={toggle}
      aria-label={
        theme === 'light' ? t('neutral.theme.enableDark') : t('neutral.theme.enableLight')
      }
    >
      {theme === 'light' ? <MoonIcon aria-hidden="true" /> : <SunIcon aria-hidden="true" />}
    </Button>
  )
}
