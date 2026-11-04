<script lang="ts">
  // Grote kop met ruimte erboven (bereik met de duim); bij scrollen verschijnt een smalle balk (besluit 222).
  import type { Snippet } from 'svelte';
  let { titel, onder, acties }: { titel: Snippet | string; onder?: string; acties?: Snippet } = $props();
  let kop: HTMLElement | undefined = $state();
  let ingeklapt = $state(false);

  $effect(() => {
    if (!kop) return;
    const waarnemer = new IntersectionObserver(([e]) => (ingeklapt = !e.isIntersecting), { rootMargin: '-56px 0px 0px 0px' });
    waarnemer.observe(kop);
    return () => waarnemer.disconnect();
  });
</script>

<div class="balk" class:zichtbaar={ingeklapt} aria-hidden={!ingeklapt}>
  <span>{typeof titel === 'string' ? titel : ''}</span>
  {#if acties}<span class="acties">{@render acties()}</span>{/if}
</div>
<header bind:this={kop}>
  <h1>{#if typeof titel === 'string'}{titel}{:else}{@render titel()}{/if}</h1>
  {#if onder}<p>{onder}</p>{/if}
  {#if acties}<div class="acties">{@render acties()}</div>{/if}
</header>

<style>
  header { padding: clamp(64px, 18vh, 96px) 8px 8px; position: relative; }
  h1 { margin: 0; font-size: 32px; font-weight: 600; line-height: 1.15; }
  p { margin: 8px 0 0; color: var(--zacht); }
  header .acties { position: absolute; top: 12px; right: 0; display: flex; gap: 4px; }
  .balk {
    position: fixed; inset: 0 0 auto 0; z-index: 10; height: 56px; padding: 0 16px; display: flex; align-items: center;
    justify-content: space-between; background: var(--achtergrond); border-bottom: 1px solid var(--rand);
    font-weight: 600; font-size: 16px; transform: translateY(-100%); transition: transform 0.15s ease-out;
  }
  .balk.zichtbaar { transform: none; }
  @media (prefers-reduced-motion: reduce) { .balk { transition: none; } }
</style>
