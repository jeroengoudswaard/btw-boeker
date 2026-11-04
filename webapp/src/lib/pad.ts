// Koppeling van paden.ts aan het basispad van SvelteKit (`paths.base`, uit BASE_PATH bij het bouwen).
import { resolve } from '$app/paths';
import { metBasis, zonderBasis } from './paden.ts';

/** Het basispad, bijv. '' of '/verenigd/app'. */
export const basis: string = (resolve as (p: string) => string)('').replace(/\/$/, '');

/** Pad binnen de app met basispad, voor `href` en `goto`. */
export const pad = (p: string): string => metBasis(basis, p);

/** Pad uit de adresbalk zonder basispad. */
export const huidig = (pathname: string): string => zonderBasis(basis, pathname);
