<script lang="ts">
    import type { Component } from 'svelte';
    import type { HTMLAnchorAttributes } from 'svelte/elements';

    import { page } from '$app/stores';

    type Props = {
        icon: Component;
        isLargeScreen?: boolean;
        isSidebarOpen?: boolean;
        title: string;
    } & HTMLAnchorAttributes;

    let { href, icon, isLargeScreen, isSidebarOpen = $bindable(), title, ...props }: Props = $props();

    const Icon = $state(icon);
    const active = $derived($page.url.pathname === href);
</script>

<a
    {href}
    {title}
    {...props}
    class="group flex items-center rounded-lg p-2 text-base font-normal hover:bg-accent hover:text-accent-foreground"
    class:bg-accent={active}
    class:text-accent-foreground={active}
>
    <Icon class="h-6 w-6 text-muted-foreground transition duration-75 group-hover:text-foreground {active ? 'text-foreground' : ''}" />
    <span class="ml-3 {!isSidebarOpen && isLargeScreen ? 'lg:absolute lg:hidden' : ''}">{title}</span>
</a>
