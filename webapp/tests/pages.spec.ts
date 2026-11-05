import { expect, test } from '@playwright/test';

// Besluit 254: de webapp onder /verenigd/app op GitHub Pages, nog zonder koppeling met Supabase.
test('stuurt naar inloggen binnen het basispad en meldt dat de server nog niet is ingericht', async ({ page }) => {
  await page.goto('/verenigd/app/');
  await expect(page).toHaveURL(/\/verenigd\/app\/inloggen$/);
  await expect(page.getByTestId('niet-ingericht')).toBeVisible();
  await expect(page.getByRole('button', { name: 'Inloggen' })).toBeDisabled();
});

test('elke pagina bestaat als bestand, dus een directe link werkt', async ({ request }) => {
  for (const pad of ['', 'inloggen', 'voorstellen', 'account', 'wachtwoord', 'mfa', 'mfa/instellen', 'lid', 'lid/wijzigen']) {
    const antwoord = await request.get(`/verenigd/app/${pad}`);
    expect(antwoord.status(), pad).toBe(200);
  }
});

test('manifest en pictogrammen staan onder het basispad', async ({ page, request }) => {
  await page.goto('/verenigd/app/inloggen');
  const manifest = await page.evaluate(() => (document.querySelector('link[rel="manifest"]') as HTMLLinkElement).href);
  expect(new URL(manifest).pathname).toBe('/verenigd/app/manifest.webmanifest');
  const inhoud = await (await request.get(manifest)).json();
  expect(inhoud.start_url).toBe('./');
  // Besluit 261: de balk van de geïnstalleerde app in het donkerblauw van het app-icoon.
  expect(inhoud.theme_color).toBe('#0B1F4B');
  expect((await request.get(`/verenigd/app/${inhoud.icons[0].src}`)).status()).toBe(200);
});

test('heeft een Content-Security-Policy in de pagina', async ({ page }) => {
  await page.goto('/verenigd/app/inloggen');
  const csp = await page.locator('meta[http-equiv="Content-Security-Policy"]').getAttribute('content');
  expect(csp).toContain("default-src 'self'");
  expect(csp).toContain('https://*.supabase.co');
});

test('het inlogscherm toont het echte app-icoon', async ({ page }) => {
  await page.goto('/verenigd/app/inloggen');
  const logo = page.locator('main img.logo');
  await expect(logo).toBeVisible();
  expect(await logo.evaluate((img: HTMLImageElement) => new URL(img.src).pathname)).toBe('/verenigd/app/icoon-192.png');
  expect(await logo.evaluate((img: HTMLImageElement) => img.naturalWidth)).toBe(192);
});
