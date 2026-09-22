<script lang="ts">
    import { A } from '$comp/typography';
    import * as Tooltip from '$comp/ui/tooltip';

    import TimeAgo from './time-ago.svelte';

    interface Props {
        linked?: boolean;
        value: Date | string | undefined;
    }

    let { linked = false, value }: Props = $props();
</script>

<Tooltip.Provider>
    {#if linked}
        <TimeAgo {value}>
            {#snippet child({ children, props })}
                <A {...props} href="/next/event/test" aria-label="Open event test">{@render children()}</A>
            {/snippet}
        </TimeAgo>
    {:else}
        <TimeAgo {value} />
    {/if}
</Tooltip.Provider>
