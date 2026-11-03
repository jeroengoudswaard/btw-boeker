<script lang="ts">
  import { kleurVoor } from '#lib/leden.ts';
  let { id, tekst, grootte = 42 }: { id: string; tekst: string; grootte?: number } = $props();
  const kleur = $derived(kleurVoor(id));
</script>

<span
  aria-hidden="true"
  class="avatar"
  style="width:{grootte}px;height:{grootte}px;flex:0 0 {grootte}px;font-size:{Math.round(grootte * 0.36)}px;background:{kleur.vlak};color:{kleur.inkt}"
>{tekst}</span>

<style>
  .avatar { border-radius: 50%; display: grid; place-items: center; font-weight: 700; }
</style>
