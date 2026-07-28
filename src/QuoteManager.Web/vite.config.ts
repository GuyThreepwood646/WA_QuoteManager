import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// The production build emits straight into the API's wwwroot so a Release run of the API serves
// the SPA from the same origin and the same process. That is what makes the demo one command and
// one URL, with no CORS configuration and no second server to start.
const apiOrigin = process.env.API_ORIGIN ?? 'http://localhost:5080'

export default defineConfig({
  plugins: [react()],
  build: {
    outDir: '../QuoteManager.Api/wwwroot',
    emptyOutDir: true,
  },
  server: {
    port: 5173,
    // During development the SPA is served by Vite and proxied to the API, so the browser still
    // sees a single origin and the code never needs an environment-dependent base URL.
    proxy: {
      '/api': { target: apiOrigin, changeOrigin: true },
      '/health': { target: apiOrigin, changeOrigin: true },
    },
  },
})
