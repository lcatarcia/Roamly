import { lazy, Suspense } from 'react'
import { BrowserRouter, Route, Routes } from 'react-router-dom'

import { AppShell } from '@/components/layout/AppShell'
import { EmailConfirmationPage } from '@/features/auth/EmailConfirmationPage'
import { LoginPage } from '@/features/auth/LoginPage'
import { RegistrationPage } from '@/features/auth/RegistrationPage'
import { PrivacyPage } from '@/features/privacy/PrivacyPage'
import { RibbonPrototypePage } from '@/features/design-system/prototype/RibbonPrototypePage'
import { Toaster } from '@/components/ui/toast'

const PlacesWishlistPrototypePage = import.meta.env.DEV
  ? lazy(() => import('@/features/places/prototype/PlacesWishlistPrototypePage'))
  : null

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
          {import.meta.env.DEV && PlacesWishlistPrototypePage && (
            <Route
              path="/__dev/places-wishlist"
              element={<Suspense fallback={null}><PlacesWishlistPrototypePage /></Suspense>}
            />
          )}
        </Routes>
      </AppShell>
      <Toaster />
    </BrowserRouter>
  )
}
