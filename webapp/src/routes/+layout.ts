// Een app in de browser (statische site, besluit 226): geen server-rendering.
// Elke pagina wordt vooraf als leeg HTML-bestand gebouwd, zodat ook GitHub Pages (zonder terugvalpagina per map,
// besluit 254) een directe link of verversen kan tonen. Daarom staat het lid in de zoekparameter (`/lid?id=…`).
export const ssr = false;
export const prerender = true;
