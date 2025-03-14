<script lang="ts">
    import type { HTMLAttributes } from 'svelte/elements';

    import * as Navigation from '$lib/features/shared/components/navigation';

    import type { NavigationItem } from '../../routes';

    interface Props extends HTMLAttributes<HTMLElement> {
        routes: NavigationItem[];
    }

    let { routes, ...props }: Props = $props();
</script>

<Navigation.Container {...props}>
    {#each routes as route (route.href)}
        <Navigation.Item href={route.href} title={route.title} />
    {/each}
</Navigation.Container>
