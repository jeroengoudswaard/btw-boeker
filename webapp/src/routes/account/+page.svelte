<script lang="ts">
  import { onMount } from 'svelte';
  import { goto } from '$app/navigation';
  import GroteKop from '#lib/onderdelen/GroteKop.svelte';
  import Onderbalk from '#lib/onderdelen/Onderbalk.svelte';
  import { bewaak, laad, openVoorstellen, stand } from '#lib/gegevens.svelte.ts';
  import { supabase } from '#lib/supabase.ts';

  let email = $state('');

  onMount(async () => {
    if (!(await bewaak())) return;
    await laad();
    email = (await supabase.auth.getUser()).data.user?.email ?? '';
  });

  async function uitloggen() {
    await supabase.auth.signOut();
    stand.geladen = false;
    stand.leden = [];
    stand.voorstellen = [];
    await goto('/inloggen', { replaceState: true });
  }
</script>

<main class="pagina">
  <GroteKop titel="Account" />
  <section class="kaart">
    <div class="veldregel"><span>E-mailadres</span>{email}</div>
    <div class="veldregel"><span>Speltakken</span>{stand.speltakken.join(', ') || '—'}</div>
    <div class="veldregel"><span>Beveiliging</span>✓ Wachtwoord en authenticator-app</div>
  </section>
  <p class="zacht">De gegevens komen uit de ledenadministratie van de vereniging. Wijzigingen zijn voorstellen; de beheerder verwerkt ze.</p>
  <a class="knop neutraal breed" href="/wachtwoord">Wachtwoord wijzigen</a>
  <button class="knop neutraal breed" onclick={uitloggen}>Uitloggen</button>
</main>
<Onderbalk open={openVoorstellen().length} />

<style>
  .veldregel { font-size: 16px; }
  .zacht { color: var(--zacht); font-size: 14px; padding: 0 8px; }
  .breed { width: 100%; margin-top: 8px; }
</style>
