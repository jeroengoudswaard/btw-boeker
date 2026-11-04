<script lang="ts">
  // Eerste scherm: de ledenlijst van de eigen speltak (besluit 224).
  import { onMount } from 'svelte';
  import Avatar from '#lib/onderdelen/Avatar.svelte';
  import GroteKop from '#lib/onderdelen/GroteKop.svelte';
  import Onderbalk from '#lib/onderdelen/Onderbalk.svelte';
  import { bewaak, laad, openVoorstellen, stand } from '#lib/gegevens.svelte.ts';
  import { groepeer, initialen, isLeiding, leeftijd, volledigeNaam, type Lid } from '#lib/leden.ts';

  let zoek = $state('');
  const groepen = $derived(groepeer(stand.leden, stand.speltak, zoek));
  const aantal = $derived(stand.leden.filter((l) => l.functies.some((f) => f.speltak === stand.speltak)));
  const leidingAantal = $derived(aantal.filter((l) => isLeiding(l, stand.speltak)).length);

  function onderregel(lid: Lid): string {
    if (isLeiding(lid, stand.speltak)) return lid.functies.find((f) => f.speltak === stand.speltak)?.rol ?? 'Leiding';
    const jaren = leeftijd(lid.geboortedatum);
    const voogden = lid.voogden.length;
    return [jaren === null ? null : `${jaren} jaar`, voogden === 1 ? '1 voogd' : `${voogden} voogden`].filter(Boolean).join(' · ');
  }

  onMount(async () => {
    if (await bewaak()) await laad();
  });
</script>

<main class="pagina">
  <GroteKop onder={stand.geladen ? `${aantal.length - leidingAantal} leden · ${leidingAantal} leiding` : 'Laden…'}>
    {#snippet titel()}
      {#if stand.speltakken.length > 1}
        <label class="speltakkeuze">
          <span class="verborgen">Speltak kiezen</span>
          <select bind:value={stand.speltak}>
            {#each stand.speltakken as s (s)}<option value={s}>{s}</option>{/each}
          </select>
        </label>
      {:else}
        {stand.speltak || 'Leden'}
      {/if}
    {/snippet}
  </GroteKop>

  <label class="zoeken">
    <svg aria-hidden="true" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><circle cx="11" cy="11" r="6.5"/><path d="M16 16l4.5 4.5"/></svg>
    <span class="verborgen">Zoeken</span>
    <input type="search" placeholder="Zoek op naam" bind:value={zoek} />
  </label>

  {#if stand.fout}<p class="melding let-op" role="alert">⚠ {stand.fout}</p>{/if}

  {#if stand.geladen && groepen.length === 0}
    <section class="kaart leeg">
      <h2>{zoek ? 'Niemand gevonden' : 'Nog geen leden'}</h2>
      <p>{zoek ? 'Probeer een andere naam.' : 'De beheerder zet de leden van uw speltak klaar vanuit Verenigd.'}</p>
    </section>
  {/if}

  {#each groepen as groep (groep.titel)}
    <h2 class="sectiekop">{groep.titel}</h2>
    <section class="kaart lijst" aria-label={groep.titel}>
      {#each groep.leden as lid (lid.id)}
        <a href={`/lid/${lid.id}`} class="regel">
          <Avatar groep={stand.speltak} tekst={initialen(lid)} />
          <span class="tekst">
            <span class="naam">{volledigeNaam(lid)}</span>
            <span class="onder">{onderregel(lid)}</span>
          </span>
          {#if openVoorstellen(lid.id).length}<span class="label">Voorstel open</span>{/if}
        </a>
      {/each}
    </section>
  {/each}
</main>
<Onderbalk open={openVoorstellen().length} />

<style>
  .speltakkeuze select {
    font: inherit; font-size: 32px; font-weight: 600; border: 0; background: transparent; color: var(--tekst);
    padding: 0 32px 0 0; appearance: none; min-height: 44px; cursor: pointer;
    background-image: linear-gradient(45deg, transparent 50%, var(--zacht) 50%), linear-gradient(135deg, var(--zacht) 50%, transparent 50%);
    background-position: calc(100% - 14px) 55%, calc(100% - 8px) 55%; background-size: 6px 6px; background-repeat: no-repeat;
  }
  .zoeken { display: flex; align-items: center; gap: 8px; min-height: 46px; padding: 0 16px; border-radius: 8px; background: var(--veld); color: var(--zacht); }
  .zoeken input { flex: 1; min-width: 0; border: 0; outline: none; background: transparent; font-size: 16px; color: var(--tekst); }
  .lijst { padding: 4px 0; }
  .regel { display: flex; align-items: center; gap: 16px; min-height: 64px; padding: 0 16px; text-decoration: none; color: var(--tekst); }
  .regel + .regel .tekst { border-top: 1px solid var(--lijn); }
  .tekst { flex: 1; min-width: 0; padding: 12px 0; align-self: stretch; display: flex; flex-direction: column; justify-content: center; }
  .naam { font-size: 16px; }
  .onder { font-size: 12px; color: var(--zacht); }
  .label { padding: 4px 8px; border-radius: 8px; background: var(--let-op-vlak); color: var(--let-op-inkt); font-size: 12px; font-weight: 600; }
  .leeg { padding: 24px; margin-top: 16px; }
  .leeg h2 { margin: 0 0 4px; font-size: 16px; }
  .leeg p { margin: 0; color: var(--zacht); }
  .melding { margin: 16px 0 0; }
</style>
