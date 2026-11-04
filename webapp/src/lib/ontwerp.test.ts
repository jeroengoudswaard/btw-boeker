import { readFileSync, readdirSync, statSync } from 'node:fs';
import { join } from 'node:path';
import { describe, expect, it } from 'vitest';
import { kleurVoorGroep, PALET } from './leden.ts';

// Besluiten 240 en 243: dezelfde maten als de desktop-app (architectuurtests daar).
const LETTERGROOTTES = [12, 14, 16, 24, 32];
const RONDINGEN = [4, 8, 10, 14];
// 96: de ruimte boven de grote kop (besluit 222, bereik met de duim), twee keer 48.
const RASTER = [0, 4, 8, 12, 16, 24, 32, 48, 96];

function bestanden(map: string): string[] {
  return readdirSync(map).flatMap((naam) => {
    const pad = join(map, naam);
    if (statSync(pad).isDirectory()) return bestanden(pad);
    return /\.(svelte|css)$/.test(naam) ? [pad] : [];
  });
}

function afwijkingen(eigenschap: RegExp, toegestaan: number[]): string[] {
  const fouten: string[] = [];
  for (const pad of bestanden('src')) {
    const tekst = readFileSync(pad, 'utf8');
    for (const regel of tekst.matchAll(eigenschap)) {
      for (const getal of regel[1].matchAll(/(?<![\w.(-])-?(\d+(?:\.\d+)?)px/g)) {
        if (!toegestaan.includes(Number(getal[1]))) fouten.push(`${pad}: ${regel[0].trim()}`);
      }
    }
  }
  return [...new Set(fouten)];
}

describe('ontwerpsysteem', () => {
  it('lettergroottes volgen de schaal', () => {
    expect(afwijkingen(/font-size:\s*([^;"}]+)/g, LETTERGROOTTES)).toEqual([]);
  });

  it('rondingen zijn 4 of 8 (pil 10, rondje 14)', () => {
    expect(afwijkingen(/border-radius:\s*([^;"}]+)/g, RONDINGEN)).toEqual([]);
  });

  it('afstanden liggen op het raster', () => {
    expect(afwijkingen(/(?:^|[\s;{"])(?:padding|margin|gap)(?:-[a-z]+)?:\s*([^;"}]+)/g, RASTER)).toEqual([]);
  });
});

// Besluit 243: dezelfde groep heeft in de webapp dezelfde kleur als in de desktop-app (Onderdeelkleuren.VoorGroep).
describe('groepskleur', () => {
  it.each([
    ['Bevers', 'Violet'],
    ['Welpen', 'Amber'],
    ['Scouts', 'Groenblauw'],
    ['Explorers', 'Koraal'],
    ['Junioren', 'Indigo'],
  ])('%s is %s', (groep, kleur) => {
    expect(kleurVoorGroep(groep).naam).toBe(kleur);
  });

  it('zonder groep grijs', () => {
    expect(kleurVoorGroep(null).naam).toBe('Grijs');
    expect(PALET.map((k) => k.naam)).toEqual(['Koraal', 'Amber', 'Indigo', 'Violet', 'Blauw', 'Groenblauw', 'Grijs']);
  });
});
