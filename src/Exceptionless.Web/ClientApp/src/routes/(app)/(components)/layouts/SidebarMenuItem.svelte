<script lang="ts">
    import type { HTMLAnchorAttributes } from 'svelte/elements';
    import { page } from '$app/stores';

    let {
        href,
        isLargeScreen,
        isSidebarOpen = $bindable(),
        title
    }: { href: HTMLAnchorAttributes['href']; isLargeScreen: boolean; isSidebarOpen: boolean; title: string } = $props();

    // eslint-disable-next-line svelte/valid-compile
    const active = $derived($page.url.pathname === href);
</script>

<a
    {href}
    {title}
    class="group flex items-center rounded-lg p-2 text-base font-normal hover:bg-accent hover:text-accent-foreground"
    class:bg-accent={active}
    class:text-accent-foreground={active}
>
    <slot name="icon" iconClass="w-6 h-6 transition duration-75 text-muted-foreground group-hover:text-foreground {active ? 'text-foreground' : ''}" />
    <span class="ml-3 {!isSidebarOpen && isLargeScreen ? 'lg:absolute lg:hidden' : ''}">{title}</span>
</a>
