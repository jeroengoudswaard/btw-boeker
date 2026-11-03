<script lang="ts">
  // Eenmalig: de authenticator-app koppelen (TOTP). Zonder MFA geeft de database niets (besluit 33).
  import { onMount } from 'svelte';
  import { goto } from '$app/navigation';
  import { supabase } from '$lib/supabase';

  let qr: string | null = $state(null);
  let geheim: string | null = $state(null);
  let factorId: string | null = $state(null);
  let code = $state('');
  let fout: string | null = $state(null);

  onMount(async () => {
    const { data: sessie } = await supabase.auth.getSession();
    if (!sessie.session) return goto('/inloggen', { replaceState: true });
    // Een eerder half ingestelde koppeling eerst opruimen.
    const { data: factoren } = await supabase.auth.mfa.listFactors();
    for (const f of factoren?.all ?? []) if (f.status !== 'verified') await supabase.auth.mfa.unenroll({ factorId: f.id });
    if (factoren?.totp.length) return goto('/mfa', { replaceState: true });

    const { data, error } = await supabase.auth.mfa.enroll({ factorType: 'totp', friendlyName: 'Verenigd' });
    if (error || !data) {
      fout = 'Het instellen lukte niet. Probeer het later opnieuw.';
      return;
    }
    factorId = data.id;
    qr = data.totp.qr_code;
    geheim = data.totp.secret;
  });

  async function bevestig(e: SubmitEvent) {
    e.preventDefault();
    if (!factorId) return;
    const { error } = await supabase.auth.mfa.challengeAndVerify({ factorId, code: code.replace(/\D/g, '') });
    if (error) {
      fout = 'De code klopt niet. Kijk of de tijd op uw telefoon goed staat en probeer de nieuwe code.';
      return;
    }
    await goto('/', { replaceState: true });
  }
</script>

<main class="pagina">
  <header>
    <h1>Authenticator-app instellen</h1>
    <p>Eenmalig. Gebruik bijvoorbeeld Microsoft Authenticator of Google Authenticator.</p>
  </header>
  <section class="kaart stappen">
    <ol>
      <li>Open de app en kies <em>Account toevoegen</em>.</li>
      <li>Scan de code hieronder.</li>
      <li>Typ de 6 cijfers die de app toont.</li>
    </ol>
    {#if qr}
      <img src={qr} alt="QR-code voor de authenticator-app" width="200" height="200" />
      <details><summary>Kan niet scannen?</summary><p>Voer deze sleutel in: <code>{geheim}</code></p></details>
    {/if}
    <form onsubmit={bevestig} id="instellen">
      <label class="veldregel"><span>Code uit de app</span><input inputmode="numeric" autocomplete="one-time-code" maxlength="7" required bind:value={code} /></label>
    </form>
  </section>
  {#if fout}<p class="fout" role="alert">✕ {fout}</p>{/if}
  <button class="knop hoofd breed" form="instellen" disabled={!factorId || code.replace(/\D/g, '').length !== 6}>Koppelen</button>
</main>

<style>
  header { padding: clamp(48px, 10vh, 100px) 8px 14px; }
  h1 { margin: 0; font-size: 28px; }
  header p { margin: 6px 0 0; color: var(--zacht); }
  .stappen { padding: 16px; display: flex; flex-direction: column; align-items: center; gap: 12px; }
  ol { align-self: stretch; margin: 0; padding-left: 22px; line-height: 1.6; }
  img { background: #fff; border-radius: 12px; padding: 8px; }
  code { overflow-wrap: anywhere; }
  form { align-self: stretch; }
  .breed { width: 100%; margin-top: 16px; }
</style>
