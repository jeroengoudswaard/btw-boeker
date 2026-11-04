// End-to-end: inloggen-stappen, ledenlijst, lid, voogd kiezen en een voorstel indienen (besluiten 33, 34, 222–224, 231).
import { expect, test, type Page } from '@playwright/test';

const API = 'http://127.0.0.1:54321';
const SLEUTEL = 'sb-127-auth-token';

const b64 = (o: object) => Buffer.from(JSON.stringify(o)).toString('base64url');
const jwt = (aal: 'aal1' | 'aal2') =>
  `${b64({ alg: 'HS256', typ: 'JWT' })}.${b64({ sub: 'u1', aal, amr: [{ method: 'password', timestamp: 1 }], exp: 4102444800, role: 'authenticated' })}.c2lnbmF0dXVy`;

async function sessie(page: Page, aal: 'aal1' | 'aal2') {
  const waarde = {
    access_token: jwt(aal), refresh_token: 'r', token_type: 'bearer', expires_in: 3600, expires_at: 4102444800,
    user: { id: 'u1', email: 'akela@example.org', aud: 'authenticated', factors: [{ id: 'f1', factor_type: 'totp', status: 'verified' }] },
  };
  await page.addInitScript(([k, v]) => localStorage.setItem(k, v), [SLEUTEL, JSON.stringify(waarde)]);
}

const leden = [
  { id: 'l1', lidnummer: '1', voornaam: 'Bram', tussenvoegsel: 'de', achternaam: 'Jong', geboortedatum: '2018-03-01', email: null, telefoon: '+31633333333',
    voogden: [{ volgnummer: 1, naam: 'Petra Jong', email: 'p@example.org', telefoon: null, mobiel: '+31611111111' },
              { volgnummer: 2, naam: 'Kees Jong', email: null, telefoon: null, mobiel: '+31622222222' }],
    lid_speltakken: [{ speltak: 'Welpen', rol: 'Jeugdlid', is_leiding: false }] },
  { id: 'l2', lidnummer: '2', voornaam: 'Anouk', tussenvoegsel: null, achternaam: 'Bakker', geboortedatum: '2017-01-01', email: null, telefoon: null,
    voogden: [], lid_speltakken: [{ speltak: 'Welpen', rol: 'Jeugdlid', is_leiding: false }] },
  { id: 'l3', lidnummer: '3', voornaam: 'Akela', tussenvoegsel: null, achternaam: 'Test', geboortedatum: null, email: 'akela@example.org', telefoon: null,
    voogden: [], lid_speltakken: [{ speltak: 'Welpen', rol: 'Leiding', is_leiding: true }] },
];

async function server(page: Page) {
  const ingediend: unknown[] = [];
  const voorstellen: object[] = [];
  await page.route(`${API}/**`, async (route) => {
    const verzoek = route.request();
    const pad = new URL(verzoek.url()).pathname;
    const json = (body: unknown, status = 200) => route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(body) });
    if (pad === '/rest/v1/leden') return json(leden);
    if (pad === '/rest/v1/speltakken') return json([{ naam: 'Welpen' }]);
    if (pad === '/rest/v1/voorstellen' && verzoek.method() === 'GET') return json(voorstellen);
    if (pad === '/rest/v1/voorstellen' && verzoek.method() === 'POST') {
      const body = verzoek.postDataJSON();
      ingediend.push(...body);
      for (const v of body) voorstellen.push({ ...v, id: `v${voorstellen.length}`, status: 'open', ingediend_op: '2026-10-31T20:41:00Z', afgehandeld_op: null, toelichting: null });
      return route.fulfill({ status: 201, body: '' });
    }
    if (pad === '/auth/v1/user') return json({ id: 'u1', email: 'akela@example.org' });
    if (pad === '/auth/v1/factors') return json({ totp: [], all: [] });
    return json({ message: 'niet nagebootst: ' + pad }, 404);
  });
  return ingediend;
}

test('zonder sessie: het inlogscherm', async ({ page }) => {
  await server(page);
  await page.goto('/');
  await expect(page).toHaveURL(/\/inloggen$/);
  await expect(page.getByRole('heading', { name: 'Verenigd' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Inloggen' })).toBeVisible();
});

test('alleen wachtwoord (aal1): eerst de code uit de app', async ({ page }) => {
  await server(page);
  await sessie(page, 'aal1');
  await page.goto('/');
  await expect(page).toHaveURL(/\/mfa$/);
  await expect(page.getByRole('heading', { name: 'Bevestig dat u het bent' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Bevestigen' })).toBeDisabled();
});

test('met MFA: ledenlijst met leiding apart en zoeken', async ({ page }) => {
  await server(page);
  await sessie(page, 'aal2');
  await page.goto('/');
  await expect(page.getByRole('heading', { level: 1 })).toContainText('Welpen');
  await expect(page.getByRole('region', { name: 'Leiding' })).toContainText('Akela Test');
  await expect(page.getByRole('region', { name: 'A' })).toContainText('Anouk Bakker');
  await expect(page.getByRole('region', { name: 'B' })).toContainText('Bram de Jong');
  await page.getByPlaceholder('Zoek op naam').fill('bram');
  await expect(page.getByRole('region', { name: 'A' })).toHaveCount(0);
  await expect(page.getByRole('link', { name: /Bram de Jong/ })).toBeVisible();
});

test('lid: bellen vraagt eerst wie (voogden en het lid zelf), wijzigen wordt een voorstel', async ({ page }) => {
  const ingediend = await server(page);
  await sessie(page, 'aal2');
  await page.goto('/');
  await page.getByRole('link', { name: /Bram de Jong/ }).click();
  await expect(page.getByRole('heading', { name: 'Bram de Jong' })).toBeVisible();

  await page.getByRole('button', { name: 'Bellen' }).click();
  const blad = page.getByRole('dialog', { name: 'Bellen naar' });
  await expect(blad.getByRole('link')).toHaveCount(3);
  await expect(blad.getByRole('link', { name: /Petra Jong/ })).toHaveAttribute('href', 'tel:+31611111111');
  await expect(blad.getByRole('link', { name: /Lid zelf/ })).toHaveAttribute('href', 'tel:+31633333333'); // besluit 249
  await blad.getByRole('button', { name: 'Annuleren' }).click();

  await page.getByRole('link', { name: 'Wijzigen' }).click();
  const mobiel = page.getByRole('textbox', { name: /^Mobiel/ }).first();
  await expect(mobiel).toHaveValue('06 11111111');
  await mobiel.fill('06 87654321');
  await expect(page.getByText('Was: 06 11111111')).toBeVisible();
  await page.getByRole('button', { name: 'Voorstel versturen' }).click();

  await expect(page).toHaveURL(/\/voorstellen$/);
  expect(ingediend).toEqual([{ lid_id: 'l1', veld: 'voogd1_mobiel', oud: '+31611111111', nieuw: '+31687654321' }]);
  await expect(page.getByRole('article')).toContainText('Voogd 1 · mobiel');
  await expect(page.getByRole('article')).toContainText('06 87654321');
});
