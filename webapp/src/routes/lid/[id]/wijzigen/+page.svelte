<script lang="ts">
  // Wijzigen = een voorstel per veld (besluit 34); de oude waarde blijft staan tot de beheerder beslist.
  import { onMount } from 'svelte';
  import { goto } from '$app/navigation';
  import { page } from '$app/state';
  import { dienIn } from '$lib/api';
  import { bewaak, laad, stand } from '$lib/gegevens.svelte';
  import { volledigeNaam } from '$lib/leden';
  import { weergaveTelefoon } from '$lib/telefoon';
  import { huidigeWaarde, voorstellenUit, type Veld } from '$lib/voorstellen';

  const lid = $derived(stand.leden.find((l) => l.id === page.params.id));
  let formulier: Partial<Record<Veld, string>> = $state({});
  let bezig = $state(false);
  let fout: string | null = $state(null);

  const isTelefoon = (veld: Veld) => veld === 'telefoon' || veld.endsWith('_telefoon') || veld.endsWith('_mobiel');
  const toon = (veld: Veld) => {
    const w = lid ? huidigeWaarde(lid, veld) : null;
    return w && isTelefoon(veld) ? weergaveTelefoon(w) : (w ?? '');
  };
  const wijzigingen = $derived(lid ? voorstellenUit(lid, formulier) : []);
  const gewijzigd = (veld: Veld) => wijzigingen.some((w) => w.veld === veld);

  function vul() {
    if (!lid) return;
    const velden: Veld[] = ['email', 'telefoon'];
    for (const n of [1, 2]) for (const d of ['naam', 'email', 'telefoon', 'mobiel']) velden.push(`voogd${n}_${d}` as Veld);
    formulier = Object.fromEntries(velden.map((v) => [v, toon(v)]));
  }

  async function verstuur(e: SubmitEvent) {
    e.preventDefault();
    if (!lid || wijzigingen.length === 0) return;
    bezig = true;
    fout = null;
    try {
      await dienIn(wijzigingen);
      await laad(true);
      await goto('/voorstellen');
    } catch (x) {
      fout = x instanceof Error ? x.message : 'Versturen mislukt.';
    } finally {
      bezig = false;
    }
  }

  onMount(async () => {
    if (await bewaak()) {
      await laad();
      vul();
    }
  });

  const groepen = [
    { titel: 'Lid', velden: [['telefoon', 'Telefoon lid', 'tel'], ['email', 'E-mail lid', 'email']] },
    ...[1, 2].map((n) => ({
      titel: `Voogd ${n}`,
      velden: [[`voogd${n}_naam`, 'Naam', 'text'], [`voogd${n}_mobiel`, 'Mobiel', 'tel'], [`voogd${n}_telefoon`, 'Telefoon', 'tel'], [`voogd${n}_email`, 'E-mail', 'email']],
    })),
  ] as { titel: string; velden: [Veld, string, string][] }[];
</script>

<main class="pagina">
  <header>
    <h1>Wijzigen</h1>
    <p>{lid ? volledigeNaam(lid) : 'Laden…'}</p>
  </header>
  <p class="melding info">Uw wijziging wordt een <strong>voorstel</strong>. De beheerder keurt het per veld goed of af; tot dan blijft de oude waarde staan.</p>

  {#if lid}
    <form onsubmit={verstuur}>
      {#each groepen as groep (groep.titel)}
        <h2 class="sectiekop">{groep.titel}</h2>
        <section class="kaart">
          {#each groep.velden as [veld, label, soort] (veld)}
            <label class="veldregel" class:gewijzigd={gewijzigd(veld)}>
              <span>{label}{#if gewijzigd(veld)} · gewijzigd{/if}</span>
              <input type={soort} inputmode={soort === 'tel' ? 'tel' : undefined} autocomplete="off" bind:value={formulier[veld]} />
              {#if gewijzigd(veld)}<span>Was: {toon(veld) || '(leeg)'}</span>{/if}
            </label>
          {/each}
        </section>
      {/each}
      {#if fout}<p class="fout" role="alert">✕ {fout}</p>{/if}
      <div class="knoppen">
        <a href={`/lid/${lid.id}`} class="knop neutraal">Annuleren</a>
        <button class="knop hoofd" disabled={bezig || wijzigingen.length === 0}>
          {wijzigingen.length > 1 ? `${wijzigingen.length} voorstellen versturen` : 'Voorstel versturen'}
        </button>
      </div>
    </form>
  {/if}
</main>

<style>
  header { padding: clamp(64px, 14vh, 120px) 8px 10px; }
  h1 { margin: 0; font-size: 34px; }
  header p { margin: 4px 0 0; color: var(--zacht); }
  .melding { margin: 6px 0 0; }
  .gewijzigd { background: var(--accent-vlak); }
  .gewijzigd > span:first-child { color: var(--accent-inkt); font-weight: 600; }
  .knoppen {
    position: sticky; bottom: 0; display: flex; gap: 10px; padding: 16px 0 calc(16px + env(safe-area-inset-bottom));
    background: linear-gradient(transparent, var(--achtergrond) 30%);
  }
  .knoppen > * { flex: 1; }
  .knoppen .hoofd { flex: 1.4; }
</style>
