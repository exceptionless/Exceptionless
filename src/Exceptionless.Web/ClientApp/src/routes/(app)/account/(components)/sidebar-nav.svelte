<script lang="ts">
    import { cubicInOut } from 'svelte/easing';
    import { crossfade } from 'svelte/transition';

    import { page } from '$app/stores';
    import { Button } from '$comp/ui/button';
    import { cn } from '$lib/utils';
    import type { NavigationItem } from '../../../routes';

    let className: string | undefined | null = undefined;
    export let routes: NavigationItem[];
    export { className as class };

    const [send, receive] = crossfade({
        duration: 250,
        easing: cubicInOut
    });
</script>

<nav class={cn('flex space-x-2 lg:flex-col lg:space-x-0 lg:space-y-1', className)}>
    {#each routes as route (route.href)}
        {@const isActive = $page.url.pathname === route.href}

        <Button
            href={route.href}
            variant="ghost"
            class={cn(!isActive && 'hover:underline', 'relative justify-start hover:bg-transparent')}
            data-sveltekit-noscroll
        >
            {#if isActive}
                <div class="absolute inset-0 rounded-md bg-muted" in:send={{ key: 'active-sidebar-tab' }} out:receive={{ key: 'active-sidebar-tab' }} />
            {/if}
            <div class="relative">
                {route.title}
            </div>
        </Button>
    {/each}
</nav>
