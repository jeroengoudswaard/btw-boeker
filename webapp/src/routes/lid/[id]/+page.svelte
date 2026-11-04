<script lang="ts">
  // Lid: grote avatar, snelle knoppen (besluiten 223 en 249: kiezen uit voogd 1, voogd 2 en het lid zelf), voogden en gegevens.
  import { onMount } from 'svelte';
  import { page } from '$app/state';
  import Avatar from '#lib/onderdelen/Avatar.svelte';
  import { bewaak, laad, openVoorstellen, stand } from '#lib/gegevens.svelte.ts';
  import { contactopties, type Actie } from '#lib/contact.ts';
  import { initialen, isLeiding, volledigeNaam, type Voogd } from '#lib/leden.ts';
  import { weergaveTelefoon } from '#lib/telefoon.ts';
  import { veldOmschrijving } from '#lib/voorstellen.ts';

  const lid = $derived(stand.leden.find((l) => l.id === page.params.id));
  const voogden = $derived(lid?.voogden ?? []);
  const open = $derived(lid ? openVoorstellen(lid.id) : []);
  let keuze: Actie | null = $state(null);
  let blad: HTMLDialogElement | undefined = $state();

  const opties = (actie: Actie) => (lid ? contactopties(lid, actie) : []);
  const voogdnaam = (v: Voogd) => v.naam ?? `Voogd ${v.volgnummer}`;
  const datum = (d: string | null) => (d ? d.split('-').reverse().join('-') : '—');
  const functie = $derived(lid?.functies.find((f) => f.speltak === stand.speltak) ?? lid?.functies[0]);

  function kies(actie: Actie) {
    const kandidaten = opties(actie);
    if (kandidaten.length === 1) location.href = kandidaten[0].href;
    else if (kandidaten.length > 1) {
      keuze = actie;
      blad?.showModal();
    }
  }

  onMount(async () => {
    if (await bewaak()) await laad();
  });
</script>

<main class="pagina">
  <div class="boven">
    <a href="/" class="rond" aria-label="Terug naar Leden">
      <svg aria-hidden="true" width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"><path d="M15 5l-7 7 7 7"/></svg>
    </a>
    {#if lid}<a href={`/lid/${lid.id}/wijzigen`} class="wijzig">Wijzigen</a>{/if}
  </div>

  {#if !lid}
    <p>{stand.geladen ? 'Dit lid is niet (meer) zichtbaar voor u.' : 'Laden…'}</p>
  {:else}
    <div class="kop">
      <Avatar groep={functie?.speltak} tekst={initialen(lid)} grootte={72} />
      <h1>{volledigeNaam(lid)}</h1>
      <p>{functie?.speltak} · {lid && isLeiding(lid, functie?.speltak ?? '') ? 'leiding' : 'jeugdlid'} · {lid.lidnummer}</p>
    </div>

    <section class="snel" aria-label="Contact">
      {#each [['bellen', 'Bellen'], ['bericht', 'Bericht'], ['email', 'E-mail']] as [actie, tekst] (actie)}
        <button type="button" class="kaart" disabled={opties(actie as Actie).length === 0} onclick={() => kies(actie as Actie)}>
          {#if actie === 'bellen'}
            <svg aria-hidden="true" width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M5 4h4l2 5-2.5 1.5a11 11 0 005 5L15 13l5 2v4a1 1 0 01-1 1A16 16 0 014 5a1 1 0 011-1z"/></svg>
          {:else if actie === 'bericht'}
            <svg aria-hidden="true" width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M4 5h16v11H9l-5 4z"/></svg>
          {:else}
            <svg aria-hidden="true" width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M3 6h18v12H3z"/><path d="M3 7l9 6 9-6"/></svg>
          {/if}
          {tekst}
        </button>
      {/each}
    </section>

    {#each open as v (v.id)}
      <p class="melding let-op" role="status">Uw voorstel voor <strong>{veldOmschrijving(v.veld)}</strong> wacht op de beheerder.</p>
    {/each}

    <h2 class="sectiekop">Voogden</h2>
    <section class="kaart blok">
      {#each voogden as v (v.volgnummer)}
        <div class="voogd">
          <div>{voogdnaam(v)}</div>
          <div class="zacht">{[v.mobiel ?? v.telefoon, v.email].filter(Boolean).map((t) => (t!.startsWith('+') ? weergaveTelefoon(t!) : t)).join(' · ') || 'Geen contactgegevens'}</div>
        </div>
      {:else}
        <div class="voogd zacht">Geen voogden bekend.</div>
      {/each}
    </section>

    <h2 class="sectiekop">Gegevens</h2>
    <section class="kaart blok">
      <dl>
        <dt>Geboortedatum</dt><dd>{datum(lid.geboortedatum)}</dd>
        <dt>Telefoon lid</dt><dd>{lid.telefoon ? weergaveTelefoon(lid.telefoon) : '—'}</dd>
        <dt>E-mail lid</dt><dd>{lid.email ?? '—'}</dd>
      </dl>
    </section>
    <p class="zacht klein">Alleen ledengegevens; geen adres en geen betaalgegevens.</p>
  {/if}
</main>

<dialog bind:this={blad} aria-labelledby="bladkop" onclose={() => (keuze = null)}>
  <h2 id="bladkop">{keuze === 'bellen' ? 'Bellen naar' : keuze === 'bericht' ? 'Bericht aan' : 'E-mail aan'}</h2>
  {#if keuze}
    {#each opties(keuze) as optie (optie.href)}
      <a class="keuze" href={optie.href}>
        <Avatar tekst={optie.rol === 'Lid zelf' ? initialen(lid!) : optie.rol.replace('Voogd ', 'V')} groep={optie.rol === 'Lid zelf' ? functie?.speltak : null} />
        <span><span class="naam">{optie.wie}</span><span class="zacht">{optie.rol} · {optie.soort} · {optie.weergave}</span></span>
      </a>
    {/each}
  {/if}
  <form method="dialog"><button class="knop neutraal breed">Annuleren</button></form>
</dialog>

<style>
  .boven { display: flex; justify-content: space-between; align-items: center; padding-top: 48px; }
  .rond { width: 44px; height: 44px; border-radius: 50%; display: grid; place-items: center; color: var(--tekst); }
  .wijzig { min-height: 44px; padding: 0 16px; display: flex; align-items: center; font-weight: 600; text-decoration: none; }
  .kop { display: flex; flex-direction: column; align-items: center; gap: 8px; padding: 8px 0 16px; text-align: center; }
  .kop h1 { margin: 8px 0 0; font-size: 24px; }
  .kop p { margin: 0; color: var(--zacht); }
  .snel { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 8px; margin-bottom: 16px; }
  .snel button {
    border: 0; min-height: 76px; border-radius: 8px; display: flex; flex-direction: column; align-items: center; justify-content: center;
    gap: 8px; font-size: 12px; color: var(--tekst); cursor: pointer;
  }
  .snel button svg { color: var(--accent); }
  .snel button:disabled { opacity: 0.45; cursor: default; }
  .melding { margin: 0 0 8px; }
  .blok { padding: 8px 0; }
  .voogd { padding: 8px 24px; }
  .voogd + .voogd { border-top: 1px solid var(--lijn); }
  dl { margin: 0; padding: 8px 24px 12px; display: grid; grid-template-columns: minmax(0, 130px) minmax(0, 1fr); gap: 8px 12px; font-size: 16px; }
  dt { color: var(--zacht); }
  dd { margin: 0; overflow-wrap: anywhere; }
  .zacht { color: var(--zacht); font-size: 14px; }
  .klein { font-size: 12px; text-align: center; margin-top: 16px; }
  dialog {
    border: 0; border-radius: 8px; padding: 24px 8px 12px; width: min(100% - 20px, 540px); margin: auto auto 12px;
    background: var(--kaart); color: var(--tekst);
  }
  dialog::backdrop { background: rgb(0 0 0 / 0.38); }
  dialog h2 { margin: 0 0 8px; padding: 0 16px; font-size: 16px; }
  .keuze { display: flex; align-items: center; gap: 16px; min-height: 64px; padding: 0 16px; border-radius: 8px; text-decoration: none; color: var(--tekst); }
  .keuze:hover { background: var(--achtergrond); }
  .keuze .naam, .keuze .zacht { display: block; }
  .breed { width: 100%; margin-top: 8px; }
</style>
