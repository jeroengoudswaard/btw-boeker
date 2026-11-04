<script lang="ts">
  // Stap 1 van 2: e-mail en wachtwoord; daarna de code uit de authenticator-app (besluit 231).
  import { pad } from '#lib/pad.ts';
  import { goto } from '$app/navigation';
  import { routeVoor, serverIngericht, supabase, toegang } from '#lib/supabase.ts';

  let email = $state('');
  let wachtwoord = $state('');
  let bezig = $state(false);
  let fout: string | null = $state(null);
  let melding: string | null = $state(null);

  async function inloggen(e: SubmitEvent) {
    e.preventDefault();
    bezig = true;
    fout = null;
    const { error } = await supabase.auth.signInWithPassword({ email: email.trim(), password: wachtwoord });
    bezig = false;
    if (error) {
      fout = 'Het e-mailadres of wachtwoord klopt niet.';
      return;
    }
    wachtwoord = '';
    await goto(pad(routeVoor(await toegang())), { replaceState: true });
  }

  async function vergeten() {
    fout = null;
    if (!email.includes('@')) {
      fout = 'Vul eerst uw e-mailadres in.';
      return;
    }
    await supabase.auth.resetPasswordForEmail(email.trim(), { redirectTo: `${location.origin}${pad('/wachtwoord')}` });
    melding = 'Als dit adres bekend is, ontvangt u een e-mail om een nieuw wachtwoord te kiezen.';
  }
</script>

<main class="pagina inlog">
  <div class="kop">
    <svg aria-hidden="true" width="64" height="64" viewBox="0 0 16 16"><rect x="0.5" y="0.5" width="15" height="15" rx="4" fill="#2E7D32"/><path d="M4.5 8.2l2.3 2.3 4.7-5" fill="none" stroke="#fff" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round"/></svg>
    <h1>Verenigd</h1>
    <p>Voor de leiding: de leden en contactgegevens van uw speltak.</p>
  </div>
  <form class="kaart" onsubmit={inloggen} id="inlog">
    <label class="veldregel"><span>E-mailadres</span><input type="email" autocomplete="username" required bind:value={email} /></label>
    <label class="veldregel"><span>Wachtwoord</span><input type="password" autocomplete="current-password" required bind:value={wachtwoord} /></label>
  </form>
  {#if !serverIngericht}
    <p class="melding info" role="status" data-testid="niet-ingericht">⚠ De server voor deze webapp is nog niet ingericht. Inloggen kan pas als de beheerder de koppeling heeft gemaakt.</p>
  {/if}
  {#if fout}<p class="fout" role="alert">✕ {fout}</p>{/if}
  {#if melding}<p class="melding info" role="status">{melding}</p>{/if}
  <div class="vul"></div>
  <button type="button" class="link" onclick={vergeten} disabled={!serverIngericht}>Wachtwoord vergeten?</button>
  <button class="knop hoofd" form="inlog" disabled={bezig || !serverIngericht}>Inloggen</button>
</main>

<style>
  .inlog { min-height: 100dvh; display: flex; flex-direction: column; padding-bottom: 32px; }
  .kop { min-height: 300px; display: flex; flex-direction: column; justify-content: flex-end; align-items: center; gap: 12px; padding-bottom: 32px; text-align: center; }
  h1 { margin: 0; font-size: 32px; }
  .kop p { margin: 0; color: var(--zacht); max-width: 290px; }
  form { padding: 8px 0; }
  .vul { flex: 1; min-height: 24px; }
  .link { background: none; border: 0; color: var(--accent-tekst); font-weight: 600; min-height: 44px; margin-bottom: 8px; cursor: pointer; }
  .fout, .melding { margin: 12px 0 0; }
</style>
