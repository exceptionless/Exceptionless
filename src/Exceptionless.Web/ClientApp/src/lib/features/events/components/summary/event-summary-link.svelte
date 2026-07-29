<script lang="ts">
    import type { Snippet } from 'svelte';

    import { A } from '$comp/typography';

    import { buildEventDetailsHref } from './index';

    interface Props {
        children: Snippet;
        eventId: string;
        linkToDetails: boolean;
    }

    let { children, eventId, linkToDetails }: Props = $props();
</script>

{#if linkToDetails}
    <A class="inline" href={buildEventDetailsHref(eventId)}>
        {@render children()}
    </A>
{:else}
    {@render children()}
{/if}
