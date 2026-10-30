import { BrowserRouter, Route, Routes } from 'react-router-dom'

import { AppShell } from '@/components/layout/AppShell'
import { EmailConfirmationPage } from '@/features/auth/EmailConfirmationPage'
import { LoginPage } from '@/features/auth/LoginPage'
import { RegistrationPage } from '@/features/auth/RegistrationPage'
import { PrivacyPage } from '@/features/privacy/PrivacyPage'
import { RibbonPrototypePage } from '@/features/design-system/prototype/RibbonPrototypePage'
import { Toaster } from '@/components/ui/toast'

export function App() {
  return (
    <BrowserRouter>
      <AppShell>
        <Routes>
          {import.meta.env.DEV && (
            <Route path="/register" element={<RegistrationPage />} />
          )}
          <Route path="/email/confirm" element={<EmailConfirmationPage />} />
          <Route path="/account/privacy" element={<PrivacyPage />} />
          <Route path="/*" element={<LoginPage />} />
          {import.meta.env.DEV && (
            <Route path="/__dev/ribbon" element={<RibbonPrototypePage />} />
          )}
        </Routes>
      </AppShell>
      <Toaster />
    </BrowserRouter>
  )
}
