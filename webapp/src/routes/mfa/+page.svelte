<script lang="ts">
  // Stap 2 van 2: de code uit de authenticator-app (MFA verplicht, besluit 33).
  import { pad } from '#lib/pad.ts';
  import { isEigenPad } from '#lib/paden.ts';
  import { onMount } from 'svelte';
  import { goto } from '$app/navigation';
  import { page } from '$app/state';
  import { supabase } from '#lib/supabase.ts';

  let code = $state('');
  let fout: string | null = $state(null);
  let bezig = $state(false);
  let email = $state('');

  onMount(async () => {
    const { data } = await supabase.auth.getSession();
    if (!data.session) await goto(pad('/inloggen'), { replaceState: true });
    email = data.session?.user.email ?? '';
  });

  async function bevestig(e: SubmitEvent) {
    e.preventDefault();
    fout = null;
    bezig = true;
    const { data: factoren } = await supabase.auth.mfa.listFactors();
    const factor = factoren?.totp[0];
    if (!factor) {
      bezig = false;
      await goto(pad('/mfa/instellen'), { replaceState: true });
      return;
    }
    const { error } = await supabase.auth.mfa.challengeAndVerify({ factorId: factor.id, code: code.replace(/\D/g, '') });
    bezig = false;
    if (error) {
      fout = 'De code klopt niet of is verlopen. Probeer de nieuwe code uit de app.';
      code = '';
      return;
    }
    const verder = page.url.searchParams.get('verder');
    await goto(pad(isEigenPad(verder) ? verder : '/'), { replaceState: true });
  }

  async function ander() {
    await supabase.auth.signOut();
    await goto(pad('/inloggen'), { replaceState: true });
  }
</script>

<main class="pagina inlog">
  <div class="kop">
    <h1>Bevestig dat u het bent</h1>
    <p>Open uw authenticator-app en typ de 6 cijfers voor Verenigd.</p>
  </div>
  <form class="kaart vak" onsubmit={bevestig} id="mfa">
    <div class="wie"><span>{email}</span><button type="button" class="link" onclick={ander}>Ander account</button></div>
    <label for="code" class="label">Code uit de app</label>
    <input id="code" inputmode="numeric" autocomplete="one-time-code" maxlength="7" pattern="[0-9 ]*" required bind:value={code} />
  </form>
  {#if fout}<p class="fout" role="alert">✕ {fout}</p>{/if}
  <div class="vul"></div>
  <button class="knop hoofd" form="mfa" disabled={bezig || code.replace(/\D/g, '').length !== 6}>Bevestigen</button>
</main>

<style>
  .inlog { min-height: 100dvh; display: flex; flex-direction: column; padding-bottom: 32px; }
  .kop { min-height: 260px; display: flex; flex-direction: column; justify-content: flex-end; align-items: center; gap: 12px; padding-bottom: 32px; text-align: center; }
  h1 { margin: 0; font-size: 32px; }
  .kop p { margin: 0; color: var(--zacht); max-width: 290px; }
  .vak { padding: 16px; display: flex; flex-direction: column; gap: 8px; }
  .wie { display: flex; justify-content: space-between; align-items: center; gap: 8px; overflow-wrap: anywhere; }
  .label { font-size: 12px; font-weight: 600; color: var(--zacht); }
  #code {
    min-height: 56px; border-radius: 14px; border: 2px solid var(--accent); background: var(--achtergrond); font-size: 24px;
    letter-spacing: 0.4em; text-align: center; font-variant-numeric: tabular-nums;
  }
  .link { background: none; border: 0; color: var(--accent-tekst); font-weight: 600; min-height: 44px; cursor: pointer; }
  .vul { flex: 1; min-height: 24px; }
  .fout { margin: 12px 0 0; }
</style>
