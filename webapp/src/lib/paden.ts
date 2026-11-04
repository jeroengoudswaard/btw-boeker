// Paden binnen de app (besluit 254): de site draait in de root (Cloudflare, lokaal) of onder een basispad
// (GitHub Pages: /verenigd/app). Overal in de code staan paden zonder basispad, bijv. '/voorstellen'.

/** Zet het basispad voor een pad binnen de app (`p` begint met '/'). */
export function metBasis(basis: string, p: string): string {
  return basis + p;
}

/** Haalt het basispad van een pad uit de adresbalk af, zodat de app het kan vergelijken. */
export function zonderBasis(basis: string, pathname: string): string {
  if (!basis || !pathname.startsWith(basis)) return pathname;
  return pathname.slice(basis.length) || '/';
}

/** Alleen een pad binnen deze site mag een vervolgstap zijn (geen '//andere-site' of volledige URL). */
export function isEigenPad(p: string | null | undefined): p is string {
  return typeof p === 'string' && p.startsWith('/') && !p.startsWith('//') && !p.startsWith('/\\');
}
