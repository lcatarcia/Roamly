import assert from 'node:assert/strict'
import test from 'node:test'

import { collectDisplayFontUsageByComponent } from '../lib/display-font-usage.mjs'

test('counts display-font use independently for each screen component', () => {
  const usages = collectDisplayFontUsageByComponent(
    'RegistrationPage.tsx',
    `
      export function RegistrationPage() {
        return <h1 className="page-title">Registrazione</h1>
      }
      function RegistrationAcceptedPage() {
        return <h1 className="page-title numeral-display">Richiesta inviata</h1>
      }
    `,
  )

  assert.deepEqual(usages, [
    { name: 'RegistrationPage', pageTitles: 1, total: 1 },
    { name: 'RegistrationAcceptedPage', pageTitles: 1, total: 2 },
  ])
})

test('reports multiple titles within the same screen component', () => {
  const usages = collectDisplayFontUsageByComponent(
    'PrivacyPage.tsx',
    `
      export function PrivacyPage() {
        return <>
          <h1 className="page-title">Privacy</h1>
          <h2 className="page-title">Esportazione</h2>
        </>
      }
    `,
  )

  assert.deepEqual(usages, [
    { name: 'PrivacyPage', pageTitles: 2, total: 2 },
  ])
})

test('counts arrow-function page components and ignores dynamic classes and helpers', () => {
  const usages = collectDisplayFontUsageByComponent(
    'AccountPage.tsx',
    `
      export const AccountPage = () => <h1 className="page-title">Account</h1>
      export const Layout = ({ children }) => <main className={children.className}>{children}</main>
    `,
  )

  assert.deepEqual(usages, [
    { name: 'AccountPage', pageTitles: 1, total: 1 },
  ])
})
