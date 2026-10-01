import { BrowserRouter, Route, Routes } from 'react-router-dom'

import { AppShell } from '@/components/layout/AppShell'
import { LoginPage } from '@/features/auth/LoginPage'
import { RibbonPrototypePage } from '@/features/design-system/prototype/RibbonPrototypePage'
import { Toaster } from '@/components/ui/toast'

export function App() {
  return (
    <BrowserRouter>
      <AppShell>
        <Routes>
          <Route path="/" element={<LoginPage />} />
          {import.meta.env.DEV && (
            <Route path="/__dev/ribbon" element={<RibbonPrototypePage />} />
          )}
        </Routes>
      </AppShell>
      <Toaster />
    </BrowserRouter>
  )
}
