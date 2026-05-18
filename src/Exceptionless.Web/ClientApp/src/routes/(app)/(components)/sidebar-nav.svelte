<script lang="ts">
    import type { HTMLAttributes } from 'svelte/elements';

    import * as Navigation from '$lib/features/shared/components/navigation';

    import type { NavigationItem } from '../../routes.svelte';

    interface Props extends HTMLAttributes<HTMLElement> {
        orientation?: 'horizontal' | 'responsive';
        routes: NavigationItem[];
    }

    let { orientation, routes, ...props }: Props = $props();
</script>

<Navigation.Container {orientation} {...props}>
    {#each routes as route (route.href)}
        <Navigation.Item href={route.href} title={route.title} />
    {/each}
</Navigation.Container>
