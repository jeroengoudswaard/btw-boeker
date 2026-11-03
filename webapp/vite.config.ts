import adapter from '@sveltejs/adapter-static';
import { sveltekit } from '@sveltejs/kit/vite';
import { vitePreprocess } from '@sveltejs/vite-plugin-svelte';
import { defineConfig } from 'vitest/config';

/**
 * Statische site voor Cloudflare Pages (besluiten 226 en 227); alles draait in de browser.
 * Sinds SvelteKit 3 staan de instellingen hier (`svelte.config.js` wordt niet meer gelezen).
 */
export default defineConfig({
  plugins: [
    sveltekit({
      adapter: adapter({ fallback: 'index.html' }),
      preprocess: vitePreprocess(),
    }),
  ],
  test: { include: ['src/**/*.test.ts'] },
});
