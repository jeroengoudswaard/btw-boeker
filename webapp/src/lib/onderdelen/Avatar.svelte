<script lang="ts">
  // Initialen in de kleur van de groep, zoals in de desktop-app (besluiten 239 en 243).
  import { kleurVoorGroep, tint } from '#lib/leden.ts';
  let { groep = null, tekst, grootte = 40 }: { groep?: string | null; tekst: string; grootte?: number } = $props();
  const kleur = $derived(kleurVoorGroep(groep));
</script>

<span
  aria-hidden="true"
  class="avatar"
  class:groot={grootte >= 64}
  style="width:{grootte}px;height:{grootte}px;flex:0 0 {grootte}px;background:{tint(kleur)};color:{kleur.inkt}"
>{tekst}</span>

<style>
  .avatar { border-radius: 50%; display: grid; place-items: center; font-weight: 600; font-size: 14px; }
  .avatar.groot { font-size: 24px; }
</style>
