<script lang="ts">
    import type { HTMLAttributes } from 'svelte/elements';

    import { page } from '$app/stores';
    import { Button } from '$comp/ui/button';
    import { cn } from '$lib/utils';
    import { cubicInOut } from 'svelte/easing';
    import { crossfade } from 'svelte/transition';

    import type { NavigationItem } from '../../../routes';

    type Props = HTMLAttributes<HTMLElement> & {
        routes: NavigationItem[];
    };

    let { class: className, routes, ...props }: Props = $props();

    const [send, receive] = crossfade({
        duration: 250,
        easing: cubicInOut
    });
</script>

<nav class={cn('flex space-x-2 lg:flex-col lg:space-x-0 lg:space-y-1', className)} {...props}>
    {#each routes as route (route.href)}
        {@const isActive = $page.url.pathname === route.href}

        <Button
            class={cn(!isActive && 'hover:underline', 'relative justify-start hover:bg-transparent')}
            data-sveltekit-noscroll
            href={route.href}
            variant="ghost"
        >
            {#if isActive}
                <div class="absolute inset-0 rounded-md bg-muted" in:send={{ key: 'active-sidebar-tab' }} out:receive={{ key: 'active-sidebar-tab' }}></div>
            {/if}
            <div class="relative">
                {route.title}
            </div>
        </Button>
    {/each}
</nav>
