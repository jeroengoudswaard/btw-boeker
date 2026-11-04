// Content-Security-Policy (besluit 254): verbinden mag alleen met de eigen site en de ingestelde Supabase-server.
export function cspServer(adres: string | undefined): string {
  try {
    if (adres) return new URL(adres).origin;
  } catch {
    // Ongeldig adres: de standaard.
  }
  return 'https://*.supabase.co';
}
