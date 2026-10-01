import path from 'node:path'

import basicSsl from '@vitejs/plugin-basic-ssl'
import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// Porta reale letta da src/Roamly.Api/Properties/launchSettings.json (profilo "https").
// Vedi ADR-0003: frontend e API devono stare same-site in produzione; in sviluppo
// otteniamo lo stesso effetto con il proxy del dev server verso il backend HTTPS.
const apiTarget = 'https://localhost:7187'

// https://vite.dev/config/
export default defineConfig({
  // Il cookie di sessione e' Secure: il browser lo scarta se il dev server e' su http.
  // basicSsl genera un certificato self-signed al volo cosi' anche il frontend gira su
  // https://localhost, mantenendo il cookie Secure valido durante lo sviluppo (ADR-0003).
  plugins: [react(), tailwindcss(), basicSsl()],
  resolve: {
    alias: {
      '@': path.resolve(import.meta.dirname, './src'),
    },
  },
  server: {
    // https gestito interamente dal plugin basicSsl sopra: non serve
    // ripeterlo qui (la sua opzione `server.https: true` letterale non
    // e' tipizzata da Vite come valore booleano valido per tsc -b).
    proxy: {
      '/api': {
        target: apiTarget,
        changeOrigin: true,
        secure: false, // certificato di sviluppo ASP.NET Core (dotnet dev-certs) e' self-signed
      },
    },
  },
})
