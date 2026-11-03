<script lang="ts">
  // Menubalk onderin: Leden, Voorstellen, Account (besluit 222).
  import { page } from '$app/state';
  let { open = 0 }: { open?: number } = $props();
  const pad = $derived(page.url.pathname);
  const items = [
    { href: '/', tekst: 'Leden', actief: (p: string) => p === '/' || p.startsWith('/lid') },
    { href: '/voorstellen', tekst: 'Voorstellen', actief: (p: string) => p.startsWith('/voorstellen') },
    { href: '/account', tekst: 'Account', actief: (p: string) => p.startsWith('/account') },
  ];
</script>

<nav aria-label="Hoofdmenu">
  {#each items as item (item.href)}
    <a href={item.href} aria-current={item.actief(pad) ? 'page' : undefined} class:actief={item.actief(pad)}>
      <span class="pictogram">
        {#if item.tekst === 'Leden'}
          <svg aria-hidden="true" width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><circle cx="9" cy="8" r="3.5"/><path d="M3 19c.8-3.4 3.3-5 6-5s5.2 1.6 6 5"/><path d="M16 5.5a3 3 0 010 5.5M18 14c1.6.7 2.6 2.4 3 5"/></svg>
        {:else if item.tekst === 'Voorstellen'}
          <svg aria-hidden="true" width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><path d="M5 4h14v16H5z"/><path d="M9 9h6M9 13h6M9 17h3"/></svg>
          {#if open > 0}<span class="teller">{open}<span class="verborgen"> open</span></span>{/if}
        {:else}
          <svg aria-hidden="true" width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><circle cx="12" cy="8" r="4"/><path d="M4 20c1.2-3.6 4.2-5.5 8-5.5s6.8 1.9 8 5.5"/></svg>
        {/if}
      </span>
      {item.tekst}
    </a>
  {/each}
</nav>

<style>
  nav {
    position: fixed; inset: auto 0 0 0; z-index: 10; display: flex; justify-content: space-around;
    padding: 8px 12px calc(14px + env(safe-area-inset-bottom)); background: var(--achtergrond); border-top: 1px solid var(--rand);
  }
  a { display: flex; flex-direction: column; align-items: center; gap: 4px; min-width: 72px; min-height: 48px; text-decoration: none; color: var(--zacht); font-size: 12px; }
  a.actief { color: var(--tekst); font-weight: 700; }
  .pictogram { position: relative; padding: 4px 18px; border-radius: 16px; display: grid; }
  a.actief .pictogram { background: var(--accent-vlak); color: var(--accent-inkt); }
  .teller {
    position: absolute; top: -2px; right: 8px; min-width: 18px; height: 18px; border-radius: 9px; background: #d35400; color: #fff;
    font-size: 11px; font-weight: 700; display: grid; place-items: center; padding: 0 4px;
  }
</style>
