import { defineConfig } from '@playwright/test'

/**
 * One end-to-end flow against the real stack: the API (with its own idempotent demo seed) and the
 * Vite dev server, exactly as a reviewer would run them. `reuseExistingServer` lets this attach to
 * servers already running locally instead of racing a second copy on the same ports.
 */
export default defineConfig({
  testDir: './tests',
  fullyParallel: false,
  retries: 0,
  reporter: 'list',
  use: {
    baseURL: 'http://localhost:5173',
    trace: 'retain-on-failure',
  },
  webServer: [
    {
      command: 'dotnet run --project ../../src/QuoteManager.Api',
      url: 'http://localhost:5080/health',
      reuseExistingServer: true,
      timeout: 60_000,
    },
    {
      command: 'npm run dev --prefix ../../src/QuoteManager.Web',
      url: 'http://localhost:5173',
      reuseExistingServer: true,
      timeout: 30_000,
    },
  ],
})
