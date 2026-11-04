import { describe, expect, it } from 'vitest';
import { cspServer } from './csp.ts';

describe('cspServer', () => {
  it('neemt alleen de oorsprong van het ingestelde adres', () => {
    expect(cspServer('https://abcd.supabase.co/rest/v1')).toBe('https://abcd.supabase.co');
  });
  it('valt zonder of bij een ongeldig adres terug op Supabase', () => {
    expect(cspServer(undefined)).toBe('https://*.supabase.co');
    expect(cspServer('')).toBe('https://*.supabase.co');
    expect(cspServer('geen adres')).toBe('https://*.supabase.co');
  });
});
