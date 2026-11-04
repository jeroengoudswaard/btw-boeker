// Draait alleen bij het bouwen (vooraf gebouwde pagina's, src/routes/+layout.ts): vult de CSP in app.html in.
import type { Handle } from '@sveltejs/kit/hooks';
import { cspServer } from '#lib/csp.ts';

const server = cspServer(import.meta.env.VITE_SUPABASE_URL);

export const handle: Handle = ({ event, resolve }) =>
  resolve(event, { transformPageChunk: ({ html }) => html.replaceAll('__CSP_SERVER__', server) });
