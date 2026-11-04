import { describe, expect, it } from 'vitest';
import { metBasis, zonderBasis } from './paden.ts';

// Besluit 254: de webapp draait ook onder een basispad (GitHub Pages: /verenigd/app).
describe('paden met en zonder basispad', () => {
  it('zet het basispad voor een pad binnen de app', () => {
    expect(metBasis('', '/voorstellen')).toBe('/voorstellen');
    expect(metBasis('/verenigd/app', '/')).toBe('/verenigd/app/');
    expect(metBasis('/verenigd/app', '/lid/abc/wijzigen')).toBe('/verenigd/app/lid/abc/wijzigen');
    expect(metBasis('/verenigd/app', '/mfa?verder=/wachtwoord')).toBe('/verenigd/app/mfa?verder=/wachtwoord');
  });

  it('haalt het basispad van de adresbalk af', () => {
    expect(zonderBasis('', '/lid/1')).toBe('/lid/1');
    expect(zonderBasis('/verenigd/app', '/verenigd/app/voorstellen')).toBe('/voorstellen');
    expect(zonderBasis('/verenigd/app', '/verenigd/app')).toBe('/');
    expect(zonderBasis('/verenigd/app', '/elders')).toBe('/elders');
  });

  it('accepteert alleen een eigen pad als vervolgstap (geen open redirect)', async () => {
    const { isEigenPad } = await import('./paden.ts');
    expect(isEigenPad('/wachtwoord')).toBe(true);
    expect(isEigenPad('//evil.example')).toBe(false);
    expect(isEigenPad('https://evil.example')).toBe(false);
    expect(isEigenPad(null)).toBe(false);
  });
});
