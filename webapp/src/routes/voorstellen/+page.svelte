<script lang="ts">
  import { onMount } from 'svelte';
  import GroteKop from '#lib/onderdelen/GroteKop.svelte';
  import Onderbalk from '#lib/onderdelen/Onderbalk.svelte';
  import { bewaak, laad, openVoorstellen, stand } from '#lib/gegevens.svelte.ts';
  import { volledigeNaam } from '#lib/leden.ts';
  import { weergaveTelefoon } from '#lib/telefoon.ts';
  import { veldOmschrijving } from '#lib/voorstellen.ts';

  let tab: 'open' | 'klaar' = $state('open');
  const lijst = $derived(stand.voorstellen.filter((v) => (tab === 'open' ? v.status === 'open' : v.status !== 'open')));
  const naam = (id: string) => {
    const lid = stand.leden.find((l) => l.id === id);
    return lid ? volledigeNaam(lid) : '(lid niet zichtbaar)';
  };
  const waarde = (w: string | null) => (w === null ? '(leeg)' : w.startsWith('+') ? weergaveTelefoon(w) : w);
  const moment = (t: string) => new Date(t).toLocaleString('nl-NL', { day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' });
  const status = {
    open: { tekst: 'Wacht', klasse: 'wacht' },
    goedgekeurd: { tekst: '✓ Goedgekeurd', klasse: 'goed' },
    afgewezen: { tekst: '✕ Afgewezen', klasse: 'af' },
  };

  onMount(async () => {
    if (await bewaak()) await laad(true);
  });
</script>

<main class="pagina">
  <GroteKop titel="Voorstellen" onder={`${openVoorstellen().length} wachten op de beheerder`} />
  <div role="tablist" aria-label="Soort" class="tabs">
    <button role="tab" aria-selected={tab === 'open'} onclick={() => (tab = 'open')}>Open · {openVoorstellen().length}</button>
    <button role="tab" aria-selected={tab === 'klaar'} onclick={() => (tab = 'klaar')}>Afgehandeld</button>
  </div>

  {#if stand.geladen && lijst.length === 0}
    <section class="kaart leeg">
      <p>{tab === 'open' ? 'Geen open voorstellen. Wijzig iets bij een lid; het komt hier te staan tot de beheerder beslist.' : 'Nog niets afgehandeld.'}</p>
    </section>
  {/if}

  {#each lijst as v (v.id)}
    <article class="kaart voorstel">
      <div class="rij">
        <span class="lid">{naam(v.lid_id)}</span>
        <span class="status {status[v.status].klasse}">{status[v.status].tekst}</span>
      </div>
      <div class="zacht">{veldOmschrijving(v.veld)}</div>
      <div class="wijziging"><s>{waarde(v.oud)}</s> <span aria-hidden="true">→</span><span class="verborgen">wordt</span> <strong>{waarde(v.nieuw)}</strong></div>
      {#if v.toelichting}<div class="zacht">{v.toelichting}</div>{/if}
      <div class="zacht klein">{moment(v.ingediend_op)}</div>
    </article>
  {/each}
</main>
<Onderbalk open={openVoorstellen().length} />

<style>
  .tabs { display: flex; padding: 4px; border-radius: 8px; background: var(--veld); margin-bottom: 16px; }
  .tabs button { flex: 1; min-height: 40px; border-radius: 8px; border: 0; background: transparent; color: var(--zacht); cursor: pointer; }
  .tabs button[aria-selected='true'] { background: var(--kaart); color: var(--tekst); font-weight: 600; }
  .voorstel { padding: 16px 24px; display: flex; flex-direction: column; gap: 8px; margin-bottom: 8px; }
  .rij { display: flex; align-items: center; gap: 8px; }
  .lid { flex: 1; font-weight: 600; }
  .status { padding: 4px 8px; border-radius: 8px; font-size: 12px; font-weight: 600; }
  .wacht { background: var(--let-op-vlak); color: var(--let-op-inkt); }
  .goed { background: var(--accent-vlak); color: var(--accent-inkt); }
  .af { background: var(--veld); color: var(--tekst); }
  .wijziging { overflow-wrap: anywhere; }
  s { color: var(--zacht); }
  .zacht { color: var(--zacht); font-size: 14px; }
  .klein { font-size: 12px; }
  .leeg { padding: 24px; color: var(--zacht); }
  .leeg p { margin: 0; }
</style>
