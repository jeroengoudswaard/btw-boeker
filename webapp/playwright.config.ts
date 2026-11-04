import { defineConfig } from '@playwright/test';

// End-to-end tests tegen de gebouwde site; Supabase wordt in de test nagebootst (page.route), dus geen echte server.
export default defineConfig({
  testDir: 'tests',
  testIgnore: 'pages.spec.ts',
  use: { baseURL: 'http://127.0.0.1:4173', viewport: { width: 390, height: 844 }, locale: 'nl-NL' },
  webServer: {
    command: 'npm run build && npm run preview -- --host 127.0.0.1 --port 4173 --strictPort',
    url: 'http://127.0.0.1:4173',
    env: { VITE_SUPABASE_URL: 'http://127.0.0.1:54321', VITE_SUPABASE_PUBLISHABLE_KEY: 'test' },
    reuseExistingServer: false,
    timeout: 120_000,
  },
});
