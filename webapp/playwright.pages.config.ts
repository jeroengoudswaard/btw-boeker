import { defineConfig } from '@playwright/test';

// Besluit 254: de build voor GitHub Pages (basispad /verenigd/app, nog zonder Supabase-server).
export default defineConfig({
  testDir: 'tests',
  testMatch: 'pages.spec.ts',
  use: { baseURL: 'http://127.0.0.1:4174', viewport: { width: 390, height: 844 }, locale: 'nl-NL' },
  webServer: {
    command: 'npm run build && npm run preview -- --host 127.0.0.1 --port 4174 --strictPort',
    url: 'http://127.0.0.1:4174/verenigd/app/',
    env: { BASE_PATH: '/verenigd/app', VITE_SUPABASE_URL: '', VITE_SUPABASE_PUBLISHABLE_KEY: '' },
    reuseExistingServer: false,
    timeout: 120_000,
  },
});
