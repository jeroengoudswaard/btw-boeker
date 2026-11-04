<script lang="ts">
  // Na de uitnodiging (besluit 230) of "wachtwoord vergeten": een (nieuw) wachtwoord kiezen.
  import { pad } from '#lib/pad.ts';
  import { onMount } from 'svelte';
  import { goto } from '$app/navigation';
  import { routeVoor, supabase, toegang } from '#lib/supabase.ts';

  let wachtwoord = $state('');
  let herhaling = $state('');
  let fout: string | null = $state(null);
  const MINIMUM = 12;

  onMount(async () => {
    // De link in de e-mail zet de sessie (detectSessionInUrl); even wachten tot die er is.
    const { data } = await supabase.auth.getSession();
    if (!data.session) return goto(pad('/inloggen'), { replaceState: true });
    // Met een gekoppelde app eerst de code; daarna terug naar hier.
    if ((await toegang()) === 'mfa') return goto(pad('/mfa?verder=/wachtwoord'), { replaceState: true });
  });

  async function bewaar(e: SubmitEvent) {
    e.preventDefault();
    fout = null;
    if (wachtwoord.length < MINIMUM) return (fout = `Kies minstens ${MINIMUM} tekens.`);
    if (wachtwoord !== herhaling) return (fout = 'De twee wachtwoorden zijn niet gelijk.');
    const { error } = await supabase.auth.updateUser({ password: wachtwoord });
    if (error) return (fout = 'Opslaan lukte niet. Kies een ander wachtwoord of probeer het later opnieuw.');
    wachtwoord = herhaling = '';
    await goto(pad(routeVoor(await toegang())), { replaceState: true });
  }
</script>

<main class="pagina">
  <header>
    <h1>Kies een wachtwoord</h1>
    <p>Minstens {MINIMUM} tekens. Daarna stelt u de authenticator-app in.</p>
  </header>
  <form class="kaart" onsubmit={bewaar} id="wachtwoord">
    <label class="veldregel"><span>Nieuw wachtwoord</span><input type="password" autocomplete="new-password" required bind:value={wachtwoord} /></label>
    <label class="veldregel"><span>Nog een keer</span><input type="password" autocomplete="new-password" required bind:value={herhaling} /></label>
  </form>
  {#if fout}<p class="fout" role="alert">✕ {fout}</p>{/if}
  <button class="knop hoofd breed" form="wachtwoord">Opslaan</button>
</main>

<style>
  header { padding: clamp(48px, 12vh, 96px) 8px 16px; }
  h1 { margin: 0; font-size: 32px; }
  header p { margin: 8px 0 0; color: var(--zacht); }
  form { padding: 8px 0; }
  .breed { width: 100%; margin-top: 16px; }
</style>
