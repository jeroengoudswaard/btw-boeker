// Telefoonnummers zoals in de desktop-app (besluit 32): opslaan als +31612345678, tonen als 06 12345678.
// Een nummer dat niet herkend wordt, blijft ongewijzigd.

export interface Telefoonresultaat {
  waarde: string;
  herkend: boolean;
}

export function normaliseerTelefoon(invoer: string): Telefoonresultaat {
  const tekst = invoer.trim().replaceAll('(0)', '');
  let cijfers = [...tekst].filter((t) => !' -./()'.includes(t)).join('');
  if (cijfers.startsWith('00')) cijfers = '+' + cijfers.slice(2);

  const kern = cijfers.startsWith('+') ? cijfers.slice(1) : cijfers;
  if (kern.length === 0 || !/^[0-9]+$/.test(kern)) return { waarde: invoer, herkend: false };

  let internationaal: string | null = null;
  if (cijfers.startsWith('+31')) internationaal = kern.length === 11 ? cijfers : null;
  else if (cijfers.startsWith('+32')) internationaal = kern.length === 10 || kern.length === 11 ? cijfers : null;
  else if (cijfers.startsWith('+')) internationaal = kern.length >= 8 && kern.length <= 15 ? cijfers : null;
  else if (cijfers.startsWith('0') && cijfers.length === 10) internationaal = '+31' + cijfers.slice(1);
  else if (cijfers.startsWith('6') && cijfers.length === 9) internationaal = '+31' + cijfers;

  return internationaal === null ? { waarde: invoer, herkend: false } : { waarde: internationaal, herkend: true };
}

export function weergaveTelefoon(opgeslagen: string): string {
  if (opgeslagen.startsWith('+316') && opgeslagen.length === 12) return '06 ' + opgeslagen.slice(4);
  if (opgeslagen.startsWith('+31') && opgeslagen.length === 12) return '0' + opgeslagen.slice(3);
  if (opgeslagen.startsWith('+32')) return '+32 ' + opgeslagen.slice(3);
  return opgeslagen;
}
