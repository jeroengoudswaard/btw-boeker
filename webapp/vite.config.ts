import adapter from '@sveltejs/adapter-static';
import { sveltekit } from '@sveltejs/kit/vite';
import { vitePreprocess } from '@sveltejs/vite-plugin-svelte';
import { defineConfig } from 'vitest/config';

/**
 * Statische site voor Cloudflare Pages (besluiten 226 en 227); alles draait in de browser.
 * Sinds SvelteKit 3 staan de instellingen hier (`svelte.config.js` wordt niet meer gelezen).
 * BASE_PATH (bijv. `/verenigd/app`) bouwt de site voor een submap, zoals GitHub Pages (besluit 254).
 */
const basis = (process.env.BASE_PATH ?? '') as '' | `/${string}`;
export default defineConfig({
  plugins: [
    sveltekit({
      // Geen terugvalpagina: elke pagina is vooraf gebouwd (src/routes/+layout.ts).
      adapter: adapter(),
      preprocess: vitePreprocess(),
      paths: { base: basis },
    }),
  ],
  test: { include: ['src/**/*.test.ts'] },
});
